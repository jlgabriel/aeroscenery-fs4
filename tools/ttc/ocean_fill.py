#!/usr/bin/env python3
"""Build a low-zoom fallback image for one grid square, to fill in where Bing has no imagery.

Bing serves nothing above zoom 13 more than a few km offshore - z14 and up answer `no-tile` - so a
square that reaches out to sea comes back with most of its area pure black, and the converter
writes that black as if it were scenery. This produces a coarse image of the same water from the
zoom Bing *does* have, to sit underneath as a second source.

It drops `z_ocean_<zoom>.png` plus its `.aid` into the square's stitched folder. The name matters:
`SourceImage.OpenFolder` sorts by filename and `TileSampler` lets the first source to reach a pixel
keep it, so anything sorting after the real stitches is a fallback. Run the converter with black
treated as missing and the two composite with no mask and no engine involvement:

    python tools/ttc/ocean_fill.py "<working folder>\\map_09_4d00_6780\\b\\17-stitched"
    tools/ttc/csharp/convert.ps1 "<...>\\b_17_stitch.tmc" out -BlackIsMissing

Measured before this was written: at one coastal point Bing gives (10,41,54) at zoom 15 and
(10,40,52) at zoom 13, so the fallback matches the primary's tone to within a level or two and the
internal joint should not be visible. The real ocean gradient - darker further out - is in the
imagery already.

Rows are resampled onto a LINEAR latitude grid rather than left in Web Mercator, because an `.aid`
can only describe a linear mapping. Over a whole level-9 square the difference is ~50 m; that is
invisible on open water but it is free to get right, and the same file would be wrong if it were
ever used over land.
"""
import argparse
import io
import math
import os
import re
import sys
import urllib.request

import numpy as np
from PIL import Image

K = 2.3311223704144
TILE = 256
TEMPLATE = "https://t.ssl.ak.tiles.virtualearth.net/tiles/a{0}.jpeg?g=15615&n=z&prx=1"

SQUARE_RE = re.compile(r"map_09_([0-9a-f]{4})_([0-9a-f]{4})")


def lon_of(x, level):
    return 180.0 * (2.0 * (x / 2 ** level) - 1.0)


def lat_of(y, level):
    return math.atan(K * (2.0 * (y / 2 ** level) - 1.0)) * 180.0 / K


def merc_y(lat):
    """Web Mercator y in [0,1], which is NOT the Aerofly grid - this is the tile source's own."""
    s = math.sin(math.radians(lat))
    return 0.5 - math.log((1 + s) / (1 - s)) / (4 * math.pi)


def quadkey(tx, ty, z):
    out = []
    for i in range(z, 0, -1):
        d, m = 0, 1 << (i - 1)
        if tx & m:
            d += 1
        if ty & m:
            d += 2
        out.append(str(d))
    return "".join(out)


def fetch(tx, ty, z, cache):
    path = os.path.join(cache, "%d_%d_%d.jpg" % (z, tx, ty))
    if os.path.exists(path):
        with open(path, "rb") as f:
            data = f.read()
    else:
        req = urllib.request.Request(TEMPLATE.format(quadkey(tx, ty, z)),
                                     headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=30) as r:
            data = r.read()
        with open(path, "wb") as f:
            f.write(data)
    if not data:
        return None
    return np.asarray(Image.open(io.BytesIO(data)).convert("RGB"), dtype=np.uint8)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("stitched", help="the square's <zoom>-stitched folder")
    ap.add_argument("--zoom", type=int, default=13,
                    help="source zoom (default 13, the deepest Bing has offshore)")
    ap.add_argument("--margin", type=float, default=0.01,
                    help="degrees of overhang beyond the square, so edges are covered (default 0.01)")
    args = ap.parse_args()

    m = SQUARE_RE.search(os.path.abspath(args.stitched).replace("\\", "/"))
    if not m:
        sys.exit("could not find map_09_XXXX_YYYY in the path: " + args.stitched)
    sx, sy = int(m.group(1), 16) // 128, int(m.group(2), 16) // 128

    west, east = lon_of(sx, 9) - args.margin, lon_of(sx + 1, 9) + args.margin
    south, north = lat_of(sy, 9) - args.margin, lat_of(sy + 1, 9) + args.margin
    print("square (%d, %d): lon %.5f..%.5f  lat %.5f..%.5f" % (sx, sy, west, east, south, north))

    z = args.zoom
    n = 2 ** z
    tx0 = int((west + 180.0) / 360.0 * n)
    tx1 = int((east + 180.0) / 360.0 * n)
    ty0 = int(merc_y(north) * n)
    ty1 = int(merc_y(south) * n)
    print("zoom %d: tiles x %d..%d, y %d..%d  (%d tiles)"
          % (z, tx0, tx1, ty0, ty1, (tx1 - tx0 + 1) * (ty1 - ty0 + 1)))

    cache = os.path.join(args.stitched, "_oceancache")
    os.makedirs(cache, exist_ok=True)

    mosaic = np.zeros(((ty1 - ty0 + 1) * TILE, (tx1 - tx0 + 1) * TILE, 3), dtype=np.uint8)
    missing = 0
    for ty in range(ty0, ty1 + 1):
        for tx in range(tx0, tx1 + 1):
            img = fetch(tx, ty, z, cache)
            if img is None:
                missing += 1
                continue
            r, c = (ty - ty0) * TILE, (tx - tx0) * TILE
            mosaic[r:r + TILE, c:c + TILE] = img
    print("mosaic %dx%d, %d tiles had no imagery" % (mosaic.shape[1], mosaic.shape[0], missing))

    # Output grid: linear in lon and lat, so the .aid describes it exactly.
    out_w = mosaic.shape[1]
    out_h = mosaic.shape[0]

    lons = west + (np.arange(out_w) + 0.5) * (east - west) / out_w
    lats = north - (np.arange(out_h) + 0.5) * (north - south) / out_h

    src_x = (lons + 180.0) / 360.0 * n * TILE - tx0 * TILE
    src_y = np.array([merc_y(v) for v in lats]) * n * TILE - ty0 * TILE

    ix = np.clip(np.round(src_x - 0.5).astype(np.int64), 0, mosaic.shape[1] - 1)
    iy = np.clip(np.round(src_y - 0.5).astype(np.int64), 0, mosaic.shape[0] - 1)
    out = mosaic[np.ix_(iy, ix)]

    name = "z_ocean_%d" % z
    png = os.path.join(args.stitched, name + ".png")
    Image.fromarray(out).save(png)

    # Byte-for-byte the shape AIDFile.ToString writes, copied from a real one rather than invented:
    # top left corner plus a signed step per pixel, y negative because rows run south.
    aid = os.path.join(args.stitched, name + ".aid")
    with open(aid, "w") as f:
        f.write("<[file][][]\n")
        f.write("\t<[tm_aerial_image_definition][][]\n")
        f.write("\t\t<[string8][image][%s.png]>\n" % (name + ""))
        f.write("\t\t<[string8][mask][]>\n")
        f.write("\t\t<[vector2_float64][steps_per_pixel][%.15e %.15e]>\n"
                % ((east - west) / out_w, -(north - south) / out_h))
        f.write("\t\t<[vector2_float64][top_left][%.15g %.15g]>\n" % (west, north))
        f.write("\t\t<[string8][coordinate_system][lonlat]>\n")
        f.write("\t\t<[bool][flip_vertical][false]>\n")
        f.write("\t>\n")
        f.write(">\n")

    px_m = (east - west) / out_w * 111320.0 * math.cos(math.radians(0.5 * (north + south)))
    print("wrote %s  (%dx%d, %.1f MB, %.1f m/px)"
          % (png, out_w, out_h, os.path.getsize(png) / 1e6, px_m))
    print("wrote %s" % aid)
    print("the cache in %s can be deleted" % cache)


if __name__ == "__main__":
    main()
