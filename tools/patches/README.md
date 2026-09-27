# Colour patches

Tools to remove colour patches from the stitched imagery before it is converted. A colour patch is
a rectangle of a different acquisition inside the imagery. The case these tools were made for is
Google at zoom 17 over an island in the Pacific: dozens of pink squares of about 300–400 m, off the
tile grid, on top of brown and green grass.

These are developer tools, like the rest of `tools\`. The app does not use them yet.

## What was measured

- A patch is an **additive tint on the same ground**. Measured at full resolution: +18, +7, +32
  (red, green, blue) against the grass beside it, with the same standard deviation. So one colour
  offset per patch is the right model.
- A patch is also about **25% less sharp** and carries a faint "© Google" watermark. Google filled
  those squares with upscaled lower-zoom imagery. The colour can be corrected; the missing detail
  cannot.
- The patch borders are **straight lines**, horizontal and vertical, and sharp at full resolution.
- A lower-zoom mosaic is **not** a usable reference here. Google's zoom 12 of the island has the same
  pink tone as the patches, so matching to it pulls the grass towards pink.

## How it works

1. `patch_editor.py` shows the stitched images at 1/16 in the browser. You click inside each patch.
   A magic wand marks everything connected to the click whose colour is within a tolerance of the
   colour there.
2. The marks become one label per patch. Thin leaks into the terrain are cut off (an opening with a
   7 px square, which keeps rectangles exactly). Holes are filled, also where a dark thing inside
   the patch reaches its edge. Border cells that look like the terrain beside them go back to the
   terrain.
3. Each patch gets one offset: the **median of the colour step across its whole border**, read two
   pixels away from the border. The median ignores the stretches of border along forest or lava.
   Patches that touch each other are solved together. **The terrain is never changed.**
4. `patch_apply.py` puts every straight run of a border on the exact full-resolution column or row
   of the colour step, and writes corrected **copies** of the stitched images. The originals are
   not changed.

## Use

The state file holds the clicks, in degrees. Keep it with your working data, not in the repository.

```powershell
python tools\patches\patch_editor.py "<state.json>" "<Working Folder>\<square>\<source>\<zoom>-stitched" [...]
```

Open http://localhost:8765. Click the patches, check with *Preview* and `V`, and *Save*.

```powershell
python tools\patches\patch_apply.py "<state.json>" --out "<folder for the copies>" "<Working Folder>\<square>\<source>\<zoom>-stitched" [...]
```

Give `patch_apply.py` the same folders as the editor. It writes only the squares that have a patch,
as `<out>\<square>\<source>\<zoom>-stitched`, with a `.tmc` that points to the new folders. Then:

```powershell
AeroSceneryConvert "<out>\<square>\<source>\<zoom>-stitched\<source>_<zoom>_stitch.tmc"
```

Install the tiles as usual. Keep a backup of the installed square outside `addons\scenery`.

Before you trust a new area, convert one original square with `AeroSceneryConvert` and check that
it gives the installed tiles byte for byte. Then any difference is the correction.

## Limits

- It corrects colour only. A patch stays a little blurred.
- Click only on patches. A click on water is ignored. A click on ordinary terrain marks a large
  area of similar colour as one "patch" and levels it against its surroundings, which is not what
  you want.
- Where the wand includes a region of a different tone inside a patch, that region gets the
  patch's offset and stays lighter or darker.
- A thin line can remain on some borders, and a few cells of terrain can get a patch offset. In the
  simulator they are hard to see from about 800 ft above the ground.
- The mosaic is held in memory at 1/16. A few level 11 squares are small; a large area at zoom 17
  needs more memory.
