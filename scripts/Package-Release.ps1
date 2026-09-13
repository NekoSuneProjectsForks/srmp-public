[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$SrmpDll,
    [Parameter(Mandatory = $false)]
    [string]$Version = 'dev',
    [Parameter(Mandatory = $false)]
    [string]$OutputDirectory
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'dist' }
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$cleanVersion = ($Version -replace '[^0-9A-Za-z._-]', '-')
$stage = Join-Path $OutputDirectory "SRMP-Revival-$cleanVersion-Windows"
$zip = "$stage.zip"
$checksums = Join-Path $OutputDirectory "SRMP-Revival-$cleanVersion-SHA256.txt"

if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
New-Item -ItemType Directory -Path (Join-Path $stage 'installer') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stage 'payload') -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $root 'Install SRMP.cmd') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'Uninstall SRMP.cmd') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'README-FIRST.txt') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'installer\Install-SRMP.ps1') -Destination (Join-Path $stage 'installer')
Copy-Item -LiteralPath (Join-Path $root 'installer\Uninstall-SRMP.ps1') -Destination (Join-Path $stage 'installer')
Copy-Item -LiteralPath $SrmpDll -Destination (Join-Path $stage 'payload\SRMP.dll')

$dllHash = (Get-FileHash -LiteralPath (Join-Path $stage 'payload\SRMP.dll') -Algorithm SHA256).Hash
@(
    "SRMP Revival release: $Version",
    "SRMP.dll SHA256: $dllHash",
    '',
    'Requires Slime Rancher 1 and SRML.',
    'Install: double-click Install SRMP.cmd',
    'Uninstall: double-click Uninstall SRMP.cmd'
) | Set-Content -LiteralPath (Join-Path $stage 'RELEASE.txt') -Encoding UTF8

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
"$zipHash  $([System.IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath $checksums -Encoding ASCII
Write-Host "Created: $zip" -ForegroundColor Green
Write-Host "Checksum: $checksums" -ForegroundColor Green
