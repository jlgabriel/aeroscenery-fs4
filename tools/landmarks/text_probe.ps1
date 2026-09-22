# Text probe: will Aerofly FS 4 read a .tft written as plain text?
#
# The shipped .tft files are compressed, and reading one needs LZHAM. But Aerofly reads tm files
# as text too -- world/aircraft.tmw is plain text in the usual <[file][][] syntax. If the parser
# accepts a .tft as text, LZHAM stops mattering: we write place names by hand.
#
#   .\text_probe.ps1 -Setup     write the test files into the user landmark folder
#   .\text_probe.ps1 -Read      report what tm.log says
#   .\text_probe.ps1 -Undo      put the game's own files back
#
# Undo restores each file from the game folder, so the user folder cannot be left damaged.
#
# What is known, and what is being guessed
# ----------------------------------------
# Known, from the executable: the classes are tmterrain_landmark_list, its field landmarks holds
# tmterrain_landmark elements, and there is a field called extra.
# Known, from world/aircraft.tmw: a list element is written <[TYPE][element][INDEX] ... >.
# Known, from missions/custom_flights.tmc: a geographic position is
# <[vector2_float64][lon_lat][LON LAT]> -- longitude first.
# Guessed: the field names of a landmark. That is what the variants below are for.
#
# The variants sit in ONE file as six landmarks of the same list, five minutes apart along a
# north-south line over Santiago. If the parser skips fields it does not know, the ones that
# guessed right draw their label and the ones that guessed wrong draw nothing or land at 0,0.
# A second and third file carry the safest variant on its own, in case the six-way file is
# rejected whole.

[CmdletBinding()]
param(
    [switch]$Setup,
    [switch]$Read,
    [switch]$Undo,
    # The game install. Defaults to AEROFLY_FS4_DIR, then to the default Steam library.
    [string]$GameDir = $(if ($env:AEROFLY_FS4_DIR) { $env:AEROFLY_FS4_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Aerofly FS 4 Flight Simulator" }),
    [string]$UserDir = "$env:USERPROFILE\Documents\Aerofly FS 4"
)

$ErrorActionPreference = 'Stop'
$LandmarkDir = "$UserDir\scenery\landmarks"
$LogPath     = "$UserDir\tm.log"
$LogBackup   = "$UserDir\tm.log.before-text-probe"

# lm_07_4c00_6600  Santiago and Valparaiso, lat -34.15..-31.82  -- the six variants
# lm_07_4c00_6400  Talca and Curico,        lat -36.41..-34.15  -- variant A alone
# lm_07_4c00_6800  La Serena and Ovalle,    lat -31.82..-29.43  -- variant A plus an 'extra' field
$Targets = @("lm_07_4c00_6600.tft", "lm_07_4c00_6400.tft", "lm_07_4c00_6800.tft")

# One landmark per variant. The label names the variant, so whatever appears in the sky says
# which guess was right.
#   A  name + lon_lat, string8            the likeliest: matches the mission file convention
#   B  name + lon_lat, string8u           same, unicode string type
#   C  text + lon_lat                     the label field might be called text
#   D  name + position                    the position field might be called position
#   E  Name + LonLat, PascalCase          aircraft.tmw uses PascalCase field names
#   F  name + lon_lat_alt + type + size   a landmark may need a height and a class
$Variants = @(
    @{ id='A'; lat=-33.30; body=@'
                <[string8][name][TEST-A]>
                <[vector2_float64][lon_lat][{LON} {LAT}]>
'@ },
    @{ id='B'; lat=-33.37; body=@'
                <[string8u][name][TEST-B]>
                <[vector2_float64][lon_lat][{LON} {LAT}]>
'@ },
    @{ id='C'; lat=-33.44; body=@'
                <[string8][text][TEST-C]>
                <[vector2_float64][lon_lat][{LON} {LAT}]>
'@ },
    @{ id='D'; lat=-33.51; body=@'
                <[string8][name][TEST-D]>
                <[vector2_float64][position][{LON} {LAT}]>
'@ },
    @{ id='E'; lat=-33.58; body=@'
                <[string8][Name][TEST-E]>
                <[vector2_float64][LonLat][{LON} {LAT}]>
'@ },
    @{ id='F'; lat=-33.65; body=@'
                <[string8][name][TEST-F]>
                <[vector3_float64][lon_lat_alt][{LON} {LAT} 600]>
                <[uint32][type][0]>
                <[float64][size][1]>
'@ }
)

$LON = -70.70   # over the city, so every label lands on built-up ground

function New-TftText([array]$items, [string]$extraField) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<[file][][]')
    [void]$sb.AppendLine('    <[tmterrain_landmark_list][][]')
    if ($extraField) { [void]$sb.AppendLine("        $extraField") }
    [void]$sb.AppendLine('        <[list_tmterrain_landmark][landmarks][]')
    $i = 0
    foreach ($v in $items) {
        [void]$sb.AppendLine("            <[tmterrain_landmark][element][$i]")
        $body = $v.body.Replace('{LON}', $LON).Replace('{LAT}', $v.lat)
        [void]$sb.AppendLine($body.TrimEnd())
        [void]$sb.AppendLine('            >')
        $i++
    }
    [void]$sb.AppendLine('        >')
    [void]$sb.AppendLine('    >')
    [void]$sb.AppendLine('>')
    return $sb.ToString()
}

function Invoke-Setup {
    if (-not (Test-Path $LandmarkDir)) {
        throw "$LandmarkDir does not exist. Run probe.ps1 -Populate first."
    }
    $n = (Get-ChildItem $LandmarkDir -Filter *.tft).Count
    if ($n -lt 2229) {
        throw "$LandmarkDir holds $n files, not 2229. Run probe.ps1 -Populate first."
    }

    # The whole six-way file, over Santiago.
    $all = New-TftText $Variants $null
    # Variant A on its own, moved into each neighbour tile so it sits on that tile's own ground.
    $aTalca    = New-TftText @(@{ id='A'; lat=-35.43; body=$Variants[0].body }) $null
    $aSerena   = New-TftText @(@{ id='A'; lat=-30.03; body=$Variants[0].body }) '<[uint32][extra][0]>'

    $written = @{ $Targets[0] = $all; $Targets[1] = $aTalca; $Targets[2] = $aSerena }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    foreach ($name in $Targets) {
        $path = Join-Path $LandmarkDir $name
        [System.IO.File]::WriteAllText($path, $written[$name], $utf8NoBom)
        Write-Host "wrote text .tft: $path"
    }

    Write-Host "`n--- what went into $($Targets[0]) ---"
    Write-Host $all

    if (Test-Path $LogPath) {
        if (Test-Path $LogBackup) { Remove-Item $LogBackup -Force }
        Move-Item $LogPath $LogBackup
        Write-Host "moved the old tm.log to tm.log.before-text-probe"
    }
    Write-Host "Ready. Start the simulator, load a flight near Santiago, then run: .\text_probe.ps1 -Read"
}

function Invoke-Read {
    if (-not (Test-Path $LogPath)) { Write-Host "no tm.log yet -- the simulator has not run."; return }
    $lines = Get-Content $LogPath

    Write-Host "=== landmark folder ==="
    $lines | Where-Object { $_ -match 'landmarks? folder' } | ForEach-Object { Write-Host "  $($_.Trim())" }

    Write-Host "`n=== landmark class messages, and anything naming our tiles ==="
    $pat = 'terrain landmark class|4c00_6600|4c00_6400|4c00_6800|\.tft'
    $hits = $lines | Where-Object { $_ -match $pat }
    if ($hits) { $hits | ForEach-Object { Write-Host "  $($_.Trim())" } } else { Write-Host "  (nothing)" }

    Write-Host "`n=== any parse or file error ==="
    $errs = $lines | Where-Object { $_ -match 'error|failed|invalid|cannot|unable' }
    if ($errs) { $errs | Select-Object -Last 25 | ForEach-Object { Write-Host "  $($_.Trim())" } }
    else       { Write-Host "  (none)" }

    Write-Host "`n=== how to read it ==="
    Write-Host "  An error naming one of our three tiles -> text is rejected. LZHAM stays the wall."
    Write-Host "  No error, and a TEST- label in the sky  -> text WORKS, and the label says which"
    Write-Host "     variant was right. That is the whole problem solved."
    Write-Host "  No error and no label -> the file parsed but the fields are still wrong."
}

function Invoke-Undo {
    foreach ($name in $Targets) {
        $src = Join-Path $GameDir "scenery\landmarks\$name"
        $dst = Join-Path $LandmarkDir $name
        if (Test-Path $src) { Copy-Item $src $dst -Force; Write-Host "restored from the game: $dst" }
        elseif (Test-Path $dst) { Remove-Item $dst -Force; Write-Host "removed (no game original): $dst" }
    }
    Write-Host "`nThe user folder is back to the game's own files."
}

if     ($Setup) { Invoke-Setup }
elseif ($Read)  { Invoke-Read }
elseif ($Undo)  { Invoke-Undo }
else { Write-Host "Use one of:  -Setup  |  -Read  |  -Undo" }
