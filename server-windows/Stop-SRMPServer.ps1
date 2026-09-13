<#
.SYNOPSIS
    Asks a running SRMP Windows server to save its world and quit.

.DESCRIPTION
    Unity will not flush a save in response to being killed, so stopping the
    server is a handshake rather than a kill: this drops a request file that the
    mod is watching, then waits for the game to shut itself down.

    Use this instead of Stop-Process or closing the window. A hard kill costs
    everything since the last autosave.

.EXAMPLE
    .\Stop-SRMPServer.ps1

.EXAMPLE
    .\Stop-SRMPServer.ps1 -GamePath "D:\Games\Slime Rancher" -TimeoutSeconds 90
#>
[CmdletBinding()]
param(
    [string]$GamePath,
    [int]$TimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Server {
    param([string]$Message, [string]$Colour = 'Gray')
    Write-Host "[srmp-server] $Message" -ForegroundColor $Colour
}

if (-not $GamePath) {
    $process = Get-Process -Name 'SlimeRancher' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $process) {
        throw 'No running SlimeRancher process found. Pass -GamePath to target an install directly.'
    }
    $GamePath = Split-Path -Parent $process.Path
    Write-Server "Found running server at $GamePath"
}

$modDataPath = [IO.Path]::Combine($GamePath, 'SRMP')
if (-not (Test-Path -LiteralPath $modDataPath)) {
    throw "No SRMP data folder at $modDataPath - is that the right install?"
}

New-Item -ItemType File -Path ([IO.Path]::Combine($modDataPath, 'shutdown.request')) -Force | Out-Null
Write-Server "Shutdown requested; the server will save and quit shortly." 'Yellow'

$waited = 0
while ($waited -lt $TimeoutSeconds) {
    $still = Get-Process -Name 'SlimeRancher' -ErrorAction SilentlyContinue
    if (-not $still) {
        Write-Server "Server saved and exited cleanly after ${waited}s." 'Green'
        return
    }
    Start-Sleep -Seconds 1
    $waited++
}

Write-Server "Still running after ${TimeoutSeconds}s. Check the server console." 'Red'
