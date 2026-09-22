# Checks the streaming reader against GDI+ on a real stitched PNG.
#
#   tools/ttc/csharp/probe.ps1 "C:\...\g_15_stitch_1_1.png"
#
# Not part of the validation suite - it needs a large source image that is not in the repo, and it
# takes long enough that it does not belong in a fast loop. Run it when the reader changes, or when
# a Windows update might have moved WIC underneath us: the memory behaviour this depends on is
# measured, not documented.
#
# Each mode runs in its own process because peak memory is a process-lifetime figure.

param([Parameter(Mandatory = $true)][string]$Image)

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here '..\..\..')
$afs2 = Join-Path $repo 'AeroScenery\AFS2'
$out  = Join-Path $here 'bin'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
if (-not (Test-Path $Image)) { throw "image not found: $Image" }

New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $out 'ProbeReader.exe'

$sources = @(
    (Join-Path $afs2 'IScanlineSource.cs'),
    (Join-Path $afs2 'WicScanlineSource.cs'),
    (Join-Path $here 'ProbeReader.cs')
)

# csc resolves plain assembly names against its own folder, which has no WPF in it, so point it
# at the 4.8 reference assemblies. MSBuild does this for the real build via the targeting pack.
$refs = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
if (-not (Test-Path $refs)) { throw ".NET Framework 4.8 reference assemblies not found at $refs" }

& $csc /nologo /optimize+ /platform:x64 /out:$exe "/lib:$refs" `
    /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll /r:System.Drawing.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw "compile failed" }

$size = (Get-Item $Image).Length / 1MB
Write-Host ("source: {0} ({1:n0} MB)" -f (Split-Path -Leaf $Image), $size)
Write-Host ""

foreach ($mode in @('wic', 'gdi')) {
    Write-Host "--- $mode ---"
    & $exe $mode $Image
    if ($LASTEXITCODE -ne 0) { throw "$mode failed" }
    Write-Host ""
}
