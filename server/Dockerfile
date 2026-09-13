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
#
# steamcmd.sh locates linux32/steamcmd relative to its own $0, so symlinking it
# into /usr/local/bin makes it search the wrong directory and fail at runtime
# with "linux32/steamcmd: No such file or directory". A wrapper that cds first
# is the fix. It is then bootstrapped once so the operator's first run is quick.
# steamcmd's exit code is unreliable on a first run, so the file test is the
# real gate on the layout being correct.
RUN mkdir -p /opt/steamcmd \
 && curl -sSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz \
    | tar -xz -C /opt/steamcmd \
 && printf '#!/bin/sh\ncd /opt/steamcmd || exit 1\nexec ./steamcmd.sh "$@"\n' > /usr/local/bin/steamcmd \
 && chmod +x /usr/local/bin/steamcmd \
 && ( /usr/local/bin/steamcmd +quit >/dev/null 2>&1 || true ) \
 && test -x /opt/steamcmd/linux32/steamcmd

# Wine Mono. SRML's installer is a .NET console application, so Wine needs a
# .NET runtime to run it — without this it dies with
# "ShellExecuteEx failed: File not found". Staging the MSI here lets wineboot
# install it into every prefix automatically and offline.
# Debian bookworm ships Wine 8.0, which pairs with Wine Mono 8.0.0. Override
# with --build-arg WINE_MONO_VERSION=... if the wine package here changes.
ARG WINE_MONO_VERSION=8.0.0
ENV WINE_MONO_MSI=/usr/share/wine/mono/wine-mono-${WINE_MONO_VERSION}-x86.msi
RUN mkdir -p /usr/share/wine/mono \
 && curl -fsSL -o "${WINE_MONO_MSI}" \
      "https://dl.winehq.org/wine/wine-mono/${WINE_MONO_VERSION}/wine-mono-${WINE_MONO_VERSION}-x86.msi" \
 && test -s "${WINE_MONO_MSI}"

# Pre-build a Wine prefix template so the operator's first boot does not pay for
# it. /wine is a volume at runtime, so this is staged elsewhere and copied in.
# Non-fatal: the entrypoint falls back to initializing the prefix itself.
# Mono is also installed explicitly with msiexec rather than relying on wineboot
# picking the staged MSI up, because that only happens when the filename matches
# the version this Wine build expects.
RUN set -eux; \
    Xvfb :98 -screen 0 640x480x24 -nolisten tcp & \
    XVFB_PID=$!; \
    sleep 3; \
    export DISPLAY=:98 WINEPREFIX=/opt/wine-template WINEDLLOVERRIDES="mshtml=" WINEDEBUG=-all; \
    ( wineboot --init >/dev/null 2>&1 || true ); \
    ( wineserver -w || true ); \
    ( wine msiexec /i "${WINE_MONO_MSI}" /qn >/dev/null 2>&1 || true ); \
    ( wineserver -w || true ); \
    kill "$XVFB_PID" 2>/dev/null || true; \
    test -d /opt/wine-template/drive_c || echo "wine prefix template not built; runtime will init it"

# No GPU on a VPS: render in software via llvmpipe when rendering is needed.
# WINEDLLOVERRIDES disables mshtml (nothing here renders HTML) but must leave
# mscoree enabled — that is the .NET shim the SRML installer needs to start.
# WINEDEBUG silences fixme noise but keeps err messages: those are usually the
# only explanation when the game or the installer dies on startup.
ENV LIBGL_ALWAYS_SOFTWARE=1 \
    GALLIUM_DRIVER=llvmpipe \
    MESA_LOADER_DRIVER_OVERRIDE=llvmpipe \
    WINEPREFIX=/wine \
    WINE_TEMPLATE=/opt/wine-template \
    WINEDLLOVERRIDES="mshtml=" \
    WINEDEBUG="fixme-all" \
    GAME_DIR=/game \
    MODS_DIR=/mods \
    STEAM_APPID=433340 \
    DISPLAY_NUM=99

COPY entrypoint.sh /usr/local/bin/
# `docker exec` bypasses ENTRYPOINT, so expose the same dispatcher as a plain
# command: `docker exec srmp-server srmp code`.
RUN chmod +x /usr/local/bin/entrypoint.sh \
 && ln -s /usr/local/bin/entrypoint.sh /usr/local/bin/srmp

VOLUME ["/game", "/wine", "/steam"]

LABEL org.opencontainers.image.title="SRMP headless server runtime" \
      org.opencontainers.image.description="Wine + Xvfb runtime for hosting Slime Rancher Multiplayer headless. Contains no game content." \
      org.opencontainers.image.licenses="MIT"

# The entrypoint dispatches on its first argument: serve (default), login,
# code, stop or shell. See `docker run IMAGE help`.
ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/entrypoint.sh"]
CMD ["serve"]
