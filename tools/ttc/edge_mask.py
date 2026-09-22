#!/usr/bin/env python3
"""Give an installed photoscenery package a soft outer edge.

Writes a `_mask.ttc` beside colour tiles that sit near the outer boundary of the package. The
mask carries an alpha ramp, so the imagery fades out over a chosen distance instead of stopping
dead at a tile edge - which is what a hard line of photo sea meeting Aerofly's own water looks
like from the air.

Nothing on disk is modified and no colour tile is even read. A mask is a separate file, so
**deleting every `_mask.ttc` restores exactly what was there before**. Output goes to a staging
folder; installing it is a copy the caller makes deliberately.

    python tools/ttc/edge_mask.py "<...>/addons/scenery/<package>/images" --out staging --fade-km 8

WHAT IS NOT KNOWN YET: whether Aerofly FS 4 *blends* an L8 mask or thresholds it. Every mask this
project has written so far has been binary, so the question has never come up - see section 6 of
docs/ttc-format.md. This tool exists to settle it. Generate a ramp, fly it, and look:
a blend gives a gradient, a threshold moves the hard line inward by half the band, being ignored
changes nothing. Three outcomes that cannot be mistaken for each other, which is the whole point
of testing a convention against the real consumer rather than against a reference file.
"""
import argparse
import math
import os
import re
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ttc

# Same constant as AeroScenery/AFS2/AFS2World.cs. Do not re-derive it; the grid is not Mercator.
K = 2.3311223704144

MASK_SIZE = 512
KM_PER_DEGREE = 111.32

# The values AeroScenery's own converter writes on a mask, and which Aerofly has been verified to
# render. Their meaning is still open, so copy them rather than invent one.
MASK_UNK24 = 0x00FFFFFF
MASK_UNK28 = 0x00000000

SQUARE_RE = re.compile(r"^map_09_([0-9a-f]{4})_([0-9a-f]{4})$")
TILE_RE = re.compile(r"^map_(\d{2})_([0-9a-f]{4})_([0-9a-f]{4})\.ttc$")


def lon_of(x, level):
    return 180.0 * (2.0 * (x / 2 ** level) - 1.0)


def lat_of(y, level):
    return math.atan(K * (2.0 * (y / 2 ** level) - 1.0)) * 180.0 / K


def grid_x(lon, level):
    return 2 ** level * (0.5 + 0.5 * (lon / 180.0))


def grid_y(lat, level):
    return 2 ** level * (0.5 + 0.5 * math.tan(K * lat * math.pi / 180.0 / math.pi) / K)


def find_squares(package):
    """The level-9 squares the package covers, from its folder names."""
    out = set()
    for name in os.listdir(package):
        m = SQUARE_RE.match(name)
        if m and os.path.isdir(os.path.join(package, name)):
            out.add((int(m.group(1), 16) // 128, int(m.group(2), 16) // 128))
    return out


def empty_neighbours(squares):
    """The level-9 cells just outside the package, as (x, y) pairs.

    Distance is measured to these rather than to a rasterised outline, so it is exact and there is
    no resolution to tune. One ring is enough because a fade is a few km and a square is 65: a
    point deep enough inside to be nearer some second ring cell is far past fully opaque anyway.

    The covered area is not a rectangle - the Chile block has a staircase for a west edge, since it
    follows the coast - so a corner has to fade in two directions at once and a notch on three.
    Taking the minimum over cells gets all of that for free.
    """
    xs = [x for x, _ in squares]
    ys = [y for _, y in squares]
    out = []
    for x in range(min(xs) - 1, max(xs) + 2):
        for y in range(min(ys) - 1, max(ys) + 2):
            if (x, y) not in squares:
                out.append((x, y))
    return out


def _box_distance(lo_x, hi_x, lo_y, hi_y, ex, ey, km_x, km_y):
    """Nearest approach, in km, between an axis-aligned box and one level-9 cell."""
    dx = max(ex * km_x - hi_x, lo_x - (ex + 1) * km_x, 0.0)
    dy = max(ey * km_y - hi_y, lo_y - (ey + 1) * km_y, 0.0)
    return math.hypot(dx, dy)


def tile_alpha(level, tx, ty, empties, fade_km, steps=0):
    """The 512x512 alpha for one tile, north-up. 255 keeps the photo, 0 hands over to Aerofly.

    Returns None when the whole tile is opaque, which is almost all of them - a package is mostly
    interior. Checking the tile's bounding box against each cell first is what keeps this quick
    enough to run over the whole package rather than a hand-picked corner of it.
    """
    lon_w, lon_e = lon_of(tx, level), lon_of(tx + 1, level)
    lat_s, lat_n = lat_of(ty, level), lat_of(ty + 1, level)
    lat_mid = 0.5 * (lat_s + lat_n)

    # One level-9 grid unit in km, at this tile's own latitude. Longitude converges towards the
    # poles and this block spans eight degrees of latitude, so one constant for the whole package
    # would make the ramp visibly wider at one end than the other. Latitude is not linear in this
    # grid either, hence measuring the y unit rather than assuming it.
    y9 = grid_y(lat_mid, 9)
    km_x = 0.703125 * KM_PER_DEGREE * math.cos(math.radians(lat_mid))
    km_y = (lat_of(y9 + 0.5, 9) - lat_of(y9 - 0.5, 9)) * 110.57

    lo_x, hi_x = grid_x(lon_w, 9) * km_x, grid_x(lon_e, 9) * km_x
    lo_y, hi_y = grid_y(lat_s, 9) * km_y, grid_y(lat_n, 9) * km_y

    near = [c for c in empties
            if _box_distance(lo_x, hi_x, lo_y, hi_y, c[0], c[1], km_x, km_y) < fade_km]
    if not near:
        return None

    lons = lon_w + (np.arange(MASK_SIZE) + 0.5) * (lon_e - lon_w) / MASK_SIZE
    # Row 0 is the north edge here; the flip into Aerofly's bottom-up order happens on the way out.
    lats = np.array([lat_of((ty + 1) - (j + 0.5) / MASK_SIZE, level) for j in range(MASK_SIZE)])

    px = np.array([grid_x(v, 9) for v in lons])[None, :] * km_x
    py = np.array([grid_y(v, 9) for v in lats])[:, None] * km_y

    best = np.full((MASK_SIZE, MASK_SIZE), np.inf)
    for ex, ey in near:
        # Distance to an axis-aligned cell, in km. Zero inside it, which is what makes a point
        # already outside the package come out fully transparent.
        dx = np.maximum(np.maximum(ex * km_x - px, px - (ex + 1) * km_x), 0.0)
        dy = np.maximum(np.maximum(ey * km_y - py, py - (ey + 1) * km_y), 0.0)
        np.minimum(best, np.hypot(dx, dy), out=best)

    alpha = np.clip(best / fade_km, 0.0, 1.0)

    if steps:
        # A staircase is the diagnostic version of the ramp. If Aerofly blends, the bands are
        # visible and countable; if it thresholds, exactly one of the step edges becomes the new
        # hard line and the rest vanish. A smooth ramp cannot tell those two apart, because a
        # threshold applied to it just moves the line and still looks like a line.
        # Rounding rather than flooring or ceiling, so the staircase reaches a true 0 at the outer
        # edge and a true 255 at the inner one. Either of the others leaves a step at one end.
        alpha = np.rint(alpha * steps) / steps

    return np.rint(alpha * 255.0).astype(np.uint8)


def build_mask_ttc(level, alpha_north_up):
    """A stored L8 .ttc from a north-up alpha map."""
    flipped = alpha_north_up[::-1]
    chain, mips = ttc.build_mip_chain(flipped, ttc.FORMAT_L8)
    return ttc.build_ttc_stored(level, MASK_SIZE, MASK_SIZE, mips, ttc.FORMAT_L8,
                                chain, MASK_UNK24, MASK_UNK28)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("package", help="the package's images folder")
    ap.add_argument("--out", required=True, help="staging folder to write into")
    ap.add_argument("--fade-km", type=float, default=8.0,
                    help="how far the fade reaches inward from the outer edge (default 8)")
    ap.add_argument("--square", action="append", default=None,
                    help="limit output to this level-9 square folder; repeatable")
    ap.add_argument("--levels", default=None,
                    help="comma separated levels to write, e.g. 12,13,14 (default: all present)")
    ap.add_argument("--steps", type=int, default=0,
                    help="quantise the ramp into N bands instead of making it smooth, "
                         "which is what tells a blend apart from a threshold")
    args = ap.parse_args()

    squares = find_squares(args.package)
    if not squares:
        sys.exit("no map_09_* folders in " + args.package)

    empties = empty_neighbours(squares)
    print("package covers %d level-9 squares, %d cells border it" % (len(squares), len(empties)))

    wanted_levels = {int(v) for v in args.levels.split(",")} if args.levels else None
    folders = sorted(f for f in os.listdir(args.package) if SQUARE_RE.match(f))
    if args.square:
        folders = [f for f in folders if f in set(args.square)]

    written = 0
    total_bytes = 0
    untouched = 0

    for folder in folders:
        src_dir = os.path.join(args.package, folder)
        out_dir = os.path.join(args.out, folder)
        here = 0

        for name in sorted(os.listdir(src_dir)):
            m = TILE_RE.match(name)
            if not m:
                continue
            level = int(m.group(1))
            if wanted_levels is not None and level not in wanted_levels:
                continue
            step = 1 << (16 - level)
            tx = int(m.group(2), 16) // step
            ty = int(m.group(3), 16) // step

            alpha = tile_alpha(level, tx, ty, empties, args.fade_km, args.steps)
            if alpha is None or alpha.min() == 255:
                untouched += 1
                continue

            data = build_mask_ttc(level, alpha)
            os.makedirs(out_dir, exist_ok=True)
            with open(os.path.join(out_dir, name[:-4] + "_mask.ttc"), "wb") as f:
                f.write(data)
            written += 1
            here += 1
            total_bytes += len(data)

        if here:
            print("  %-24s %d masks" % (folder, here))

    print()
    print("%d masks, %.1f MB; %d tiles are far enough inside to need none"
          % (written, total_bytes / 1e6, untouched))
    print("to undo after installing: delete every *_mask.ttc under the package")


if __name__ == "__main__":
    main()
