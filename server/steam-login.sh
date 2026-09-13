#!/usr/bin/env bash
# One-time interactive Steam login.
#
# Steam Guard cannot be answered by an unattended container, so log in once
# interactively; steamcmd then caches the sentry file in /steam and every later
# unattended run reuses it.
#
#   docker compose run --rm --entrypoint steam-login.sh srmp
set -euo pipefail

STEAM_USER="${STEAM_USER:-}"

if [[ -z "${STEAM_USER}" ]]; then
  read -r -p "Steam username: " STEAM_USER
fi

export HOME=/steam
mkdir -p /steam

echo "Logging in as ${STEAM_USER}. Enter your password and Steam Guard code when asked."
steamcmd +login "${STEAM_USER}" +quit

echo ""
echo "Login cached in the /steam volume. Unattended runs will reuse it."
echo "Set STEAM_USER=${STEAM_USER} in docker-compose.yml and start normally."
