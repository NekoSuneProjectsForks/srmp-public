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
- Lobby capacity defaults to 16 including the host, configurable up to 64 via `SRMP_SLOTS`.
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
  -e RENDER_MODE=nographics \
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
| `saves` | Lists the worlds this server has saved. |
| `diagnose` | Dumps Wine/SRML state and runs the installer verbosely. |
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
| `nographics` (default) | `-batchmode -nographics`; Unity skips graphics initialization entirely. | Cheapest, and avoids the whole graphics problem. Not every non-server Unity build tolerates it. |
| `software` | Runs the game's real D3D11 path through Wine's wined3d on Mesa llvmpipe, at 640x480. | Closer to a normal launch, but burns CPU drawing frames nobody sees. |

**Do not add `-force-glcore`.** This build ships D3D11 shaders only, so forcing
OpenGL Core kills the game before it starts:

```
Forced GfxDevice 'OpenGL Core' was not built from editor, shaders will not be available
InitializeEngineGraphics failed
```

If `nographics` misbehaves, switch to `software` — it keeps the D3D11 path the
game actually ships shaders for.

## Sizing the server

**First, a correction worth knowing:** Docker applies **no memory limit** unless
you set one. If you never set `mem_limit`, the container was already free to use
all host RAM — so a "default RAM limit" was not throttling anything, and the
1–2 GB you observed was simply what the game uses.

### How resources actually scale with players

SRMP is a listen server: the host runs one complete game world, and **the world
is the same size whether one player is connected or twelve**. Each extra player
adds a player object, their held actors, and their share of packet traffic — not
another copy of the world. So:

| Resource | How it scales | Why |
| --- | --- | --- |
| RAM | Barely | One world, loaded once. Extra players are small objects. |
| CPU | Noticeably | The host arbitrates every actor and every packet for everyone. |
| Bandwidth | Linearly | Every update is relayed to every other player. |

**RAM is the resource least likely to be your problem.** CPU is the one that
bites.

### Rough guidance

These are estimates from the observed 1–2 GB baseline, not measurements across
player counts — I have not profiled this at each slot count:

| Slots | `mem_limit` | `cpus` |
| --- | --- | --- |
| 2–4 | 3g | 2 |
| 8 | 4g | 2–3 |
| 16 (default) | 4–6g | 3–4 |
| 32+ | 8g | 4+ |

Set `mem_limit` comfortably above real usage. A limit that is too low does not
degrade gracefully — the container gets OOM-killed and the world reverts to the
last autosave.

### If players are desyncing, suspect CPU before RAM

Remote player positions are sent from the game's `Update` loop, so **the host's
frame rate directly sets how often everyone else's updates go out**. A host
starved of CPU sends fewer updates, and every client sees other players stutter
and slide. Under `RENDER_MODE=software` the host also burns CPU on software
rendering nobody looks at.

Two things to try, in order:

1. `RENDER_MODE=nographics` — stops paying for frames nobody sees.
2. Raise `cpus`, and check the host is not oversubscribed. `docker stats
   srmp-server` showing CPU pinned near its limit means the host cannot keep up.

Watch the ping column in the multiplayer menu while testing: if pings are low
but players still slide around, it is host frame rate, not the network.

## Already have the game on the box?

Skip SteamCMD entirely: mount your install at `./game` and set
`STEAM_UPDATE: "never"`. SRML and the mod are still installed automatically if
missing.

## Configuration

| Variable | Meaning |
| --- | --- |
| `STEAM_USER` | Steam account that owns the game. Blank means no download. |
| `STEAM_UPDATE` | `auto` (download only if missing), `always`, or `never`. |
| `RENDER_MODE` | `nographics` (default) or `software`. See above. |
| `SRMP_USERNAME` | Name the host player appears as. |
| `SRMP_GAME` | Existing save to host. Blank creates a new world. |
| `SRMP_NEW_GAME_NAME` | Display name used when creating a new world. |
| `SRMP_GAMEMODE` | `CLASSIC`, `CASUAL`, `TIME_LIMIT` or `TIME_LIMIT_V2`. |
| `SRMP_SLOTS` | Lobby capacity including the host (2-64). Default 16. |
| `SRMP_LOAD_LATEST` | With `SRMP_GAME` blank, continue the newest world instead of creating one. Default true. |
| `SRMP_HIDE_PLAYER` | Park the host character underground and make it invulnerable. Default true. |
| `SRMP_PARK_DEPTH` | How far below its start point the host is parked, in metres. Default 40. |
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

**`SRML patch did not produce .../SRML.dll`** — the installer did not run. Read
the Wine output just above it:

- `ShellExecuteEx failed: File not found` means Wine could not start the
  installer as a .NET program. The image stages Wine Mono for this; if it still
  fails, the Wine Mono version may not match the Wine package — rebuild with
  `--build-arg WINE_MONO_VERSION=<version>`.
- `is not a Windows executable` means a failed download left an HTML error page
  in place of the installer. Delete `game/SRMLInstaller.exe` and retry, or drop
  a known-good one into `./mods`.

Get the full picture with:

```bash
docker compose run --rm srmp diagnose
```

That prints the Wine version, whether Wine Mono is in the prefix, the game
folder contents, and a verbose installer run — without a restart loop.

**Fallback that always works:** install SRML on a Windows machine, then copy
these from that install into your `./game` folder here:

```
SlimeRancher_Data/Managed/Assembly-CSharp.dll      <- the patched one
SlimeRancher_Data/Managed/Assembly-CSharp_old.dll
SlimeRancher_Data/Managed/SRML.dll
SlimeRancher_Data/Managed/SRML.Editor.dll
SlimeRancher_Data/Managed/SRML.xml
SRML/                                              <- the whole folder
```

The entrypoint detects a patched install (`SRML.dll` **and**
`Assembly-CSharp_old.dll` present) and skips the patch step entirely. This is
the reliable route — SRML's installer is a Windows-native tool, and running it
under Wine is the least certain part of this whole setup.

**`Server is already active for display 99`** — a restarting container keeps its
filesystem, so a previous run's X lock survived. The entrypoint now clears stale
locks on startup; if you see this on an older image, pull the latest.

**`Wine Mono is not installed`, but the image has the MSI** — the Wine prefix
lives in a volume that outlives the image, so a prefix built by an older image
does not gain things a newer one ships. The entrypoint now installs Mono into an
existing prefix on startup, so this heals itself. To force a clean prefix
anyway:

```bash
docker compose down
docker volume rm srmp_wineprefix
docker compose up -d
```

Note that Wine Mono is only needed to run SRML's *installer*. If your `/game` is
already patched, the server does not need it at all.

**`no SRMP.dll found`** — build it on Windows and copy it into `./mods`.

**No SRMP log appears** — SRML did not load the mod. Check that
`game/SlimeRancher_Data/Managed/Assembly-CSharp_old.dll` exists, which is the
marker that SRML actually patched the game.

**Server starts but no friend code** — EOS login failed. Look for `EOS login did
not complete` in the log. EOS relay needs outbound internet; a blocked egress
will do this.

**Game exits immediately after "launching Slime Rancher headless"** — the
entrypoint prints diagnostics on any non-zero exit: the last 60 lines of the
game's own output (prefixed `[game]`) and of Unity's `Player.log` (prefixed
`[unity]`). Read those first. Two common patterns:

- mentions of `steam_api64` or Steam — the game wants a running Steam client.
  The workaround is running Steam inside the same Wine prefix, a heavier setup
  than this image provides.
- `InitializeEngineGraphics failed`, or mentions of GL/GLX/the display —
  graphics init is failing. Use `RENDER_MODE=nographics`, and make sure nothing
  is passing `-force-glcore`: this build has D3D11 shaders only.

Live output from the game is streamed as it happens, so `docker logs -f
srmp-server` shows the failure as it occurs.
