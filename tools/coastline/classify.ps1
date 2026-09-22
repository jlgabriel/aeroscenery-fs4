# Says which squares the drawn coastline touches, before any of them is converted.
#
#     tools\coastline\classify.ps1 <working folder>\coastline.txt <working folder>
#     tools\coastline\classify.ps1 <coast> <working folder>\map_09_4b80_6200
#     tools\coastline\classify.ps1 <coast> <working root> -MarginNm 5
#
# Run this FIRST when a line has grown and squares have to be re-cut. Most squares need nothing,
# and the answer costs seconds per square and opens no source image: build the field over the
# square and bound it with RangeOver. The whole-package pass of 2026-08-09 was 17 squares to
# rebuild out of 45, which is fifty minutes of converting instead of two and a half hours.
#
# Three answers:
#
#   LAND  the whole square is inside the margin. Converting it with --coast gives back the same
#         bytes it already has, so leave it alone.
#   CUT   the cut crosses it. Rebuild it, and clear the output folder first - the cut writes fewer
#         tiles than the uncut build, so leftovers would survive and be installed as sea.
#   SEA   the whole square is past the cut. There is no photoscenery in it, so delete it.
#
# The verdicts are exact, not indicative, and that rests on two things. The box comes from
# TtcConverter.CoastFieldBox, which is the same call the converter makes, so this cannot bound a
# smaller box than the one that gets sampled. And bilinear interpolation never leaves the range of
# the four texels it reads, so a maximum inside the margin means every lookup in the square is
# inside it.
#
# Rebuild each CUT square with the converter, two or three at a time:
#
#     AeroSceneryConvert "<square>\b\17-stitched\b_17_stitch.tmc" --coast <coast>

param(
    # The file the Map tab's Draw Coast button saves.
    [Parameter(Mandatory = $true)][string]$Coastline,
    # A working root holding map_* squares, one square folder, or a single .tmc.
    [Parameter(Mandatory = $true)][string]$Path,
    # How far out to sea to cut, overriding the file. Default is whatever the file says, 3 NM.
    [double]$MarginNm = 0
)

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here '..\..')
$afs2 = Join-Path $repo 'AeroScenery\AFS2'
$bin = Join-Path $here 'bin'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
if (-not (Test-Path $Coastline)) { throw "coastline not found: $Coastline" }
if (-not (Test-Path $Path)) { throw "not found: $Path" }

$refs = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
if (-not (Test-Path $refs)) { throw ".NET Framework 4.8 reference assemblies not found at $refs" }

New-Item -ItemType Directory -Force $bin | Out-Null
$exe = Join-Path $bin 'Classify.exe'

# The whole converter, because the box is asked for rather than reproduced. Duplicating the work
# range here is what the tool exists to avoid - see TtcConverter.CoastFieldBox.
$sources = @(
    'TmFields.cs', 'TmcReader.cs', 'AIDFile.cs',
    'IScanlineSource.cs', 'WicScanlineSource.cs',
    'AFS2World.cs', 'TtcToneCurve.cs', 'SourceImage.cs',
    'Coastline.cs', 'CoastlineField.cs', 'WaterFixField.cs', 'TileSampler.cs',
    'TtcFile.cs', 'Bc1Encoder.cs', 'TtcMipChain.cs', 'TtcTileName.cs',
    'TtcTileWriter.cs', 'TtcConverter.cs'
) | ForEach-Object { Join-Path $afs2 $_ }
$sources += (Join-Path $here 'Classify.cs')

& $csc /nologo /optimize+ /platform:x64 /out:$exe "/lib:$refs" `
    /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw "compile failed" }

$argv = @($Coastline, $Path)
if ($MarginNm -gt 0) {
    $argv += $MarginNm.ToString([Globalization.CultureInfo]::InvariantCulture)
}

& $exe @argv
exit $LASTEXITCODE
