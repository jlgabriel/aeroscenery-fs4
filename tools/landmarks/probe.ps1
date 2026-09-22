# Landmark probe: does Aerofly FS 4 read landmarks from the user folder?
#
# The place name labels drawn over the terrain come from <game>\scenery\landmarks\*.tft.
# We do not know if the simulator reads them from anywhere else. This probe answers that,
# and it answers a second question at the same time: are the coordinates inside a .tft
# absolute, or relative to the tile the file name names?
#
# Method: copy one dense European .tft into three candidate user folders, under two
# Chilean tile names. Then fly and read tm.log.
#
#   .\probe.ps1 -Setup     put the files in place
#   .\probe.ps1 -Read      report what tm.log says
#   .\probe.ps1 -Undo      remove every file this script wrote
#
# Nothing in the game folder is touched. Undo restores the machine exactly.

[CmdletBinding()]
param(
    [switch]$Setup,
    [switch]$Read,
    [switch]$Undo,
    [switch]$Populate,
    # The game install. Defaults to AEROFLY_FS4_DIR, then to the default Steam library.
    [string]$GameDir = $(if ($env:AEROFLY_FS4_DIR) { $env:AEROFLY_FS4_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Aerofly FS 4 Flight Simulator" }),
    [string]$UserDir = "$env:USERPROFILE\Documents\Aerofly FS 4"
)

$ErrorActionPreference = 'Stop'

# The densest landmark tile IPACS ship: 336,936 bytes uncompressed, lat 44.8..46.7,
# lon 5.6..8.4. Geneva and the French Alps. Its names cannot be mistaken for Chilean ones.
$SourceTile = "lm_07_8400_a400.tft"

# lm_07_4a00_6600  sea west of Valparaiso, lon -75.94..-73.12 -- no base file, so a hit here
#                  is the simulator ADDING a landmark file it did not have.
# lm_07_4c00_6600  Santiago and Valparaiso, lat -34.15..-31.82 -- a base file exists, so a
#                  hit here says the user folder WINS over the game folder.
$DestNames = @("lm_07_4a00_6600.tft", "lm_07_4c00_6600.tft")

# The three places the simulator might look. tm.log prints the folder it found, so one
# flight tells us which of them works.
$Candidates = @(
    "$UserDir\scenery\landmarks",
    "$UserDir\addons\scenery\landmark_probe\landmarks",
    "$UserDir\addons\scenery\landmark_probe\scenery\landmarks"
)

$LogPath   = "$UserDir\tm.log"
$LogBackup = "$UserDir\tm.log.before-landmark-probe"

# Undo removes what setup created, and only that. Setup writes the list here.
$MadeList = "$UserDir\landmark-probe-made.txt"

function Invoke-Setup {
    $source = Join-Path $GameDir "scenery\landmarks\$SourceTile"
    if (-not (Test-Path $source)) { throw "source tile not found: $source" }
    $bytes = (Get-Item $source).Length
    Write-Host "source: $SourceTile ($bytes bytes on disk)"

    $made = New-Object System.Collections.Generic.List[string]
    foreach ($dir in $Candidates) {
        # Record every level we create, deepest last, so undo can walk it backwards.
        $missing = @()
        $walk = $dir
        while ($walk -and -not (Test-Path $walk)) { $missing += $walk; $walk = Split-Path $walk -Parent }
        [array]::Reverse($missing)
        $missing | ForEach-Object { $made.Add("dir`t$_") }
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        foreach ($name in $DestNames) {
            $dest = Join-Path $dir $name
            if (Test-Path $dest) {
                Write-Host "  SKIP (already there, not overwritten): $dest"
            } else {
                Copy-Item $source $dest
                $made.Add("file`t$dest")
                Write-Host "  wrote: $dest"
            }
        }
    }
    $made | Set-Content $MadeList -Encoding UTF8

    # Keep the current log, so a stale line cannot be read as a result.
    if (Test-Path $LogPath) {
        if (Test-Path $LogBackup) { Remove-Item $LogBackup -Force }
        Move-Item $LogPath $LogBackup
        Write-Host "`nmoved the old tm.log to tm.log.before-landmark-probe"
    }

    Write-Host "`nReady. Now fly, then run:  .\probe.ps1 -Read"
}

function Invoke-Read {
    if (-not (Test-Path $LogPath)) {
        Write-Host "no tm.log yet -- the simulator has not run since -Setup."
        return
    }

    $lines = Get-Content $LogPath
    $hits = $lines | Where-Object { $_ -match 'landmark folder|landmarks folder' }

    Write-Host "=== what tm.log says about landmark folders ==="
    if ($hits) { $hits | ForEach-Object { Write-Host "  $($_.Trim())" } }
    else       { Write-Host "  (nothing -- the simulator logged no landmark folder at all)" }

    Write-Host "`n=== errors, if any ==="
    $errs = $lines | Where-Object { $_ -match 'terrain landmark class' }
    if ($errs) { $errs | ForEach-Object { Write-Host "  $($_.Trim())" } }
    else       { Write-Host "  (none)" }

    Write-Host "`n=== how to read it ==="
    Write-Host "  'scenery/landmarks/', files=2229  -> the game folder. The user folder is not in use."
    Write-Host "  a path under Documents, files=2229 -> the user folder is in use and complete."
    Write-Host "  a path under Documents, files<2229 -> the user folder is in use and INCOMPLETE."
    Write-Host "     It replaces the game folder, so every tile missing from it has no labels at all."
    Write-Host "     Run -Populate."
}

function Invoke-Undo {
    if (-not (Test-Path $MadeList)) {
        Write-Host "no record of a setup run ($MadeList). Nothing removed."
        return
    }
    # Set-Content writes a BOM, which would otherwise glue itself to the first word.
    $entries = Get-Content $MadeList |
        ForEach-Object { $_.TrimStart([char]0xFEFF) } |
        Where-Object { $_ -match "`t" }

    # Files first, then the folders deepest first, so an empty folder can go.
    foreach ($e in $entries) {
        $kind, $path = $e -split "`t", 2
        if ($kind -eq 'file' -and (Test-Path $path)) {
            Remove-Item $path -Force; Write-Host "removed: $path"
        }
    }
    $dirs = $entries | Where-Object { $_ -like "dir`t*" } | ForEach-Object { ($_ -split "`t", 2)[1] }
    foreach ($d in ($dirs | Sort-Object -Property Length -Descending)) {
        if ((Test-Path $d) -and -not (Get-ChildItem $d -Force)) {
            Remove-Item $d -Force; Write-Host "removed empty folder: $d"
        }
    }

    Remove-Item $MadeList -Force
    Write-Host "`nThe game folder was never touched. Nothing else to restore."
}

# The probe showed the user folder REPLACES the game folder rather than adding to it: with two
# files in it, the whole world lost its labels. So a user folder is only usable once it holds the
# 2,229 files the game ships. After this the folder is ours: Steam cannot overwrite it, and any
# .tft we learn to write goes in beside them.
function Invoke-Populate {
    $src = Join-Path $GameDir "scenery\landmarks"
    if (-not (Test-Path $src)) { throw "game landmarks folder not found: $src" }
    $dst = "$UserDir\scenery\landmarks"

    $probeLeftovers = $DestNames | ForEach-Object { Join-Path $dst $_ } | Where-Object { Test-Path $_ }
    if ($probeLeftovers) {
        throw "run -Undo first: the probe files are still in $dst"
    }

    if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst -Force | Out-Null }
    $files = Get-ChildItem $src -Filter *.tft
    Write-Host "copying $($files.Count) files to $dst ..."
    $files | Copy-Item -Destination $dst -Force

    $n = (Get-ChildItem $dst -Filter *.tft).Count
    $mb = ((Get-ChildItem $dst -Filter *.tft | Measure-Object Length -Sum).Sum / 1MB)
    Write-Host ("done: {0} files, {1:N1} MB" -f $n, $mb)
    if ($n -ne $files.Count) { Write-Warning "count does not match the game folder ($($files.Count))" }
    Write-Host "`nRestart the simulator. tm.log should now say files=$n for the Documents folder."
}

if     ($Setup)    { Invoke-Setup }
elseif ($Read)     { Invoke-Read }
elseif ($Undo)     { Invoke-Undo }
elseif ($Populate) { Invoke-Populate }
else { Write-Host "Use one of:  -Setup  |  -Read  |  -Undo  |  -Populate" }
