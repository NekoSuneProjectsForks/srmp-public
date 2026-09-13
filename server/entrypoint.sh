#!/usr/bin/env bash
# Headless SRMP host.
#
# Commands:
#   serve   (default) install anything missing, then run the server
#   login             one-time interactive Steam login (answers Steam Guard)
#   code              print the current friend code and exit
#   stop              ask a running server to save and quit
#   shell             drop into a shell inside the runtime
set -euo pipefail

GAME_DIR="${GAME_DIR:-/game}"
MODS_DIR="${MODS_DIR:-/mods}"
STEAM_APPID="${STEAM_APPID:-433340}"
STEAM_USER="${STEAM_USER:-}"
STEAM_UPDATE="${STEAM_UPDATE:-auto}"      # auto | always | never
RENDER_MODE="${RENDER_MODE:-software}"    # software | nographics
DISPLAY_NUM="${DISPLAY_NUM:-99}"
SCREEN="${SCREEN:-640x480x24}"
SRML_URL="${SRML_URL:-https://github.com/Sm0lDelta/SRML/releases/latest/download/SRMLInstaller.exe}"

SRMP_USERNAME="${SRMP_USERNAME:-Server}"
SRMP_GAME="${SRMP_GAME:-}"
SRMP_GAMEMODE="${SRMP_GAMEMODE:-CLASSIC}"

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
  log "starting Xvfb on ${DISPLAY} (${SCREEN})"
  Xvfb "${DISPLAY}" -screen 0 "${SCREEN}" -nolisten tcp &
  XVFB_PID=$!
  for _ in $(seq 1 50); do
    [[ -e "/tmp/.X11-unix/X${DISPLAY_NUM}" ]] && return 0
    sleep 0.1
  done
  die "Xvfb failed to start"
}

init_wine() {
  if [[ -d "${WINEPREFIX}/drive_c" ]]; then
    return 0
  fi

  # The image bakes a prefix template at build time; reuse it when present so
  # the operator's first boot is quick.
  local template="${WINE_TEMPLATE:-/opt/wine-template}"
  if [[ -d "${template}/drive_c" ]]; then
    log "seeding wine prefix from image template"
    mkdir -p "${WINEPREFIX}"
    cp -a "${template}/." "${WINEPREFIX}/"
    return 0
  fi

  log "initializing wine prefix at ${WINEPREFIX} (first run, takes a minute)"
  wineboot --init >/dev/null 2>&1 || true
  wineserver -w
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
  if [[ ! -f "${installer}" ]]; then
    if [[ -f "${MODS_DIR}/SRMLInstaller.exe" ]]; then
      log "using SRMLInstaller.exe from ${MODS_DIR}"
      cp "${MODS_DIR}/SRMLInstaller.exe" "${installer}"
    else
      log "downloading SRMLInstaller.exe from ${SRML_URL}"
      curl -sSL -o "${installer}" "${SRML_URL}" \
        || die "could not download SRML. Drop SRMLInstaller.exe into ${MODS_DIR} instead."
    fi
  fi

  log "patching the game with SRML"
  # The installer is a console app that patches the folder it sits in. It waits
  # on a keypress at the end, so feed it stdin rather than letting it block.
  ( cd "${GAME_DIR}" && wine SRMLInstaller.exe < /dev/null ) || true
  wineserver -w

  [[ -f "${managed}/SRML.dll" ]] \
    || die "SRML patch did not produce ${managed}/SRML.dll — check the Wine output above"
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

  # Rewritten every boot so the container environment stays the source of truth.
  cat > "${data}/autohost.json" <<JSON
{
  "Enabled": true,
  "Username": "${SRMP_USERNAME}",
  "GameName": "${SRMP_GAME}",
  "NewGameDisplayName": "${SRMP_NEW_GAME_NAME:-SRMP Server}",
  "GameMode": "${SRMP_GAMEMODE}",
  "CreateGameIfMissing": true,
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
      log "render mode: software (llvmpipe on Xvfb)"
      render_args=(-screen-width 640 -screen-height 480 -screen-fullscreen 0 -force-glcore)
      ;;
    *)
      die "RENDER_MODE must be software or nographics (got '${RENDER_MODE}')"
      ;;
  esac

  cd "${GAME_DIR}"
  log "launching Slime Rancher headless as '${SRMP_USERNAME}'"

  wine SlimeRancher.exe \
    -srmp-autohost \
    -srmp-username "${SRMP_USERNAME}" \
    -srmp-gamemode "${SRMP_GAMEMODE}" \
    ${SRMP_GAME:+-srmp-game "${SRMP_GAME}"} \
    "${render_args[@]}" &
  GAME_PID=$!

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

  # Follow the SRMP log so docker logs shows what the server is doing. The mod
  # writes a fresh timestamped file per run, so wait for it and tail the newest.
  local logfile=""
  for _ in $(seq 1 180); do
    logfile="$(ls -1t "${GAME_DIR}/SRMP/Logs"/log-*.txt 2>/dev/null | head -n1 || true)"
    [[ -n "${logfile}" ]] && break
    sleep 1
  done
  if [[ -n "${logfile}" ]]; then
    log "following ${logfile}"
    tail -n +1 -F "${logfile}" &
    TAIL_PID=$!
  else
    log "WARNING: no SRMP log in ${GAME_DIR}/SRMP/Logs — the mod may not have loaded"
  fi

  local exit_code=0
  wait "${GAME_PID}" || exit_code=$?
  log "game exited with code ${exit_code}"
  [[ -n "${TAIL_PID}" ]] && kill "${TAIL_PID}" 2>/dev/null || true
  [[ -n "${XVFB_PID}" ]] && kill "${XVFB_PID}" 2>/dev/null || true
  exit "${exit_code}"
}

# ------------------------------------------------------------------- main ---
COMMAND="${1:-serve}"
shift || true

case "${COMMAND}" in
  serve)          run_server ;;
  login)          run_login ;;
  code)           run_code ;;
  stop)           run_stop ;;
  shell|bash)     exec bash "$@" ;;
  help|--help|-h) usage ;;
  *)
    # Anything else is run verbatim, so `docker run IMAGE steamcmd +quit` works.
    exec "${COMMAND}" "$@"
    ;;
esac
