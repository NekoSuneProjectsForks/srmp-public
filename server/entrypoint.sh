#!/usr/bin/env bash
# Headless SRMP host. Optionally pulls the game with steamcmd, patches SRML into
# it, installs the SRMP mod, then runs the game with auto-hosting enabled on a
# machine with no GPU and no display.
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
one-time login first:  docker compose run --rm --entrypoint steam-login.sh srmp"

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
login first:  docker compose run --rm --entrypoint steam-login.sh srmp"

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
    die "no SRMP.dll found. Build it on Windows and put it in ${MODS_DIR}.
Every client must run this exact same build."
  fi
}

# ---------------------------------------------------------------- autohost ---
write_config() {
  local data="${GAME_DIR}/SRMP"
  mkdir -p "${data}"

  # Rewritten every boot so the compose file stays the source of truth.
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
  rm -f "${data}/servercode.txt"
}

# ---------------------------------------------------------------- shutdown ---
shutdown() {
  log "shutting down, asking the game to close cleanly..."
  [[ -n "${GAME_PID}" ]] && kill -TERM "${GAME_PID}" 2>/dev/null || true
  for _ in $(seq 1 30); do
    [[ -n "${GAME_PID}" ]] && kill -0 "${GAME_PID}" 2>/dev/null || break
    sleep 1
  done
  wineserver -k 2>/dev/null || true
  [[ -n "${TAIL_PID}" ]] && kill "${TAIL_PID}" 2>/dev/null || true
  [[ -n "${XVFB_PID}" ]] && kill "${XVFB_PID}" 2>/dev/null || true
  exit 0
}

# -------------------------------------------------------------------- main ---
start_xvfb
init_wine
fetch_game
install_srml
install_srmp
write_config

trap shutdown TERM INT

RENDER_ARGS=()
case "${RENDER_MODE}" in
  nographics)
    # Cheapest on a GPU-less VPS: Unity skips rendering entirely. Not every
    # non-server build tolerates this, so fall back to software if it misbehaves.
    log "render mode: nographics (no rendering at all)"
    RENDER_ARGS=(-batchmode -nographics)
    ;;
  software)
    log "render mode: software (llvmpipe on Xvfb)"
    RENDER_ARGS=(-screen-width 640 -screen-height 480 -screen-fullscreen 0 -force-glcore)
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
  "${RENDER_ARGS[@]}" &
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
LOGFILE=""
for _ in $(seq 1 180); do
  LOGFILE="$(ls -1t "${GAME_DIR}/SRMP/Logs"/log-*.txt 2>/dev/null | head -n1 || true)"
  [[ -n "${LOGFILE}" ]] && break
  sleep 1
done
if [[ -n "${LOGFILE}" ]]; then
  log "following ${LOGFILE}"
  tail -n +1 -F "${LOGFILE}" &
  TAIL_PID=$!
else
  log "WARNING: no SRMP log in ${GAME_DIR}/SRMP/Logs — the mod may not have loaded"
fi

EXIT_CODE=0
wait "${GAME_PID}" || EXIT_CODE=$?
log "game exited with code ${EXIT_CODE}"
[[ -n "${TAIL_PID}" ]] && kill "${TAIL_PID}" 2>/dev/null || true
[[ -n "${XVFB_PID}" ]] && kill "${XVFB_PID}" 2>/dev/null || true
exit "${EXIT_CODE}"
