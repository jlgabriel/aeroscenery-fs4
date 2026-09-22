# The Aerofly FS 4 `.ttc` tile format

This document describes the photoscenery tile files that Aerofly FS 4 reads: the colour tile
(`.ttc`), its mask (`_mask.ttc`), and the `.tmc` and `.aid` files that a converter reads to make
them. It is for developers who want to read or write these files.

This is an independent description. We worked out the format from files on disk and from what the
simulator does with them. We did not use IPACS documentation or IPACS code. Aerofly FS is a
trademark of IPACS. IPACS made the format and the original GeoConvert tool, and the earlier
AeroScenery authors built the grid code that this work stands on. Any error here is ours.

The shipping implementation is in [AeroScenery/AFS2/](../AeroScenery/AFS2/). The reference
implementation, in Python, is [tools/ttc/ttc.py](../tools/ttc/ttc.py). When this document and the
code disagree, the code and its test suite are correct.

**Status labels.** **Verified** means measured, rebuilt byte for byte, or reproduced by a test
suite. **Verified in the simulator** means a test file was installed in Aerofly FS 4 and the result
was seen in flight. **Inferred** means strong evidence, not confirmed. **Unknown** items are listed
in [Still unknown](#still-unknown).

## The files at a glance

| file | what it is | who reads it |
|---|---|---|
| `map_LL_XXXX_YYYY.ttc` | One colour tile: 2048 × 2048 pixels, BC1, full mip chain. | Aerofly |
| `map_LL_XXXX_YYYY_mask.ttc` | The coverage mask for that tile: 512 × 512, L8, full mip chain. | Aerofly |
| `*.tmc` | The job: which levels and which lon/lat box to build. | the converter |
| `*.aid` | Georeferencing for one source image. | the converter |

## 1. The `.ttc` container — Verified

A `.ttc` is a flat 256-byte header, then a payload. All values are little-endian.

| offset | type | field | colour tile | mask tile |
|---|---|---|---|---|
| `0x00` | u32 | magic | `0x0000303A` | `0x0000303A` |
| `0x04` | u32 | version | `0x00000100` | `0x00000100` |
| `0x08` | u32 | level | grid level, for example 9 | same as its colour tile |
| `0x0C` | u32 | size_compressed | bytes of payload after `0x100` | same |
| `0x10` | u32 | size_uncompressed | bytes of the raw mip chain | same |
| `0x14` | u32 | width | 2048 | 512 |
| `0x18` | u32 | height | 2048 | 512 |
| `0x1C` | u32 | num_mips | 12 | 10 |
| `0x20` | u32 | format | `10` = BC1 (DXT1) | `0` = L8 |
| `0x24` | u32 | unknown | `0xFFFFFFFF` | `0x00FFFFFF` |
| `0x28` | u32 | unknown | `0xFFFFFFFF` | `0x00000000` |
| `0x2C`–`0xFF` | | padding | zero | zero |
| `0x100` | | payload | `size_compressed` bytes | same |

The file size is always `0x100 + size_compressed`. **A `.ttc` has no checksum.** It is also not
the hash-keyed node-tree container that some other Aerofly files use. It has no field names, no
name hashes and no node table. The header above is the whole structure.

The values in `0x24` and `0x28` are what GeoConvert writes. Their meaning is unknown. Tiles that
carry these values render correctly, so a writer can use them as they are.

### Stored and compressed payloads

The payload has one of two shapes:

- **Stored.** The raw mip chain starts at `0x100`. `size_compressed` equals `size_uncompressed`.
- **Compressed.** The payload is a `tmcompress` chunk (below). GeoConvert writes this shape.

A reader tells them apart by the chunk magic. If the u32 at `0x104` is `0xA810BEF4`, the payload is
a chunk. If not, the payload is stored.

**Aerofly FS 4 renders stored tiles — Verified in the simulator.** This is true for colour tiles
and for masks. The simulator log showed no error for them. Thus **a writer does not need LZHAM or
any other compressor.** The cost is disk space. Over the BC1 tiles in a full install, compression
saves a median factor of 1.28. On real photoscenery it saves a factor of about 1.14. A stored tile
is thus about 14 to 28 percent larger.

### The `tmcompress` chunk

| offset in chunk | type | value |
|---|---|---|
| `+0x00` | u32 | `0x40`, the chunk header length (also the offset of the codec data) |
| `+0x04` | u32 | magic `0xA810BEF4` |
| `+0x08` | u64 | size_uncompressed |
| `+0x10` | u64 | size_total: chunk header plus codec data (equals the file's `size_compressed`) |
| `+0x18` | 16 bytes | zero |
| `+0x28` | u64 | magic `0x17F34DF32797945C` |
| `+0x30` | u32 | `21` |
| `+0x34` | u32 | `20` |
| `+0x38` | 8 bytes | zero |
| `+0x40` | | codec data |

The layout is Verified: the reference code rebuilds GeoConvert's files byte for byte from these
fields. The codec is **Inferred** to be LZHAM. GeoConvert links LZHAM and contains its error
strings, and the data is not zlib or raw deflate. Nobody has decompressed one yet. The values `21`
and `20` were the same in every file seen. Their meaning is unknown. (`21` may be an LZHAM
dictionary size of 2^21 bytes. That is a guess.)

### Formats seen in a full install — Verified

We surveyed 205,490 `.ttc` files in a full Aerofly FS 4 install: the base game, scenery add-ons
from IPACS and from third parties.

| `format` | files | payload | chunk |
|---|---:|---|---|
| `0` | 882 | L8 mask mip chain | always |
| `10` | 99,169 | BC1 mip chain | always |
| `0x12345671` | 59,637 | Basis Universal file | never |
| `0x12345672` | 45,802 | Basis Universal file | always |

The two Basis values are markers, not texture types. They only tell whether the Basis file is
wrapped in a chunk. The tools here identify Basis tiles but do not write them.

## 2. Colour tiles — Verified

A colour tile is 2048 × 2048 pixels in BC1 (DXT1), with the full mip chain down to 1 × 1. That is
12 mip levels.

**The mip chain.** Mip 0 (2048 × 2048) comes first. Each next mip is half the width and half the
height. The mips follow each other with no header and no padding between them. Mips smaller than
4 × 4 still use one whole BC1 block of 8 bytes. The total is exactly 2,796,216 bytes:

```
sum over i = 0..11 of  max(1, ceil((2048 >> i) / 4))^2  *  8  =  2,796,216
```

The reference code makes each mip from the one above with a 2 × 2 box filter,
`(a + b + c + d + 2) / 4`, per channel.

**The BC1 blocks.** Each block is 8 bytes and holds 4 × 4 pixels:

| bytes | content |
|---|---|
| 0–1 | colour 0, RGB565, u16 little-endian |
| 2–3 | colour 1, RGB565, u16 little-endian |
| 4–7 | 16 two-bit indices, u32 little-endian; pixel `i = row * 4 + col` uses bits `2i` and `2i + 1` |

Blocks are in row-major order. This is standard BC1. The reference encoder always uses the
four-colour mode, so colour 0 is greater than colour 1. When a block is flat, the two colours are
equal and every pixel uses index 0. A colour survives BC1 exactly only if it is on the RGB565
lattice. Other colours lose only the quantisation.

The encoder in [Bc1Encoder.cs](../AeroScenery/AFS2/Bc1Encoder.cs) is a simple bounding-box encoder.
GeoConvert uses a different encoder. Thus our tiles are valid BC1, but their bytes are not the same
as the bytes of a GeoConvert tile made from the same source.

## 3. Rows are stored south to north — Verified in the simulator

> **Row 0 of every texture in a `.ttc` is the SOUTH edge of the tile.**
> The rows go bottom-up, from south to north. This applies to colour tiles and to masks, to every
> mip level, and to the rows inside each BC1 block.

Most image tools, most source imagery and most tile servers put north at the top, in row 0. If you
write a tile north-up, Aerofly shows it mirrored top to bottom. Each tile still looks sharp, so the
error is easy to miss. But the mosaic does not fit together, and features are in the wrong place.

The shipping code flips the rows once, just before the BC1 encode. See `FlipRows` in
[TtcTileWriter.cs](../AeroScenery/AFS2/TtcTileWriter.cs). A reader must flip the rows back to get a
north-up image.

Two warnings, learned the hard way:

- **A file-to-file comparison cannot find this error.** GeoConvert can also write each tile as a
  plain PNG before the encode, and those PNGs are north-up. The flip happens after that step. A
  round trip through your own encoder and decoder also cannot find it.
- **A symmetric test pattern cannot find it.** A checkerboard mirrors into a checkerboard. The
  error was found with an asymmetric pattern (a large letter F, with a coloured stripe on the north
  edge) installed at a known place and looked at in the simulator.

## 4. Mask tiles

A mask tile is 512 × 512 pixels in L8 (one byte per pixel), with the full mip chain down to 1 × 1.
That is 10 mip levels and exactly 349,525 bytes. It has the same name as its colour tile, with
`_mask` added. It uses the same row order: **row 0 is the south edge**.

One mask pixel covers 4 × 4 colour pixels. A value of `0` means "no photo here": Aerofly shows its
own imagery. A non-zero value means "show the photo".

**Aerofly thresholds the mask. It does not blend it — Verified in the simulator.** A test tile had
stripes with mask values 255, 224, 192, 160, 128, 96, 64, 32 and 0. All stripes from 255 down to
32 showed at full strength. The stripe with 0 did not show. Thus the threshold is somewhere in
**(0, 32]**. In practice, **any non-zero value is fully opaque**. You can cut a boundary, but you
cannot fade it.

The mip chain of a mask comes from the same 2 × 2 box filter as a colour tile. Thus the smaller
mips hold values between 0 and 255 along the edge of the coverage. This does not mean that the
engine blends.

**When a mask is written.** The shipping converter does this:

| coverage of the tile | files written |
|---|---|
| no source pixel reaches the tile | nothing — not even a black tile |
| some pixels are covered | colour tile, and a mask if `write_images_with_mask` is true |
| all pixels are covered | colour tile only |

A mask pixel is 255 if any of its 16 colour pixels is covered, and 0 if none is. (The converter
takes the maximum, not the average. An average would pull the coverage in at every edge.) Where a
pixel is not covered, the colour tile holds black, and the mask hides it.

**Write the mask together with its colour tile.** In one test, masks were added later beside
colour tiles that were already installed without a mask. The masks had no visible effect. The
cause is unknown. Masks written at the same time as their colour tiles work.

## 5. The grid and tile names

### The grid — Verified

The grid is **not Web Mercator**. The shipping code is in
[AFS2Grid.cs](../AeroScenery/AFS2/AFS2Grid.cs) and in plain functions in
[AFS2World.cs](../AeroScenery/AFS2/AFS2World.cs). The mapping comes from the original AeroScenery
project, which credits a
[thread on the Aerofly community forum](https://www.aerofly.com/community/forum/index.php?thread/12550-image-tile-coordinates/&pageNo=1).

With `k = 2.3311223704144`, and latitude and longitude in radians:

```
x = 2^level * (0.5 + 0.5 * lon / pi)
y = 2^level * (0.5 + 0.5 * tan(k * lat / pi) / k)
```

The inverse:

```
lon = pi * (2 * x / 2^level - 1)
lat = pi * atan(k * (2 * y / 2^level - 1)) / k
```

The tile index is `floor(x)`, `floor(y)`. Some facts that follow from the formula:

- **`y` counts northward from the south pole.** `y = 0` is 90° S and `y = 2^level` is 90° N. The
  equator is at `y = 2^(level-1)`. A southern latitude gives a `y` below that.
- Longitude is linear. A tile is `360 / 2^level` degrees wide: 0.703125° at level 9.
- Latitude is not linear. At level 9 a tile is about 0.703° high at the equator, 0.600° at 30°,
  and 0.356° at 60°. Thus a tile stays roughly square on the ground at low and middle latitudes.
- Latitude is WGS84 geodetic latitude.
- The grid nests exactly. Tile `(x, y)` at level N-1 covers the four tiles `(2x, 2y)`,
  `(2x+1, 2y)`, `(2x, 2y+1)` and `(2x+1, 2y+1)` at level N. The children with `2y+1` are the
  NORTH half.

The formula was checked at four places in both hemispheres against GeoConvert's own log and
against the terrain position that Aerofly FS 4 reports. It agreed to within 1e-5 of a tile.

### Tile names — Verified

```
map_LL_XXXX_YYYY.ttc
map_LL_XXXX_YYYY_mask.ttc
```

- `LL` is the level in two decimal digits.
- `XXXX` and `YYYY` are four lowercase hex digits: the tile index times `2^(16 - level)`.

The name is thus the position of the tile's **south-west corner** on a fixed 65536 × 65536 world
grid. The multiplier is 128 at level 9, 16 at level 12 and 4 at level 14. It is **not** a constant
128. (That mistake gives correct names at level 9 only.) A name can express levels 0 to 16.

Example: level 9, tile `(139, 291)` is `map_09_4580_9180.ttc`, because `139 × 128 = 0x4580` and
`291 × 128 = 0x9180`. It covers lon −82.265625 to −81.5625 and lat 23.8235 to 24.4601. Its
south-west child at level 12 is tile `(1112, 2328)`, and it has the same hex pair:
`map_12_4580_9180.ttc`.

See [TtcTileName.cs](../AeroScenery/AFS2/TtcTileName.cs) for the name in both directions.

## 6. Levels, and how a level N-1 tile relates to level N

Each level halves the tile size on the ground, but every tile is still 2048 × 2048 pixels. A level
N-1 tile covers the same ground as its four level N children.

The shipping converter samples only the deepest level from the source images. It makes each
shallower level from the level below: it puts the four children into a 4096 × 4096 square and
halves it with the 2 × 2 box filter. The coverage is halved the same way: a parent pixel is covered
when at least two of its four child pixels are covered. This is a choice of the converter, not a
rule of the format. Any correct 2048 × 2048 image of the right ground works.

## 7. The tone curve

GeoConvert does not copy source pixels into the tile unchanged. It makes them brighter. We measured
this against the plain PNGs that GeoConvert can write before the encode, over 15 million pixels in
five tiles at three levels. The difference is one curve, the same in all three channels. It is not
a simple gamma curve, so the code keeps it as a 256-entry table: see
[TtcToneCurve.cs](../AeroScenery/AFS2/TtcToneCurve.cs) and
[tone_curve.py](../tools/ttc/tone_curve.py). With the curve, the mean difference from GeoConvert's
pixels fell from 33.07 to 1.79 out of 255.

The converter applies the curve so that new tiles match the brightness of tiles that GeoConvert
made. The curve is not part of the file format. Aerofly reads whatever pixel values the tile holds.

## 8. The converter inputs: `.tmc` and `.aid`

Both files use the same text syntax: nested `<[type][name][value]>` entries. The converter reads
them as a flat list of fields, in order. See [TmFields.cs](../AeroScenery/AFS2/TmFields.cs).

### `.tmc` — the job

A `.tmc` holds a `tmcolormap_regions` block with a `region_list`. Each region starts with a `level`
field. The fields after a `level` belong to that region, until the next `level`.

| field | used by the shipping converter |
|---|---|
| `folder_source_files` | Yes. The folder that holds the `.aid` files. If it is empty, or the folder does not exist, the converter uses the folder of the `.tmc`. |
| `folder_destination_ttc` | Yes, when no output folder is given to the converter. |
| `level` | Yes. Starts a region. |
| `lonlat_min`, `lonlat_max` | Yes. Two corners of the region, as `lon lat` in degrees. |
| `write_images_with_mask` | Yes, at file level and per region. Controls whether masks are written. |
| `write_ttc_files`, `write_raw_files`, `folder_destination_raw` | Parsed, but not acted on. The converter always writes `.ttc` and never writes raw PNGs. |
| `do_heightmaps`, `always_overwrite`, all other keys | Ignored. |

In the files AeroScenery writes, `lonlat_min` holds the **north-west** corner and `lonlat_max` the
**south-east** corner, in spite of the names. The converter sorts the values, so it does not depend
on this. The regions that AeroScenery writes use consecutive levels over the same square.

### `.aid` — one source image

| field | used by the shipping converter |
|---|---|
| `image` | Yes. The image file name, in the same folder as the `.aid`. |
| `top_left` | Yes. `lon lat` of the top-left corner of the image, in degrees. This is the outer corner of the first pixel, not its centre. |
| `steps_per_pixel` | Yes. Degrees per pixel in longitude and latitude. The latitude step is normally negative, because the image runs north to south. |
| `flip_vertical` | Parsed, but not acted on. |
| `mask`, `coordinate_system` | Ignored. AeroScenery writes an empty `mask` and `lonlat`. |

A source image is thus a plain linear lon/lat raster. The output grid is not linear in latitude, so
the converter must resample. It uses the nearest source pixel at the centre of each output pixel.
When source images overlap, the first `.aid` in file-name order that covers a pixel gives that
pixel.

## 9. How this was verified

- **Fixtures from the original tool.** [tools/ttc/testdata/](../tools/ttc/testdata/) holds a
  colour tile and a mask that GeoConvert 1.4.5 made from synthetic noise. The test suites parse
  each file into its parts and rebuild it **byte for byte**. That proves the header and the chunk
  layout, not only that they are plausible.
- **Size arithmetic.** Both size fields and the file length agree exactly with the mip chain sizes
  above, with no bytes left over.
- **A reference and a port that must agree.** The Python reference and the C# code are separate
  implementations. A cross-check compares SHA-256 hashes of their BC1 output over a fixed test
  pattern. On a real source image, the two converters wrote the same set of files, four levels
  deep, with identical hashes for every file. A quality metric such as PSNR cannot show this kind
  of agreement. Only a hash can.
- **A survey of real files.** The format table in section 1 comes from 205,490 files on disk.
- **Flights in the simulator.** Four things were verified only by installing test tiles and looking
  at them in Aerofly FS 4: a stored colour tile renders, a stored mask works, rows are bottom-up,
  and the mask is thresholded. Each test used an asymmetric pattern or a control tile, so that a
  wrong result would look different from a correct one.

To run the suites, see [tools/ttc/README.md](../tools/ttc/README.md). In short:

```bash
python tools/ttc/validate_ttc.py   # Python reference
tools/ttc/csharp/run.ps1           # C# implementation
python tools/ttc/cross_check.py    # hashes to compare with the C# run
```

The reference needs `numpy` and `Pillow`. The C# suite uses the `csc` compiler that comes with
.NET Framework 4.x on Windows.

## Still unknown

- **The header fields at `0x24` and `0x28`.** Real tiles carry many different values. They look
  like two 32-bit masks, possibly a coarse occupancy grid, but that is a guess. The values in
  section 1 are safe.
- **The compression codec.** LZHAM is inferred, not confirmed. Nothing here decompresses a
  compressed tile, so the tools cannot read the pixels of IPACS tiles. The chunk values `21` and
  `20` are not explained.
- **The exact mask threshold.** It is somewhere in (0, 32]. Values 1 to 31 were not tested.
- **Why a mask added later has no effect.** See section 4.
- **`flip_vertical` in the `.aid`.** Its effect in GeoConvert is not confirmed. The shipping
  converter does not use it, and AeroScenery always writes `false`.
- **The other `.tmc` keys** in GeoConvert, such as `do_heightmaps`. They are not tested.
- **Basis Universal tiles.** They can be identified, but we have not written one. It is not known
  if they are a better output than stored BC1.
- **Aerofly FS 2.** Stored tiles were tested in Aerofly FS 4 only.
