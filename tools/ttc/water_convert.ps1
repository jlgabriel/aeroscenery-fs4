# Converts a list of squares with the water fix, several squares at once, and installs them.
#
#   tools\ttc\water_convert.ps1 -Working <working folder> -Plan <working folder>\water_plan.txt `
#       -Coastline <working folder>\coastline.txt -Install -Package <package>\images
#
# The list is what `water_fix.py --targets ... --plan <file>` writes: the squares whose field
# actually corrects something. Everything else is left alone, which is most of the package.
#
# Three things this exists to get right:
#
# - **The conversion is not CPU bound.** Inside one square the PNG decode and the sampling are
#   single threaded and only BC1 uses the rest, so the lever is running SQUARES at once, not
#   threads. Two at a time is about 14 GB of working set; three needs a quiet machine.
# - **Pass the coastline exactly where the line was actually drawn.** Twenty of the squares are cut
#   at the sea, and converting one of them without the line would fill its ocean back in and drop
#   its masks. But where the line STOPS the converter carries the coast straight on, so a square
#   outside the drawn latitudes gets its ocean cut against an extrapolation: `map_09_4b80_5f00` sits
#   2.1 degrees south of the end of the line and came out 435 tiles and 63 masks instead of 1365 and
#   none. So the line goes only to squares that lie wholly inside its own latitude range, which is
#   read from the file. Inside that range a landward square converts byte for byte identical with
#   the line or without it, so there is still no list to keep in step.
# - **Clear the output folder first.** Both the cut and the fix write FEWER tiles than a plain
#   build, so leftovers survive and get installed as sea.
#
# The output goes to `<zoom>-geoconvert-ttc-waterfix`, beside the uncorrected `<zoom>-geoconvert-ttc`
# rather than over it. That is what the next survey measures from - measure a corrected tile and
# the correction is applied twice - and it is how a square is put back the way it was.
param(
    # A file with one square name per line, or the names themselves, comma separated.
    [Parameter(Mandatory = $true)][string]$Plan,
    # The app's working folder, which holds one folder per grid square.
    [Parameter(Mandatory = $true)][string]$Working,
    # The hand-drawn waterline. Left out, a coastal square's sea comes back.
    [string]$Coastline = '',
    [string]$Source = 'b',
    [int]$Zoom = 17,
    # Squares at once. About 7 GB of working set each.
    [int]$Parallel = 2,
    [int]$Threads = [Environment]::ProcessorCount,
    # Copy each square into the installed package as it finishes.
    [switch]$Install,
    # The package's images folder, for example
    # <Aerofly user folder>\addons\scenery\<package>\images. Needed with -Install.
    [string]$Package = ''
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$convert = Join-Path $here 'csharp\convert.ps1'

if ($Install -and -not $Package) { throw "-Install needs -Package: the package's images folder" }

if (Test-Path $Plan) {
    $names = @(Get-Content $Plan | ForEach-Object { $_.Trim() } | Where-Object { $_ })
} else {
    $names = @($Plan -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}
if (-not $names) { throw "no squares in: $Plan" }
if ($Coastline -and -not (Test-Path $Coastline)) { throw "coastline not found: $Coastline" }

# The Aerofly grid, so a square's own edges can be worked out from its name. Not Mercator -
# see AeroScenery\AFS2\AFS2World.cs, which is where this comes from. Level 9 only.
$K = 2.3311223704144
function Lat-Of([int]$gy) { [Math]::Atan($K * (2.0 * $gy / 512.0 - 1.0)) * 180.0 / $K }
function Lon-Of([int]$gx) { 180.0 * (2.0 * $gx / 512.0 - 1.0) }

# Where the drawn line begins and ends. A square outside it must not be given the line at all.
# The stretch runs the way the coast does: latitude when the land is east or west, longitude
# when the land is north or south. The app makes the same choice in AeroSceneryManager.CoastFor.
$coastFrom = 0.0
$coastTo = 0.0
$alongLat = $true
if ($Coastline) {
    $lats = @(); $lons = @()
    foreach ($line in (Get-Content $Coastline)) {
        if ($line -match '^\s*#\s*land\s+(\w+)') {
            $alongLat = @('east', 'west') -contains $Matches[1].ToLowerInvariant()
        } elseif ($line -match '^\s*(-?\d+(\.\d+)?)\s+(-?\d+(\.\d+)?)') {
            $lats += [double]$Matches[1]
            $lons += [double]$Matches[3]
        }
    }
    if (-not $lats) { throw "no points in: $Coastline" }
    $along = if ($alongLat) { $lats } else { $lons }
    $axis = if ($alongLat) { 'lat' } else { 'lon' }
    $coastFrom = ($along | Measure-Object -Minimum).Minimum
    $coastTo = ($along | Measure-Object -Maximum).Maximum
    Write-Host ("coastline: {0} points, {1} {2:0.####} .. {3:0.####}" -f $lats.Count, $axis, $coastFrom, $coastTo)
}

Write-Host ("{0} square(s), {1} at a time" -f $names.Count, $Parallel)

$jobs = @()
$done = 0
$failed = @()

function Wait-Slot([int]$keep) {
    while (@($script:jobs | Where-Object { -not $_.Proc.HasExited }).Count -ge $keep) {
        Start-Sleep -Seconds 5
    }
    foreach ($j in @($script:jobs | Where-Object { $_.Proc.HasExited })) {
        $script:jobs = @($script:jobs | Where-Object { $_ -ne $j })
        $script:done++
        $secs = [int]($j.Proc.ExitTime - $j.Proc.StartTime).TotalSeconds
        if (Test-Path $j.Bin) { Remove-Item $j.Bin -Recurse -Force -ErrorAction SilentlyContinue }
        if ($j.Proc.ExitCode -ne 0) {
            $script:failed += $j.Name
            Write-Host ("  {0}  FAILED (exit code {1}), see {2}" -f $j.Name, $j.Proc.ExitCode, $j.Log)
            continue
        }
        $n = @(Get-ChildItem $j.Out -Filter *.ttc -File).Count
        $m = @(Get-ChildItem $j.Out -Filter *_mask.ttc -File).Count
        Write-Host ("  {0}/{1} {2}  {3} s, {4} tiles + {5} masks" -f `
                    $script:done, $names.Count, $j.Name, $secs, ($n - $m), $m)
        if ($n -eq 0) {
            # A square wholly out at sea converts to nothing at all, and /MIR would then empty the
            # installed folder - or create an empty one where the square had rightly been deleted.
            # map_09_4c80_6700 is the case: it lies west of the coastline from end to end.
            Write-Host ("      0 files, not installed (the cut removes the whole square)")
            continue
        }
        if ($Install) {
            $dst = Join-Path $Package $j.Name
            $r = robocopy $j.Out $dst /MIR /NJH /NJS /NP /NFL /NDL
            # robocopy says 0 for nothing to do and 1..7 for work done; 8 and up is a failure.
            if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $($j.Name): $LASTEXITCODE" }
            Write-Host ("      installed in {0}" -f $dst)
        }
    }
}

foreach ($name in $names) {
    $stitched = Join-Path $Working "$name\$Source\$Zoom-stitched"
    $tmc = Join-Path $stitched ("{0}_{1}_stitch.tmc" -f $Source, $Zoom)
    $awfx = Join-Path $stitched 'water_fix.awfx'
    $out = Join-Path $Working "$name\$Source\$Zoom-geoconvert-ttc-waterfix"
    $log = Join-Path $stitched 'water_convert.log'
    if (-not (Test-Path $tmc)) { throw "no tmc: $tmc" }
    if (-not (Test-Path $awfx)) { throw "no water fix field: $awfx" }

    $cut = ''
    if ($Coastline) {
        if ($name -notmatch 'map_09_([0-9a-f]{4})_([0-9a-f]{4})') { throw "odd square name: $name" }
        if ($alongLat) {
            $g = [Convert]::ToInt32($Matches[2], 16) / 128
            $from = Lat-Of $g
            $to = Lat-Of ($g + 1)
        } else {
            $g = [Convert]::ToInt32($Matches[1], 16) / 128
            $from = Lon-Of $g
            $to = Lon-Of ($g + 1)
        }
        if ($from -ge $coastFrom -and $to -le $coastTo) {
            $cut = $Coastline
        } else {
            Write-Host ("  {0}  no cut: {1} {2:0.##}..{3:0.##} is not inside the line" -f $name, $axis, $from, $to)
        }
    }

    Wait-Slot $Parallel
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    New-Item -ItemType Directory -Force $out | Out-Null

    # Its own build directory. convert.ps1 compiles ProbeConvert.exe every run, and csc cannot
    # overwrite an exe another square is running.
    $bin = Join-Path $env:TEMP ("aeroscenery_build\" + $name)
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $convert,
           '-Tmc', $tmc, '-OutDir', $out, '-Threads', $Threads, '-WaterFix', $awfx, '-Bin', $bin)
    if ($cut) { $a += @('-Coastline', $cut) }
    $p = Start-Process -FilePath 'powershell.exe' -ArgumentList $a -PassThru `
                       -RedirectStandardOutput $log -RedirectStandardError "$log.err" `
                       -WindowStyle Hidden
    $jobs += [pscustomobject]@{ Name = $name; Proc = $p; Out = $out; Log = $log; Bin = $bin }
    Write-Host ("  -> {0}" -f $name)
}

Wait-Slot 1
if ($failed) {
    Write-Host ("{0} square(s) failed: {1}" -f $failed.Count, ($failed -join ' '))
    exit 1
}
Write-Host ("{0} square(s) done" -f $done)
# robocopy leaves 1 in $LASTEXITCODE when it has copied something, and a script with no explicit
# exit hands that on as a failure.
exit 0
