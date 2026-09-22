<#
    Builds the release ZIP from AeroScenery\bin\Release.

    Build Release by hand first, or pass -Build to have this do it from a clean output folder:

        .\package.ps1 -Build

    What goes in is everything the app needs to run, the command-line converter
    AeroSceneryConvert.exe, the licence, the README, the changelog and the user guide.
    What stays out is the IntelliSense .xml of every dependency (5 MB that no runtime reads) and
    third-party .pdb. Our own AeroScenery.pdb is kept, so a stack trace from a user still has line
    numbers in it.

    Nothing from the Aerofly FS 2 SDK is ever packaged: this only ever reads bin\Release.
#>
[CmdletBinding()]
param(
    [switch]$Build,
    [string]$MSBuild = "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe"
)

$ErrorActionPreference = "Stop"

$root      = $PSScriptRoot
$binRelease = Join-Path $root "AeroScenery\bin\Release"
$convertExe = Join-Path $root "AeroSceneryConvert\bin\Release\AeroSceneryConvert.exe"
$dist      = Join-Path $root "dist"

if ($Build) {
    # Start from an empty output folder, so that nothing a build no longer makes goes in the zip
    if (Test-Path $binRelease) { Remove-Item -Recurse -Force $binRelease }

    & $MSBuild (Join-Path $root "AeroScenery\AeroScenery.sln") -t:Build -p:Configuration=Release -v:minimal -m -nodeReuse:false
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    # Not in the solution: it links the converter sources and builds on its own
    & $MSBuild (Join-Path $root "AeroSceneryConvert\AeroSceneryConvert.csproj") -t:Build -p:Configuration=Release -v:minimal -nodeReuse:false
    if ($LASTEXITCODE -ne 0) { throw "AeroSceneryConvert build failed" }
}

if (-not (Test-Path $convertExe)) { throw "No AeroSceneryConvert.exe - run with -Build" }

$exe = Join-Path $binRelease "AeroScenery.exe"
if (-not (Test-Path $exe)) { throw "No build in $binRelease - run with -Build" }

$fileVersion = (Get-Item $exe).VersionInfo.FileVersion
$version = ($fileVersion -split '\.')[0..1] -join '.'
$name = "AeroScenery-$version"

$staging = Join-Path ([System.IO.Path]::GetTempPath()) "aeroscenery-package\$name"
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Path $staging -Force | Out-Null

Copy-Item -Path (Join-Path $binRelease "*") -Destination $staging -Recurse -Force

# 5 MB of IntelliSense documentation, and symbols for other people's assemblies
Get-ChildItem $staging -Recurse -Filter *.xml | Remove-Item -Force
Get-ChildItem $staging -Recurse -Filter *.pdb |
    Where-Object { $_.Name -ne "AeroScenery.pdb" } | Remove-Item -Force

Copy-Item $convertExe $staging -Force

foreach ($doc in @("LICENSE", "README.md", "CHANGELOG.md")) {
    Copy-Item (Join-Path $root $doc) $staging -Force
}

# The README links the user guide as docs\user-guide.md, so it keeps that place in the zip
New-Item -ItemType Directory -Path (Join-Path $staging "docs") | Out-Null
Copy-Item (Join-Path $root "docs\user-guide.md") (Join-Path $staging "docs") -Force

# The README and the user guide show the screenshots from docs\images
Copy-Item (Join-Path $root "docs\images") (Join-Path $staging "docs") -Recurse -Force

if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }
$zip = Join-Path $dist "$name.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }

Compress-Archive -Path $staging -DestinationPath $zip -CompressionLevel Optimal

Remove-Item -Recurse -Force (Split-Path $staging -Parent)

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "$zip  ($mb MB)"
