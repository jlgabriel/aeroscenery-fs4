# Samples one tile with the C# resampler and prints hashes, for comparison with the Python
# reference over the same inputs.
#
#   tools/ttc/csharp/sample.ps1 "C:\...\g_15_stitch.tmc" 12
#   tools/ttc/csharp/sample.ps1 "C:\...\g_15_stitch.tmc" 12 1240 1379
#
# Then, and this is the point of it:
#
#   python tools/ttc/cross_sample.py "C:\...\g_15_stitch.tmc" 12 1240 1379
#
# Both hashes must match. Not in the validation suite because it needs a real stitched source,
# which is not in the repo.

param(
    [Parameter(Mandatory = $true)][string]$Tmc,
    [Parameter(Mandatory = $true)][int]$Level,
    [int]$TileX = -1,
    [int]$TileY = -1
)

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here '..\..\..')
$afs2 = Join-Path $repo 'AeroScenery\AFS2'
$out  = Join-Path $here 'bin'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
if (-not (Test-Path $Tmc)) { throw "tmc not found: $Tmc" }

$refs = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
if (-not (Test-Path $refs)) { throw ".NET Framework 4.8 reference assemblies not found at $refs" }

New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $out 'ProbeSampler.exe'

$sources = @(
    'TmFields.cs', 'TmcReader.cs', 'AIDFile.cs',
    'IScanlineSource.cs', 'WicScanlineSource.cs',
    'AFS2World.cs', 'TtcToneCurve.cs', 'SourceImage.cs',
    'Coastline.cs', 'CoastlineField.cs', 'WaterFixField.cs', 'TileSampler.cs',
    'TtcTileName.cs'
) | ForEach-Object { Join-Path $afs2 $_ }
$sources += (Join-Path $here 'ProbeSampler.cs')

& $csc /nologo /optimize+ /platform:x64 /out:$exe "/lib:$refs" `
    /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw "compile failed" }

if ($TileX -ge 0 -and $TileY -ge 0) {
    & $exe $Tmc $Level $TileX $TileY
} else {
    & $exe $Tmc $Level
}
exit $LASTEXITCODE
