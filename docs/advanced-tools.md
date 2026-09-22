# Advanced tools

This guide is for power users and developers. It describes the command-line tools that sit beside
the AeroScenery app. The app itself is described in the [README](../README.md) and the
[user guide](user-guide.md). Read those first: this guide uses the same terms and does not repeat
them.

Only one of these tools, `AeroSceneryConvert.exe`, is in the release zip. The others are in the
`tools\` folder of the source repository. They are developer tooling and are not shipped. To use
them, clone the repository and run them from its root folder.

## Before you start

| Tool | Needs |
|---|---|
| Python scripts (`*.py`) | Python 3 |
| `tools\ttc\*.py` | Python 3 with `numpy` and `Pillow` |
| `tools\landmarks\*.py` | Python 3 only (standard library) |
| `tools\ttc\water_fix.py` | also an internet connection, to download reference imagery from Bing |
| PowerShell scripts (`*.ps1`) | Windows PowerShell |
| `.ps1` scripts that compile C# | `csc.exe` from the 64-bit .NET Framework 4.x folder that is part of Windows |
| `classify.ps1`, `convert.ps1` | also the .NET Framework 4.8 reference assemblies, from the .NET Framework 4.8 Developer Pack or the Build Tools |

```powershell
python -m pip install numpy Pillow
```

If PowerShell refuses to run a script, run it for one call only, without a change to the system
policy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\coastline\run.ps1
```

The commands in this guide use these placeholders:

| Placeholder | Meaning |
|---|---|
| `<working folder>` | the **Working Folder** from *Settings* |
| `<grid square>` | a level 9 square folder, for example `map_09_4c00_5f80` |
| `<source>`, `<zoom>` | the image source letter and the zoom, for example `b` (Bing) and `17` |
| `<package>` | the **Scenery Package Name** from *Settings* |
| `<Aerofly user folder>` | `Documents\Aerofly FS 4` |
| `<Aerofly install folder>` | the folder where Aerofly FS 4 itself is installed |

The landmark scripts need to know where the game is installed. They read the environment
variable `AEROFLY_FS4_DIR`, and without it they use the default Steam library,
`C:\Program Files (x86)\Steam\steamapps\common\Aerofly FS 4 Flight Simulator`. If the game is in
another Steam library, set the variable first:

```powershell
$env:AEROFLY_FS4_DIR = "<Aerofly install folder>"
```

## 1. AeroSceneryConvert — the converter on the command line

`AeroSceneryConvert.exe` does the same job as the app's *Run Converter* action, with the same code.
It turns one `.tmc` and its stitched images into `.ttc` tiles. Use it in a script, for example to
convert many grid squares two or three at a time, or to convert a square again with a different
coastline.

```powershell
AeroSceneryConvert "<working folder>\<grid square>\<source>\<zoom>-stitched\<source>_<zoom>_stitch.tmc" `
    --coast "<working folder>\coastline.txt"
```

The options are `--out`, `--threads`, `--coast`, `--margin-nm` and `--quiet`. Exit code 0 means
converted, 1 means failed, 2 means bad arguments. The full description and two rules for `--coast`
are in [AeroSceneryConvert/README.md](../AeroSceneryConvert/README.md).

`AeroSceneryConvert` does not apply the water fix (section 3). For that, use
`tools\ttc\csharp\convert.ps1`.

## 2. classify.ps1 — which squares a coastline cuts

When you draw more coastline, some squares must be converted again. Most squares do not. This
script tells you which ones, without a conversion. It reads only the `.tmc` of each square and
opens no source image, so it takes seconds per square.

```powershell
tools\coastline\classify.ps1 "<working folder>\coastline.txt" "<working folder>"
tools\coastline\classify.ps1 "<working folder>\coastline.txt" "<working folder>\<grid square>"
tools\coastline\classify.ps1 "<working folder>\coastline.txt" "<working folder>" -MarginNm 5
```

The second argument is a working folder that holds `map_*` squares, one square folder, or one
`.tmc` file. `-MarginNm` sets the distance out to sea. Without it, the script uses the distance
saved in the coastline file.

It gives one verdict per square:

| Verdict | Meaning | What to do |
|---|---|---|
| `LAND` | The whole square is inside the margin. | Nothing. A new conversion gives the same bytes. |
| `CUT` | The cut crosses the square. | Empty its `<zoom>-geoconvert-ttc` folder, then convert it again with `--coast`. |
| `SEA` | The whole square is past the cut. | Delete the square. It has no photoscenery. |
| `OUTSIDE` | The square is not wholly inside the stretch of coast the line covers. | Do not give it `--coast`. Past an end of the line the cut is only a guess. Draw the line past the square first. |

The verdicts are exact. The script asks the converter's own code for the area to check, so it
checks the same area that the converter samples.

`OUTSIDE` applies the same rule as the app and `water_convert.ps1`: latitudes when the land is
east or west, longitudes when it is north or south. See section 5 of the
[user guide](user-guide.md).

The script compiles the converter sources with `csc` each time it runs.

## 3. The water fix — haze and cloud over water

### What it fixes

Bing builds its high-zoom imagery from separate acquisitions. Over water the joins are very
visible: straight-edged blocks of haze, white patches and cloud. Lakes show it most.

Three facts make a correction possible:

- **Bing's zoom 12 mosaic is clean.** It is a different mosaic, with no blocks and no cloud. Zooms
  13 and up share the defect, so a new download at the working zoom does not help.
- **The defect is a haze layer on top of the same water.** A hazy block is brighter by about the
  same amount on all three channels, with the same texture. So a subtraction can remove it.
- **The defect is on the water only.** The fix must never touch land.

### How it works

The correction is **measured in Python and applied in C#**. No stitched image is ever rewritten.

1. `water_fix.py` uses the zoom 12 mosaic to find where the water is. It does not take colour from
   it.
2. It divides the water into separate **bodies of water** (`water_bodies.py`), and levels each body
   onto the tone of its own cleanest part.
3. It writes the result as a small **field** per square: `pixel * scale + offset` per channel.
   Where there is no water, the field is exactly scale 1, offset 0.
4. The converter reads the field and applies it while it samples the source images.

The field is a file named `water_fix.awfx` in `<grid square>\<source>\<zoom>-stitched\`.

### Rules

- **Measure every square that a body of water touches, in one survey.** Each body gets one target
  tone. If you measure two squares that share a lake separately, each picks a different target,
  and the lake gets a straight step where the squares meet.
- **A body counts as water only if its own tone is blue.** Its blue minus its red must be at least
  15. This rejects mountain shadow and snow, which are dark or grey but not blue.
- **Exact black is unbuilt source, not dark water.** Where the download had no imagery, the source
  is exactly (0, 0, 0). The script leaves those pixels out.
- **A partial batch leaves a small step at its edge.** Where water crosses from a corrected square
  into a square without a correction, the tone changes a little at the border.
- **Measure from uncorrected tiles.** The script measures each square's own level 9 tile in
  `<zoom>-geoconvert-ttc`. If that tile already has the correction, the correction is applied
  twice. Keep the uncorrected tiles, or keep the field.

### Step by step: a small group

For one lake or two, which lie in a few squares, one run does all of it. Every square must be
converted once, without the fix, before you start.

```powershell
python tools\ttc\water_fix.py "<working folder>\<grid square 1>" "<working folder>\<grid square 2>" --preview
```

`--preview` writes `water_fix_preview.png`, with the image before the fix on the left and after it
on the right. Look at it before you convert anything, and look at the square borders in
particular. Then empty each square's output folder and convert it with the field:

```powershell
tools\ttc\csharp\convert.ps1 "<...>\<source>_<zoom>_stitch.tmc" "<output folder>" `
    -WaterFix "<...>\<zoom>-stitched\water_fix.awfx" -Threads <n>
```

### Step by step: a whole package

Bodies of water chain from square to square. For a large area, the job is two passes.

**Pass 1, the survey.** It fixes the target tone of every body of water. It must see all the
squares at once. It uses a coarse grid, which is enough for a target tone.

```powershell
python tools\ttc\water_fix.py "<working folder>\map_09_*" `
    --survey "<working folder>\water_targets.awft" --report "<working folder>\water_bodies.csv"
```

The report lists every body over 2 km², with its area, its target tone and the squares it lies in.
Use it to decide a batch: a body that lies in several squares makes all of them one job. The area
is also a check. If a body comes out much smaller than its real area, it runs past the edge of the
squares you gave.

**Pass 2, the fields.** It writes one field per square at full resolution, with a margin of the
neighbours' imagery around it. Give it every square, and choose the ones to write with `--only`. A
square whose neighbour is left off the command line gets a seam at that border.

```powershell
python tools\ttc\water_fix.py "<working folder>\map_09_*" `
    --targets "<working folder>\water_targets.awft" `
    --only "<grid square 1>,<grid square 2>" --plan "<working folder>\water_plan.txt"
```

`--plan` writes the names of the squares whose field changes something. The other squares need no
new conversion.

**Convert the plan.** `water_convert.ps1` converts the squares in the plan, several at a time, and
can install each square when it is complete. `-Working` is required, and so is `-Package` with
`-Install`. `-Threads` defaults to the number of logical processors:

```powershell
tools\ttc\water_convert.ps1 -Plan "<working folder>\water_plan.txt" `
    -Working "<working folder>" -Threads <n> -Parallel 2 `
    -Coastline "<working folder>\coastline.txt" `
    -Install -Package "<Aerofly user folder>\addons\scenery\<package>\images"
```

What it does:

- It writes each square to `<zoom>-geoconvert-ttc-waterfix`, beside the uncorrected
  `<zoom>-geoconvert-ttc`. It empties that folder first. The uncorrected tiles stay for the next
  survey.
- It runs `-Parallel` squares at once. Each square uses about 7 GB of memory at its peak.
- It passes the coastline only to a square that lies wholly inside the stretch of coast the line
  covers: its latitudes when the land is east or west, its longitudes when the land is north or
  south. It reads the land side from the `# land` line of `coastline.txt`, as the app does.
- With `-Install`, it mirrors each square into `-Package\<grid square>`. Files that the new build
  does not make are removed from that installed square. A square that converts to no files is not
  installed.
- Each square writes a log, `water_convert.log`, in its `<zoom>-stitched` folder.

It is safe to stop a run at any point. A square is installed only when it is complete.

### water_fix.py options

| Option | What it does |
|---|---|
| `squares` | Square folders (`map_09_XXXX_YYYY`). The script expands wildcards itself. |
| `--survey <file>` | Measure every body over all the squares given, write the targets, and stop. |
| `--report <file>` | With `--survey`: write every body of water as CSV. |
| `--targets <file>` | Read a survey's targets and write one field per square. |
| `--only <list>` | With `--targets`: write fields only for these squares. A file with one name per line, or a comma-separated list. |
| `--plan <file>` | Write the names of the squares whose field changes something. |
| `--preview` | Also write `water_fix_preview.png` (before and after). |
| `--dry-run` | Measure, but write no field. |
| `--source`, `--zoom` | The squares' source letter and zoom. Defaults `b` and `17`. |
| `--ref-zoom` | The clean Bing mosaic. Default `12`. |
| `--grid`, `--survey-grid`, `--halo`, `--target-grow` | Grid sizes, in texels. Keep the defaults. |
| `--threads` | Reference downloads at the same time. Default `8`. |
| `--cache <folder>` | Where to keep the downloaded reference tiles. Default: `_refcache` in the first square's `<zoom>-stitched` folder. |

With neither `--survey` nor `--targets`, the script measures and writes fields over the squares
given, as one group. The script works only on level 9 squares.

The reference imagery comes from Bing. As with the app, you are responsible for how you use it.

### The other water fix files

- `tools\ttc\water_bodies.py` is a module, not a command. It labels connected bodies of water with
  numpy alone. Connectivity is 4. The mask is opened by two texels before labelling, so that a
  narrow river mouth does not join a lake to the sea, and the labels are then grown back.
- `tools\ttc\csharp\convert.ps1` compiles the converter and runs it on one `.tmc`. Its arguments
  are `<tmc> <output folder>`, then `-Threads` (default 1), `-Coastline`, `-MarginNm`, `-WaterFix`,
  `-BlackIsMissing` and `-Bin`. It does not empty the output folder. Do that first.

The field is applied by `AeroScenery\AFS2\WaterFixField.cs`. The app and `AeroSceneryConvert`
never apply a field.

## 4. Landmarks — place names over the terrain

Aerofly FS 4 draws place names over towns in flight. These are **landmarks**. They come from
`.tft` files in `<Aerofly install folder>\scenery\landmarks\`, one per tile of the level 7 grid,
named `lm_07_<x>_<y>.tft`. The tools in `tools\landmarks\` write these files. Read
[tools/landmarks/README.md](../tools/landmarks/README.md) before you use them.

### What is known about the format

- **A `.tft` is a container, not a landmark file.** It holds one payload, with its sizes and CRC32
  checksums. `tft.py` reads and writes the container.
- **The payload is text, compressed with deflate with a zlib header.** Raw deflate fails. An
  uncompressed payload is refused.
- **A landmark has exactly five members:** `name`, `lon_lat`, `type`, `priority` and `height`.
- **`height` is above sea level**, not above the ground. A label with a height of 0 lies under
  the terrain and does not show.
- **Coordinates are in degrees, longitude first.**
- What `type` and `priority` change is not known. The tools write 0 for both.

### Two limits

- **The user folder replaces the game folder.** Aerofly reads landmarks from
  `<Aerofly user folder>\scenery\landmarks\` if that folder exists, and then it ignores the game's
  own folder. So the user folder must hold all the files the game ships, or the rest of the world
  loses its labels. Addon folders are not read.
- **A `.tft` can be written, but its landmarks cannot be read.** The shipped files are compressed
  with LZHAM, which these tools cannot decompress. So you cannot merge new labels into a shipped
  tile. A tile you write replaces the shipped tile, and it must hold every place that belongs in
  it.

### Step 1: fill the user folder

Copy all the game's landmark files into the user folder:

```powershell
tools\landmarks\probe.ps1 -Populate -GameDir "<Aerofly install folder>" -UserDir "<Aerofly user folder>"
```

It prints the number of files copied. After that, the folder is yours, and a game update does not
change it. To go back to the game's own labels, delete `<Aerofly user folder>\scenery\landmarks\`.

### Step 2: build tiles — build_chile.py as a worked example

`build_chile.py` builds landmark tiles for Chile from [GeoNames](https://download.geonames.org/export/dump/).
Use it as it is, or as a model for another country.

1. Download `CL.zip` from GeoNames, and also `AR.zip`, `PE.zip` and `BO.zip`. A level 7 tile is
   about 2.8° of longitude wide, so tiles on the Chilean border also cover the neighbour
   countries. Their places must be in the tile too. Unzip the `.txt` files into one folder.
2. Check that `AEROFLY_FS4_DIR` points to your `<Aerofly install folder>`, if it is not in the
   default Steam library (see [Before you start](#before-you-start)). The script uses the game's
   folder with `--install` to put back the game's file for a tile that an earlier run wrote and
   this run does not.
3. Run it without `--install` first. It prints the tiles, and the places and bytes in each. It
   writes nothing.

   ```powershell
   python tools\landmarks\build_chile.py --data "<GeoNames folder>"
   ```

4. Then install, and restart the simulator:

   ```powershell
   python tools\landmarks\build_chile.py --data "<GeoNames folder>" --install
   ```

What it selects: the comunas (GeoNames class `A`, code `ADM3`), and the cities and towns (class
`P`) that have a population or are a capital. A city and a comuna with the same name less than
8 km apart become one label. The height is the GeoNames `dem` value plus 50 m. Names are the ASCII
form, without accents. The Santiago tile also gets three test labels, `ACC-U8`, `ACC-8U` and
`ACC-L1`, which test how accented names show.

`--install` writes into `<Aerofly user folder>\scenery\landmarks\`. It stops if that folder does
not exist, so do step 1 first.

### Write your own

- `make_tft.build(places)` in `tools\landmarks\make_tft.py` takes a list of dicts with `name`,
  `lon`, `lat` and, as options, `type`, `priority` and `height`. It returns the bytes of a
  finished `.tft`.
- `cell(lat, lon)` and `tile_name(x, y)` in `build_chile.py` give the level 7 tile of a place and
  its file name.
- `tft.build_deflate(content, zlib_header=True)` wraps any payload in the container.

To check `tft.py` against your own install, run it on the game's folder. It rebuilds every
shipped file from its own payload and must say `PASS`:

```powershell
python tools\landmarks\tft.py "<Aerofly install folder>\scenery\landmarks"
```

## 5. The reference implementation and the test suites

`tools\ttc\` holds a reference implementation of the `.ttc` format in Python (`ttc.py`). The
converter in the app is checked against it. The format itself is described in
[ttc-format.md](ttc-format.md).

Run these after a change to the converter, the tile writer or the coastline code:

```powershell
python tools\ttc\validate_ttc.py
tools\ttc\csharp\run.ps1
python tools\ttc\cross_check.py
tools\coastline\run.ps1
```

| Suite | What it proves | Expected result |
|---|---|---|
| `validate_ttc.py` | The Python reference reads and writes `.ttc` files correctly. It rebuilds two tiles made by IPACS' GeoConvert (in `tools\ttc\testdata\`) byte for byte. It also checks the water body labelling. | `RESULT: <n> passed, 0 failed` |
| `csharp\run.ps1` | The same checks on the C# tile writer that ships in the app. Near its end it prints a list of SHA-256 hashes. | `RESULT: <n> passed, 0 failed` |
| `cross_check.py` | It prints SHA-256 hashes of the reference BC1 encoder over a fixed test pattern. | Every hash equals the one with the same name in the output of `run.ps1`. |
| `tools\coastline\run.ps1` | The cut lies at the margin from the drawn coast, on capes, bays and hand jitter, and the distance field that the converter reads agrees with an independent calculation. | `ALL PASSED` |

The hash comparison matters. A BC1 encoder can differ from its reference in rounding or tie
breaking and still make tiles that look good. Only equal hashes show that the two are the same.

The suites compile only the sources they test, with `csc`. So they also check that this code does
not depend on the user interface.

To compare the whole converter with the reference on a real square, convert the same `.tmc` both
ways and compare the hash lists. The two lists must be identical:

```powershell
tools\ttc\csharp\convert.ps1 "<...>\<source>_<zoom>_stitch.tmc" "<output folder>\csharp"
python tools\ttc\convert_tmc.py --tmc "<...>\<source>_<zoom>_stitch.tmc" --out "<output folder>\python"
python tools\ttc\hash_dir.py "<output folder>\csharp"
python tools\ttc\hash_dir.py "<output folder>\python"
```

Do this without `-Coastline`, `-WaterFix` and `-BlackIsMissing`. The reference does not know them,
so with them the output is different by design.

More tools, and the measurements behind them, are in [tools/ttc/README.md](../tools/ttc/README.md)
and [tools/coastline/README.md](../tools/coastline/README.md).
