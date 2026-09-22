# tools/ttc — reference implementation of the Aerofly `.ttc` format

Executable spec for the tile format GeoConvert produces. Its job is to be the thing a faster
production converter gets validated against, so the format knowledge does not live only in one
person's head or one chat log.

Not shipped with the app. Python, standalone, no relation to the C# build.

## Running it

```bash
python tools/ttc/validate_ttc.py
```

Needs `numpy` and `Pillow`. Expected result is **65 passed, 0 failed** (2026-09-22). The headline checks are the
two that rebuild each committed reference file **byte for byte** from its parsed parts — that is what
proves the header construction is right, not merely plausible.

## What is here

| file | what |
|---|---|
| `ttc.py` | reader, writer (compressed and stored), BC1 encoder/decoder, mip chain, tile naming |
| `validate_ttc.py` | the checks against real GeoConvert output |
| `cross_check.py` | hashes of the reference output, to compare against the C# port |
| `make_reference.py` | regenerates the fixtures by driving a real GeoConvert install |
| `edge_mask.py` | fades the outer edge of an installed package — see below, **it does not work** |
| `ocean_fill.py` | builds a low-zoom fallback source, to fill where Bing has no imagery |
| `coast_probe.py` | measures how far offshore the imagery reaches, over a whole installed package |
| `sea_texture.py` | measures how far offshore it is still a photograph, which is much less far |
| `blend_test.py` | builds the flyable package that answers whether the engine blends an L8 mask |
| `water_fix.py` | measures the haze Bing leaves over water, as a field the converter applies |
| `water_bodies.py` | labels connected bodies of water, so each keeps its own tone (no scipy here) |
| `water_convert.ps1` | converts a list of squares with that field, several at once, and installs them |
| `csharp/` | the same validation suite against the C# writer that ships in the app |
| `testdata/` | two `.ttc` files produced by GeoConvert 1.4.5 |

## Keeping the C# port honest

The writer the app actually ships is `AeroScenery/AFS2/Ttc*.cs` plus `Bc1Encoder.cs`, ported from
this reference. Two suites keep the two in step:

```bash
tools/ttc/csharp/run.ps1        # 111 checks, including the ones validate_ttc.py makes
python tools/ttc/cross_check.py # hashes to compare against the tail of that run
```

`run.ps1` compiles the writer sources on their own with `csc` — no solution, no MSBuild. That is
also a standing check that the writer never grows a dependency on WinForms or log4net. It is why
reading a `.tmc` lives in `TmcReader` rather than in `TMCFile`: the latter reaches into
`AeroSceneryManager.Instance.Settings` to write, and the converter must not inherit that.

## Two more scripts, for the parts that need a real source

Both are kept out of the suite because they need a large stitched image, which is not in the repo.

**The reader**, against GDI+:

```bash
tools/ttc/csharp/probe.ps1 "path\to\g_15_stitch_1_1.png"
```

Hashes every pixel through `WicScanlineSource` and again through GDI+, in separate processes so
peak memory means something. The hashes must match; the peaks should not. Measured on a real 777 MB
16,640² source: **154 MB and 5.1 s against 4,740 MB and 26.2 s**, same SHA-256. Worth re-running
after a Windows update, since WIC's incremental behaviour is measured rather than promised.

**The resampler**, against this reference:

```bash
tools/ttc/csharp/sample.ps1 "path\to\g_15_stitch.tmc" 12 1248 1647
python tools/ttc/cross_sample.py "path\to\g_15_stitch.tmc" 12 1248 1647
```

Samples the same tile both ways and prints SHA-256 of the pixels and of the coverage mask. Pick at
least one tile that is only partly covered — that is the only case that exercises the mask, and the
mask is what decides where a tile stops and Aerofly's own imagery shows through. On the Santiago
source, tile (1240, 1647) is fully covered and (1248, 1647) is 12.5% covered; both match.

Comparing rather than eyeballing is the point. Half a pixel of offset, a `floor` that should have
been a round, or latitude treated as linear all produce output that looks completely fine — it is
just in slightly the wrong place.

**The whole converter**, against this reference:

```bash
tools/ttc/csharp/convert.ps1 "path\to\g_15_stitch.tmc" out\csharp        # add a thread count as a 3rd arg
python tools/ttc/convert_tmc.py --tmc "path\to\g_15_stitch.tmc" --out out\python
python tools/ttc/hash_dir.py out\csharp
python tools/ttc/hash_dir.py out\python
```

The thread count is worth varying when the encoder changes: the manifest must come out identical
at 1, 4 and all threads, since block rows write disjoint slices and parallelism must not be able to
alter a single byte.

`hash_dir.py` is implementation-agnostic — it hashes whatever is on disk, so the same script serves
both sides and the two outputs can just be diffed. The manifest lines name each file, so a mismatch
says *which* tile diverged rather than only that something did.

On the Santiago level-15 square, all four levels: **85 files each, identical manifest, 13.8 s
against 78.3 s**. That single check covers sampling, the tone curve, the pyramid, the row flip, the
BC1 encode and the container at once.

The cross-check matters more than it looks. A BC1 encoder can differ from its reference in tie
breaking, rounding or edge padding and still produce good-looking tiles with a healthy PSNR — so a
quality metric cannot tell you the port is faithful, and the Python suite would quietly stop
validating what ships. Comparing SHA-256 of the encoder output over a fixed pattern can. The
pattern is built from pure integer arithmetic in both languages, because a numpy RNG cannot be
reproduced in C#, and its four quadrants cover a gradient, a flat colour off the RGB565 lattice,
incompressible noise and hard block edges. **All six hashes match as of the port.**

## How far offshore the imagery goes — `coast_probe.py`

```bash
python tools/ttc/coast_probe.py "<...>/addons/scenery/<package>/images" --probe-bing 8 --mosaic block.png
```

The measurement that decides where a coastline cut should sit. Cutting a margin offshore only
helps if there is real imagery out to that margin; otherwise the margin is full of the black the
source left behind.

It reads the **built scenery**, not the network: black in our own `.ttc` is exactly where the
source served nothing, it is what the pilot flies over, and it needs no requests and no stitched
sources on disk. One `.ttc` per square — the square's own level-9 tile, 32 m/px — stands for the
1365 files under it, and since a level-9 pixel is only pure black when all 1024 of its level-14
children were, the boundary it reports is conservative by about 32 m.

Water and land are told apart by `B - R`, with the threshold placed from measured distributions
rather than taste: over the Chile block open ocean is `+44` (p1 `+20`) and land `-31` (p99 `0`).
The coast is the first sustained kilometre of land, so a shoal cannot fake one. A row only counts
as *resolved* if black is actually found west of the coast — a row that runs off the edge of the
package while still on imagery bounds the coverage from below and would understate it if averaged
in.

`--probe-bing N` then asks the live server about points just outside the edge, with a control over
land and one 200 km out. That is what tells an absent acquisition footprint from our own download
dropping tiles, which want opposite fixes: near lat -35 the black enters in axis-aligned
rectangles that look exactly like a download hole, and all eight probes came back empty.

A row only counts as resolved if a run of at least 1 km of black is actually found west of the
coast inside built data. Two traps make that rule necessary rather than fussy, and both cost a
measurement before they were noticed: a three-pixel black run is a hole in the coverage, not the
end of it; and the last pixel or two before a package boundary is often black, so a naive walk
stops on that sliver and reports where the *package* ends as if the imagery had run out there. On
the Chile block that mistake moved the median by 0.8 km and doubled the apparent number of thin
stretches.

On the 45-square Chile block, 2026-08-08: coverage west of the coast is **median 17.8 km** (p5 8.3,
min 1.11, max 53.8) over 20,682 resolved rows, **99.69% of all the block's black is at sea**, and
cutting 3 NM offshore leaves 0.354% of the sea band black while removing 99.60% of it.

## Filling where Bing has nothing — `ocean_fill.py`

```bash
python tools/ttc/ocean_fill.py "E:\...\map_09_4d00_6780\b\17-stitched" --zoom 13
tools/ttc/csharp/convert.ps1 "E:\...\b_17_stitch.tmc" out 14 -BlackIsMissing
```

Bing serves nothing above zoom 13 more than a few km offshore, so a square reaching out to sea comes
back mostly pure black and the converter writes that black as scenery. This downloads the same area
at a zoom Bing *does* have, resamples it onto a linear lat/lon grid so the `.aid` is exact, and drops
`z_ocean_<zoom>.png` into the stitched folder. The name is the whole mechanism: `OpenFolder` sorts by
filename and the sampler lets the first source to reach a pixel keep it, so anything sorting after
the real stitches is a fallback. `-BlackIsMissing` is what makes black defer to it.

Measured on a real square: 9.6% black to **0.0%**, 122 s, and the fill is tonally within a level or
two of the primary so the internal joint does not show.

Two things it does not do: it does not save disk (BC1 is fixed rate, a filled tile costs the same as
a black one), and it is not a coastline. Both are covered in that document.

## Is the sea photographed, or filled — `sea_texture.py`

```bash
python tools/ttc/sea_texture.py "<...>/addons/scenery/<package>/images" --sheet patches.png
```

`coast_probe.py` asks whether there is imagery. This asks whether it is any good, and the two
answers are wildly different: coverage reaches a **median of 17.8 km** offshore while the
genuinely photographic sea is **1 to 5 km wide**. The margin should be chosen against this number.

It reads at level 12, 4 m/px, and has to: a level-9 pixel averages 1024 level-14 pixels, so wave
texture is gone before it can be measured, and profiling there produces a curve that *rises* with
distance, because block seams and haze get counted as detail. That was tried first.

Real ocean at 4 m/px never reads a zero gradient — swell, colour drift and compression noise see to
it. A fill reads exactly zero: at 5 NM off Algarrobo the standard deviation over 260×260 px is
**0.000**, adjacent pixels identical. Same signature that gave IPACS' own sea away at 0.05.

`--sheet` writes 1:1 crops at several distances, because *does it look like water* is a visual
question and deserves a visual answer. Three layers show up in them, all of which the pilot had
already spotted on the map: real photography with waves and a ship out to 1–5 km, flat fill from
about 3 to 6 NM, then a second darker and more violet image from about 8 km.

## Blend or threshold — `blend_test.py`

```bash
python tools/ttc/blend_test.py --out "%USERPROFILE%/Documents/Aerofly FS 4/addons/scenery"
```

Builds the package that settles open question 6b of
[the format description](../../docs/ttc-format.md): does the engine *blend* an L8 mask or *threshold*
it? That decides whether photoscenery can be faded out at a coastline or only cut, which is the
whole difference between a soft boundary and a hard line in a different place.

Nine north-south stripes over open ocean, mask descending `255 | 224 192 160 128 96 64 32 | 0`,
colour alternating magenta and yellow so they can be **counted** whatever the mask does to them.
The wide outer stripes are controls — 255 must always show, 0 must never. Stripes solid then
stopping dead means threshold, and says between which two values; every stripe fainter than the
last means blend; nine solid stripes means the mask is ignored.

Counting is the design. A smooth ramp cannot distinguish the two, because a thresholded ramp is
just a hard line somewhere else and there is no scale to measure it against — the same reason the
row-flip needed an asymmetric test pattern rather than a checkerboard.

It writes each colour tile **and its mask together, fresh, into a new package folder**, because
`edge_mask.py` below established that retrofitting a mask onto a finished tile does nothing. It
targets level-9 square `map_09_4c80_6880`, open ocean at lat -30.93 which the Chile package does not
contain, so it adds 26 files and swaps nothing; uninstalling is deleting the folder.

**Flown 2026-08-08. It THRESHOLDS.** Eight stripes rendered at full saturation and the mask-0 one
did not render at all, so the threshold is in `(0, 32]` — any non-zero value is opaque. A boundary
can only be cut, never faded. The cut carries a thin orange sliver exactly one mask texel wide,
which is this script's own max-pool bias showing up in the simulator at the width its comment
predicts — a better check on the geometry than the stripe count.

## Fading the outer edge — `edge_mask.py`, and it DOES NOT WORK

```bash
python tools/ttc/edge_mask.py "<...>/addons/scenery/<package>/images" --out staging --fade-km 12
```

Writes a `_mask.ttc` beside each colour tile near the package's outer boundary, carrying an alpha
ramp instead of the binary coverage a converted square produces. The point is the coastline: photo
sea currently stops dead at a tile edge and Aerofly's own water starts, which reads as a straight
line at sea.

It reads no pixels — only folder names, to find which level-9 squares exist and therefore where
the boundary is. The covered area is not a rectangle (a block that follows a coast has a staircase
for one edge), so distance is measured to the neighbouring *empty* squares and a corner fades in
two directions on its own. Output goes to a staging folder; installing it is a copy, and **deleting
every `_mask.ttc` undoes it exactly**, because no existing file is touched.

**Flown 2026-08-08 and it changed nothing** — see section 6 of
[the format description](../../docs/ttc-format.md) for the result and for the three checks that make it
a trustworthy negative rather than a broken setup. **A mask cannot be retrofitted onto a colour tile
the converter already emitted as complete**; it has to be written alongside it at conversion time.

The script is kept because it is the cheapest way to write a valid mask for an arbitrary tile, and
because the geometry in it — distance to the neighbouring empty squares, per-latitude km scaling —
is what the conversion-time version needs. `--steps N` quantises the ramp into bands, which is what
would have told a blend apart from a threshold had the masks been read at all.

## LZHAM is not needed to write tiles

`build_ttc_stored()` writes a complete `.ttc` with the mip chain stored straight at `0x100` and no
`tmcompress` chunk. **Aerofly FS 4 renders it** — verified in the simulator, see
[the format description](../../docs/ttc-format.md). So the writer has no compressor dependency at all.

The cost is disk: LZHAM's median ratio over the 99,169 DXT1 tiles IPACS ship is 1.279x, and on real
photoscenery 1.139x. Storing costs 14–28% more bytes and removes an LZMA-class compressor from
every tile.

`build_chunk()` still exists and still takes an already-compressed blob, because writing a
byte-exact copy of a GeoConvert tile is what the round-trip tests check. Reading an existing
compressed tile's pixels would need an LZHAM decoder; nothing here does that yet.

## Formats seen in the wild

Surveyed across 205,490 `.ttc` files on a full Aerofly FS 4 install:

| `format` | files | payload |
|---|---:|---|
| `0` | 882 | L8 mask mip chain, always in a chunk |
| `10` | 99,169 | DXT1 mip chain, always in a chunk |
| `0x12345671` | 59,637 | Basis Universal, never in a chunk |
| `0x12345672` | 45,802 | Basis Universal, always in a chunk |

The Basis values are sentinels, not texture types. `ttc.py` names them but does not write them.
Over half of a real install is Basis, so it is a first-class path in the engine, not a curiosity.

## Regenerating the fixtures

```bash
python tools/ttc/make_reference.py --geoconvert "path\to\aerofly_fs_2_geoconvert"
```

Builds a synthetic noise image plus its AID and a one-level TMC, runs GeoConvert, and copies the
`.ttc` output into `testdata/`. Add `--keep-raw` to also keep the 2048×2048 raw tile (9 MB, not
committed; `validate_ttc.py` synthesises an equivalent one when it is absent).

Noise rather than a gradient on purpose: a gradient compresses ~18:1 and would misrepresent the
compression stage entirely.

## Format summary

Full spec, including how each field was verified, is in
[docs/ttc-format.md](../../docs/ttc-format.md).

A `.ttc` is a flat 256-byte header followed by a `tmcompress` chunk. It is **not** the hash-keyed
node tree used by `.ttx`/`.tsb`/`.tff` — those are a different container that happens to share the
file family.

```
0x000  u32 magic 0x0000303A   0x014  u32 width       (2048 colour, 512 mask)
0x004  u32 version 0x100      0x018  u32 height
0x008  u32 level              0x01C  u32 num_mips    (12 colour, 10 mask)
0x00C  u32 size_compressed    0x020  u32 format      (0 = L8, 10 = DXT1)
0x010  u32 size_uncompressed  0x024/0x028  unknown
0x02C..0x0FF zero             0x100  tmcompress chunk
```

No checksum anywhere in a `.ttc`. Mip chain is concatenated raw, no per-level headers.
Filenames are `map_<level:02d>_<hexX>_<hexY>.ttc`, where X and Y are **tile index × 128** in hex.
