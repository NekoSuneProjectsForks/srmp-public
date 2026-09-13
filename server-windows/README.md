# Running SRMP as a server on Windows

The native alternative to [the Docker image](../server/README.md). Slime Rancher
is a Windows game, so running the server here skips the Wine translation layer
entirely — noticeably less overhead, and one large moving part fewer.

**Prefer this if you have a Windows box.** Use Docker only if your server is
Linux.

## What you need

- Windows with Slime Rancher installed (Steam or Epic)
- **SRML installed** into that copy, and **`SRMP.dll` in `SRML\Mods`**
- The same `SRMP.dll` given to everyone who will join

The script refuses to start if SRML or the mod is missing, rather than launching
a game that quietly is not a server.

## Start it

Double-click **`Start SRMP Server.cmd`**, or from PowerShell:

```powershell
.\Start-SRMPServer.ps1
```

With options:

```powershell
.\Start-SRMPServer.ps1 -Username "MyServer" -Slots 8 -Operators "Alice","Bob"
```

The game is found automatically via the Steam registry and library folders. If
it is somewhere unusual, point at it:

```powershell
.\Start-SRMPServer.ps1 -GamePath "D:\Games\Slime Rancher"
```

When the world is up you get the friend code:

```
  ===================================
   FRIEND CODE: A7K2M9Q
  ===================================
```

It is also written to `<game>\SRMP\servercode.txt`, and it **changes on every
restart** — it is generated per lobby, not stored.

> **Use a separate copy of the game for the server.** Starting the server writes
> `Enabled: true` into `<game>\SRMP\autohost.json`, and that install will then
> auto-host whenever you launch it normally. Set `Enabled` back to `false` if you
> want to play on that copy again.

## Stop it

Press **Ctrl+C** in the server window, or run **`Stop SRMP Server.cmd`** from
elsewhere.

Either way the world is saved first. Unity does not flush a save when it is
killed, so stopping is a handshake: a request file is written, the mod saves,
closes the lobby and quits, and only then does the process end. **Do not close
the window with the X or use Task Manager** — that skips the save entirely and
costs everything since the last autosave.

## The GPU question

`-RenderMode` controls this, and `auto` is the default:

| Mode | What happens |
| --- | --- |
| `auto` | Uses the GPU if the machine has a real display adapter; otherwise `nographics`. |
| `nographics` | `-batchmode -nographics`. Unity never initialises graphics at all. |
| `gpu` | Forces real rendering at 640x480 through the GPU. |

**A GPU does not make the server faster, and `nographics` is usually the better
choice even on a machine that has one.** There is no video encoding anywhere in
this server — the GPU would only ever draw frames that nobody is looking at.
Skipping rendering frees that time for the game loop, which is the thing that
actually limits everyone's ping.

`auto` picks `gpu` when a card is present because that is the closest thing to a
normal launch and therefore the safest default if `nographics` ever misbehaves
with a particular build. If your server is running fine, try:

```powershell
.\Start-SRMPServer.ps1 -RenderMode nographics
```

and keep it if the friend code still appears.

Adapters reported as *Basic Display*, *Basic Render*, *Remote Display* or
*Standard VGA* are software renderers and are treated as no GPU — which is what
you get over RDP or on a headless VM.

**Never add `-force-glcore`.** This build ships D3D11 shaders only; forcing
OpenGL Core kills the game at engine init with
`InitializeEngineGraphics failed`.

## Options

| Parameter | Default | Meaning |
| --- | --- | --- |
| `-GamePath` | auto-detected | Slime Rancher install to run. |
| `-Username` | `Server` | Name the host appears as. |
| `-GameName` | *(blank)* | World to host. Blank continues the most recent one. |
| `-GameMode` | `CLASSIC` | `CLASSIC`, `CASUAL`, `TIME_LIMIT`, `TIME_LIMIT_V2`. |
| `-Slots` | `16` | Lobby capacity including the host (2–64). |
| `-TickRate` | `60` | Server loop rate. Caps everyone's ping. `0` = uncapped. |
| `-RenderMode` | `auto` | See above. |
| `-Operators` | *(none)* | Names allowed to use `/tp`, `/ban`, `/unban`. |
| `-StatusIntervalSeconds` | `60` | Heartbeat line interval. `0` disables. |
| `-AutoSaveIntervalSeconds` | `300` | Forced save interval. `0` disables. |
| `-ShutdownGraceSeconds` | `45` | How long to let the world save before forcing. |
| `-WhatIfOnly` | off | Write the config and report, without launching. |

Operators are **required** for `/tp` and `/ban`: a headless server has nobody at
the keyboard, so leaving `-Operators` empty means nobody can use them.

## Running it unattended

To survive logout and start on boot, register a scheduled task:

```powershell
$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
  -Argument '-NoProfile -ExecutionPolicy Bypass -File "C:\srmp\Start-SRMPServer.ps1" -Username "MyServer"'
$trigger = New-ScheduledTaskTrigger -AtStartup
Register-ScheduledTask -TaskName 'SRMP Server' -Action $action -Trigger $trigger -RunLevel Highest
```

Note the game must run in a session that allows it to start. If you use
`nographics` this works in a background session; with `-RenderMode gpu` it needs
an interactive session to get a device.

## Chat commands

Identical to the Docker server — `/help`, `/tps`, `/ping`, `/list`, `/home`, and
`/tp`, `/ban`, `/unban`, `/banlist` for operators. Press **Tab** to complete
names. See the [Docker README](../server/README.md) for details, including how
bans are keyed and where the ban list is stored.

## Troubleshooting

**"SRML is not installed"** — run `SRMLInstaller.exe` in the game folder first.

**"SRMP.dll is not in SRML\Mods"** — build the mod and copy it there.

**No friend code appears** — EOS login failed. Check the console output and that
the machine has outbound internet.

**Players cannot join** — they must run the **exact same `SRMP.dll` build**. The
version bumps on every compile and mismatched versions are refused at connect.
