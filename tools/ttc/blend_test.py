#!/usr/bin/env python3
"""Does the engine BLEND an L8 mask, or threshold it? Builds the package that answers it.

The mask question in section 4 of docs/ttc-format.md. It decides whether photoscenery can be FADED out
at a coastline or only CUT: if the engine blends, a ramp in the mask buys a soft boundary for
nothing; if it thresholds, a ramp only moves the hard line somewhere else.

Every mask this project has written is binary, because coverage is a bool, so the question has
never come up on its own. It cannot be answered from anyone else's files either - the only ramped
masks shipped are LZHAM-compressed and there is no decoder here.

Why this is not the experiment the handoff describes
----------------------------------------------------
That one used `edge_mask.py` to drop ramped masks beside tiles that were already installed. Open
question 6 then established that **retrofitting a mask onto a finished colour tile does nothing**,
which killed the method before it could answer 6b. So the pair has to be born together, the way
question 3 verified when it flew a partly-covered strip successfully.

This writes both files fresh, into a **new package folder**, exactly as that successful test did.
No existing tile is touched and no square is swapped, so uninstalling is deleting one folder.

The pattern, and why it is shaped like this
-------------------------------------------
Nine north-south stripes over open ocean, mask value descending from east to west, with the colour
alternating magenta and yellow so the stripes can be COUNTED whatever the mask does to them:

    255 | 224 192 160 128  96  64  32 |  0
    <-- the control that proves it loaded      the control that must never show -->

Counting is the whole design. A smooth ramp cannot tell blending from thresholding - a thresholded
ramp is just a hard line in a different place, and without a scale there is nothing to compare it
to. Discrete steps of known value can, and they locate the threshold as a bonus. Same lesson as
the asymmetric test pattern that caught the row flip.

  nothing at all         -> it did not load, or the mask is inverted. Check before concluding.
  stripe 0 only, hard W edge -> THRESHOLD above 224
  stripes 0..k solid, then nothing -> THRESHOLD, between stripe k's value and the next
  all 8 visible, each fainter than the last -> BLEND
  all 9 solid, including the mask-0 one -> the mask is being ignored entirely

Open ocean is deliberate: Aerofly's own imagery there is a flat synthetic colour, so what shows through is uniform and every stripe's tint is a pure function
of its mask value. Over land the underlying texture varies and none of it could be read.

The pattern is 8.8 km wide on purpose. The previous mask flight was inconclusive partly because a
15 km band seen from 2,000 ft foreshortens into 2 degrees at the horizon; 800 m stripes flown
directly over are unmissable.

    python tools/ttc/blend_test.py --out "%USERPROFILE%/Documents/Aerofly FS 4/addons/scenery"
"""
import argparse
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ttc
from convert_tmc import grid_x, grid_y, lat_of_grid_y, lon_of_grid_x

TILE_PX = 2048
MASK_PX = 512

MAGENTA = (255, 0, 255)
YELLOW = (255, 255, 0)

# (width in km, mask value). The two controls are wide so they cannot be missed or miscounted.
STRIPES = [
    (1.6, 255),
    (0.8, 224), (0.8, 192), (0.8, 160), (0.8, 128),
    (0.8, 96), (0.8, 64), (0.8, 32),
    (1.6, 0),
]

# Square (153, 209) at level 9: open ocean off Chile at lat -31, ~25 km out, and NOT part of the
# 45-square chile package - so this adds files rather than swapping any, and FS4's recursive scan
# cannot find two tiles with the same name. Its own imagery there is the flat ocean colour.
SQUARE_X, SQUARE_Y, SQUARE_LEVEL = 153, 209, 9
LEVELS = (9, 10, 11, 12)
HEIGHT_KM = 8.0

KM_PER_DEG_LAT = 110.9


def build_stripes(centre_lon, centre_lat):
    """Stripe boundaries in longitude, east to west, plus each one's colour and mask value."""
    km_per_deg_lon = 111.320 * np.cos(np.radians(centre_lat))
    total_km = sum(w for w, _ in STRIPES)
    east = centre_lon + (total_km / 2.0) / km_per_deg_lon

    out, lon = [], east
    for i, (w_km, value) in enumerate(STRIPES):
        west = lon - w_km / km_per_deg_lon
        out.append(dict(east=lon, west=west, value=value,
                        colour=MAGENTA if i % 2 == 0 else YELLOW))
        lon = west
    return out, east, lon


def paint(level, tx, ty, stripes, lat_s, lat_n):
    """One tile's colour and its per-pixel mask value, north-up. Returns None if untouched."""
    lon_w = lon_of_grid_x(tx, level)
    lon_e = lon_of_grid_x(tx + 1, level)
    lons = lon_w + (np.arange(TILE_PX) + 0.5) * (lon_e - lon_w) / TILE_PX
    lats = np.array([lat_of_grid_y((ty + 1) - (j + 0.5) / TILE_PX, level)
                     for j in range(TILE_PX)])

    in_lat = (lats >= lat_s) & (lats <= lat_n)
    if not in_lat.any():
        return None

    rgb = np.zeros((TILE_PX, TILE_PX, 3), np.uint8)
    value = np.zeros((TILE_PX, TILE_PX), np.uint8)

    touched = False
    for s in stripes:
        cols = (lons < s["east"]) & (lons >= s["west"])
        if not cols.any():
            continue
        touched = True
        block = np.ix_(in_lat, cols)
        rgb[block] = s["colour"]
        value[block] = s["value"]

    if not touched or not value.any():
        return None
    return rgb, value


def write_pair(out_dir, level, tx, ty, rgb, value):
    """The colour tile and its mask, written together - which is the whole point.

    Both flipped: Aerofly stores rows bottom-up, row 0 of the texture is the SOUTH edge.
    """
    rgb = rgb[::-1]
    value = value[::-1]

    chain, mips = ttc.build_mip_chain(rgb, ttc.FORMAT_DXT1)
    data = ttc.build_ttc_stored(level, TILE_PX, TILE_PX, mips, ttc.FORMAT_DXT1, chain)
    name = ttc.tile_name(level, tx, ty)
    with open(os.path.join(out_dir, name), "wb") as f:
        f.write(data)

    # Max-pooled to 512 like the converter's own mask, not averaged: averaging would erode every
    # stripe edge by up to two texels. Within a stripe the value is constant, so the pool is a
    # no-op except on the boundaries, where the brighter stripe wins by one texel (16 m at L12).
    step = TILE_PX // MASK_PX
    m = value.reshape(MASK_PX, step, MASK_PX, step).max(axis=(1, 3))
    mchain, mmips = ttc.build_mip_chain(m, ttc.FORMAT_L8)
    mdata = ttc.build_ttc_stored(level, MASK_PX, MASK_PX, mmips, ttc.FORMAT_L8,
                                 mchain, 0x00FFFFFF, 0x00000000)
    mname = ttc.tile_name(level, tx, ty, mask=True)
    with open(os.path.join(out_dir, mname), "wb") as f:
        f.write(mdata)

    return name, mname, len(data) + len(mdata)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", required=True,
                    help="the sim's addons/scenery folder, or any staging folder")
    ap.add_argument("--package", default="blend_test", help="package folder name")
    args = ap.parse_args()

    centre_lon = 0.5 * (lon_of_grid_x(SQUARE_X, SQUARE_LEVEL)
                        + lon_of_grid_x(SQUARE_X + 1, SQUARE_LEVEL))
    centre_lat = 0.5 * (lat_of_grid_y(SQUARE_Y, SQUARE_LEVEL)
                        + lat_of_grid_y(SQUARE_Y + 1, SQUARE_LEVEL))

    stripes, east, west = build_stripes(centre_lon, centre_lat)
    lat_s = centre_lat - (HEIGHT_KM / 2.0) / KM_PER_DEG_LAT
    lat_n = centre_lat + (HEIGHT_KM / 2.0) / KM_PER_DEG_LAT

    square = ttc.tile_name(SQUARE_LEVEL, SQUARE_X, SQUARE_Y).replace(".ttc", "")
    out_dir = os.path.join(args.out, args.package, "images", square)
    os.makedirs(out_dir, exist_ok=True)

    print("package  %s" % os.path.join(args.out, args.package))
    print("square   %s   lon %.4f..%.4f  lat %.4f..%.4f"
          % (square, lon_of_grid_x(SQUARE_X, SQUARE_LEVEL),
             lon_of_grid_x(SQUARE_X + 1, SQUARE_LEVEL),
             lat_of_grid_y(SQUARE_Y, SQUARE_LEVEL),
             lat_of_grid_y(SQUARE_Y + 1, SQUARE_LEVEL)))
    print("pattern  lon %.4f..%.4f  lat %.4f..%.4f  (%.1f x %.1f km)"
          % (west, east, lat_s, lat_n, sum(w for w, _ in STRIPES), HEIGHT_KM))
    print("centre   %.4f, %.4f\n" % (centre_lat, centre_lon))

    total = 0
    for level in LEVELS:
        x0 = int(np.floor(grid_x(west, level)))
        x1 = int(np.floor(grid_x(east, level)))
        y0 = int(np.floor(grid_y(lat_s, level)))
        y1 = int(np.floor(grid_y(lat_n, level)))
        n = 0
        for ty in range(y0, y1 + 1):
            for tx in range(x0, x1 + 1):
                painted = paint(level, tx, ty, stripes, lat_s, lat_n)
                if painted is None:
                    continue
                name, mname, nbytes = write_pair(out_dir, level, tx, ty, *painted)
                total += nbytes
                n += 1
        print("  level %d: %d tile%s + %d mask%s"
              % (level, n, "" if n == 1 else "s", n, "" if n == 1 else "s"))

    print("\n%.1f MB written to %s" % (total / 1e6, out_dir))
    print("""
Fly to %.4f, %.4f - open ocean, about 25 km west of the coast at lat -31. Cross the pattern
east to west at low level and look straight down. What you see decides it:

  nothing at all                      it did not load, or the mask is inverted - check, do not conclude
  one solid stripe, hard western edge  THRESHOLD above 224
  stripes solid then stopping dead     THRESHOLD, between the last visible value and the next
  every stripe fainter than the last   BLEND - a coastline can be faded
  nine solid stripes, no fading        the mask is being ignored entirely

Stripe values east to west: %s

To uninstall, delete the package folder. Nothing else was touched.""" % (
        centre_lat, centre_lon, ", ".join(str(v) for _, v in STRIPES)))


if __name__ == "__main__":
    main()
