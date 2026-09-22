# Landmarks: the place names drawn over the terrain

The labels Aerofly FS 4 draws over towns in flight are **landmarks**. They come from
`scenery/landmarks/*.tft`, 2,229 files, drawn with `texture/landmarks.tff` and switched by the
`show_landmarks` setting. The grid is the one in [AFS2Grid.cs](../../AeroScenery/AFS2/AFS2Grid.cs)
at **level 7**, so the step is `0x200` and Santiago falls in `lm_07_4c00_6600.tft`.

Chile is thin, and it is measurable:

| tile | bytes uncompressed |
|---|---:|
| Santiago, `lm_07_4c00_6600` | 8,264 |
| mean of the 18 Chilean tiles | 2,936 |
| the densest tile in the world, `lm_07_8400_a400` (Geneva, French Alps) | 336,936 |

**Done and flown, 2026-08-27.** Chile carries its cities and its 346 comunas, built from
GeoNames by [build_chile.py](build_chile.py): 616 places, 931 labels, 33 tiles. They were flown over
several parts of the country, Santiago included, and the labels read right.

Two things are still open. Names are **unaccented** -- three test labels over Pudahuel ask whether
`string8` takes UTF-8, whether `string8u` does, and whether latin-1 does; read them, then set
`ACCENTS` and rebuild. And what **`type` and `priority`** change has never been measured.

## The format, all of it

A `.tft` is **not a landmark file**. It is a generic **`tmfile_properties_compressed`** container
holding one payload. The executable names its five fields, and they line up one for one with the
nodes in the file:

    SizeUncompressed      uint64
    SizeCompressed        uint64
    ChecksumUncompressed  uint32
    ChecksumCompressed    uint32
    Compressed            blob

The node layout, the FNV-1a name hashes and the CRC32 checksums are all in
[tft.py](tft.py), which **rebuilds every one of the 2,229 shipped files byte for byte** from its
own payload. Run it to check:

```bash
python tools/landmarks/tft.py
```

The payload is **tm text**, compressed with **deflate carrying a zlib header**:

```
<[file][][]
    <[tmterrain_landmark_list][][]
        <[list_tmterrain_landmark][landmarks][]
            <[tmterrain_landmark][element][0]
                <[string8][name][Providencia]>
                <[vector2_float64][lon_lat][-70.610000 -33.425000]>
                <[uint32][type][0]>
                <[uint32][priority][0]>
                <[float64][height][590]>
            >
```

A `tmterrain_landmark` has **exactly five members**: `name`, `lon_lat`, `type`, `priority`,
`height`. That is not a guess -- 47 candidate names were offered to the parser in one flight and
it named the 42 it rejected. `extra` is not a member of the list either, though the executable
mentions it.

Four things that are settled and cost real flights:

- **`height` is altitude above SEA LEVEL, not above the ground.** This is what made every early
  round draw nothing: the control left it at 0, Santiago stands at 520 m, and the label was under
  the terrain. H1000 and H2000 hung in the sky, H600 sat at roof height, H0/H300/H520 never
  appeared. So every place needs its own elevation.
- **Coordinates are degrees, longitude first.** The same flight put the same place in radians and
  with the pair reversed; neither appeared.
- **Deflate must carry a zlib header.** Raw deflate fails to inflate. IPACS compress their own
  files with LZHAM, which we cannot write and still cannot read -- and do not need to.
- **A stored, uncompressed payload is refused**, unlike in a `.ttc`. That refusal is what named
  the codec: `tinfl_decompress` is miniz, so `tmcompress` falls back to inflate when the blob does
  not open with a tmcompress chunk.

`type` and `priority` both draw, and what they change has **not** been measured. The engine has a
`landmark_color_table` and a `landmark_size_factor`, so they plausibly pick a colour and decide
how far away a label survives.

**The parser answers back.** Anything it does not recognise it names in `tm.log`:

    tmfile_properties: WARNING: property 'Landmarks' is not a member of
    type 'tmterrain_landmark_list'  hash=15279641139067209878

The hash is our own FNV-1a, to the digit. That is the oracle the sweep used, and it is there for
any future question about a field name -- offer many, keep whatever draws no warning.

## Where the files go

**The user folder works, and it is `<user>\scenery\landmarks\`.** Neither addon path is even
looked at, so landmarks do **not** follow the POI convention.

**It REPLACES the game folder, it does not add to it.** With two files in the user folder the
whole world loses its labels. So the folder is useless until it holds everything the game ships,
which is what `probe.ps1 -Populate` does: all 2,229 files, verified byte identical, 2.9 MB. After
that the folder is ours, and Steam cannot overwrite it on an update.

**The coordinates inside a `.tft` are absolute.** The file name only decides when a file is read,
never where its labels land.

## Using it

```bash
python tools/landmarks/make_tft.py --demo      # 23 real towns over Santiago, plus sweeps
python tools/landmarks/make_tft.py --diag      # the height / radians / order diagnosis
python tools/landmarks/make_tft.py --restore   # put the game's own file back
```

`make_tft.build(places)` takes a list of dicts with `name`, `lon`, `lat` and optionally `type`,
`priority`, `height`, and returns a finished `.tft`.

**Overwriting a tile loses what IPACS put in it.** We can write a `.tft` but still cannot read
one, so the labels already in a tile cannot be merged with new ones -- rebuilding the Santiago
tile is what dropped `Santiago`, `Puente Alto` and `La Pintana`. Any real dataset has to carry the
big towns itself, not just the missing ones.

## The probes, in order

`probe.ps1` answered where the files go. `write_probe.py` answered what is inside them, over four
rounds; its docstring is the record of how, including the two failures that named the codec and
the oracle. `make_tft.py` is what came out the other end.
