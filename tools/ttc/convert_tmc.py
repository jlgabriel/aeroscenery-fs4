"""
A GeoConvert replacement: same .tmc in, same .ttc out, no LZHAM and no GPU.

    python tools/ttc/convert_tmc.py --tmc "...\\16-stitched\\g_16_stitch.tmc" --out out\\
    python tools/ttc/convert_tmc.py --tmc ... --compare-raw "...\\16-geoconvert-raw"

Two things are being tested here, and they are independent:

1. **Storing instead of compressing.** Verified to render in Aerofly FS 4; see the handoff.
2. **Resampling the source once instead of once per level.** A level N-1 tile is exactly the
   2x2 block of level N tiles halved, so the whole pyramid can be derived from the deepest
   level. GeoConvert's own .tmc asks for five levels over the same images, and it builds a
   tile list per level, so it appears to walk the source five times. `--per-level` reproduces
   that behaviour so the two can be timed against each other.

Correctness is checked against GeoConvert's own `write_raw_files` output, which is the
resampling stage before DXT and the container, so no LZHAM decoder is needed to compare.
"""

import argparse
import math
import os
import re
import sys
import time

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ttc
from tone_curve import TONE_CURVE

Image.MAX_IMAGE_PIXELS = None

WORLD_GRID_K = 2.3311223704144      # AeroScenery/AFS2/AFS2Grid.cs
TILE_PX = 2048
MASK_PX = 512

# GeoConvert brightens the source on its way into the tile; see tone_curve.py. Without this
# our tiles come out visibly darker than every tile already installed.
TONE = np.array(TONE_CURVE, np.uint8)


# --------------------------------------------------------------------------
# the AFS2 grid  (mirror of AFS2Grid.cs - do not re-derive, see the handoff)
# --------------------------------------------------------------------------

def grid_x(lon_deg, level):
    return 2 ** level * (0.5 + 0.5 * (math.radians(lon_deg) / math.pi))


def grid_y(lat_deg, level):
    y1 = math.tan(WORLD_GRID_K * math.radians(lat_deg) / math.pi) / WORLD_GRID_K
    return 2 ** level * (0.5 + 0.5 * y1)


def lat_of_grid_y(y, level):
    """Inverse of grid_y. y counts northward from the south pole."""
    y1 = 2.0 * (y / 2 ** level) - 1.0
    return math.degrees(math.pi * math.atan(WORLD_GRID_K * y1) / WORLD_GRID_K)


def lon_of_grid_x(x, level):
    return math.degrees(math.pi * (2.0 * (x / 2 ** level) - 1.0))


# --------------------------------------------------------------------------
# the tm file format (both .tmc and .aid are the same <[type][name][value]> tree)
# --------------------------------------------------------------------------

FIELD = re.compile(r'<\[([a-z0-9_]+)\]\s*\[([a-z0-9_]*)\]\s*\[([^\]]*)\]>', re.I)


def parse_fields(text):
    return [(m.group(1), m.group(2), m.group(3).strip()) for m in FIELD.finditer(text)]


def parse_tmc(path):
    text = open(path, encoding='utf-8-sig', errors='replace').read()
    out = {'regions': []}
    cur = {}
    for typ, name, val in parse_fields(text):
        if name == 'level':
            cur = {'level': int(val)}
            out['regions'].append(cur)
        elif name in ('lonlat_min', 'lonlat_max'):
            a, b = (float(v) for v in val.split())
            cur[name] = (a, b)
        elif name in ('folder_source_files', 'folder_destination_ttc',
                      'folder_destination_raw'):
            out[name] = val
        elif name == 'write_images_with_mask':
            out.setdefault(name, val.lower() == 'true')
    return out


def parse_aid(path):
    f = dict((name, val) for _, name, val in parse_fields(open(path, errors='replace').read()))
    sx, sy = (float(v) for v in f['steps_per_pixel'].split())
    lon0, lat0 = (float(v) for v in f['top_left'].split())
    return {'image': f['image'], 'step_lon': sx, 'step_lat': sy,
            'lon0': lon0, 'lat0': lat0,
            'flip_vertical': f.get('flip_vertical', 'false').lower() == 'true'}


class Source:
    """One stitched image plus the lon/lat mapping from its .aid.

    The .aid declares a constant degrees-per-pixel in both axes, so the source is a plain
    linear lon/lat raster. The output grid is not linear in latitude, which is the whole
    reason resampling is needed rather than a copy.
    """

    def __init__(self, folder, aid_name):
        a = parse_aid(os.path.join(folder, aid_name))
        self.path = os.path.join(folder, a['image'])
        self.a = a
        with Image.open(self.path) as im:
            self.w, self.h = im.size
        self.lon_w = a['lon0']
        self.lon_e = a['lon0'] + self.w * a['step_lon']
        self.lat_n = a['lat0']
        self.lat_s = a['lat0'] + self.h * a['step_lat']
        self.pixels = None

    def load(self):
        if self.pixels is None:
            with Image.open(self.path) as im:
                self.pixels = np.asarray(im.convert('RGB'))
        return self.pixels

    def unload(self):
        self.pixels = None

    def covers(self, lon_w, lon_e, lat_s, lat_n):
        return not (lon_e <= self.lon_w or lon_w >= self.lon_e
                    or lat_n <= self.lat_s or lat_s >= self.lat_n)


# --------------------------------------------------------------------------
# resampling one output tile
# --------------------------------------------------------------------------

def sample_tile(sources, level, tx, ty, size=TILE_PX):
    """Nearest-neighbour resample of the sources into one tile.

    Returns (rgb, covered) where covered is a bool array marking pixels a source supplied.
    Row 0 is the north edge, so grid y runs downward through the tile.
    """
    lon_w = lon_of_grid_x(tx, level)
    lon_e = lon_of_grid_x(tx + 1, level)
    lons = lon_w + (np.arange(size) + 0.5) * (lon_e - lon_w) / size

    gy = (ty + 1) - (np.arange(size) + 0.5) / size          # north edge first
    k = WORLD_GRID_K
    y1 = 2.0 * (gy / 2 ** level) - 1.0
    lats = np.degrees(np.pi * np.arctan(k * y1) / k)

    rgb = np.zeros((size, size, 3), np.uint8)
    covered = np.zeros((size, size), bool)

    for s in sources:
        if not s.covers(min(lon_w, lon_e), max(lon_w, lon_e), s.lat_s, s.lat_n):
            continue
        px = (lons - s.a['lon0']) / s.a['step_lon']
        py = (lats - s.a['lat0']) / s.a['step_lat']
        cx = np.floor(px).astype(np.int64)
        cy = np.floor(py).astype(np.int64)
        okx = (cx >= 0) & (cx < s.w)
        oky = (cy >= 0) & (cy < s.h)
        if not okx.any() or not oky.any():
            continue
        img = s.load()
        sel = np.ix_(cy[oky], cx[okx])
        block = img[sel]
        rows = np.where(oky)[0][:, None]
        cols = np.where(okx)[0][None, :]
        fresh = ~covered[rows, cols]
        tgt_r, tgt_c = np.where(fresh)
        if len(tgt_r):
            rr = rows[:, 0][tgt_r]
            cc = cols[0][tgt_c]
            rgb[rr, cc] = block[tgt_r, tgt_c]
            covered[rr, cc] = True
    return TONE[rgb], covered


def halve(a):
    h, w = a.shape[:2]
    b = a[:h // 2 * 2, :w // 2 * 2].astype(np.uint16)
    return (((b[0::2, 0::2] + b[1::2, 0::2] + b[0::2, 1::2] + b[1::2, 1::2]) + 2) // 4
            ).astype(a.dtype)


# --------------------------------------------------------------------------
# writing
# --------------------------------------------------------------------------

def write_tile(out_dir, level, tx, ty, rgb, covered, want_mask):
    # A tile no source reaches is not a black tile, it is no tile. GeoConvert omits them and so
    # must we: writing them costs 2.8 MB and a full encode each, and would black out terrain
    # Aerofly would otherwise draw from its own imagery.
    if not covered.any():
        return []

    # Aerofly stores tile rows bottom-up: row 0 of the texture is the SOUTH edge. Everything
    # above works north-up, like the source and like GeoConvert's write_raw_files dump, so the
    # flip happens here, once, on the way out.
    #
    # This cost a whole debugging session. Comparing against GeoConvert's raw PNGs never caught
    # it, because those really are north-up - the flip lives between the raw dump and the DXT1
    # payload. Symmetric test patterns (a checkerboard, a centred cross) cannot catch it either.
    # An asymmetric one in the simulator can: an F drawn upright came back mirrored.
    rgb = rgb[::-1]
    covered = covered[::-1]

    chain, mips = ttc.build_mip_chain(rgb, ttc.FORMAT_DXT1)
    data = ttc.build_ttc_stored(level, TILE_PX, TILE_PX, mips, ttc.FORMAT_DXT1, chain)
    name = ttc.tile_name(level, tx, ty)
    open(os.path.join(out_dir, name), 'wb').write(data)
    written = [name]

    if want_mask and not covered.all():
        m = (covered.astype(np.uint8) * 255)
        step = TILE_PX // MASK_PX
        m = m.reshape(MASK_PX, step, MASK_PX, step).max(axis=(1, 3))
        mchain, mmips = ttc.build_mip_chain(m, ttc.FORMAT_L8)
        mdata = ttc.build_ttc_stored(level, MASK_PX, MASK_PX, mmips, ttc.FORMAT_L8,
                                     mchain, 0x00FFFFFF, 0x00000000)
        mname = ttc.tile_name(level, tx, ty, mask=True)
        open(os.path.join(out_dir, mname), 'wb').write(mdata)
        written.append(mname)
    return written


def tiles_for(region, level):
    w, s = region['lonlat_min']
    e, n = region['lonlat_max']
    if s > n:
        s, n = n, s
    if w > e:
        w, e = e, w
    x0, x1 = int(grid_x(w, level)), int(math.ceil(grid_x(e, level)))
    y0, y1 = int(grid_y(s, level)), int(math.ceil(grid_y(n, level)))
    return [(x, y) for x in range(x0, x1) for y in range(y0, y1)]


# --------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--tmc', required=True)
    ap.add_argument('--out', default=None, help='defaults to the tmc destination folder')
    ap.add_argument('--per-level', action='store_true',
                    help='resample the source separately for each level, as GeoConvert appears to')
    ap.add_argument('--levels', default=None, help='comma separated subset, for quick runs')
    ap.add_argument('--limit', type=int, default=None, help='stop after N deepest-level tiles')
    ap.add_argument('--compare-raw', default=None,
                    help="folder of GeoConvert write_raw_files output to score against")
    args = ap.parse_args()

    tmc = parse_tmc(args.tmc)
    folder = tmc.get('folder_source_files') or os.path.dirname(args.tmc)
    out = args.out or tmc.get('folder_destination_ttc')
    os.makedirs(out, exist_ok=True)

    sources = [Source(folder, f) for f in sorted(os.listdir(folder)) if f.endswith('.aid')]
    regions = tmc['regions']
    if args.levels:
        keep = {int(v) for v in args.levels.split(',')}
        regions = [r for r in regions if r['level'] in keep]
    regions.sort(key=lambda r: r['level'])
    want_mask = tmc.get('write_images_with_mask', True)

    print(f'{len(sources)} source image(s), {sum(s.w * s.h for s in sources) / 1e6:,.0f} Mpx')
    for r in regions:
        print(f'  level {r["level"]:>2}  {len(tiles_for(r, r["level"])):>5} tiles')
    print(f'output -> {out}\n')

    t0 = time.time()
    written = []
    stats = {}

    if args.per_level:
        for r in regions:
            lv = r['level']
            ts = tiles_for(r, lv)[:args.limit]
            t1 = time.time()
            for tx, ty in ts:
                rgb, cov = sample_tile(sources, lv, tx, ty)
                written += write_tile(out, lv, tx, ty, rgb, cov, want_mask)
            stats[lv] = time.time() - t1
            print(f'  level {lv:>2}  {len(ts):>5} tiles  {stats[lv]:7.1f}s')
    else:
        deepest = regions[-1]['level']
        cache = {}
        ts = tiles_for(regions[-1], deepest)[:args.limit]
        t1 = time.time()
        for tx, ty in ts:
            rgb, cov = sample_tile(sources, deepest, tx, ty)
            if not cov.any():
                continue        # nothing here, and nothing for the pyramid to inherit either
            cache[(tx, ty)] = (rgb, cov)
            written += write_tile(out, deepest, tx, ty, rgb, cov, want_mask)
        stats[deepest] = time.time() - t1
        print(f'  level {deepest:>2}  {len(ts):>5} tiles  {stats[deepest]:7.1f}s   (resampled)')

        for r in reversed(regions[:-1]):
            lv = r['level']
            t1 = time.time()
            nxt = {}
            derived = resampled = 0
            for tx, ty in tiles_for(r, lv):
                children = [cache.get((2 * tx + i, 2 * ty + j))
                            for i in (0, 1) for j in (0, 1)]

                if all(c is not None for c in children):
                    quad = np.zeros((TILE_PX * 2, TILE_PX * 2, 3), np.uint8)
                    qcov = np.zeros((TILE_PX * 2, TILE_PX * 2), bool)
                    # child (2tx+i, 2ty+j): j=1 is the north half, and row 0 of a tile is north
                    for k, (i, j) in enumerate([(i, j) for i in (0, 1) for j in (0, 1)]):
                        r0 = 0 if j == 1 else TILE_PX
                        c0 = i * TILE_PX
                        quad[r0:r0 + TILE_PX, c0:c0 + TILE_PX] = children[k][0]
                        qcov[r0:r0 + TILE_PX, c0:c0 + TILE_PX] = children[k][1]
                    rgb, cov = halve(quad), halve(qcov.astype(np.uint8)).astype(bool)
                    derived += 1
                else:
                    # A level can be asked for over a WIDER area than the level below it - the
                    # .tmc AeroScenery writes does exactly that for level 10 - so the pyramid
                    # simply has no children to halve there. Falling back to the source is what
                    # keeps those tiles from coming out half black.
                    rgb, cov = sample_tile(sources, lv, tx, ty)
                    resampled += 1

                if not cov.any():
                    continue
                nxt[(tx, ty)] = (rgb, cov)
                written += write_tile(out, lv, tx, ty, rgb, cov, want_mask)
            stats[lv] = time.time() - t1
            how = f'{derived} from level {lv+1}'
            if resampled:
                how += f', {resampled} resampled (no children)'
            print(f'  level {lv:>2}  {len(nxt):>5} tiles  {stats[lv]:7.1f}s   ({how})')
            cache = nxt

    total = time.time() - t0
    nbytes = sum(os.path.getsize(os.path.join(out, f)) for f in written)
    print(f'\n{len(written)} files, {nbytes / 2**20:,.0f} MB, {total:.1f}s '
          f'({total / max(1, len(written)):.2f}s per file)')

    if args.compare_raw:
        compare(out, args.compare_raw, sources)


def compare(out_dir, raw_dir, sources):
    """Score our resampling against GeoConvert's own pre-DXT PNG output."""
    print('\n' + '=' * 70)
    print('resampling vs GeoConvert write_raw_files')
    print('=' * 70)
    n = 0
    for f in sorted(os.listdir(out_dir)):
        if not f.endswith('.ttc') or f.endswith('_mask.ttc'):
            continue
        ref = os.path.join(raw_dir, f[:-4] + '.png')
        if not os.path.exists(ref):
            continue
        data = open(os.path.join(out_dir, f), 'rb').read()
        ours = ttc.decode_dxt1(data[0x100:0x100 + (TILE_PX // 4) ** 2 * 8], TILE_PX, TILE_PX)
        theirs = np.asarray(Image.open(ref).convert('RGB'))
        if theirs.shape != ours.shape:
            print(f'  {f}: size differs {theirs.shape} vs {ours.shape}')
            continue
        mse = ((ours.astype(np.float64) - theirs.astype(np.float64)) ** 2).mean()
        psnr = float('inf') if mse == 0 else 10 * np.log10(255 * 255 / mse)
        print(f'  {f:32} PSNR {psnr:6.2f} dB   mean abs '
              f'{np.abs(ours.astype(int) - theirs.astype(int)).mean():5.2f}')
        n += 1
        if n >= 12:
            break
    if not n:
        print('  no matching raw tiles found')


if __name__ == '__main__':
    main()
