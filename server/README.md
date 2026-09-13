# Running SRMP as a headless server on Linux

A 24/7 SRMP host on a GPU-less Linux VPS. The container pulls the game with
SteamCMD, patches SRML into it, installs the SRMP mod, and runs it with
auto-hosting on. Players join with a friend code.

## Read this first

Slime Rancher has **no Linux build and no dedicated-server build**. The SRMP
server is not a separate program — it is the game itself acting as host, because
every authoritative decision is read out of the live game scene. What runs here
is the Windows game under Wine with an idle host character standing in the world.

Consequences worth knowing before you commit a box to this:

- It uses a full game instance's RAM and CPU. Budget ~4 GB RAM and 2+ cores.
- Players join by **friend code only**. There is no `ip:port` join — that code
  path was removed when the mod moved to Epic Online Services relay.
- Because it relays through EOS, you do **not** need to forward any ports. You
  do need working outbound internet.
- Lobby cap is 16 players; the host counts as one.
- Every client must run the **exact same `SRMP.dll` build** as the server. The
  build bumps its version each compile and the mod rejects mismatched versions
  on connect, so ship your players the same file you put on the server.

## What is in the image

The image published by CI is a **runtime only**. It ships Wine, Xvfb, Mesa
software rendering and the SteamCMD tool — and no game content whatsoever: no
Slime Rancher files, no SRML, no SRMP build. Nothing game-related is downloaded
when the image is built, which is what lets it be built on public GitHub runners
and published openly.

Everything game-related happens on **your** server, at first container start:
the entrypoint downloads the game with your Steam credentials (or uses an
install you mounted), patches SRML into it, and installs your `SRMP.dll`.

```bash
docker pull ghcr.io/nekosunevr/srmp-public-server:latest
```

A CI run builds and pushes this on every change under `server/`. To build it
yourself instead, comment out `image:` in `docker-compose.yml` and uncomment
`build: .`.

## Setup

### 1. Build the mod on Windows

The mod builds against Windows game assemblies, so build it there and carry the
DLL over:

```powershell
.\scripts\Build-SRMP.ps1 -Configuration SRML
# produces Builds\SRMP\SRMP.dll
```

### 2. Stage it on the Linux box

```bash
mkdir -p ~/srmp/{game,mods,steam}
cp /path/to/SRMP.dll ~/srmp/mods/
cd ~/srmp
```

### 3. Log in to Steam once

Steam Guard cannot be answered by an unattended container, so do it once by
hand. SteamCMD caches the result in `./steam` and later boots reuse it.

```bash
docker run --rm -it \
  -v "$PWD/steam:/steam" \
  -e STEAM_USER=your_steam_name \
  ghcr.io/nekosunevr/srmp-public-server:latest login
```

`-it` is required — without a terminal there is nowhere to type the Steam Guard
code.

> You must own Slime Rancher on that account. SteamCMD will not download a game
> the account does not own.

### 4. Start it

```bash
docker run -d --name srmp-server \
  --restart unless-stopped \
  --shm-size 1g \
  --stop-timeout 60 \
  -v "$PWD/game:/game" \
  -v "$PWD/mods:/mods" \
  -v "$PWD/steam:/steam" \
  -v srmp-wine:/wine \
  -e STEAM_USER=your_steam_name \
  -e SRMP_USERNAME=Server \
  -e RENDER_MODE=software \
  ghcr.io/nekosunevr/srmp-public-server:latest
```

First boot downloads the game (~1.2 GB), patches SRML and creates a world, so
give it a while. Watch it with `docker logs -f srmp-server`. When the host is up:

```
  ===================================
   FRIEND CODE: A7K2M9Q
  ===================================
```

Ask for it any time:

```bash
docker exec srmp-server srmp code
```

**The friend code changes on every restart.** It is generated per lobby, not
stored.

## Commands built into the image

No scripts to download — the image dispatches on its first argument:

| Command | What it does |
| --- | --- |
| `serve` | Default. Installs anything missing, then runs the server. |
| `login` | One-time interactive Steam login. Needs `-it`. |
| `code` | Prints the current friend code. |
| `stop` | Asks a running server to save and quit. |
| `shell` | A shell inside the runtime, for poking around. |
| `help` | Usage, with copy-pasteable examples. |

At container start the image runs `serve` by default, so `docker run IMAGE` just
works. `docker exec` bypasses the entrypoint, so use the `srmp` command there:
`docker exec srmp-server srmp code`.

```bash
docker run --rm ghcr.io/nekosunevr/srmp-public-server:latest help
```

## Using docker compose instead

`docker-compose.yml` in this folder does the same thing if you prefer it:

```bash
docker compose run --rm -it srmp login   # one-time
docker compose up -d
docker compose logs -f
```

## Running without a GPU

A VPS has no GPU, so `RENDER_MODE` picks how the game deals with that:

| Mode | What it does | Trade-off |
| --- | --- | --- |
| `software` (default) | Renders at 640x480 through Mesa llvmpipe on an Xvfb virtual display. | Works the way a normal game launch does, but burns CPU drawing frames nobody sees. |
| `nographics` | `-batchmode -nographics`; Unity skips rendering entirely. | Far cheaper. Not every non-server Unity build tolerates it — the game may fail to start or misbehave. |

Start with `software`. Once it is up and stable, try `nographics` — if the
server still reaches "FRIEND CODE", keep it, since it saves most of the CPU.

## Already have the game on the box?

Skip SteamCMD entirely: mount your install at `./game` and set
`STEAM_UPDATE: "never"`. SRML and the mod are still installed automatically if
missing.

## Configuration

| Variable | Meaning |
| --- | --- |
| `STEAM_USER` | Steam account that owns the game. Blank means no download. |
| `STEAM_UPDATE` | `auto` (download only if missing), `always`, or `never`. |
| `RENDER_MODE` | `software` or `nographics`. See above. |
| `SRMP_USERNAME` | Name the host player appears as. |
| `SRMP_GAME` | Existing save to host. Blank creates a new world. |
| `SRMP_NEW_GAME_NAME` | Display name used when creating a new world. |
| `SRMP_GAMEMODE` | `CLASSIC`, `CASUAL`, `TIME_LIMIT` or `TIME_LIMIT_V2`. |
| `SRMP_STATUS_INTERVAL` | Seconds between "N players online" lines. `0` disables. |
| `SRMP_AUTOSAVE_INTERVAL` | Seconds between forced saves. `0` disables. |
| `SRML_URL` | Where to fetch `SRMLInstaller.exe`. Override if the default 404s. |
| `SHUTDOWN_GRACE_SECONDS` | How long to let the game save and quit on stop. Default 45. |

The entrypoint rewrites `game/SRMP/autohost.json` from these on every boot, so
edit the compose file rather than the JSON.

To host an existing save, set `SRMP_GAME` to the world's name as shown in the
game's load menu. The mod matches the internal game name first and the display
name second, then picks that world's newest save.

## Stopping it safely

```bash
docker stop srmp-server        # or: docker compose stop
```

Unity will not flush a save in response to a signal, so stopping is a handshake
rather than a kill:

1. `docker stop` sends `SIGTERM` to the entrypoint.
2. The entrypoint writes `game/SRMP/shutdown.request`.
3. The mod sees it, tells connected players in chat, calls the game's own save,
   waits for it to flush, closes the lobby and quits.
4. The entrypoint sees the game exit and the container stops.

This takes a few seconds. `SHUTDOWN_GRACE_SECONDS` (default 45) is how long the
entrypoint waits before forcing the game down; if it has to force it, it says so
in the log and you lose progress since the last autosave. Your Docker stop
timeout must be **larger** than that value — hence `--stop-timeout 60` above, and
`stop_grace_period: 60s` in the compose file.

To stop it from elsewhere without stopping the container:

```bash
docker exec srmp-server srmp stop
```

Do not `docker kill` or `kill -9` — that skips the save entirely.

## Troubleshooting

**`steamcmd failed` / it asks for a Steam Guard code** — run the one-time login
in step 3. Unattended runs cannot answer that prompt.

**`no game in /game and STEAM_USER is unset`** — set `STEAM_USER`, or mount an
existing install and set `STEAM_UPDATE: "never"`.

**`SRML patch did not produce .../SRML.dll`** — the SRML download or the Wine
run failed. Check the Wine output above it. You can sidestep the download by
dropping a known-good `SRMLInstaller.exe` into `./mods`.

**`no SRMP.dll found`** — build it on Windows and copy it into `./mods`.

**No SRMP log appears** — SRML did not load the mod. Check that
`game/SlimeRancher_Data/Managed/Assembly-CSharp_old.dll` exists, which is the
marker that SRML actually patched the game.

**Server starts but no friend code** — EOS login failed. Look for `EOS login did
not complete` in the log. EOS relay needs outbound internet; a blocked egress
will do this.

**Game will not launch under Wine at all** — the most likely cause is
`steam_api64.dll` expecting a running Steam client. If you hit this, the
workaround is running the Steam client inside the same Wine prefix, which is a
heavier setup than this image provides.
