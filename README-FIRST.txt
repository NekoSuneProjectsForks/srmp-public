SRMP REVIVAL - WINDOWS INSTALLER
================================

This kit is for Slime Rancher 1 (64-bit) using SRML.
It is NOT a Slime Rancher 2 mod.

RELEASE PACKAGE
---------------
1. Install SRML first and launch Slime Rancher once.
2. Close Slime Rancher.
3. Double-click "Install SRMP.cmd".
4. The installer auto-detects common Steam/Epic locations, validates the
   Slime Rancher 1 folder, backs up an existing SRMP.dll, and installs the
   release payload into the game's SRML\Mods folder.

CUSTOM GAME LOCATION
--------------------
If auto-detection cannot find the game, open Command Prompt in this folder:

  "Install SRMP.cmd" -GamePath "D:\Games\Slime Rancher"

DEVELOPER / SOURCE KIT
----------------------
The repository intentionally does not invent or ship an unverified SRMP.dll.
After you build the SRML configuration, run:

  "Install SRMP.cmd" -GamePath "D:\Games\Slime Rancher" -SourceDll "D:\build\SRMP.dll"

To make a distributable release ZIP:

  powershell -ExecutionPolicy Bypass -File scripts\Package-Release.ps1 ^
    -SrmpDll "D:\build\SRMP.dll" -Version "2026.09.13"

UNINSTALL
---------
Double-click "Uninstall SRMP.cmd". If this installer backed up an older
SRMP.dll, the uninstaller restores it automatically.

SAFETY
------
- The installer refuses to modify a folder that does not look like SR1.
- It refuses to install if the SRML\Mods folder is missing.
- It refuses to modify files while Slime Rancher is running.
- Existing SRMP.dll files are timestamp-backed-up before replacement.
- The copied DLL is SHA256-verified before installation completes.
