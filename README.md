# SRMP Revival

SRMP is the Slime Rancher Multiplayer Mod originally created by SatyPardus. This repository is being revived for **Slime Rancher 1** and keeps the later networking/EOS/mod-compatibility work already present in the project.

> This is for Slime Rancher 1. It is not a Slime Rancher 2 port.

## Easy installation

The revival uses the **SRML build** as the supported installation path.

1. Install SRML for Slime Rancher 1 and launch the game once.
2. Close Slime Rancher.
3. Download an SRMP Revival release package.
4. Double-click `Install SRMP.cmd`.

The installer detects common Steam/Epic locations, validates the game folder and `SRML/Mods`, refuses to modify files while the game is running, backs up an existing SRMP build, SHA-256 verifies the replacement, and records enough information for `Uninstall SRMP.cmd` to restore the previous build.

For a custom game path:

```bat
"Install SRMP.cmd" -GamePath "D:\Games\Slime Rancher"
```

See [README_REVIVAL.md](README_REVIVAL.md) for release/build details.

## Revival reliability work

The current revival branch includes targeted fixes for several failure modes found during the source audit:

- multiplayer chat Return-key focus/reopen race fixed;
- null/blank remote chat entries are no longer rendered;
- chat rendering no longer leaks faded GUI alpha into other windows;
- long unbroken chat text can no longer overflow the old fixed wrapper buffer;
- plort collectors use host authority while region ownership is still initializing;
- non-authoritative collectors no longer emit `StartCollection` synchronization;
- the global packet-handling suppression flag is forcibly cleared on every client/server handler exit, including custom-packet early returns and exceptions.

The historical bug list is tracked in [BUG_STATUS.md](BUG_STATUS.md). Timing-sensitive multiplayer defects are not called verified until they pass the real two-client matrix in [TESTING.md](TESTING.md).

## Building

Open `SRMP.sln` in Visual Studio 2022 or Rider and build the **SRML** configuration. The project still depends on Slime Rancher/SRML assemblies, so point the project Reference Paths at your Slime Rancher managed assemblies as needed.

The T4 tool used by the project is:

```text
dotnet tool install -g dotnet-t4
```

After building, create a distributable Windows package from the real DLL:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Package-Release.ps1 `
  -SrmpDll "C:\path\to\SRMP.dll" `
  -Version "2026.09.13"
```

The packager creates the installer ZIP and a SHA-256 checksum; it does not substitute an unrelated or untested binary.

## Manual / compatibility information

The original user manual and historical compatibility notes remain available in [manual.md](manual.md). Some troubleshooting workarounds in that document describe older builds, so use the revival bug status for current verification.

## Credits

SRMP was originally created by **SatyPardus** and later received maintenance and fixes from community contributors including Twirlbug and others in the repository history. The revival intentionally preserves that history and credit.
