[CmdletBinding()]
param(
    [string]$GamePath,
    [ValidateSet('SRML', 'SRML NoVer', 'Debug', 'Release', 'Standalone', 'Standalone NoVer')]
    [string]$Configuration = 'SRML',
    [switch]$Package,
    [switch]$Install,
    [string]$Version = 'dev'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $repoRoot 'SRMP'
$projectPath = Join-Path $projectDir 'SRMP.csproj'
$solutionPath = Join-Path $repoRoot 'SRMP.sln'
$outputDll = Join-Path $repoRoot 'Builds\SRMP\SRMP.dll'

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
    catch {
        return $false
    }
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
                else {
                    Write-Host "Skipping unavailable Steam library: $library" -ForegroundColor DarkYellow
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
        if ([string]::IsNullOrWhiteSpace($library)) { continue }
        if (-not (Test-Path -LiteralPath $library -PathType Container -ErrorAction SilentlyContinue)) {
            Write-Host "Skipping unavailable Steam library: $library" -ForegroundColor DarkYellow
            continue
        }

        $candidate = [IO.Path]::Combine($library, 'steamapps', 'common', 'Slime Rancher')
        if (Test-GamePath $candidate) { return $candidate }
    }

    $fallbacks = New-Object System.Collections.Generic.List[string]
    if (${env:ProgramFiles}) {
        $fallbacks.Add([IO.Path]::Combine(${env:ProgramFiles}, 'Epic Games', 'SlimeRancher'))
        $fallbacks.Add([IO.Path]::Combine(${env:ProgramFiles}, 'Slime Rancher'))
    }
    if (${env:ProgramFiles(x86)}) {
        $fallbacks.Add([IO.Path]::Combine(${env:ProgramFiles(x86)}, 'Steam', 'steamapps', 'common', 'Slime Rancher'))
    }

    foreach ($candidate in $fallbacks) {
        if (Test-GamePath $candidate) { return $candidate }
    }

    throw 'Could not find Slime Rancher 1. Pass -GamePath "C:\path\to\Slime Rancher".'
}

function Find-MSBuild {
    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere -PathType Leaf) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($found) { return $found }
    }

    throw 'MSBuild was not found. Install Visual Studio 2022 Build Tools with the .NET desktop build tools workload.'
}

$resolvedGame = Find-GamePath
$managedDir = Join-Path $resolvedGame 'SlimeRancher_Data\Managed'
$srmlLibDir = Join-Path $resolvedGame 'SRML\Libs'
$modsDir = Join-Path $resolvedGame 'SRML\Mods'

if (-not (Test-Path -LiteralPath $srmlLibDir -PathType Container)) {
    throw "SRML is not installed correctly. Expected: $srmlLibDir"
}

$requiredReferences = @(
    '0Harmony.dll',
    'DOTween.dll',
    'InControl.dll',
    'Unity.TextMeshPro.dll',
    'UnityEngine.dll',
    'UnityEngine.AnimationModule.dll',
    'UnityEngine.AssetBundleModule.dll',
    'UnityEngine.CoreModule.dll',
    'UnityEngine.IMGUIModule.dll',
    'UnityEngine.InputLegacyModule.dll',
    'UnityEngine.InputModule.dll',
    'UnityEngine.JSONSerializeModule.dll',
    'UnityEngine.PhysicsModule.dll',
    'UnityEngine.TextCoreModule.dll',
    'UnityEngine.TextRenderingModule.dll',
    'UnityEngine.UI.dll',
    'UnityEngine.UIModule.dll'
)

$createdReferences = New-Object System.Collections.Generic.List[string]
$originalProjectText = $null
$projectTemporarilyPatched = $false

try {
    Write-Host "Using Slime Rancher: $resolvedGame"
    Write-Host "Using SRML libraries: $srmlLibDir"
    Write-Host "SRML mods directory: $modsDir"

    foreach ($name in $requiredReferences) {
        $destination = Join-Path $projectDir $name
        if (Test-Path -LiteralPath $destination -PathType Leaf) {
            Write-Host "Reference already present: $name"
            continue
        }

        $source = $null
        foreach ($dir in @($srmlLibDir, $managedDir, $resolvedGame)) {
            $candidate = Join-Path $dir $name
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $source = $candidate
                break
            }
        }

        if (-not $source) {
            throw "Required build reference '$name' was not found in SRML\Libs or SlimeRancher_Data\Managed. Verify the game and SRML installation."
        }

        Copy-Item -LiteralPath $source -Destination $destination -Force
        $createdReferences.Add($destination)
        Write-Host "Staged reference: $name"
    }

    if ($Configuration -like 'SRML*') {
        $originalProjectText = Get-Content -LiteralPath $projectPath -Raw
        $patchedProjectText = [regex]::Replace(
            $originalProjectText,
            '(?ms)^\s*<Reference Include="UnityCoreMod[^"]*">.*?</Reference>\s*',
            ''
        )

        if ($patchedProjectText -ne $originalProjectText) {
            [IO.File]::WriteAllText($projectPath, $patchedProjectText, (New-Object Text.UTF8Encoding($true)))
            $projectTemporarilyPatched = $true
            Write-Host 'Removed obsolete UnityCoreMod references for this SRML build.' -ForegroundColor DarkYellow
        }
    }

    $msbuild = Find-MSBuild
    Write-Host "Using MSBuild: $msbuild"

    & $msbuild $solutionPath '/m' "/p:Configuration=$Configuration" '/p:Platform=Any CPU' '/verbosity:minimal'
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE" }

    if (-not (Test-Path -LiteralPath $outputDll -PathType Leaf)) {
        throw "Build completed but did not produce: $outputDll"
    }

    $hash = (Get-FileHash -LiteralPath $outputDll -Algorithm SHA256).Hash
    Write-Host "SRMP.dll built successfully. SHA256: $hash"

    if ($Install) {
        $installerScript = Join-Path $repoRoot 'installer\Install-SRMP.ps1'
        if (-not (Test-Path -LiteralPath $installerScript -PathType Leaf)) {
            throw "Installer script is missing: $installerScript"
        }

        Write-Host 'Installing freshly built SRMP.dll into SRML\Mods...'
        & $installerScript -GamePath $resolvedGame -SourceDll $outputDll
        if ($LASTEXITCODE -ne 0) { throw "Installation failed with exit code $LASTEXITCODE" }

        $installedDll = Join-Path $modsDir 'SRMP.dll'
        if (-not (Test-Path -LiteralPath $installedDll -PathType Leaf)) {
            throw "Installer completed but SRMP.dll is missing from: $installedDll"
        }

        $installedHash = (Get-FileHash -LiteralPath $installedDll -Algorithm SHA256).Hash
        if ($installedHash -ne $hash) {
            throw "Installed SRMP.dll hash mismatch. Built=$hash Installed=$installedHash"
        }
        Write-Host "Installed successfully: $installedDll"
        Write-Host "Installed SHA256: $installedHash"
    }

    if ($Package) {
        $packageScript = Join-Path $PSScriptRoot 'Package-Release.ps1'
        & $packageScript -SrmpDll $outputDll -Version $Version -OutputDirectory (Join-Path $repoRoot 'dist')
        if ($LASTEXITCODE -ne 0) { throw "Packaging failed with exit code $LASTEXITCODE" }
    }
}
finally {
    if ($projectTemporarilyPatched -and $null -ne $originalProjectText) {
        [IO.File]::WriteAllText($projectPath, $originalProjectText, (New-Object Text.UTF8Encoding($true)))
    }

    foreach ($path in $createdReferences) {
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    }
}
