# AeroSceneryConvert

The AeroScenery converter as a console program. It turns a `.tmc` and its stitched images into
Aerofly `.ttc` colour tiles — the same job as the app's *Run Converter* action, with the same code.

Use it when a script is easier than the app: to convert many grid squares, two or three at a time,
or to convert a square again with a different coastline. It needs no GPU and no desktop session. It
prints what it does, and it exits with a real exit code.

```
usage: AeroSceneryConvert <file.tmc> [--out <dir>] [--threads <n>]
                          [--coast <file>] [--margin-nm <n>] [--linear-rows]
                          [--quiet]

  --out <dir>      write tiles here instead of the .tmc's folder_destination_ttc
  --threads <n>    BC1 encoder threads; 0 means all cores. Default is half of them.
  --coast <file>   stop the photoscenery at this hand-drawn coastline
                   (the coastline.txt that Draw Coast on the Map tab saves)
  --margin-nm <n>  how far out to sea to cut, overriding the file. Default 3.
  --linear-rows    read the image rows as linear in latitude, as version 2.0 and
                   GeoConvert did
  --quiet          no progress, only errors
```

The rows of a stitched image are Web Mercator rows, because the map tiles are. The `.aid` only
gives the north and south edges and a constant step, which is linear in latitude. Read linearly,
the imagery is correct at the edges of each stitched image and displaced north or south between
them: about 4 m at the centre with 66 tiles of zoom 17, and about 15 m with zoom 16. So this
converter reads the rows as Mercator rows between the same edges. `--linear-rows` gives the old
placement, for a comparison.

Exit codes: `0` converted, `1` failed, `2` bad arguments.

The `.tmc` is the file the app's *Generate AID / TMC Files* action writes, beside the stitched
images: `<working>\<grid square>\<source>\<zoom>-stitched\<source>_<zoom>_stitch.tmc`. It names
where the images are and where the tiles go, so a plain run needs no other argument.

Two rules for `--coast`, both learnt on real squares:

- **Only cut a square that lies wholly inside the stretch of coast the line covers** — its
  latitudes when the land is east or west, its longitudes when the land is north or south. Past an
  end of the line the converter carries the coast straight on, so a square past the end is cut
  against a guess. The app applies this rule by itself; from the command line, it is yours to
  apply. `tools\coastline\classify.ps1` tells you which squares it allows. The rule is for the
  coast only: an island is a closed ring, and it has no ends.
- **Draw every island in the square.** The file can hold islands as well as a coast, and a point
  inside an island is land. The land of an island that was not drawn is cut away as sea.
- **Empty the output folder first.** A cut writes fewer tiles than a build without one, and an old
  tile left in the folder would be installed as sea.

A square that lies wholly past the cut writes nothing. With `--coast` that is a result, not an
error: there is no photoscenery in that square.

## What it does not do

- **No heightmaps.** A `.tmc` with `do_heightmaps` set is refused rather than half-converted.
- **No raw output.** `write_raw_files` is reported and ignored.
- **No compression.** Tiles are written stored rather than LZHAM-compressed. Aerofly FS 4 renders
  them either way — IPACS ship stored tiles themselves — but the files are larger on disk. How much
  larger depends on the imagery: flat ocean compresses a lot, and a city barely at all.

Anything else in a `.tmc` that it does not recognise is named on stderr before the run starts,
because a silent difference in the output is the worst way to find out about one.

## Building

```
msbuild AeroSceneryConvert\AeroSceneryConvert.csproj -t:Build -p:Configuration=Release
```

.NET Framework 4.8, x64, no NuGet packages. The output is `bin\Release\AeroSceneryConvert.exe`, and
it needs nothing beyond the framework itself — one file is the whole install.

The conversion is not implemented here. The `AeroScenery\AFS2` sources are **linked** into this
project, not copied, so this exe and the converter in the app cannot drift apart. It is a
reimplementation from the file format, not IPACS code.
