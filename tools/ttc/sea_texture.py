#!/usr/bin/env python3
"""Is the sea PHOTOGRAPHED, or filled? Where does the imagery stop being worth keeping?

`coast_probe.py` asks whether there is imagery. This asks whether it is any good, and the two
answers are wildly different: over the Chile block coverage reaches a median of 17.8 km offshore,
while the genuinely photographic sea is **1 to 5 km wide**. The margin should be chosen against
this number, not that one.

    python tools/ttc/sea_texture.py "<...>/addons/scenery/<package>/images"
    python tools/ttc/sea_texture.py "<...>/images" --lat -33.35 --sheet patches.png

Why level 12 and not level 9
----------------------------
It has to read at real resolution. A level-9 tile averages 1024 level-14 pixels, so wave texture
is gone before it can be measured - profiling there produces a curve that rises with distance,
which is the block seams and haze being counted as detail. Level 12 is 4 m/px, which holds swell
and the tonal seams both, at 64 tiles per square instead of 1024.

What separates a photograph from a fill
---------------------------------------
Mean absolute horizontal gradient of the green channel. East-west because the tonal blocks and the
fill boundaries all run north-south, so that axis crosses them. Real ocean at 4 m/px never reads
0: swell, colour drift and compression noise see to it. A fill reads **exactly** 0 over a whole
patch - at 5 NM off Algarrobo the standard deviation over 260x260 px is 0.000, adjacent pixels
identical. That is not a photograph. It is the same signature that gave IPACS' own sea away at a
standard deviation of 0.05.

What it found, 2026-08-09
-------------------------
Three layers, not two, and the pilot spotted them on the map before any of this was measured:

  0 to 1-5 km   real photography - waves, and at 2 NM a ship with its wake
  3 to 6 NM     flat fill, gradient 0.00
  from ~8 km    a second image, darker and more violet, textured again - the tone step he saw

The photographic band by latitude: -30.9 about 2 km, -31.9 about 5, -32.6 about 1, -33.35 about 5,
-34.2 about 2, -35.6 about 1.

So a 3 NM cut lands just past the real photography even where it reaches furthest, and just before
the second image's tone step. Cutting tighter would clip real imagery at -31.9 and -33.35; cutting
at 6 NM or more would pull a colour seam into the middle of our own scenery. And what meets
Aerofly's sea is then the flat band rather than a photograph, which turns tone matching into one
constant against another.
"""
import argparse
import math
import os
import re
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ttc
from convert_tmc import lon_of_grid_x, lat_of_grid_y, grid_x, grid_y

LEVEL = 12
TILE = 2048
SQUARE_LEVEL = 9
ROWS = 24                  # averaged, so one ship cannot carry a whole bin
COAST_RUN = 250            # 1 km of mostly-land is what makes a coast at 4 m/px

BINS = [(0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 8), (8, 10),
        (10, 13), (13, 16), (16, 20), (20, 25)]


def square_name(lat, lon):
    step = 1 << (16 - SQUARE_LEVEL)
    return "map_%02d_%04x_%04x" % (SQUARE_LEVEL,
                                   int(grid_x(lon, SQUARE_LEVEL)) * step,
                                   int(grid_y(lat, SQUARE_LEVEL)) * step)


def read_tile(images_dir, lat, lon, tx, ty, cache):
    if (tx, ty) in cache:
        return cache[(tx, ty)]
    step = 1 << (16 - LEVEL)
    path = os.path.join(images_dir, square_name(lat, lon),
                        "map_%02d_%04x_%04x.ttc" % (LEVEL, tx * step, ty * step))
    img = None
    if os.path.exists(path):
        with open(path, "rb") as f:
            hdr, _, codec = ttc.read_ttc(f.read())
        w, h = hdr["width"], hdr["height"]
        img = ttc.decode_dxt1(codec[: (w // 4) * (h // 4) * 8], w, h)[::-1]
    if len(cache) > 40:
        cache.clear()
    cache[(tx, ty)] = img
    return img


def band_at(images_dir, lat, east_lon, tiles_west, cache):
    """A band of rows across the sea at one latitude, plus each column's longitude."""
    ty = int(grid_y(lat, LEVEL))
    jc = int((1.0 - (grid_y(lat, LEVEL) - ty)) * TILE)
    j0 = min(max(jc - ROWS // 2, 0), TILE - ROWS)

    tx_e = int(grid_x(east_lon, LEVEL))
    cols = []
    for tx in range(tx_e - tiles_west, tx_e + 1):
        lon_mid = 0.5 * (lon_of_grid_x(tx, LEVEL) + lon_of_grid_x(tx + 1, LEVEL))
        img = read_tile(images_dir, lat_of_grid_y(ty + 0.5, LEVEL), lon_mid, tx, ty, cache)
        cols.append(np.zeros((ROWS, TILE, 3), np.uint8) if img is None else img[j0:j0 + ROWS])

    band = np.concatenate(cols, axis=1)
    lon0 = lon_of_grid_x(tx_e - tiles_west, LEVEL)
    lon1 = lon_of_grid_x(tx_e + 1, LEVEL)
    lons = lon0 + (np.arange(band.shape[1]) + 0.5) * (lon1 - lon0) / band.shape[1]
    return band, lons, ty, j0


def coast_column(band):
    r = band[:, :, 0].astype(np.int16)
    b = band[:, :, 2].astype(np.int16)
    black = np.all(band == 0, axis=2)
    land = ((b - r) <= 0) & ~black
    frac = land.mean(axis=0)
    cs = np.concatenate(([0], np.cumsum(frac)))
    run = (cs[COAST_RUN:] - cs[:-COAST_RUN]) / float(COAST_RUN)
    hit = np.nonzero(run >= 0.75)[0]
    return (int(hit[0]) if len(hit) else None), black


def profile(images_dir, lats, east_lon, tiles_west):
    cache = {}
    print("mean |dG/dx| at %d m/px by km offshore. 0.0 is a fill, not a photograph."
          % round(111320 * math.cos(math.radians(lats[0])) *
                  (lon_of_grid_x(1, LEVEL) - lon_of_grid_x(0, LEVEL)) / TILE))
    print("   lat   " + " ".join("%5d-%-2d" % b for b in BINS))

    for lat in lats:
        band, lons, _, _ = band_at(images_dir, lat, east_lon, tiles_west, cache)
        coast, black = coast_column(band)
        if coast is None:
            print("%7.2f  no coast in the built data" % lat)
            continue

        g = band[:, :, 1].astype(np.int16)
        gx = np.abs(np.diff(g, axis=1))
        lit = ~black[:, 1:]
        km = (lons[coast] - lons[1:]) * 111.320 * math.cos(math.radians(lat))

        cells = []
        for lo, hi in BINS:
            m = (km > lo) & (km <= hi)
            sel = gx[:, m][lit[:, m]] if m.any() else np.array([])
            cells.append("%8.2f" % sel.mean() if sel.size > 200 else "       -")
        print("%7.2f " % lat + " ".join(cells))


def sheet(images_dir, lat, east_lon, tiles_west, path, patch=260):
    """1:1 crops at a handful of distances, because 'does it look like water' is a visual question."""
    from PIL import Image, ImageDraw

    cache = {}
    band, lons, ty, j0 = band_at(images_dir, lat, east_lon, tiles_west, cache)
    coast, _ = coast_column(band)
    if coast is None:
        sys.exit("no coast found at lat %.3f" % lat)

    kmdeg = 111.320 * math.cos(math.radians(lat))
    dists = [0.5, 1, 2, 3, 5, 8, 12, 20]
    crops = []
    for nm in dists:
        target = lons[coast] - nm * 1.852 / kmdeg

        # Index the tile by longitude directly. Going out through the band's column index and back
        # was off by a tile wherever the clamping bit, and open water then read as pure black -
        # a wrong answer indistinguishable from a real finding.
        tx = int(grid_x(target, LEVEL))
        t0, t1 = lon_of_grid_x(tx, LEVEL), lon_of_grid_x(tx + 1, LEVEL)
        xx = min(max(int((target - t0) / (t1 - t0) * TILE) - patch // 2, 0), TILE - patch)
        img = read_tile(images_dir, lat_of_grid_y(ty + 0.5, LEVEL), 0.5 * (t0 + t1),
                        tx, ty, cache)
        yy = min(max(j0 - patch // 2, 0), TILE - patch)
        crop = (np.zeros((patch, patch, 3), np.uint8) if img is None
                else img[yy:yy + patch, xx:xx + patch])
        crops.append((nm, crop, crop.reshape(-1, 3).std(axis=0).mean(),
                      float(np.abs(np.diff(crop[:, :, 1].astype(np.int16), axis=1)).mean())))

    pad, lab = 8, 26
    out = Image.new("RGB", (len(dists) * (patch + pad) + pad, patch + lab + 2 * pad), (24, 24, 28))
    d = ImageDraw.Draw(out)
    for k, (nm, crop, sd, gr) in enumerate(crops):
        x = pad + k * (patch + pad)
        out.paste(Image.fromarray(crop), (x, pad + lab))
        d.text((x + 4, pad + 4), "%.1f NM  sd %.2f  grad %.2f" % (nm, sd, gr), fill=(235,) * 3)
        print("%5.1f NM   std %6.3f   mean |dG/dx| %6.3f" % (nm, sd, gr))
    out.save(path)
    print("wrote " + path)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("images", help="a package's scenery/images folder")
    ap.add_argument("--lat", type=float, action="append",
                    help="latitude to profile; repeatable, defaults to seven across the block")
    ap.add_argument("--east-lon", type=float, default=-71.2,
                    help="a longitude inland of the coast, where the scan starts")
    ap.add_argument("--tiles-west", type=int, default=16,
                    help="how many level-12 tiles of sea to read (each is ~8 km)")
    ap.add_argument("--sheet", help="also write 1:1 crops at several distances to this PNG")
    args = ap.parse_args()

    lats = args.lat or [-30.9, -31.9, -32.6, -33.35, -34.2, -35.6, -36.8]
    profile(args.images, lats, args.east_lon, args.tiles_west)
    if args.sheet:
        print()
        sheet(args.images, lats[0], args.east_lon, args.tiles_west, args.sheet)


if __name__ == "__main__":
    main()
