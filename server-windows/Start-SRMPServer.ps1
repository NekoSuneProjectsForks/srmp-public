<#
.SYNOPSIS
    Runs SRMP as an unattended headless server on Windows.

.DESCRIPTION
    The native alternative to the Docker image. Slime Rancher is a Windows game,
    so running it here skips the Wine translation layer entirely - less overhead
    and one fewer thing to go wrong.

    This writes the auto-host config, launches the game with auto-hosting on,
    prints the friend code, and stops the world cleanly on Ctrl+C.

.EXAMPLE
    .\Start-SRMPServer.ps1

.EXAMPLE
    .\Start-SRMPServer.ps1 -Username "MyServer" -Slots 8 -Operators "Alice","Bob"

.EXAMPLE
    .\Start-SRMPServer.ps1 -GamePath "D:\Games\Slime Rancher" -RenderMode nographics
#>
[CmdletBinding()]
param(
    # Slime Rancher install. Auto-detected from Steam/Epic when omitted.
    [string]$GamePath,

    # Name the host player appears as.
    [string]$Username = 'Server',

    # Existing world to host. Blank continues the most recent one.
    [string]$GameName = '',

    [ValidateSet('CLASSIC', 'CASUAL', 'TIME_LIMIT', 'TIME_LIMIT_V2')]
    [string]$GameMode = 'CLASSIC',

    # Lobby capacity including the host.
    [ValidateRange(2, 64)]
    [int]$Slots = 16,

    # Server loop rate. Caps every player's ping, because packets are only
    # drained once per frame. 0 means uncapped.
    [int]$TickRate = 60,

    # auto       - use the GPU if this machine has a usable one, else nographics
    # nographics - never render (cheapest, recommended even with a GPU)
    # gpu        - force real rendering through the GPU
    [ValidateSet('auto', 'nographics', 'gpu')]
    [string]$RenderMode = 'auto',

    # In-game names allowed to use /tp, /ban and /unban.
    [string[]]$Operators = @(),

    [int]$StatusIntervalSeconds = 60,
    [int]$AutoSaveIntervalSeconds = 300,

    # Seconds to let the world save and quit before it is forced down.
    [int]$ShutdownGraceSeconds = 45,

    # Write the config and report what would happen, then exit.
    [switch]$WhatIfOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Server {
    param([string]$Message, [string]$Colour = 'Gray')
    Write-Host "[srmp-server] $Message" -ForegroundColor $Colour
}

function Test-GamePath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    try {
        if (-not (Test-Path -LiteralPath $Path -PathType Container -ErrorAction SilentlyContinue)) {
            return $false
        }
        $exe = [IO.Path]::Combine($Path, 'SlimeRancher.exe')
        $managed = [IO.Path]::Combine($Path, 'SlimeRancher_Data', 'Managed')
        return (Test-Path -LiteralPath $exe -PathType Leaf -ErrorAction SilentlyContinue) -and
               (Test-Path -LiteralPath $managed -PathType Container -ErrorAction SilentlyContinue)
    }
    catch { return $false }
}

function Get-SteamLibraryPaths {
    $paths = New-Object System.Collections.Generic.List[string]
    $steamRoots = New-Object System.Collections.Generic.List[string]

    foreach ($key in @(
        'HKCU:\Software\Valve\Steam',
        'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam',
        'HKLM:\SOFTWARE\Valve\Steam'
    )) {
        try {
            $props = Get-ItemProperty -LiteralPath $key -ErrorAction Stop
            $value = $props.SteamPath
            if (-not $value) { $value = $props.InstallPath }
            if ($value) { $steamRoots.Add([IO.Path]::GetFullPath($value)) }
        } catch { }
    }

    if (${env:ProgramFiles(x86)}) {
        $steamRoots.Add([IO.Path]::Combine(${env:ProgramFiles(x86)}, 'Steam'))
    }

    foreach ($root in ($steamRoots | Select-Object -Unique)) {
        if ([string]::IsNullOrWhiteSpace($root)) { continue }
        if (-not (Test-Path -LiteralPath $root -PathType Container -ErrorAction SilentlyContinue)) { continue }

        $paths.Add($root)
        $vdf = [IO.Path]::Combine($root, 'steamapps', 'libraryfolders.vdf')
        if (Test-Path -LiteralPath $vdf -PathType Leaf -ErrorAction SilentlyContinue) {
            $raw = Get-Content -LiteralPath $vdf -Raw
            foreach ($match in [regex]::Matches($raw, '"path"\s+"([^"]+)"')) {
                $library = $match.Groups[1].Value -replace '\\\\', '\'
                if ([string]::IsNullOrWhiteSpace($library)) { continue }
                if (Test-Path -LiteralPath $library -PathType Container -ErrorAction SilentlyContinue) {
                    $paths.Add($library)
                }
            }
        }
    }

    return $paths | Select-Object -Unique
}

function Find-GamePath {
    if ($GamePath) {
        $candidate = [IO.Path]::GetFullPath($GamePath)
        if (-not (Test-GamePath $candidate)) {
            throw "The supplied -GamePath is not a Slime Rancher 1 installation: $candidate"
        }
        return $candidate
    }

    foreach ($library in Get-SteamLibraryPaths) {
        $candidate = [IO.Path]::Combine($library, 'steamapps', 'common', 'Slime Rancher')
        if (Test-GamePath $candidate) { return $candidate }
    }

    $fallbacks = New-Object System.Collections.Generic.List[string]
    if (${env:ProgramFiles}) {
        $fallbacks.Add([IO.Path]::Combine(${env:ProgramFiles}, 'Epic Games', 'SlimeRancher'))
        $fallbacks.Add([IO.Path]::Combine(${env:ProgramFiles}, 'Slime Rancher'))
    }
    foreach ($candidate in $fallbacks) {
        if (Test-GamePath $candidate) { return $candidate }
    }

    throw 'Could not find Slime Rancher 1. Pass -GamePath "C:\path\to\Slime Rancher".'
}

<#
    Reports a usable display adapter.

    Note there is no video encoding anywhere in this server - the GPU would only
    ever be used to draw frames, and a server draws frames nobody looks at. This
    exists so 'auto' can pick sensibly, not because a GPU makes the server faster.
#>
function Get-DisplayAdapter {
    try {
        $adapters = @(Get-CimInstance -ClassName Win32_VideoController -ErrorAction Stop |
            Where-Object { $_.Name -and $_.ConfigManagerErrorCode -eq 0 })

        if ($adapters.Count -eq 0) { return $null }

        # Basic/Remote adapters are software renderers; treat them as no GPU.
        # The @() matters: a single match is a scalar, and .Count on a scalar
        # throws under StrictMode, which would report a real GPU as absent.
        $real = @($adapters | Where-Object {
            $_.Name -notmatch 'Basic Display|Basic Render|Remote Display|Standard VGA'
        })
        if ($real.Count -eq 0) { return $null }

        return $real[0]
    }
    catch {
        Write-Server "GPU detection failed ($($_.Exception.Message)); assuming none." 'Yellow'
        return $null
    }
}

# ---------------------------------------------------------------- discovery ---

$resolvedGamePath = Find-GamePath
Write-Server "Game: $resolvedGamePath"

$modDll = [IO.Path]::Combine($resolvedGamePath, 'SRML', 'Mods', 'SRMP.dll')
$srmlDll = [IO.Path]::Combine($resolvedGamePath, 'SlimeRancher_Data', 'Managed', 'SRML.dll')
$patched = [IO.Path]::Combine($resolvedGamePath, 'SlimeRancher_Data', 'Managed', 'Assembly-CSharp_old.dll')

if (-not (Test-Path -LiteralPath $srmlDll) -or -not (Test-Path -LiteralPath $patched)) {
    throw "SRML is not installed in $resolvedGamePath. Run SRMLInstaller.exe there first."
}
if (-not (Test-Path -LiteralPath $modDll)) {
    throw "SRMP.dll is not in SRML\Mods. Build it and copy it to: $modDll"
}

$adapter = Get-DisplayAdapter
if ($adapter) {
    Write-Server "GPU detected: $($adapter.Name)" 'Green'
} else {
    Write-Server 'No usable GPU detected (headless or software adapter only).' 'Yellow'
}

$effectiveRenderMode = $RenderMode
if ($RenderMode -eq 'auto') {
    $effectiveRenderMode = if ($adapter) { 'gpu' } else { 'nographics' }
    Write-Server "Render mode 'auto' resolved to '$effectiveRenderMode'."
}

if ($effectiveRenderMode -eq 'gpu' -and -not $adapter) {
    Write-Server 'Render mode "gpu" was forced but no GPU was found; the game may fail to start.' 'Yellow'
}

# --------------------------------------------------------------- config ---

$modDataPath = [IO.Path]::Combine($resolvedGamePath, 'SRMP')
$null = New-Item -ItemType Directory -Path $modDataPath -Force

$config = [ordered]@{
    Enabled                 = $true
    Username                = $Username
    GameName                = $GameName
    LoadLatestSave          = $true
    NewGameDisplayName      = 'SRMP Server'
    GameMode                = $GameMode
    CreateGameIfMissing     = $true
    MaxPlayers              = $Slots
    GodMode                 = $true
    TargetFrameRate         = $TickRate
    Operators               = @($Operators)
    StartupDelaySeconds     = 3.0
    LoginTimeoutSeconds     = 90.0
    LoadTimeoutSeconds      = 600.0
    ServerCodeFile          = 'servercode.txt'
    ShutdownRequestFile     = 'shutdown.request'
    StatusIntervalSeconds   = [double]$StatusIntervalSeconds
    AutoSaveIntervalSeconds = [double]$AutoSaveIntervalSeconds
}

$configPath = [IO.Path]::Combine($modDataPath, 'autohost.json')
$config | ConvertTo-Json -Depth 5 | Out-File -LiteralPath $configPath -Encoding utf8
Write-Server "Config written: $configPath"

$codePath = [IO.Path]::Combine($modDataPath, 'servercode.txt')
$shutdownPath = [IO.Path]::Combine($modDataPath, 'shutdown.request')
Remove-Item -LiteralPath $codePath, $shutdownPath -ErrorAction SilentlyContinue

Write-Server "Username : $Username"
Write-Server "Slots    : $Slots"
Write-Server "Tick rate: $(if ($TickRate -gt 0) { $TickRate } else { 'uncapped' })"
Write-Server "Operators: $(if ($Operators.Count) { $Operators -join ', ' } else { '<none - /tp and /ban unavailable>' })"

if ($WhatIfOnly) {
    Write-Server 'WhatIfOnly set; not launching.' 'Yellow'
    return
}

# --------------------------------------------------------------- launch ---

$gameArgs = @(
    '-srmp-autohost'
    '-srmp-username', $Username
    '-srmp-gamemode', $GameMode
    '-srmp-slots', $Slots
)
if ($GameName) { $gameArgs += @('-srmp-game', $GameName) }

if ($effectiveRenderMode -eq 'nographics') {
    # Unity skips graphics entirely. Do NOT add -force-glcore: this build ships
    # D3D11 shaders only and forcing OpenGL Core fails engine init outright.
    $gameArgs += @('-batchmode', '-nographics')
} else {
    $gameArgs += @('-screen-width', '640', '-screen-height', '480', '-screen-fullscreen', '0')
}

$exe = [IO.Path]::Combine($resolvedGamePath, 'SlimeRancher.exe')
Write-Server "Launching: SlimeRancher.exe $($gameArgs -join ' ')" 'Cyan'

$process = Start-Process -FilePath $exe -ArgumentList $gameArgs `
    -WorkingDirectory $resolvedGamePath -PassThru

Write-Server "Started (pid $($process.Id)). Press Ctrl+C to stop the server cleanly."

# Ctrl+C must not kill the game outright: Unity will not flush a save in
# response to a signal, so the mod is asked to save and quit instead.
[Console]::TreatControlCAsInput = $false
$stopRequested = $false
$null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action { } -ErrorAction SilentlyContinue

$logFolder = [IO.Path]::Combine($modDataPath, 'Logs')
$followed = $null
$reader = $null
$codeShown = $false

try {
    while (-not $process.HasExited) {
        if (-not $codeShown -and (Test-Path -LiteralPath $codePath)) {
            $code = (Get-Content -LiteralPath $codePath -Raw).Trim()
            if ($code) {
                Write-Host ''
                Write-Host '  ===================================' -ForegroundColor Green
                Write-Host "   FRIEND CODE: $code" -ForegroundColor Green
                Write-Host '  ===================================' -ForegroundColor Green
                Write-Host ''
                $codeShown = $true
            }
        }

        # Follow the newest SRMP log so this console shows what the server does.
        if (-not $reader -and (Test-Path -LiteralPath $logFolder)) {
            $newest = Get-ChildItem -LiteralPath $logFolder -Filter 'log-*.txt' -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($newest) {
                $followed = $newest.FullName
                Write-Server "Following $followed"
                $stream = [IO.File]::Open($followed, 'Open', 'Read', 'ReadWrite')
                $reader = New-Object IO.StreamReader($stream)
            }
        }
        if ($reader) {
            while ($null -ne ($line = $reader.ReadLine())) { Write-Host $line }
        }

        Start-Sleep -Milliseconds 500
    }
}
finally {
    if ($reader) { $reader.Dispose() }

    if (-not $process.HasExited) {
        $stopRequested = $true
        Write-Server "Stopping: asking the server to save and quit (up to ${ShutdownGraceSeconds}s)..." 'Yellow'
        New-Item -ItemType File -Path $shutdownPath -Force | Out-Null

        $waited = 0
        while (-not $process.HasExited -and $waited -lt $ShutdownGraceSeconds) {
            Start-Sleep -Seconds 1
            $waited++
        }

        if (-not $process.HasExited) {
            Write-Server 'Server did not quit in time; forcing it down.' 'Red'
            Write-Server 'Progress since the last autosave may be lost.' 'Red'
            $process.Kill()
        }
        else {
            Write-Server "Server saved and exited cleanly after ${waited}s." 'Green'
        }
    }

    Remove-Item -LiteralPath $shutdownPath -ErrorAction SilentlyContinue

    if (-not $stopRequested) {
        Write-Server "Game exited with code $($process.ExitCode)."
    }
}
