# Compiles and runs the C# .ttc validation suite.
#
#   tools/ttc/csharp/run.ps1
#
# The writer sources have no dependency on WinForms, log4net or anything else the app pulls in,
# so csc can build them on their own. That keeps this suite runnable without opening the
# solution or building AeroScenery, and it is also a standing check that the writer stays free
# of UI dependencies - if this stops compiling, something reached into the app from the .ttc
# code that should not have.

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here '..\..\..')
$afs2 = Join-Path $repo 'AeroScenery\AFS2'
$data = Resolve-Path (Join-Path $here '..\testdata')
$out  = Join-Path $here 'bin'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }

New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $out 'ValidateTtc.exe'

$sources = @(
    (Join-Path $afs2 'TtcFile.cs'),
    (Join-Path $afs2 'Bc1Encoder.cs'),
    (Join-Path $afs2 'TtcMipChain.cs'),
    (Join-Path $afs2 'TtcTileName.cs'),
    (Join-Path $afs2 'TmFields.cs'),
    (Join-Path $afs2 'TmcReader.cs'),
    (Join-Path $afs2 'AIDFile.cs'),
    (Join-Path $afs2 'AFS2World.cs'),
    (Join-Path $afs2 'TtcTileWriter.cs'),
    (Join-Path $afs2 'WaterFixField.cs'),
    (Join-Path $here 'Validate.cs')
)

& $csc /nologo /optimize+ /platform:x64 /out:$exe $sources
if ($LASTEXITCODE -ne 0) { throw "compile failed" }

& $exe $data
exit $LASTEXITCODE
