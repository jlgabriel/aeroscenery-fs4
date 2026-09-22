#!/usr/bin/env python3
"""Measure a correction for the haze and the cloud Bing carries over water.

Bing builds its high-zoom imagery out of separate acquisitions. Over water the joins between them
are very visible: one pass carries a layer of thin haze and the next does not, so a lake gets a
straight-edged block that is tens of RGB units brighter than the water beside it. Measured on the
lake Llanquihue, square `map_09_4c00_5f80`: clean water is (49, 52, 83) and the block beside it is
(119, 111, 145). That is +65 on all three channels with the same standard deviation, which is a
haze layer on top of the same water rather than different water.

Three findings this is built on, all measured 2026-08-24:

- **Bing's zoom 12 mosaic is a different one, and it is clean.** No blocks, no cloud, no white
  patch, over both the lake and the sea at Puerto Montt. Zoom 13, 14 and 17 all share the defect,
  so no amount of re-downloading at the working zoom helps. There is a clean picture of the same
  water to compare against, and it needs no API.
- **The defect is on the water only.** On land the same block edge is not visible, so the fix must
  never touch land - a land pixel would be matched against a mosaic of a different season.
- **A .ttc stores its rows south to north.** Decoding one to audit it needs a vertical flip, or
  every latitude comes out mirrored.

What it measures, in the imagery's own tone rather than in the reference:

1. The zoom 12 mosaic says WHERE the water is. It is only used for that, plus for the few pixels
   under thick cloud. The tone is never taken from it, because it is 29 m/px and a different date.
2. The water is split into connected BODIES - see water_bodies.py - and each body is levelled onto
   the tone of its own cleanest part: the 15th percentile of its local means, over windows of
   about 1.8 km.
3. Whatever a window sits above its body's target is subtracted. Never added: the correction can
   only remove a haze layer, never invent one.
4. Where the correction needed is very large the cloud is thick and there is no signal left
   underneath. Only there is the zoom 12 reference put in, shifted onto the target tone.

**Every square a body of water touches has to see that body whole.** Step 2 is why. Measured one
square at a time, two squares that share a lake pick different targets from their own water and
leave a step on a straight line where they meet: `4c00_5f80` picked (22.7, 29.5, 49.7) for
Llanquihue and `4c00_5f00` picked (10.0, 33.5, 39.0), because the south square's water is mostly
the sea at Reloncaví. That is a ~18 RGB step across the middle of the lake, and the pilot saw it.

Bodies chain, though, so "the squares this lake touches" grows until it is the whole package - and
the whole package will not fit on one grid at the field's resolution. So the job is done in two
passes, which need different things:

    python tools/ttc/water_fix.py --survey targets.awft "<working folder>\\map_09_*"
    python tools/ttc/water_fix.py --targets targets.awft "<working folder>\\map_09_*"

**The SURVEY fixes the tone of every body, and it must see the whole package at once.** It tolerates
a coarse grid, because a body's target is a percentile over thousands of texels: 256 texels a
square is ~230 m/px and sixteen times less memory than the field's grid. What it writes is one
target tone per texel, grown a little way inland so a shore texel at the fine grid still finds its
own body.

**The FIELD needs the resolution, and it is purely local.** It is generated a square at a time,
with a halo of its neighbours' imagery around it so the 1.8 km windows do not see a square border
as the edge of the world. The targets are read, not measured, so two squares that share a lake
cannot disagree.

Run with neither flag and it does both at once over the squares given, which is what the southern
lakes were built with and what the small self-contained group of a lake or two still wants.

Measured on `map_09_4c00_5f80` at level 9: the step across the block falls from (70, 59, 62) to
(6.1, 6.7, 4.1), a 91% reduction, while the standard deviation of the water goes 4.5 -> 6.7. The
detail is kept - what goes is the layer on top of it.

What comes out is a small field per square, not a corrected image. All four steps fold into one
multiply and one add per channel:

    corrected = source * scale + offset

so the converter carries a few MB and applies it while it samples, and the 9 GB of stitched PNGs
are never rewritten. Where there is no water the field is exactly scale 1, offset 0, which is what
lets the sampler skip a tile over land for the price of one scan - and what lets this tell you
which squares need converting again at all.

    tools/ttc/csharp/convert.ps1 "<...>\\b_17_stitch.tmc" out -WaterFix "<...>\\water_fix.awfx"

Empty each square's `17-geoconvert-ttc` before converting it again. The correction is measured from
the squares' OWN level-9 tiles, so once a square has been converted with a field, measuring it
again would start from the already-corrected picture. Keep the uncorrected tiles, or keep the field.
"""
import argparse
import concurrent.futures
import math
import os
import re
import struct
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ttc
import water_bodies
from ocean_fill import SQUARE_RE, fetch, lat_of, lon_of, merc_y
from tone_curve import TONE_CURVE

Image.MAX_IMAGE_PIXELS = None

K = 2.3311223704144
MAGIC = 0x58465741          # "AWFX", one square's field
TARGET_MAGIC = 0x54465741   # "AWFT", the survey's targets
VERSION = 1
GRID = 1024                 # texels per side of one square's field, ~57 m/px
SURVEY_GRID = 256           # texels per side of one square in the survey, ~230 m/px
HALO = 96                   # field texels of a neighbour's imagery around each square, ~5.5 km
TARGET_GROW = 16            # survey texels the targets are grown inland, ~3.7 km
WINDOW_KM = 1.8             # the scale the water tone is levelled at
FEATHER_KM = 0.9            # how far the correction fades out at the shore
OPEN_TEXELS = 2             # narrow channels opened before bodies are labelled
MIN_BODY_KM2 = 1.5          # below this a body takes the group's target, not its own
WATER_BR = 15.0             # blue minus red a body's own tone must reach to be water at all
NO_FIX = 1000.0             # a target no tone can exceed, so a rejected body is left alone
CLOUD_LO = 60.0             # haze above this starts to be cloud rather than haze
CLOUD_HI = 100.0            # and above this it is wholly replaced by the reference
DEAD = 0.004                # below this a weight is taken as zero, so the field stays exactly 1/0


def inverse_tone_curve():
    """GeoConvert brightens the source on the way into a tile. To measure haze in the units the
    stitched PNG uses, a level-9 tile has to be taken back through that curve first.

    The curve saturates at 247 for inputs of 240 and up, so the top is not invertible. It does not
    have to be: water sits low, and land is never corrected."""
    fwd = np.array(TONE_CURVE, dtype=np.float64)
    return np.interp(np.arange(256), fwd, np.arange(256)).astype(np.float32)


def afs_row_of_lat(lat, gy, size):
    """Row in a level-9 tile for a latitude. The AFS2 grid is not linear in latitude."""
    g = 512.0 * (0.5 + 0.5 * math.tan(K * lat / 180.0) / K)
    return (gy + 1 - g) * size


class Square(object):
    def __init__(self, path, source, zoom):
        p = os.path.abspath(path)
        m = SQUARE_RE.search(p.replace("\\", "/"))
        if not m:
            sys.exit("no map_09_XXXX_YYYY in the path: " + p)
        self.path = p
        self.name = m.group(0)
        self.gx = int(m.group(1), 16) // 128
        self.gy = int(m.group(2), 16) // 128
        self.west, self.east = lon_of(self.gx, 9), lon_of(self.gx + 1, 9)
        self.south, self.north = lat_of(self.gy, 9), lat_of(self.gy + 1, 9)
        self.pixels = None
        self.stitched = os.path.join(p, source, "%d-stitched" % zoom)
        self.tile = os.path.join(p, source, "%d-geoconvert-ttc" % zoom, self.name + ".ttc")
        if not os.path.isdir(self.stitched):
            sys.exit("not there: " + self.stitched)
        if not os.path.exists(self.tile):
            sys.exit("the square has no converted level-9 tile, so its own tone cannot be "
                     "measured:\n  " + self.tile + "\nConvert the square once first.")

    def level9(self):
        """The square's own level-9 tile, decoded and turned the right way up - a .ttc stores its
        rows south to north.

        Kept once decoded. The field pass walks the squares one at a time but reads each square's
        neighbours as well, so every tile is asked for about nine times; the whole package is 81
        of them, a gigabyte, which is cheaper than decoding them again."""
        if self.pixels is None:
            with open(self.tile, "rb") as f:
                hdr, _, codec = ttc.read_ttc(f.read())
            self.pixels = ttc.decode_dxt1(codec, hdr["width"], hdr["height"])[::-1]
        return self.pixels


# --------------------------------------------------------------------------
# the two pictures, on one grid
# --------------------------------------------------------------------------

def assemble_own(squares, lons, lats, inv):
    """Every square's level-9 tile, taken back through the tone curve, laid onto the grid.

    Also returns where there is any imagery at all. Two ways a texel can have none: no square
    covers it - a group need not be a filled rectangle - or the square covers it but nothing was
    ever stitched there, which is most of the western column, where the download stops at the edge
    of what Bing has offshore. That second one is written as EXACTLY black, and it has to be taken
    out or the sea is levelled onto it: measured on map_09_4b80_6380, 86.1% of the square is black
    and the darkest real water in the package still reaches 5, so the test is sharp rather than a
    threshold to be tuned."""
    own = np.zeros((len(lats), len(lons), 3), np.float32)
    have = np.zeros((len(lats), len(lons)), bool)
    for s in squares:
        cs = np.flatnonzero((lons >= s.west) & (lons < s.east))
        rs = np.flatnonzero((lats <= s.north) & (lats > s.south))
        if cs.size == 0 or rs.size == 0:
            continue
        img = s.level9()
        h, w = img.shape[:2]
        col = np.clip(((lons[cs] - s.west) / (s.east - s.west) * w).astype(int), 0, w - 1)
        row = np.clip([int(afs_row_of_lat(v, s.gy, h)) for v in lats[rs]], 0, h - 1)
        cut = img[np.ix_(row, col)]
        own[np.ix_(rs, cs)] = inv[cut]
        have[np.ix_(rs, cs)] = cut.max(2) > 0
    return own, have


def load_reference(lons, lats, zoom, cache, threads=8, quiet=False):
    """Bing at the clean zoom, sampled straight onto the grid.

    Tile by tile rather than onto one canvas: over the whole package the canvas would be 12288 x
    48640 pixels, 1.8 GB, to be read once at one texel in sixty-four. Returns the picture and where
    there was a tile at all - a missing tile is black, and black would otherwise read as water."""
    n = 2 ** zoom
    px = (lons + 180.0) / 360.0 * n * 256.0
    py = np.array([merc_y(v) for v in lats]) * n * 256.0
    tx, ty = (px // 256).astype(int), (py // 256).astype(int)
    ix = np.clip((px - tx * 256).astype(int), 0, 255)
    iy = np.clip((py - ty * 256).astype(int), 0, 255)

    cols = {a: np.flatnonzero(tx == a) for a in np.unique(tx)}
    rows = {b: np.flatnonzero(ty == b) for b in np.unique(ty)}
    jobs = [(a, b) for b in rows for a in cols]
    if not quiet:
        print("  reference z%d: %d tiles" % (zoom, len(jobs)))

    out = np.zeros((len(lats), len(lons), 3), np.float32)
    got = np.zeros((len(lats), len(lons)), bool)
    missing = 0
    # In chunks, so that the whole package's tiles are never all decoded at once: 9000 of them is
    # 1.8 GB of pixels to read one texel in sixty-four from.
    chunk = max(threads * 8, 64)
    with concurrent.futures.ThreadPoolExecutor(threads) as pool:
        for i in range(0, len(jobs), chunk):
            part = jobs[i:i + chunk]
            for (a, b), t in zip(part, pool.map(lambda j: fetch(j[0], j[1], zoom, cache), part)):
                if t is None:
                    missing += 1
                    continue
                cs, rs = cols[a], rows[b]
                if cs.size == 0 or rs.size == 0:
                    continue
                out[np.ix_(rs, cs)] = t[iy[rs][:, None], ix[cs][None, :]]
                got[np.ix_(rs, cs)] = True
    if missing and not quiet:
        print("    %d tiles with no image" % missing)
    return out, got


# --------------------------------------------------------------------------
# the correction
# --------------------------------------------------------------------------

def box(a, k):
    """Separable box blur with an odd window, edge-padded."""
    p = k // 2
    f = np.pad(a.astype(np.float32), ((p, p), (p, p)) + ((0, 0),) * (a.ndim - 2), mode="edge")
    c = np.cumsum(f, 0)
    c = np.concatenate([np.zeros((1,) + c.shape[1:], np.float32), c], 0)
    f = (c[k:] - c[:-k]) / k
    c = np.cumsum(f, 1)
    c = np.concatenate([np.zeros((c.shape[0], 1) + c.shape[2:], np.float32), c], 1)
    return (c[:, k:] - c[:, :-k]) / k


def odd(n):
    n = max(3, int(n))
    return n if n % 2 else n + 1


def water_mask(ref, have):
    """Where the clean mosaic says there is water, and some square covers it.

    Dark and not green is most of it. Blue at least as strong as red takes out the worst of the
    mountain shadow, which is dark and slightly GREEN - the darkest tenth of `map_09_4d80_6400`
    reads (12, 16, 7). It is a soft test on purpose: real water only loses a few tenths of a
    percent by it, and what actually throws shadow out is the per-body test in body_targets."""
    return ((ref.max(2) < 120) & (ref[:, :, 2] >= ref[:, :, 1] - 10)
            & (ref[:, :, 2] >= ref[:, :, 0]) & have)


def local_means(own, wet, km_per_px):
    """The tone of the water over windows of about WINDOW_KM, water texels only.

    Returns the means and the water coverage of each window, which the reference needs as well."""
    water = wet.astype(np.float32)[:, :, None]
    kw = odd(WINDOW_KM / km_per_px)
    m = box(water, kw)
    return box(own * water, kw) / np.maximum(m, 1e-6), m


def row_area(west, east, nx, lats, lat_km):
    """Square kilometres of one texel, row by row.

    A texel is not the same size everywhere: the grid is uniform in degrees, so a degree of
    longitude is 111 km at the equator and 98 km at lat -28. Over the whole package that is a 12%
    spread from the north edge to the south, and the areas are what says whether a body has been
    seen whole - Llanquihue 852 against a real 860 - so they cannot be a square of one number."""
    return lat_km * (east - west) / nx * 111.32 * np.cos(np.radians(lats))


def body_targets(local, wet, cell, quiet=False):
    """One target tone per connected body of water, so a lake that crosses a square border keeps
    one tone, and a lake and the sea keep different ones.

    Returns the labels and a table of targets indexed by label. Entry 0 is the overall target,
    which is what a body too small to measure gets - and what a texel far from any water gets,
    where nothing is corrected anyway."""
    labels, n = water_bodies.bodies(wet, open_by=OPEN_TEXELS)
    table = np.zeros((n + 1, 3), np.float32)

    # Sort the water texels by body once, rather than testing `labels == i` for every body in
    # turn. Over the whole package that test would be twenty thousand passes over thirty-eight
    # million texels; sorting is one pass and the percentile then reads a contiguous slice.
    flat = labels.ravel()
    idx = np.flatnonzero(flat)
    order = idx[np.argsort(flat[idx], kind="stable")]
    edge = np.searchsorted(flat[order], np.arange(1, n + 2))
    vals = local.reshape(-1, 3)[order]
    cum = np.concatenate([[0.0], np.cumsum(cell[order // labels.shape[1]])])
    area = np.zeros(n + 1, np.float64)

    own_target = np.zeros((n + 1, 3), np.float32)
    keep = np.zeros(n + 1, bool)
    for i in range(1, n + 1):
        a, b = edge[i - 1], edge[i]
        area[i] = cum[b] - cum[a]
        own_target[i] = np.percentile(vals[a:b], 15, axis=0)
        keep[i] = own_target[i][2] - own_target[i][0] >= WATER_BR

    # The overall target is what a body too small to measure gets, so it must be measured over
    # the bodies that ARE water. Taken over everything the mask calls water it would carry the
    # mountain shadow into every small lake in the package.
    accepted = keep[labels]
    overall = (np.percentile(local[accepted], 15, axis=0) if accepted.any()
               else np.zeros(3, np.float32))
    table[...] = overall

    big = []
    thrown, thrown_km2 = 0, 0.0
    for i in range(1, n + 1):
        t = own_target[i]
        # A body counts as water only if its OWN tone is blue. The colour test on the reference
        # calls mountain shadow water, and shadow is the one thing that must never be corrected:
        # it is dark, so the haze it seems to carry is large, and taking that away turns a whole
        # valley blue-black. Measured over `map_09_4d80_6400` and `map_09_4e00_6680`, no false
        # body reaches a blue minus red of 11; over `map_09_4c80_5f80` no real one falls below 27.
        # This is also what throws out the bodies of snow, which read (104, 107, 105).
        # The test is on EVERY body, whatever its size: a body too small to measure a target from
        # would otherwise take the group's, which is water by construction.
        if t[2] - t[0] < WATER_BR:
            table[i] = NO_FIX
            thrown += 1
            thrown_km2 += area[i]
            continue
        if area[i] < MIN_BODY_KM2:
            continue                                  # too small to measure: keep the overall
        table[i] = t
        big.append((area[i], i))
    if not quiet:
        print("  %d bodies, overall target RGB %.1f %.1f %.1f"
              % (n, overall[0], overall[1], overall[2]))
        print("  %d thrown out as not water, %.0f km2" % (thrown, thrown_km2))
        for km2, i in sorted(big, reverse=True):
            print("    body %-6d %8.1f km2   target RGB %5.1f %5.1f %5.1f"
                  % (i, km2, table[i][0], table[i][1], table[i][2]))
    return labels, table, area


def correction(own, ref, wet, local, m, target, km_per_px, quiet=False):
    """scale and offset over the grid. `own`, `ref` and `target` are in stitched-PNG units.

    The four steps of the correction fold into one multiply and one add:

        corrected = (own - haze * soft) * (1 - heavy) + refc * heavy
                  = own * (1 - heavy) + (refc * heavy - haze * soft * (1 - heavy))

    so scale is (1 - heavy) and offset is the rest. Over land both are exactly 1 and 0."""
    water = wet.astype(np.float32)[:, :, None]
    kw = odd(WINDOW_KM / km_per_px)
    kf = odd(FEATHER_KM / km_per_px)

    haze = np.clip(local - target, 0.0, None)
    soft = box(box(water, kf), kf)
    heavy = np.clip((haze.mean(2, keepdims=True) - CLOUD_LO) /
                    (CLOUD_HI - CLOUD_LO), 0.0, 1.0) * soft

    # A weight that is nearly zero is made exactly zero. Otherwise a land texel carries a
    # millionth of a correction, the field is nowhere exactly (1, 0), and the sampler's
    # skip-this-tile test never fires.
    soft[soft < DEAD] = 0.0
    heavy[heavy < DEAD] = 0.0

    ref_local = box(ref * water, kw) / np.maximum(m, 1e-6)
    refc = ref + (target - ref_local)

    scale = (1.0 - heavy)[:, :, 0]
    offset = refc * heavy - haze * soft * (1.0 - heavy)
    offset[np.abs(offset) < 0.25] = 0.0
    scale[np.abs(scale - 1.0) < 1e-4] = 1.0

    if not quiet:
        untouched = (scale > 0.9999) & (np.abs(offset).max(2) < 1e-6)
        print("  haze: median %.1f, max %.1f over the water"
              % (float(np.median(haze[wet].mean(1))), float(haze[wet].mean(1).max())))
        print("  thick cloud (replaced by the reference): %.2f%%"
              % (100.0 * (heavy > 0.5).mean()))
        print("  the field corrects %.1f%% of the grid, the rest is untouched"
              % (100.0 * (~untouched).mean()))
    return scale.astype(np.float32), offset.astype(np.float32)


# --------------------------------------------------------------------------
# the survey: one target tone per texel, over the whole package
# --------------------------------------------------------------------------

def write_targets(path, west, east, north, south, target):
    ny, nx = target.shape[:2]
    with open(path, "wb") as f:
        f.write(struct.pack("<IIII", TARGET_MAGIC, VERSION, nx, ny))
        f.write(struct.pack("<dddd", west, east, north, south))
        f.write(np.ascontiguousarray(target, "<f4").tobytes())
    print("  %s  %d x %d texels, %.1f MB"
          % (path, nx, ny, os.path.getsize(path) / 2.0 ** 20))


class Targets(object):
    """The tones the survey fixed, read back for the field pass."""

    def __init__(self, path):
        with open(path, "rb") as f:
            magic, ver, nx, ny = struct.unpack("<IIII", f.read(16))
            if magic != TARGET_MAGIC or ver != VERSION:
                sys.exit("not a water_fix survey: " + path)
            self.west, self.east, self.north, self.south = struct.unpack("<dddd", f.read(32))
            self.grid = np.frombuffer(f.read(ny * nx * 3 * 4), "<f4").reshape(ny, nx, 3)
        print("  targets: %s, %d x %d texels, lon %.4f..%.4f lat %.4f..%.4f"
              % (os.path.basename(path), nx, ny, self.west, self.east, self.south, self.north))

    def covers(self, west, east, north, south):
        return (west >= self.west and east <= self.east
                and north <= self.north and south >= self.south)

    def at(self, lons, lats):
        return lookup(self.grid, self.west, self.east, self.north, self.south, lons, lats)


def write_report(path, squares, labels, table, area, lons, lats, min_km2=2.0):
    """Every body of water over min_km2: its size, its tone, and which squares it lies in.

    That last column is the one worth having. A body spread over several squares makes all of them
    one job - convert one and leave its neighbour, and the two halves of the lake are levelled onto
    tones measured at different times. The survey is the only place this is known, because it is
    the only pass that sees the whole package at once."""
    rows = []
    for s in squares:
        cs = np.flatnonzero((lons >= s.west) & (lons < s.east))
        rs = np.flatnonzero((lats <= s.north) & (lats > s.south))
        if cs.size == 0 or rs.size == 0:
            continue
        for i in np.unique(labels[np.ix_(rs, cs)]):
            # A body the water test threw out is not listed. It is not water, so it has no tone
            # to report and it makes no square part of anyone else's job.
            if i and area[i] >= min_km2 and table[i][0] < NO_FIX:
                rows.append((int(i), s.name))
    where = {}
    for i, name in rows:
        where.setdefault(i, []).append(name)
    with open(path, "w") as f:
        f.write("body,km2,r,g,b,squares\n")
        for i in sorted(where, key=lambda k: -area[k]):
            f.write("%d,%.1f,%.1f,%.1f,%.1f,%s\n"
                    % (i, area[i], table[i][0], table[i][1], table[i][2],
                       " ".join(sorted(where[i]))))
    print("  %s: %d bodies over %.0f km2" % (path, len(where), min_km2))


def survey(squares, args, cache):
    """Fix the tone of every body of water in one pass, on a coarse grid."""
    west = min(s.west for s in squares)
    east = max(s.east for s in squares)
    south = min(s.south for s in squares)
    north = max(s.north for s in squares)
    # The grid is so many texels per square SLOT, counted off the grid squares themselves rather
    # than off one square's size. A level-9 square is not the same height in degrees at every
    # latitude, so taking the step from whichever square happened to be listed first would make
    # the resolution - and with it every measured area - depend on the order of the arguments.
    nx = (max(s.gx for s in squares) - min(s.gx for s in squares) + 1) * args.survey_grid
    ny = (max(s.gy for s in squares) - min(s.gy for s in squares) + 1) * args.survey_grid
    lons = west + (east - west) * (np.arange(nx) + 0.5) / nx
    lats = north + (south - north) * (np.arange(ny) + 0.5) / ny
    km_per_px = (north - south) * 111.32 / ny
    print("%d square(s): lon %.4f..%.4f  lat %.4f..%.4f" % (len(squares), west, east, south, north))
    print("  survey grid: %d x %d texels, %.0f m/texel"
          % (nx, ny, km_per_px * 1000.0))

    ref, got = load_reference(lons, lats, args.ref_zoom, cache, args.threads)
    own, have = assemble_own(squares, lons, lats, inverse_tone_curve())
    wet = water_mask(ref, have & got)
    cell = row_area(west, east, nx, lats, km_per_px)
    print("  built imagery: %.1f%% of the block" % (100.0 * have.mean()))
    print("  water: %.1f%% of the block, %.0f km2"
          % (100.0 * wet.mean(), (wet * cell[:, None]).sum()))
    if wet.sum() < 1000:
        sys.exit("  the block has no water")

    local, _ = local_means(own, wet, km_per_px)
    labels, table, area = body_targets(local, wet, cell)
    if args.report:
        write_report(args.report, squares, labels, table, area, lons, lats)

    # Grow the labels a little way inland. The field grid is four times finer, so a shore texel
    # there can sit on a survey texel the coarse mask called land; without this it would take the
    # overall target instead of its own body's.
    labels = water_bodies.grow(labels, np.ones(labels.shape, bool), args.target_grow)
    write_targets(args.survey, west, east, north, south, table[labels])


# --------------------------------------------------------------------------
# writing one field per square
# --------------------------------------------------------------------------

def lookup(grid, gw, ge, gn, gs, lons, lats):
    """Nearest-texel lookup of a grid whose box is (gw, ge, gn, gs), at the given lons and lats.

    Nearest rather than bilinear on purpose: interpolating would only blur the exactly-1-and-0
    land texels into not-quite, which is what the sampler's skip test reads."""
    ny, nx = grid.shape[:2]
    ci = np.clip(np.round((lons - gw) / (ge - gw) * nx - 0.5).astype(int), 0, nx - 1)
    ri = np.clip(np.round((lats - gn) / (gs - gn) * ny - 0.5).astype(int), 0, ny - 1)
    return grid[np.ix_(ri, ci)]


def resample(grid, gw, ge, gn, gs, west, east, north, south, size):
    """The same lookup, over one square's own field grid."""
    lons = west + (east - west) * (np.arange(size) + 0.5) / size
    lats = north + (south - north) * (np.arange(size) + 0.5) / size
    return lookup(grid, gw, ge, gn, gs, lons, lats)


def is_identity(scale, offset):
    return bool((scale == 1.0).all() and (offset == 0.0).all())


def write_field(path, west, east, north, south, scale, offset):
    with open(path, "wb") as f:
        f.write(struct.pack("<III", MAGIC, VERSION, scale.shape[0]))
        f.write(struct.pack("<dddd", west, east, north, south))
        f.write(np.ascontiguousarray(scale, "<f4").tobytes())
        f.write(np.ascontiguousarray(offset, "<f4").tobytes())
    print("    %s  %.1f MB" % (path, os.path.getsize(path) / 2.0 ** 20))


def write_preview(path, own, scale, offset, width=1600):
    """Before and after, side by side. This is what the water will look like, coarsely and without
    a conversion in between - including across the square borders, which is the thing measuring
    more than one square at a time is meant to fix."""
    fwd = np.array(TONE_CURVE, np.uint8)
    a = fwd[np.clip(own, 0, 255).astype(np.uint8)]
    b = fwd[np.clip(own * scale[:, :, None] + offset, 0, 255).astype(np.uint8)]
    h, w = a.shape[:2]
    out = np.zeros((h, w * 2 + 8, 3), np.uint8)
    out[:, :w] = a
    out[:, w + 8:] = b
    im = Image.fromarray(out)
    if im.width > width:
        im = im.resize((width, int(width * im.height / im.width)), Image.LANCZOS)
    im.save(path)
    print("  %s: before on the left, after on the right" % path)


def field_from_targets(s, others, targets, args, cache):
    """One square's field, with a halo of its neighbours' imagery around it.

    The halo is what keeps a square border out of the picture: the tone is levelled over windows
    of 1.8 km and feathered over another 0.9 km twice, so without it the blur would take the edge
    of the square for the edge of the water and put a step there."""
    h = args.halo
    step_lon = (s.east - s.west) / args.grid
    step_lat = (s.north - s.south) / args.grid
    west, east = s.west - h * step_lon, s.east + h * step_lon
    north, south = s.north + h * step_lat, s.south - h * step_lat
    n = args.grid + 2 * h
    lons = west + (east - west) * (np.arange(n) + 0.5) / n
    lats = north + (south - north) * (np.arange(n) + 0.5) / n
    km_per_px = (north - south) * 111.32 / n

    near = [t for t in others
            if t.east > west and t.west < east and t.north > south and t.south < north]
    ref, got = load_reference(lons, lats, args.ref_zoom, cache, args.threads, quiet=True)
    own, have = assemble_own([s] + near, lons, lats, inverse_tone_curve())
    wet = water_mask(ref, have & got)
    if wet.sum() < 100:
        return None, None, own
    local, m = local_means(own, wet, km_per_px)
    target = targets.at(lons, lats)
    scale, offset = correction(own, ref, wet, local, m, target, km_per_px, quiet=True)
    return scale, offset, own


# --------------------------------------------------------------------------

def expand(paths):
    """Let a shell that does not glob - or a caller who would rather not - pass map_09_*."""
    import glob
    out = []
    for p in paths:
        hits = sorted(glob.glob(p)) if ("*" in p or "?" in p) else [p]
        out.extend([q for q in hits if os.path.isdir(q)])
    if not out:
        sys.exit("no squares matched: " + " ".join(paths))
    return out


def expand_names(spec):
    """Square names from a file with one per line, or comma separated on the command line."""
    if os.path.isfile(spec):
        with open(spec) as f:
            text = f.read()
    else:
        text = spec
    return [w for w in re.split(r"[\s,]+", text.strip()) if w]


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("squares", nargs="+",
                    help="working folders, map_09_XXXX_YYYY. Globs are expanded here too.")
    ap.add_argument("--survey", default="",
                    help="measure the tone of every body over all the squares given, write it "
                         "here, and stop. Give it the whole package.")
    ap.add_argument("--targets", default="",
                    help="a survey's tones. With this, each square gets its field on its own, "
                         "with a halo of its neighbours, and nothing is re-measured.")
    ap.add_argument("--only", default="",
                    help="with --targets: write a field only for these squares, using all the "
                         "others as context for the halo. A file or a comma-separated list.")
    ap.add_argument("--source", default="b", help="source letter (default b, Bing)")
    ap.add_argument("--zoom", type=int, default=17, help="the squares' zoom (default 17)")
    ap.add_argument("--ref-zoom", type=int, default=12,
                    help="the clean Bing mosaic to take the water mask from (default 12)")
    ap.add_argument("--grid", type=int, default=GRID,
                    help="texels per side of each square's field (default %d)" % GRID)
    ap.add_argument("--survey-grid", type=int, default=SURVEY_GRID,
                    help="texels per side of a square in the survey (default %d)" % SURVEY_GRID)
    ap.add_argument("--halo", type=int, default=HALO,
                    help="field texels of a neighbour's imagery around each square (default %d)"
                         % HALO)
    ap.add_argument("--target-grow", type=int, default=TARGET_GROW,
                    help="survey texels the targets are grown inland (default %d)" % TARGET_GROW)
    ap.add_argument("--threads", type=int, default=8, help="reference downloads at once (default 8)")
    ap.add_argument("--cache", default="",
                    help="where the reference tiles are kept (default: beside the first square)")
    ap.add_argument("--preview", action="store_true",
                    help="also write water_fix_preview.png beside each square")
    ap.add_argument("--dry-run", action="store_true", help="measure but write no field")
    ap.add_argument("--plan", default="",
                    help="write the names of the squares whose field is not empty here")
    ap.add_argument("--report", default="",
                    help="with --survey: write every body of water, its tone and the squares it "
                         "lies in, as CSV")
    args = ap.parse_args()

    squares = [Square(p, args.source, args.zoom) for p in expand(args.squares)]
    cache = args.cache or os.path.join(squares[0].stitched, "_refcache")
    os.makedirs(cache, exist_ok=True)

    if args.survey:
        survey(squares, args, cache)
        return

    if args.targets:
        targets = Targets(args.targets)
        # Every square given is context for the halo, whether or not a field is wanted for it.
        # A square whose neighbour is missing has the blur read the square border as the edge of
        # the water, which is the seam the halo exists to prevent - so the way to do a few squares
        # is to name them here, not to leave the rest off the command line.
        pick = set(expand_names(args.only)) if args.only else None
        todo = [s for s in squares if pick is None or s.name in pick]
        if pick and len(todo) != len(pick):
            sys.exit("not among the squares given: "
                     + " ".join(sorted(pick - set(s.name for s in todo))))
        print("  %d square(s) with a field, %d for context" % (len(todo), len(squares) - len(todo)))
        # A lookup outside the survey is clamped to its edge rather than refused, which would
        # quietly give a square somebody else's tone. Only the halo may hang over.
        outside = [s.name for s in todo if not targets.covers(s.west, s.east, s.north, s.south)]
        if outside:
            sys.exit("the survey does not reach these squares: " + " ".join(outside))
        needed = []
        for i, s in enumerate(todo, start=1):
            scale, offset, own = field_from_targets(s, [t for t in squares if t is not s],
                                                    targets, args, cache)
            h = args.halo
            if scale is None:
                print("  %3d/%d %s  no water" % (i, len(todo), s.name))
                continue
            if args.preview:
                write_preview(os.path.join(s.stitched, "water_fix_preview.png"),
                              own, scale, offset)
            sc = scale[h:h + args.grid, h:h + args.grid]
            of = offset[h:h + args.grid, h:h + args.grid]
            if is_identity(sc, of):
                print("  %3d/%d %s  the field corrects nothing" % (i, len(todo), s.name))
                continue
            touched = 100.0 * (1.0 - np.mean((sc == 1.0) & (np.abs(of).max(2) == 0.0)))
            print("  %3d/%d %s  corrects %.1f%%" % (i, len(todo), s.name, touched))
            needed.append(s.name)
            if not args.dry_run:
                write_field(os.path.join(s.stitched, "water_fix.awfx"),
                            s.west, s.east, s.north, s.south, sc, of)
        print("  %d of %d squares need converting again" % (len(needed), len(todo)))
        if args.plan:
            # newline="\n" on purpose: the plan gets compared against other lists with the usual
            # shell tools, and a stray carriage return makes every name miss.
            with open(args.plan, "w", newline="\n") as f:
                f.write("\n".join(needed) + "\n")
            print("  %s" % args.plan)
        return

    # Neither flag: measure and generate over the squares given, as one group.
    west = min(s.west for s in squares)
    east = max(s.east for s in squares)
    south = min(s.south for s in squares)
    north = max(s.north for s in squares)
    print("%d square(s): lon %.4f..%.4f  lat %.4f..%.4f"
          % (len(squares), west, east, south, north))
    first = squares[0]
    nx = max(1, int(round((east - west) / ((first.east - first.west) / args.grid))))
    ny = max(1, int(round((north - south) / ((first.north - first.south) / args.grid))))
    lons = west + (east - west) * (np.arange(nx) + 0.5) / nx
    lats = north + (south - north) * (np.arange(ny) + 0.5) / ny
    print("  group grid: %d x %d texels" % (nx, ny))

    ref, got = load_reference(lons, lats, args.ref_zoom, cache, args.threads)
    own, have = assemble_own(squares, lons, lats, inverse_tone_curve())
    km_per_px = (north - south) * 111.32 / ny
    wet = water_mask(ref, have & got)
    print("  water: %.1f%% of the group" % (100.0 * wet.mean()))
    if wet.sum() < 1000:
        print("  the group has no water, there is nothing to correct")
        return
    local, m = local_means(own, wet, km_per_px)
    labels, table, _ = body_targets(local, wet, row_area(west, east, nx, lats, km_per_px))
    scale, offset = correction(own, ref, wet, local, m, table[labels], km_per_px)

    if args.preview:
        write_preview(os.path.join(first.stitched, "water_fix_preview.png"), own, scale, offset)
    if args.dry_run:
        print("  --dry-run: no field is written")
        return

    print("  fields:")
    for s in squares:
        sc = resample(scale[:, :, None], west, east, north, south,
                      s.west, s.east, s.north, s.south, args.grid)[:, :, 0]
        of = resample(offset, west, east, north, south,
                      s.west, s.east, s.north, s.south, args.grid)
        write_field(os.path.join(s.stitched, "water_fix.awfx"),
                    s.west, s.east, s.north, s.south, sc, of)
    print("  Next: empty %d-geoconvert-ttc of each square and convert with -WaterFix."
          % args.zoom)


if __name__ == "__main__":
    main()
