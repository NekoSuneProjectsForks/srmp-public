<#
.SYNOPSIS
    Builds a standalone SRMP server folder on Windows using SteamCMD.

.DESCRIPTION
    Produces a copy of the game that runs without the Steam client, so the
    machine hosting the server never holds a Steam session and nobody is
    disconnected from Steam elsewhere while playing.

    Two things worth being clear about before you use this:

    1. SteamCMD still needs a Steam account that OWNS the game in order to
       download it. This removes the need to *run* Steam on the server, not the
       need to own the game. The login happens once, at download time.

    2. If you already have the game installed somewhere, you do not need this at
       all - copy that folder to the server and point Start-SRMPServer.ps1 at it.
       That involves no Steam account on the server whatsoever. Use -FromExisting
       to do exactly that.

    After the files are in place this installs SRML and the SRMP mod, leaving a
    folder Start-SRMPServer.ps1 can run directly.

.EXAMPLE
    .\Get-SRMPServerFiles.ps1 -InstallPath C:\srmp-server -SteamUser myaccount

.EXAMPLE
    .\Get-SRMPServerFiles.ps1 -InstallPath C:\srmp-server -FromExisting "E:\SteamLibrary\steamapps\common\Slime Rancher"
#>
[CmdletBinding()]
param(
    # Where the standalone server copy is built.
    [Parameter(Mandatory = $true)]
    [string]$InstallPath,

    # Steam account that owns Slime Rancher. Used only to download.
    [string]$SteamUser,

    # Copy from an existing install instead of downloading. No Steam account used.
    [string]$FromExisting,

    # SRMP.dll to install. Defaults to this repository's build output.
    [string]$SrmpDll,

    # SRMLInstaller.exe to use. Downloaded if omitted and not already present.
    [string]$SrmlInstaller,

    [string]$SrmlUrl = 'https://cdn.0x00sec.xyz/files/games/Slime/SRMLInstaller.exe',

    [int]$AppId = 433340,

    # Re-run the download even when the game is already present.
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Setup {
    param([string]$Message, [string]$Colour = 'Gray')
    Write-Host "[srmp-setup] $Message" -ForegroundColor $Colour
}

$InstallPath = [IO.Path]::GetFullPath($InstallPath)
$null = New-Item -ItemType Directory -Path $InstallPath -Force

$gameExe = [IO.Path]::Combine($InstallPath, 'SlimeRancher.exe')
$managed = [IO.Path]::Combine($InstallPath, 'SlimeRancher_Data', 'Managed')

# ------------------------------------------------------------- game files ---

if ((Test-Path -LiteralPath $gameExe) -and -not $Force) {
    Write-Setup "Game already present in $InstallPath (use -Force to refresh)."
}
elseif ($FromExisting) {
    $source = [IO.Path]::GetFullPath($FromExisting)
    if (-not (Test-Path -LiteralPath ([IO.Path]::Combine($source, 'SlimeRancher.exe')))) {
        throw "Not a Slime Rancher install: $source"
    }

    Write-Setup "Copying from $source ..." 'Cyan'
    Write-Setup 'This uses no Steam account at all.'
    # /XO would skip newer files; a server copy should match the source exactly.
    robocopy $source $InstallPath /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with code $LASTEXITCODE" }
    Write-Setup 'Copy complete.' 'Green'
}
else {
    if (-not $SteamUser) {
        throw @'
Provide either -SteamUser (to download with SteamCMD) or -FromExisting (to copy
an install you already have). SteamCMD cannot download a game the account does
not own.
'@
    }

    $toolsDir = [IO.Path]::Combine($InstallPath, '..', '.steamcmd')
    $toolsDir = [IO.Path]::GetFullPath($toolsDir)
    $steamCmd = [IO.Path]::Combine($toolsDir, 'steamcmd.exe')

    if (-not (Test-Path -LiteralPath $steamCmd)) {
        Write-Setup 'Downloading SteamCMD...' 'Cyan'
        $null = New-Item -ItemType Directory -Path $toolsDir -Force
        $zip = [IO.Path]::Combine($toolsDir, 'steamcmd.zip')

        # TLS 1.2 is not the default on stock Windows PowerShell 5.1.
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri 'https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip' `
            -OutFile $zip -UseBasicParsing
        Expand-Archive -LiteralPath $zip -DestinationPath $toolsDir -Force
        Remove-Item -LiteralPath $zip -ErrorAction SilentlyContinue

        if (-not (Test-Path -LiteralPath $steamCmd)) { throw 'SteamCMD did not unpack as expected.' }
    }

    Write-Setup "Downloading app $AppId as '$SteamUser'." 'Cyan'
    Write-Setup 'Steam Guard will prompt here on the first run. This login is used'
    Write-Setup 'only to download; the server does not keep a Steam session.'

    & $steamCmd +force_install_dir $InstallPath +login $SteamUser +app_update $AppId validate +quit

    if (-not (Test-Path -LiteralPath $gameExe)) {
        throw "SteamCMD finished but $gameExe is missing. Does that account own the game?"
    }
    Write-Setup 'Download complete.' 'Green'
}

# ------------------------------------------------------------------ SRML ---

$srmlDll = [IO.Path]::Combine($managed, 'SRML.dll')
$patched = [IO.Path]::Combine($managed, 'Assembly-CSharp_old.dll')

if ((Test-Path -LiteralPath $srmlDll) -and (Test-Path -LiteralPath $patched)) {
    Write-Setup 'SRML already installed.'
}
else {
    $installer = [IO.Path]::Combine($InstallPath, 'SRMLInstaller.exe')

    if ($SrmlInstaller) {
        Copy-Item -LiteralPath $SrmlInstaller -Destination $installer -Force
    }
    elseif (-not (Test-Path -LiteralPath $installer)) {
        Write-Setup "Downloading SRMLInstaller.exe from $SrmlUrl" 'Cyan'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $SrmlUrl -OutFile $installer -UseBasicParsing
    }

    # Guards against an HTML error page saved under an .exe name.
    $header = [IO.File]::ReadAllBytes($installer)[0..1]
    if ([char]$header[0] -ne 'M' -or [char]$header[1] -ne 'Z') {
        Remove-Item -LiteralPath $installer -Force
        throw 'The downloaded SRMLInstaller.exe is not a Windows executable. Supply one with -SrmlInstaller.'
    }

    Write-Setup 'Patching the game with SRML...' 'Cyan'
    # The installer patches whatever folder it sits in, so run it from there.
    Push-Location $InstallPath
    try { & $installer | Write-Host }
    finally { Pop-Location }

    if (-not (Test-Path -LiteralPath $srmlDll)) {
        throw "SRML patch did not produce $srmlDll - see the installer output above."
    }
    Write-Setup 'SRML installed.' 'Green'
}

# ------------------------------------------------------------------ SRMP ---

if (-not $SrmpDll) {
    $candidate = [IO.Path]::Combine($PSScriptRoot, '..', 'Builds', 'SRMP', 'SRMP.dll')
    if (Test-Path -LiteralPath $candidate) { $SrmpDll = [IO.Path]::GetFullPath($candidate) }
}

if (-not $SrmpDll -or -not (Test-Path -LiteralPath $SrmpDll)) {
    throw 'Could not find SRMP.dll. Build it, or pass -SrmpDll "C:\path\to\SRMP.dll".'
}

$modsDir = [IO.Path]::Combine($InstallPath, 'SRML', 'Mods')
$null = New-Item -ItemType Directory -Path $modsDir -Force
Copy-Item -LiteralPath $SrmpDll -Destination ([IO.Path]::Combine($modsDir, 'SRMP.dll')) -Force

$hash = (Get-FileHash $SrmpDll -Algorithm SHA256).Hash
Write-Setup "Installed SRMP.dll (SHA256 $hash)" 'Green'

# ----------------------------------------------------------------- done ---

Write-Host ''
Write-Setup 'Server files ready.' 'Green'
Write-Setup "Location: $InstallPath"
Write-Host ''
Write-Host '  Start it with:' -ForegroundColor Cyan
Write-Host "    .\Start-SRMPServer.ps1 -GamePath `"$InstallPath`"" -ForegroundColor Cyan
Write-Host ''
Write-Setup 'Every player must run this exact same SRMP.dll, or they cannot join.' 'Yellow'
Write-Setup 'The server needs no Steam client running from here on.' 'Yellow'
