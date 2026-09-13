[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$GamePath,

    [Parameter(Mandatory = $false)]
    [switch]$DoNotRestoreBackup
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Fail([string]$Message, [int]$Code = 1) {
    Write-Host "[SRMP] ERROR: $Message" -ForegroundColor Red
    exit $Code
}

function Get-FullPathSafe([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    try { return [System.IO.Path]::GetFullPath($Path.Trim('"')) } catch { return $null }
}

function Test-SlimeRancherPath([string]$Path) {
    $full = Get-FullPathSafe $Path
    if (-not $full) { return $false }
    return (Test-Path -LiteralPath (Join-Path $full 'SlimeRancher.exe') -PathType Leaf) -and
           (Test-Path -LiteralPath (Join-Path $full 'SlimeRancher_Data') -PathType Container)
}

function Add-UniquePath([System.Collections.Generic.List[string]]$List, [string]$Path) {
    $full = Get-FullPathSafe $Path
    if (-not $full) { return }
    foreach ($existing in $List) {
        if ([string]::Equals($existing, $full, [System.StringComparison]::OrdinalIgnoreCase)) { return }
    }
    $List.Add($full)
}

function Find-SlimeRancher {
    $steamRoots = New-Object 'System.Collections.Generic.List[string]'
    if (${env:ProgramFiles(x86)}) { Add-UniquePath $steamRoots (Join-Path ${env:ProgramFiles(x86)} 'Steam') }
    if ($env:ProgramFiles) { Add-UniquePath $steamRoots (Join-Path $env:ProgramFiles 'Steam') }
    foreach ($key in @('HKCU:\SOFTWARE\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam','HKLM:\SOFTWARE\Valve\Steam')) {
        try {
            $value = Get-ItemProperty -Path $key -ErrorAction Stop
            if ($value.PSObject.Properties['SteamPath'] -and $value.SteamPath) { Add-UniquePath $steamRoots $value.SteamPath }
            if ($value.PSObject.Properties['InstallPath'] -and $value.InstallPath) { Add-UniquePath $steamRoots $value.InstallPath }
        } catch { }
    }

    $libraries = New-Object 'System.Collections.Generic.List[string]'
    foreach ($root in $steamRoots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        Add-UniquePath $libraries $root
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf -PathType Leaf) {
            foreach ($line in (Get-Content -LiteralPath $vdf -ErrorAction SilentlyContinue)) {
                $candidate = $null
                if ($line -match '"path"\s+"([^"]+)"') { $candidate = $Matches[1] }
                elseif ($line -match '^\s*"\d+"\s+"([^"]+)"') { $candidate = $Matches[1] }
                if ($candidate) {
                    $candidate = $candidate -replace '\\\\', '\'
                    Add-UniquePath $libraries $candidate
                }
            }
        }
    }

    foreach ($library in $libraries) {
        $candidate = Join-Path $library 'steamapps\common\Slime Rancher'
        if (Test-SlimeRancherPath $candidate) { return $candidate }
    }

    if ($env:ProgramFiles) {
        foreach ($candidate in @((Join-Path $env:ProgramFiles 'Epic Games\SlimeRancher'), (Join-Path $env:ProgramFiles 'Slime Rancher'))) {
            if (Test-SlimeRancherPath $candidate) { return $candidate }
        }
    }
    return $null
}

if (Get-Process -Name 'SlimeRancher' -ErrorAction SilentlyContinue) {
    Fail 'Slime Rancher is currently running. Close the game before uninstalling.' 10
}

if ($GamePath) {
    $resolvedGame = Get-FullPathSafe $GamePath
    if (-not (Test-SlimeRancherPath $resolvedGame)) { Fail 'Invalid -GamePath.' 11 }
} else {
    $resolvedGame = Find-SlimeRancher
    if (-not $resolvedGame) { Fail 'Could not locate Slime Rancher 1. Re-run with -GamePath.' 12 }
}

$modsPath = Join-Path $resolvedGame 'SRML\Mods'
$destination = Join-Path $modsPath 'SRMP.dll'
$manifestPath = Join-Path $modsPath 'SRMP.install.json'
$backupToRestore = $null

if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($manifest.PreviousBackup) { $backupToRestore = [string]$manifest.PreviousBackup }
    } catch {
        Write-Warning 'Install manifest exists but could not be parsed. SRMP.dll can still be removed.'
    }
}

if (Test-Path -LiteralPath $destination -PathType Leaf) {
    Remove-Item -LiteralPath $destination -Force
    Write-Host "[SRMP] Removed: $destination" -ForegroundColor Cyan
} else {
    Write-Host '[SRMP] SRMP.dll was not installed in the SRML Mods folder.' -ForegroundColor Yellow
}

if (-not $DoNotRestoreBackup -and $backupToRestore -and (Test-Path -LiteralPath $backupToRestore -PathType Leaf)) {
    Copy-Item -LiteralPath $backupToRestore -Destination $destination -Force
    Write-Host "[SRMP] Restored previous SRMP.dll from: $backupToRestore" -ForegroundColor Green
}

if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    Remove-Item -LiteralPath $manifestPath -Force
}

Write-Host '[SRMP] Uninstall completed.' -ForegroundColor Green
exit 0
