# Runs the in-app converter over a real .tmc, outside the app.
#
#   tools/ttc/csharp/convert.ps1 "C:\...\g_15_stitch.tmc" "C:\scratch\out_csharp"
#
# The end-to-end check is against the reference over the same input:
#
#   python tools/ttc/convert_tmc.py --tmc "C:\...\g_15_stitch.tmc" --out C:\scratch\out_python
#   python tools/ttc/hash_dir.py C:\scratch\out_csharp
#   python tools/ttc/hash_dir.py C:\scratch\out_python
#
# The two manifests must match. Not in the validation suite: it needs a real stitched source and
# it takes minutes.

param(
    [Parameter(Mandatory = $true)][string]$Tmc,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [int]$Threads = 1,
    # Treat pure black source pixels as no data, so a later source or the mask takes over.
    # Changes output, so the reference cross-check above only holds without it.
    [switch]$BlackIsMissing,
    # Stop the photoscenery at a hand-drawn coastline - the file the Map tab's Draw Coast saves.
    # Changes output as well, and for the same reason: the reference has never heard of a coast.
    [string]$Coastline = '',
    # How far out to sea to cut, overriding the file. Default is whatever the file says, 3 NM.
    [double]$MarginNm = 0,
    # Take the haze and the cloud out of the water, using a field measured by
    # tools/ttc/water_fix.py. Changes output as well, and for the same reason as the two above.
    [string]$WaterFix = '',
    # Where to build ProbeConvert.exe. Two of these running at once compile to the same path and
    # the second one cannot overwrite an exe the first one is running, so a caller that converts
    # several squares in parallel must give each its own.
    [string]$Bin = ''
)

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here '..\..\..')
$afs2 = Join-Path $repo 'AeroScenery\AFS2'
$out  = if ($Bin) { $Bin } else { Join-Path $here 'bin' }

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
if (-not (Test-Path $Tmc)) { throw "tmc not found: $Tmc" }

$refs = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
if (-not (Test-Path $refs)) { throw ".NET Framework 4.8 reference assemblies not found at $refs" }

New-Item -ItemType Directory -Force $out | Out-Null
New-Item -ItemType Directory -Force $OutDir | Out-Null
$exe = Join-Path $out 'ProbeConvert.exe'

$sources = @(
    'TmFields.cs', 'TmcReader.cs', 'AIDFile.cs',
    'IScanlineSource.cs', 'WicScanlineSource.cs',
    'AFS2World.cs', 'TtcToneCurve.cs', 'SourceImage.cs',
    'Coastline.cs', 'CoastlineField.cs', 'WaterFixField.cs', 'TileSampler.cs',
    'TtcFile.cs', 'Bc1Encoder.cs', 'TtcMipChain.cs', 'TtcTileName.cs',
    'TtcTileWriter.cs', 'TtcConverter.cs'
) | ForEach-Object { Join-Path $afs2 $_ }
$sources += (Join-Path $here 'ProbeConvert.cs')

& $csc /nologo /optimize+ /platform:x64 /out:$exe "/lib:$refs" `
    /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw "compile failed" }

# Built as an array rather than written out, because an empty string is not reliably passed
# through to a native exe and the coastline arguments are the optional ones.
$argv = @($Tmc, $OutDir, $Threads, $(if ($BlackIsMissing) { '1' } else { '0' }))
if ($Coastline) {
    $argv += $Coastline
    $argv += $MarginNm.ToString([Globalization.CultureInfo]::InvariantCulture)
}
# Named, so it can be given on its own - the positional coastline arguments may be absent.
if ($WaterFix) {
    if (-not (Test-Path $WaterFix)) { throw "water fix field not found: $WaterFix" }
    $argv += '--water'
    $argv += (Resolve-Path $WaterFix).Path
}

& $exe @argv
exit $LASTEXITCODE
