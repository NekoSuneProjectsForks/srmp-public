# Headless SRMP host runtime: Slime Rancher (Windows build) under Wine, no GPU.
#
# This image contains the RUNTIME ONLY. It deliberately ships no game content,
# no SRML and no SRMP build:
#   * nothing game-related is downloaded at build time;
#   * the game, SRML and the mod are fetched/installed by the entrypoint on the
#     operator's own server, the first time the container is started.
# That keeps the published image free of proprietary game files and lets it be
# built on public CI runners.
FROM debian:bookworm-slim

ENV DEBIAN_FRONTEND=noninteractive

# i386 is required twice over: Wine needs 32-bit support even for a 64-bit
# game, and steamcmd is a 32-bit binary.
RUN dpkg --add-architecture i386 \
 && apt-get update \
 && apt-get install -y --no-install-recommends \
      wine wine64 wine32:i386 \
      xvfb xauth \
      cabextract unzip curl ca-certificates \
      lib32gcc-s1 lib32stdc++6 \
      libgl1 libgl1-mesa-dri libglx-mesa0 \
      libasound2 \
      procps tini \
 && rm -rf /var/lib/apt/lists/*

# SteamCMD: the downloader tool only. No game is downloaded here — the operator
# supplies credentials at runtime, or mounts an install they already have.
RUN mkdir -p /opt/steamcmd \
 && curl -sSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz \
    | tar -xz -C /opt/steamcmd \
 && ln -s /opt/steamcmd/steamcmd.sh /usr/local/bin/steamcmd

# Pre-build a Wine prefix template so the operator's first boot does not pay for
# it. /wine is a volume at runtime, so this is staged elsewhere and copied in.
# Non-fatal: the entrypoint falls back to initializing the prefix itself.
RUN set -eux; \
    Xvfb :98 -screen 0 640x480x24 -nolisten tcp & \
    XVFB_PID=$!; \
    sleep 3; \
    ( DISPLAY=:98 WINEPREFIX=/opt/wine-template WINEDLLOVERRIDES="mscoree,mshtml=" \
        WINEDEBUG=-all wineboot --init >/dev/null 2>&1 || true ); \
    ( WINEPREFIX=/opt/wine-template wineserver -w || true ); \
    kill "$XVFB_PID" 2>/dev/null || true; \
    test -d /opt/wine-template/drive_c || echo "wine prefix template not built; runtime will init it"

# No GPU on a VPS: render in software via llvmpipe when rendering is needed.
ENV LIBGL_ALWAYS_SOFTWARE=1 \
    GALLIUM_DRIVER=llvmpipe \
    MESA_LOADER_DRIVER_OVERRIDE=llvmpipe \
    WINEPREFIX=/wine \
    WINE_TEMPLATE=/opt/wine-template \
    WINEDLLOVERRIDES="mscoree,mshtml=" \
    WINEDEBUG="fixme-all,err-all" \
    GAME_DIR=/game \
    MODS_DIR=/mods \
    STEAM_APPID=433340 \
    DISPLAY_NUM=99

COPY entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/entrypoint.sh

VOLUME ["/game", "/wine", "/steam"]

LABEL org.opencontainers.image.title="SRMP headless server runtime" \
      org.opencontainers.image.description="Wine + Xvfb runtime for hosting Slime Rancher Multiplayer headless. Contains no game content." \
      org.opencontainers.image.licenses="MIT"

# The entrypoint dispatches on its first argument: serve (default), login,
# code, stop or shell. See `docker run IMAGE help`.
ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/entrypoint.sh"]
CMD ["serve"]
