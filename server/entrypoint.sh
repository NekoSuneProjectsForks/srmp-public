#!/usr/bin/env bash
# Headless SRMP host.
#
# Commands:
#   serve   (default) install anything missing, then run the server
#   login             one-time interactive Steam login (answers Steam Guard)
#   code              print the current friend code and exit
#   stop              ask a running server to save and quit
#   saves             list the worlds this server has saved
#   diagnose          dump wine/SRML state and run the installer verbosely
#   shell             drop into a shell inside the runtime
set -euo pipefail

GAME_DIR="${GAME_DIR:-/game}"
MODS_DIR="${MODS_DIR:-/mods}"
STEAM_APPID="${STEAM_APPID:-433340}"
STEAM_USER="${STEAM_USER:-}"
STEAM_UPDATE="${STEAM_UPDATE:-auto}"      # auto | always | never
RENDER_MODE="${RENDER_MODE:-nographics}"  # nographics | software
DISPLAY_NUM="${DISPLAY_NUM:-99}"
SCREEN="${SCREEN:-640x480x24}"
SRML_URL="${SRML_URL:-https://cdn.0x00sec.xyz/files/games/Slime/SRMLInstaller.exe}"

SRMP_USERNAME="${SRMP_USERNAME:-Server}"
SRMP_GAME="${SRMP_GAME:-}"
SRMP_GAMEMODE="${SRMP_GAMEMODE:-CLASSIC}"
SRMP_SLOTS="${SRMP_SLOTS:-16}"
SRMP_LOAD_LATEST="${SRMP_LOAD_LATEST:-true}"
SAVES_DIR="${SAVES_DIR:-/saves}"
SRMP_GOD_MODE="${SRMP_GOD_MODE:-true}"
SRMP_TICK_RATE="${SRMP_TICK_RATE:-60}"
SRMP_OPERATORS="${SRMP_OPERATORS:-}"

export WINEPREFIX="${WINEPREFIX:-/wine}"
export DISPLAY=":${DISPLAY_NUM}"
export HOME=/steam

XVFB_PID=""
TAIL_PID=""
GAME_PID=""

log() { echo "[srmp-server] $*"; }
die() { log "ERROR: $*"; exit 1; }

usage() {
  cat <<'TXT'
SRMP headless server runtime.

  docker run ... IMAGE [command]

Commands:
  serve    (default) install anything missing, then run the server
  login    one-time interactive Steam login, needed once per Steam account
  code     print the current friend code and exit
  stop     ask a running server to save and quit
  saves    list the worlds this server has saved
  diagnose dump wine/SRML state and run the installer verbosely
  shell    drop into a shell inside the runtime

Typical first run:

  # 1. one-time Steam login (needs -it for the Steam Guard prompt)
  docker run --rm -it \
    -v "$PWD/steam:/steam" \
    -e STEAM_USER=your_steam_name \
    IMAGE login

  # 2. start the server
  docker run -d --name srmp-server \
    -v "$PWD/game:/game" -v "$PWD/mods:/mods" -v "$PWD/steam:/steam" \
    -v srmp-wine:/wine \
    -e STEAM_USER=your_steam_name \
    -e SRMP_USERNAME=Server \
    IMAGE

  # 3. read the friend code (docker exec needs the `srmp` command)
  docker exec srmp-server srmp code

  # 4. stop it cleanly (saves the world first)
  docker stop srmp-server
TXT
}

# ---------------------------------------------------------------- display ---
# Even in nographics mode Wine is happier with a display to talk to, and the
# SRML installer runs under the same prefix.
start_xvfb() {
  # A restarting container keeps its filesystem, so a previous run's lock and
  # socket survive. Xvfb then refuses to start ("Server is already active"),
  # and the leftover socket makes the wait below succeed against a dead display.
  if [[ -e "/tmp/.X${DISPLAY_NUM}-lock" ]] && ! pgrep -f "Xvfb :${DISPLAY_NUM}" >/dev/null 2>&1; then
    log "clearing stale X lock from a previous run"
    rm -f "/tmp/.X${DISPLAY_NUM}-lock" "/tmp/.X11-unix/X${DISPLAY_NUM}"
  fi

  log "starting Xvfb on ${DISPLAY} (${SCREEN})"
  Xvfb "${DISPLAY}" -screen 0 "${SCREEN}" -nolisten tcp &
  XVFB_PID=$!

  for _ in $(seq 1 50); do
    if ! kill -0 "${XVFB_PID}" 2>/dev/null; then
      die "Xvfb died on startup — see its error above"
    fi
    [[ -e "/tmp/.X11-unix/X${DISPLAY_NUM}" ]] && return 0
    sleep 0.1
  done
  die "Xvfb did not create its socket in time"
}

wine_mono_installed() {
  [[ -d "${WINEPREFIX}/drive_c/windows/mono" ]]
}

# The prefix lives in a volume that outlives the image, so a prefix created by
# an older build can be missing things a newer image ships. Mono is installed
# here rather than only at image build time, otherwise the operator would have
# to delete the volume to pick it up.
ensure_wine_mono() {
  if wine_mono_installed; then
    return 0
  fi

  local msi="${WINE_MONO_MSI:-}"
  if [[ -z "${msi}" || ! -f "${msi}" ]]; then
    msi="$(ls -1 /usr/share/wine/mono/*.msi 2>/dev/null | head -n1 || true)"
  fi

  if [[ -z "${msi}" ]]; then
    log "WARNING: no Wine Mono MSI in this image; .NET programs will not run"
    return 0
  fi

  log "installing wine mono into the prefix ($(basename "${msi}"))"
  wine msiexec /i "${msi}" /qn >/dev/null 2>&1 || true
  wineserver -w

  if wine_mono_installed; then
    log "wine mono installed"
  else
    log "WARNING: wine mono still not present after installing ${msi}"
    log "WARNING: .NET programs such as the SRML installer will not start"
  fi
}

init_wine() {
  if [[ -d "${WINEPREFIX}/drive_c" ]]; then
    ensure_wine_mono
    return 0
  fi

  # The image bakes a prefix template at build time; reuse it when present so
  # the operator's first boot is quick.
  local template="${WINE_TEMPLATE:-/opt/wine-template}"
  if [[ -d "${template}/drive_c" ]]; then
    log "seeding wine prefix from image template"
    mkdir -p "${WINEPREFIX}"
    cp -a "${template}/." "${WINEPREFIX}/"
    ensure_wine_mono
    return 0
  fi

  log "initializing wine prefix at ${WINEPREFIX} (first run, takes a minute)"
  wineboot --init >/dev/null 2>&1 || true
  wineserver -w
  ensure_wine_mono
}

# ------------------------------------------------------------------ login ---
# Steam Guard cannot be answered by an unattended container, so this is run once
# by hand. SteamCMD caches the sentry in /steam and later boots reuse it.
run_login() {
  mkdir -p /steam

  if [[ ! -t 0 ]]; then
    die "login needs an interactive terminal. Re-run with -it:
  docker run --rm -it -v \"\$PWD/steam:/steam\" -e STEAM_USER=you IMAGE login"
  fi

  local user="${STEAM_USER}"
  if [[ -z "${user}" ]]; then
    read -r -p "Steam username: " user
  fi

  log "logging in as ${user}; enter your password and Steam Guard code when asked"
  steamcmd +login "${user}" +quit

  echo ""
  log "login cached in the /steam volume; unattended runs will reuse it"
  log "set STEAM_USER=${user} when you start the server"
}

# ------------------------------------------------------------------- code ---
run_code() {
  local path="${GAME_DIR}/SRMP/servercode.txt"
  [[ -s "${path}" ]] || die "no friend code yet. Is the server finished starting?"
  cat "${path}"
}

# ------------------------------------------------------------------- stop ---
# Asks a running server to save and quit, from a second container/exec. The
# running container's own entrypoint then sees the game exit and shuts down.
run_stop() {
  [[ -d "${GAME_DIR}/SRMP" ]] || die "no SRMP data at ${GAME_DIR}/SRMP — is this the right volume?"
  : > "${GAME_DIR}/SRMP/shutdown.request"
  log "shutdown requested; the server will save and quit shortly"
}

# ------------------------------------------------------------------ saves ---
# Saves are named <timestamp>_<world>_<n>.sav. The game writes them into the
# Wine prefix, which lives in a volume that is reasonable to throw away and
# rebuild -- so the folder is relocated onto its own mount and only symlinked
# into the prefix. Otherwise resetting the prefix destroys every world.
link_saves() {
  local parent="${WINEPREFIX}/drive_c/users/root/AppData/LocalLow/Monomi Park"
  local target="${parent}/Slime Rancher"

  mkdir -p "${SAVES_DIR}" "${parent}"

  if [[ -L "${target}" ]]; then
    return 0
  fi

  if [[ -d "${target}" ]]; then
    # a prefix from before this change: move the worlds out before linking
    log "moving existing saves out of the wine prefix into ${SAVES_DIR}"
    cp -an "${target}/." "${SAVES_DIR}/" 2>/dev/null || true
    rm -rf "${target}"
  fi

  ln -s "${SAVES_DIR}" "${target}"
  log "saves are stored in ${SAVES_DIR} (outside the wine prefix)"
}

saves_dir() {
  if [[ -d "${SAVES_DIR}" ]]; then
    echo "${SAVES_DIR}"
    return 0
  fi
  find "${WINEPREFIX}/drive_c/users" -type d -path '*Monomi Park/Slime Rancher' 2>/dev/null | head -n1
}

run_saves() {
  local dir
  dir="$(saves_dir)"

  if [[ -z "${dir}" ]]; then
    die "no Slime Rancher data folder in the wine prefix yet — has the game run once?"
  fi

  echo "save folder: ${dir}"
  echo ""

  if ! ls "${dir}"/*.sav >/dev/null 2>&1; then
    echo "no saves yet"
    return 0
  fi

  echo "worlds (newest first):"
  # shellcheck disable=SC2012
  ls -1t "${dir}"/*.sav | while read -r f; do
    local base world
    base="$(basename "${f}" .sav)"
    # <timestamp>_<world>_<index>: strip the leading stamp and trailing index
    world="$(echo "${base}" | sed -E 's/^[0-9]+_//; s/_[0-9]+$//')"
    printf '  %-28s %s  (%s)
' "${world}" "$(date -r "${f}" '+%Y-%m-%d %H:%M')" "${base}"
  done

  echo ""
  echo "The server continues the newest of these automatically."
  echo "To pin one, set SRMP_GAME to its world name and restart."
}

# --------------------------------------------------------------- diagnose ---
# Everything needed to work out why the SRML patch is not taking, without
# sitting through a restart loop.
run_diagnose() {
  local managed="${GAME_DIR}/SlimeRancher_Data/Managed"

  # Bring the prefix up to date first, exactly as `serve` would, so this reports
  # the state the server will actually run with.
  start_xvfb
  init_wine
  link_saves

  echo "== wine =="
  wine --version || true
  echo "WINEPREFIX=${WINEPREFIX}"
  echo "WINEDLLOVERRIDES=${WINEDLLOVERRIDES:-<unset>}"
  if wine_mono_installed; then
    ls -1 "${WINEPREFIX}/drive_c/windows/mono"
  else
    echo "NO wine mono in this prefix"
  fi
  ls -1 /usr/share/wine/mono/ 2>/dev/null || echo "no mono MSI staged in the image"

  echo ""
  echo "== game folder =="
  ls -la "${GAME_DIR}" 2>/dev/null | head -20

  echo ""
  echo "== managed assemblies =="
  ls -la "${managed}" 2>/dev/null | grep -iE 'srml|assembly-csharp' || echo "none found"

  echo ""
  echo "== running the SRML installer verbosely =="
  ( cd "${GAME_DIR}" \
      && WINEDEBUG="err+all,fixme-all" wine SRMLInstaller.exe < /dev/null ) 2>&1 || true
  wineserver -w

  echo ""
  echo "== managed assemblies after =="
  ls -la "${managed}" 2>/dev/null | grep -iE 'srml|assembly-csharp' || echo "none found"

  echo ""
  echo "== unity player log (may be from an earlier run) =="
  local player_log
  player_log="$(find_player_log)"
  if [[ -n "${player_log}" ]]; then
    echo "${player_log}"
    tail -n 40 "${player_log}"
  else
    echo "none found"
  fi

  [[ -n "${XVFB_PID}" ]] && kill "${XVFB_PID}" 2>/dev/null || true
}

# --------------------------------------------------------------- steamcmd ---
fetch_game() {
  local have_game="no"
  [[ -f "${GAME_DIR}/SlimeRancher.exe" ]] && have_game="yes"

  case "${STEAM_UPDATE}" in
    never)  log "STEAM_UPDATE=never, skipping steamcmd"; return 0 ;;
    auto)   [[ "${have_game}" == "yes" ]] && { log "game already present, skipping steamcmd"; return 0; } ;;
    always) ;;
    *)      die "STEAM_UPDATE must be auto, always or never (got '${STEAM_UPDATE}')" ;;
  esac

  [[ -n "${STEAM_USER}" ]] || die "no game in ${GAME_DIR} and STEAM_USER is unset.
Either mount an existing install at ${GAME_DIR}, or set STEAM_USER and run the
one-time login first:  docker run --rm -it -v \"\$PWD/steam:/steam\" IMAGE login"

  mkdir -p "${GAME_DIR}"
  log "downloading app ${STEAM_APPID} (Windows depot) as ${STEAM_USER}"

  # @sSteamCmdForcePlatformType windows is what lets a Linux steamcmd fetch the
  # Windows build; it must come before force_install_dir.
  steamcmd \
    +@sSteamCmdForcePlatformType windows \
    +force_install_dir "${GAME_DIR}" \
    +login "${STEAM_USER}" \
    +app_update "${STEAM_APPID}" validate \
    +quit \
    || die "steamcmd failed. If it asked for a Steam Guard code, run the one-time
login first:  docker run --rm -it -v \"\$PWD/steam:/steam\" IMAGE login"

  [[ -f "${GAME_DIR}/SlimeRancher.exe" ]] \
    || die "steamcmd finished but ${GAME_DIR}/SlimeRancher.exe is missing"
  log "game downloaded"
}

# ------------------------------------------------------------------- SRML ---
install_srml() {
  # SRML patches Assembly-CSharp.dll in place and leaves Assembly-CSharp_old.dll
  # behind, which is how we detect an already-patched install.
  local managed="${GAME_DIR}/SlimeRancher_Data/Managed"

  if [[ -f "${managed}/SRML.dll" && -f "${managed}/Assembly-CSharp_old.dll" ]]; then
    log "SRML already installed"
    return 0
  fi

  local installer="${GAME_DIR}/SRMLInstaller.exe"
  if [[ -f "${installer}" ]]; then
    log "using SRMLInstaller.exe already in the game folder"
  elif [[ -f "${MODS_DIR}/SRMLInstaller.exe" ]]; then
    log "using SRMLInstaller.exe from ${MODS_DIR}"
    cp "${MODS_DIR}/SRMLInstaller.exe" "${installer}"
  else
    log "downloading SRMLInstaller.exe from ${SRML_URL}"
    # -f matters: without it curl saves a 404 page to the file and exits 0,
    # leaving something that looks like an installer but is HTML.
    curl -fsSL -o "${installer}" "${SRML_URL}" \
      || die "could not download SRML. Drop SRMLInstaller.exe into ${MODS_DIR} instead."
  fi

  # A stray HTML error page or truncated download looks like a file but is not
  # an executable; MZ is the DOS header every Windows binary starts with.
  if [[ "$(head -c 2 "${installer}")" != "MZ" ]]; then
    die "${installer} is not a Windows executable (probably a failed download).
Delete it and retry, or drop a known-good SRMLInstaller.exe into ${MODS_DIR}."
  fi

  # Wine Mono is what lets a .NET installer start at all. Say so up front,
  # because its absence otherwise shows up as a silent no-op.
  if [[ -d "${WINEPREFIX}/drive_c/windows/mono" ]]; then
    log "wine mono present"
  else
    log "WARNING: no wine mono in the prefix — a .NET installer cannot run."
    log "WARNING: delete the wine volume and recreate it to pick up a rebuilt image."
  fi

  log "patching the game with SRML"
  # The installer is a .NET console app that patches the folder it sits in. It
  # waits on a keypress at the end, so feed it stdin rather than letting it block.
  # Its output is the only diagnostic when the patch quietly does nothing, so
  # capture it with Wine's own errors turned back on and always echo it.
  local out="/tmp/srml-install.log"
  ( cd "${GAME_DIR}" \
      && WINEDEBUG="err+all,fixme-all" wine SRMLInstaller.exe < /dev/null ) \
    > "${out}" 2>&1 || true
  wineserver -w

  if [[ -s "${out}" ]]; then
    log "--- SRML installer output ---"
    sed 's/^/[srml] /' "${out}"
    log "--- end installer output ---"
  else
    log "the SRML installer produced no output at all"
  fi

  if [[ ! -f "${managed}/SRML.dll" ]]; then
    die "SRML patch did not produce ${managed}/SRML.dll.

Check the Wine output above. Common causes:
  * 'ShellExecuteEx failed' or a mono/.NET error — Wine could not run the
    installer. This image stages Wine Mono for exactly that; if it still fails,
    use the fallback below.
  * the game folder is incomplete or read-only.

Run 'srmp diagnose' for a verbose installer run.

Fallback that always works: install SRML on a Windows machine, then copy these
from that install into ${GAME_DIR} here:

  SlimeRancher_Data/Managed/Assembly-CSharp.dll      (the patched one)
  SlimeRancher_Data/Managed/Assembly-CSharp_old.dll
  SlimeRancher_Data/Managed/SRML.dll
  SlimeRancher_Data/Managed/SRML.Editor.dll
  SlimeRancher_Data/Managed/SRML.xml
  SRML/                                              (the whole folder)

The entrypoint detects a patched install and skips this step entirely."
  fi
  log "SRML installed"
}

# ------------------------------------------------------------------- SRMP ---
install_srmp() {
  local target="${GAME_DIR}/SRML/Mods/SRMP.dll"
  mkdir -p "${GAME_DIR}/SRML/Mods"

  if [[ -f "${MODS_DIR}/SRMP.dll" ]]; then
    log "installing SRMP.dll from ${MODS_DIR}"
    cp "${MODS_DIR}/SRMP.dll" "${target}"
  elif [[ -f "${target}" ]]; then
    log "using SRMP.dll already present in SRML/Mods"
  else
    die "no SRMP.dll found. Build it on Windows and mount it at ${MODS_DIR}.
Every client must run this exact same build."
  fi
}

# ---------------------------------------------------------------- autohost ---
write_config() {
  local data="${GAME_DIR}/SRMP"
  mkdir -p "${data}"

  # Operators arrive as a comma separated list; the config wants a JSON array.
  local SRMP_OPERATORS_JSON=""
  if [[ -n "${SRMP_OPERATORS}" ]]; then
    SRMP_OPERATORS_JSON="$(echo "${SRMP_OPERATORS}"       | tr ',' '
'       | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'       | grep -v '^$'       | sed 's/.*/"&"/'       | paste -sd, -)"
  fi

  # Rewritten every boot so the container environment stays the source of truth.
  cat > "${data}/autohost.json" <<JSON
{
  "Enabled": true,
  "Username": "${SRMP_USERNAME}",
  "GameName": "${SRMP_GAME}",
  "NewGameDisplayName": "${SRMP_NEW_GAME_NAME:-SRMP Server}",
  "GameMode": "${SRMP_GAMEMODE}",
  "CreateGameIfMissing": true,
  "LoadLatestSave": ${SRMP_LOAD_LATEST},
  "GodMode": ${SRMP_GOD_MODE},
  "TargetFrameRate": ${SRMP_TICK_RATE},
  "Operators": [${SRMP_OPERATORS_JSON}],
  "MaxPlayers": ${SRMP_SLOTS},
  "StartupDelaySeconds": 5.0,
  "LoginTimeoutSeconds": 90.0,
  "LoadTimeoutSeconds": 600.0,
  "ServerCodeFile": "servercode.txt",
  "StatusIntervalSeconds": ${SRMP_STATUS_INTERVAL:-60},
  "AutoSaveIntervalSeconds": ${SRMP_AUTOSAVE_INTERVAL:-300}
}
JSON
  log "autohost config written to ${data}/autohost.json"
  # A request left over from a previous crash would quit the server on sight.
  rm -f "${data}/servercode.txt" "${data}/shutdown.request"
}

# ---------------------------------------------------------------- shutdown ---
# Unity will not flush a save in response to a signal, so stopping cleanly is a
# handshake: ask the mod to save and quit, wait for the game to go away on its
# own, and only force it down if it stops responding.
shutdown() {
  local grace="${SHUTDOWN_GRACE_SECONDS:-45}"

  if [[ -z "${GAME_PID}" ]]; then
    wineserver -k 2>/dev/null || true
    [[ -n "${XVFB_PID}" ]] && kill "${XVFB_PID}" 2>/dev/null || true
    exit 0
  fi

  log "stop requested: asking the server to save and quit (up to ${grace}s)"
  mkdir -p "${GAME_DIR}/SRMP"
  : > "${GAME_DIR}/SRMP/shutdown.request"

  local waited=0
  while kill -0 "${GAME_PID}" 2>/dev/null && (( waited < grace )); do
    sleep 1
    waited=$((waited + 1))
  done

  if kill -0 "${GAME_PID}" 2>/dev/null; then
    log "WARNING: server did not quit within ${grace}s, terminating it"
    log "WARNING: progress since the last autosave may be lost"
    kill -TERM "${GAME_PID}" 2>/dev/null || true
    sleep 5
    kill -KILL "${GAME_PID}" 2>/dev/null || true
  else
    log "server saved and exited cleanly after ${waited}s"
  fi

  rm -f "${GAME_DIR}/SRMP/shutdown.request"
  wineserver -k 2>/dev/null || true
  [[ -n "${TAIL_PID}" ]] && kill "${TAIL_PID}" 2>/dev/null || true
  [[ -n "${XVFB_PID}" ]] && kill "${XVFB_PID}" 2>/dev/null || true
  log "container stopping"
  exit 0
}

# ------------------------------------------------------------------ serve ---
run_server() {
  start_xvfb
  init_wine
  link_saves
  fetch_game
  install_srml
  install_srmp
  write_config

  trap shutdown TERM INT

  local render_args=()
  case "${RENDER_MODE}" in
    nographics)
      # Cheapest on a GPU-less VPS: Unity skips rendering entirely. Not every
      # non-server build tolerates this, so fall back to software if it misbehaves.
      log "render mode: nographics (no rendering at all)"
      render_args=(-batchmode -nographics)
      ;;
    software)
      # No -force-glcore here: this build ships only D3D11 shaders, so forcing
      # OpenGL Core makes InitializeEngineGraphics fail before the game starts.
      # Letting it pick D3D11 routes through Wine's wined3d onto llvmpipe.
      log "render mode: software (D3D11 via wined3d on llvmpipe)"
      render_args=(-screen-width 640 -screen-height 480 -screen-fullscreen 0)
      ;;
    *)
      die "RENDER_MODE must be software or nographics (got '${RENDER_MODE}')"
      ;;
  esac

  cd "${GAME_DIR}"
  log "launching Slime Rancher headless as '${SRMP_USERNAME}' (${SRMP_SLOTS} slots)"

  # Wine's own errors are the only clue when the game dies on startup, so turn
  # them back on here regardless of the quieter default used elsewhere.
  local game_out="/tmp/game-stdout.log"
  : > "${game_out}"

  WINEDEBUG="err+all,fixme-all" wine SlimeRancher.exe \
    -srmp-autohost \
    -srmp-username "${SRMP_USERNAME}" \
    -srmp-gamemode "${SRMP_GAMEMODE}" \
    -srmp-slots "${SRMP_SLOTS}" \
    ${SRMP_GAME:+-srmp-game "${SRMP_GAME}"} \
    "${render_args[@]}" > "${game_out}" 2>&1 &
  GAME_PID=$!

  # Stream whatever the game says straight into docker logs.
  tail -n +1 -F "${game_out}" 2>/dev/null | sed 's/^/[game] /' &
  local game_tail_pid=$!

  # Surface the friend code as soon as the mod publishes it.
  (
    for _ in $(seq 1 900); do
      if [[ -s "${GAME_DIR}/SRMP/servercode.txt" ]]; then
        printf '\n  ===================================\n'
        printf '   FRIEND CODE: %s\n' "$(cat "${GAME_DIR}/SRMP/servercode.txt")"
        printf '  ===================================\n\n'
        exit 0
      fi
      sleep 1
    done
    log "WARNING: no friend code after 15 minutes, check the log above"
  ) &

  # Follow the SRMP log once it appears. This has to run in the background:
  # waiting for it inline would hide a game that dies during startup.
  (
    local found=""
    for _ in $(seq 1 180); do
      found="$(ls -1t "${GAME_DIR}/SRMP/Logs"/log-*.txt 2>/dev/null | head -n1 || true)"
      if [[ -n "${found}" ]]; then
        echo "[srmp-server] following ${found}"
        exec tail -n +1 -F "${found}"
      fi
      sleep 1
    done
    echo "[srmp-server] WARNING: no SRMP log in ${GAME_DIR}/SRMP/Logs — the mod may not have loaded"
  ) &
  TAIL_PID=$!

  local exit_code=0
  wait "${GAME_PID}" || exit_code=$?

  #let the streamed output catch up before anything is torn down
  sleep 2
  log "game exited with code ${exit_code}"

  if (( exit_code != 0 )); then
    dump_crash_diagnostics "${game_out}"
  fi

  kill "${game_tail_pid}" 2>/dev/null || true
  [[ -n "${TAIL_PID}" ]] && kill "${TAIL_PID}" 2>/dev/null || true
  [[ -n "${XVFB_PID}" ]] && kill "${XVFB_PID}" 2>/dev/null || true
  exit "${exit_code}"
}

# Unity records far more in its own Player.log than it prints to stdout, so pull
# that in too when the game fails to start.
find_player_log() {
  find "${WINEPREFIX}/drive_c/users" \
    -path '*Monomi Park/Slime Rancher/Player.log' -type f 2>/dev/null | head -n1
}

dump_crash_diagnostics() {
  local game_out="$1"

  log "--- the game failed to start; diagnostics follow ---"

  if [[ -s "${game_out}" ]]; then
    log "--- last 60 lines of game output ---"
    tail -n 60 "${game_out}" | sed 's/^/[game] /'
  else
    log "the game produced no output at all"
  fi

  local player_log
  player_log="$(find_player_log)"
  if [[ -n "${player_log}" ]]; then
    log "--- last 60 lines of ${player_log} ---"
    tail -n 60 "${player_log}" | sed 's/^/[unity] /'
  else
    log "no Unity Player.log found — the game did not get far enough to write one"
  fi

  log "--- end diagnostics ---"
  log "If this mentions steam_api64 or Steam, the game wants a running Steam"
  log "client. If it mentions GL or the display, try RENDER_MODE=nographics."
}

# ------------------------------------------------------------------- main ---
COMMAND="${1:-serve}"
shift || true

case "${COMMAND}" in
  serve)          run_server ;;
  login)          run_login ;;
  code)           run_code ;;
  stop)           run_stop ;;
  saves)          run_saves ;;
  diagnose)       run_diagnose ;;
  shell|bash)     exec bash "$@" ;;
  help|--help|-h) usage ;;
  *)
    # Anything else is run verbatim, so `docker run IMAGE steamcmd +quit` works.
    exec "${COMMAND}" "$@"
    ;;
esac
