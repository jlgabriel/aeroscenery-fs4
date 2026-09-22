# Checks AeroScenery\AFS2\Coastline.cs and CoastlineField.cs - the hand-drawn coastline, the cut it
# produces, and the rasterised version of that cut the converter actually reads.
#
# Compiles those two files with csc and nothing else, which is also a standing check that the
# geometry never grows a dependency on WinForms or GMap. It has to stay clean: the converter uses
# it with no UI anywhere in sight, and the editor is only one of its callers.
#
#     tools\coastline\run.ps1
#
# Two tests matter more than the rest. For the line: every point of the cut sits at the margin
# from the coast, on a shape with capes, bays and hand jitter, which settles the corner rounding
# and the fold culling at once. For the field: it agrees with an oracle written a completely
# different way - the sign from "which side of a single-valued line", the distance from
# Coastline.DistanceKm - because a distance field that is merely plausible looks perfect on a map
# and cuts holes in the scenery.

$ErrorActionPreference = 'Stop'

$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$afs2 = Join-Path $root 'AeroScenery\AFS2'
$src = @('Coastline.cs', 'CoastlineField.cs') | ForEach-Object { Join-Path $afs2 $_ }
$tests = Join-Path $PSScriptRoot 'CoastlineTests.cs'
$exe = Join-Path $env:TEMP 'aeroscenery_coastline_tests.exe'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
foreach ($f in $src) { if (-not (Test-Path $f)) { throw "not found: $f" } }

& $csc /nologo /optimize+ /platform:x64 /out:$exe $tests $src
if ($LASTEXITCODE -ne 0) { throw "compile failed" }

& $exe
exit $LASTEXITCODE
