"""Level colour patches in stitched imagery: the shared code of patch_editor.py and patch_apply.py.

A colour patch is a rectangle of a different acquisition inside the imagery - for example a pink,
blurred square that Google filled with upscaled lower-zoom imagery. It is a constant tint on top of
the same ground, so one colour offset per patch removes it.

The work is done on a mosaic at 1/16 of the source resolution:

1. The user clicks inside a patch. The magic wand takes everything connected to the click whose
   colour is within a tolerance of the colour there (wand).
2. The wand masks become one label per patch (labels): thin leaks are cut off, holes are filled,
   and cells near the border that look like the terrain beside them go back to the terrain.
3. Each patch gets one offset: the median of the colour step across its whole border (level).
   The terrain is never changed.

patch_apply.py then puts the borders on the exact full-resolution pixel and writes corrected copies
of the stitched images.
"""
import glob
import json
import math
import os
import re

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None

SCALE = 16          # the mosaic is 1/16 of the source resolution


# --------------------------------------------------------------------------
# geometry: the stitched images and the mosaic
# --------------------------------------------------------------------------

def merc_y(lat):
    s = math.sin(math.radians(lat))
    return 0.5 - math.log((1 + s) / (1 - s)) / (4 * math.pi)


def lat_of_merc(y):
    return math.degrees(math.atan(math.sinh(math.pi * (1 - 2 * y))))


def read_aid(path):
    txt = open(path, encoding='utf-8-sig').read()
    def vec(name):
        m = re.search(r'\[' + name + r'\]\[([-0-9.eE+]+) ([-0-9.eE+]+)\]', txt)
        return float(m.group(1)), float(m.group(2))
    return vec('top_left'), vec('steps_per_pixel')


class StitchedImage(object):
    """One stitched PNG and where it sits on the source's own tile grid (web mercator pixels)."""

    def __init__(self, png):
        self.png = png
        self.aid = os.path.splitext(png)[0] + '.aid'
        (lon, lat), (step_x, _) = read_aid(self.aid)
        self.zoom = int(round(math.log2(360.0 / (step_x * 256))))
        n = 2 ** self.zoom * 256
        with Image.open(png) as im:
            self.width, self.height = im.size
        # the stitched images start on a tile edge, so these are whole pixels
        self.x = int(round((lon + 180.0) / 360.0 * n))
        self.y = int(round(merc_y(lat) * n))


class StitchedFolder(object):
    """A '<zoom>-stitched' folder: its .tmc and its stitched images."""

    def __init__(self, path):
        self.path = os.path.abspath(path)
        tmc = glob.glob(os.path.join(self.path, '*.tmc'))
        if not tmc:
            raise SystemExit('no .tmc in ' + self.path)
        self.tmc = tmc[0]
        self.images = [StitchedImage(p) for p in sorted(glob.glob(os.path.join(self.path, '*.png')))
                       if os.path.exists(os.path.splitext(p)[0] + '.aid')]
        if not self.images:
            raise SystemExit('no stitched image with an .aid in ' + self.path)
        # <square>\<source>\<zoom>-stitched
        self.source_dir = os.path.dirname(self.path)
        self.square_dir = os.path.dirname(self.source_dir)
        self.name = os.path.basename(self.square_dir)


class Mosaic(object):
    """All the stitched images of some folders, reduced 16 times and laid on one grid."""

    def __init__(self, folders):
        self.folders = folders
        self.images = [im for f in folders for im in f.images]
        zooms = set(im.zoom for im in self.images)
        if len(zooms) != 1:
            raise SystemExit('all the stitched images must have the same zoom, not %s' % sorted(zooms))
        self.zoom = zooms.pop()
        self.x0 = min(im.x for im in self.images)
        self.y0 = min(im.y for im in self.images)
        x1 = max(im.x + im.width for im in self.images)
        y1 = max(im.y + im.height for im in self.images)
        self.w = (x1 - self.x0 + SCALE - 1) // SCALE
        self.h = (y1 - self.y0 + SCALE - 1) // SCALE
        src = np.zeros((self.h, self.w, 3), np.uint8)
        for im in self.images:
            with Image.open(im.png) as pic:
                small = np.asarray(pic.convert('RGB').reduce(SCALE))
            ox, oy = self.offset(im)
            src[oy:oy + small.shape[0], ox:ox + small.shape[1]] = small
        self.src = src.astype(np.float32)
        self.have = self.src.max(2) > 0
        # water: clearly blue over both red and green. The test is made on the imagery itself.
        ss = box(self.src, 9)
        self.land = self.have & ~(ss[..., 2] > np.maximum(ss[..., 0], ss[..., 1]) + 12)
        self._s3 = box(self.src, 3)

    def offset(self, im):
        """Where a stitched image starts in the mosaic, in mosaic pixels."""
        return (im.x - self.x0) // SCALE, (im.y - self.y0) // SCALE

    def lonlat(self, x, y):
        n = 2 ** self.zoom * 256
        return ((self.x0 + (x + 0.5) * SCALE) / n * 360.0 - 180.0,
                lat_of_merc((self.y0 + (y + 0.5) * SCALE) / n))

    def xy(self, lon, lat):
        n = 2 ** self.zoom * 256
        return (int(math.floor(((lon + 180.0) / 360.0 * n - self.x0) / SCALE)),
                int(math.floor((merc_y(lat) * n - self.y0) / SCALE)))


# --------------------------------------------------------------------------
# small image tools
# --------------------------------------------------------------------------

def box(a, k):
    """Separable box blur with an odd window, edge-padded."""
    p = k // 2
    f = np.pad(a.astype(np.float32), ((p, p), (p, p)) + ((0, 0),) * (a.ndim - 2), mode='edge')
    c = np.cumsum(f, 0)
    c = np.concatenate([np.zeros((1,) + c.shape[1:], np.float32), c], 0)
    f = (c[k:] - c[:-k]) / k
    c = np.cumsum(f, 1)
    c = np.concatenate([np.zeros((c.shape[0], 1) + c.shape[2:], np.float32), c], 1)
    return (c[:, k:] - c[:, :-k]) / k


def dilate(m):
    """One step of dilation with a cross."""
    o = m.copy()
    o[1:] |= m[:-1]; o[:-1] |= m[1:]; o[:, 1:] |= m[:, :-1]; o[:, :-1] |= m[:, 1:]
    return o


def sq_dilate(m, r):
    """Dilation with a (2r+1) square."""
    o = m.copy()
    for k in range(1, r + 1):
        o[:, k:] |= m[:, :-k]; o[:, :-k] |= m[:, k:]
    p = o.copy()
    for k in range(1, r + 1):
        p[k:] |= o[:-k]; p[:-k] |= o[k:]
    return p


def sq_open(m, r):
    """Opening with a (2r+1) square: removes anything narrower, keeps rectangles exactly."""
    return sq_dilate(~sq_dilate(~m, r), r)


def grow(seed, allowed, max_iter=4000):
    """Constrained flood fill by repeated dilation, in blocks of 8 steps between checks."""
    r = seed & allowed
    for _ in range(max_iter // 8):
        prev = r.sum()
        for _ in range(8):
            r = dilate(r) & allowed
        if r.sum() == prev:
            break
    return r


def frame(shape):
    b = np.zeros(shape, bool)
    b[0] = b[-1] = True; b[:, 0] = b[:, -1] = True
    return b


# --------------------------------------------------------------------------
# the wand, the labels and the offsets
# --------------------------------------------------------------------------

def wand(mos, x, y, tol):
    """Everything connected to (x, y) whose colour is within tol of the colour around the click.
    Holes (a crater, a dark shrub) are filled. The edge is grown by one pixel to take the mixed
    pixels on the border."""
    s = mos._s3
    h, w = s.shape[:2]
    x, y = int(np.clip(x, 2, w - 3)), int(np.clip(y, 2, h - 3))
    c = s[y - 2:y + 3, x - 2:x + 3].reshape(-1, 3).mean(0)
    allowed = (np.linalg.norm(s - c, axis=2) < tol) & mos.have
    seed = np.zeros((h, w), bool); seed[y, x] = True
    r = grow(seed, allowed)
    outside = grow(frame((h, w)) & ~r, ~r)
    return dilate(~outside)


def labels(mos, masks):
    """One label per patch. 0 is terrain.

    - A mask is opened with a 7 px square first: the patches are rectangles, so this cuts off the
      thin leaks of the wand into the terrain and keeps the patch.
    - A mask that is mostly water is ignored. The levelling is for land.
    - A mask that is mostly covered by an earlier label (the same patch clicked twice) joins it.
    - A dark thing inside a patch (lava, a shrub) is under the same tint but not the same colour,
      so the wand leaves it out. Each patch is closed by 3 px (~50 m at zoom 17) and what the
      closing encloses is filled.
    - Last, the border cells that look like the terrain beside them go back to the terrain (prune).
    """
    shape = mos.src.shape[:2]
    lab = np.zeros(shape, np.int32)
    n = 0
    for m in masks:
        m = sq_open(m, 3)
        if m.sum() == 0 or (m & mos.land).sum() < 0.5 * m.sum():
            continue
        under = lab[m]
        vals, cnt = np.unique(under[under > 0], return_counts=True)
        if vals.size and cnt.max() > 0.5 * m.sum():
            lab[m & (lab == 0)] = vals[cnt.argmax()]
        else:
            n += 1
            lab[m & (lab == 0)] = n
    for p in range(1, n + 1):
        ys, xs = np.nonzero(lab == p)
        if ys.size == 0:
            continue
        y0, y1 = max(ys.min() - 8, 0), min(ys.max() + 9, shape[0])
        x0, x1 = max(xs.min() - 8, 0), min(xs.max() + 9, shape[1])
        sub = lab[y0:y1, x0:x1]                      # a view: writes go to lab
        c = sub == p
        for _ in range(3):
            c = dilate(c)
        for _ in range(3):
            c = ~dilate(~c)
        outside = grow(frame(c.shape) & ~c, ~c)
        sub[~outside & (sub == 0)] = p
    prune(mos, lab)
    return lab


def _local_mean(s, valid, k=15):
    den = box(valid.astype(np.float32), k)
    m = box(s * valid[..., None], k) / np.maximum(den, 1e-6)[..., None]
    if valid.any():
        m[den < 0.02] = s[valid].mean(0)
    return m


def prune(mos, lab, rounds=3, depth=4):
    """Give back to the terrain the cells near a patch border that look like the terrain beside
    them rather than like the patch's own interior. The wand at tolerance 20 leaks a few cells
    into terrain of a close colour, and the patch offset turns those cells yellow."""
    s5 = box(mos.src, 5)
    for _ in range(rounds):
        for p in range(1, int(lab.max()) + 1):
            r = lab == p
            if r.sum() < 30:
                continue
            ys, xs = np.nonzero(r)
            y0, y1 = max(ys.min() - 12, 0), min(ys.max() + 13, lab.shape[0])
            x0, x1 = max(xs.min() - 12, 0), min(xs.max() + 13, lab.shape[1])
            sub = lab[y0:y1, x0:x1]
            rr = sub == p
            ss = s5[y0:y1, x0:x1]
            inner = ~sq_dilate(~rr, depth)
            if inner.sum() < 10:
                continue
            ring = sq_dilate(rr, 6) & ~sq_dilate(rr, 1) & (sub == 0) & mos.land[y0:y1, x0:x1]
            if ring.sum() < 10:
                continue
            mi = _local_mean(ss, inner)
            mo = _local_mean(ss, ring)
            edge = rr & ~inner
            sub[edge & (((ss - mo) ** 2).sum(2) < ((ss - mi) ** 2).sum(2))] = 0
            keep = sq_open(sub == p, 2)
            sub[(sub == p) & ~keep] = 0
    return lab


def level(mos, masks):
    """One colour offset per patch: the median of the colour step across its whole border.

    The median ignores the stretches of border that run along forest or lava. Patches that touch
    each other are solved together, by least squares over the pairs. The terrain is fixed at 0.
    The colour on each side of a border is read two pixels away from it, so the mixed pixels on
    the border do not count.

    Returns the corrected mosaic, the offsets (row 0 is the terrain, always 0) and the labels."""
    src, land = mos.src, mos.land
    lab = labels(mos, masks)
    K = int(lab.max())
    if K == 0:
        return src.copy(), np.zeros((1, 3), np.float32), lab
    s3 = mos._s3
    pairs = {}
    for axis in (1, 0):
        a = lab
        b = np.roll(lab, -1, axis)
        near = np.roll(s3, 2, axis)
        far = np.roll(s3, -3, axis)
        ok = (a != b) & np.roll(land, 2, axis) & np.roll(land, -3, axis) & land & np.roll(land, -1, axis)
        if axis == 1:
            ok[:, -4:] = False; ok[:, :3] = False
        else:
            ok[-4:] = False; ok[:3] = False
        ys, xs = np.nonzero(ok)
        la, lb = a[ys, xs], b[ys, xs]
        jump = (near - far)[ys, xs]                  # wanted: offset[b] - offset[a]
        for p, q, j in ((la, lb, jump), (lb, la, -jump)):
            key = p * 100000 + q
            for k in np.unique(key[p < q]):
                sel = (key == k) & (p < q)
                pairs.setdefault((int(k // 100000), int(k % 100000)), []).append(j[sel])
    rows, rhs, wts = [], [], []
    for (p, q), js in pairs.items():
        j = np.concatenate(js)
        if len(j) < 5:
            continue
        rows.append((p, q)); rhs.append(np.median(j, 0)); wts.append(np.sqrt(len(j)))
    off = np.zeros((K + 1, 3), np.float32)
    if rows:
        A = np.zeros((len(rows), K))
        Y = np.array(rhs) * np.array(wts)[:, None]
        for i, ((p, q), w) in enumerate(zip(rows, wts)):
            if q > 0: A[i, q - 1] += w
            if p > 0: A[i, p - 1] -= w
        off[1:] = np.linalg.lstsq(A, Y, rcond=None)[0]
    out = np.clip(src + off[lab], 0, 255)
    out[~mos.have] = 0
    return out, off, lab


# --------------------------------------------------------------------------
# the state file: the clicks, in degrees, so it does not depend on the mosaic
# --------------------------------------------------------------------------

def load_state(path):
    if not os.path.exists(path):
        return {'version': 1, 'clicks': []}
    with open(path, encoding='utf-8') as f:
        return json.load(f)


def save_state(path, state):
    tmp = path + '.tmp'
    with open(tmp, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(state, f, indent=1)
    os.replace(tmp, path)


def masks_of(mos, state, cache=None):
    out = []
    for c in state.get('clicks', []):
        x, y = mos.xy(c['lon'], c['lat'])
        key = (x, y, int(c['tol']))
        if cache is not None and key in cache:
            out.append(cache[key]); continue
        m = wand(mos, x, y, c['tol']) if 0 <= x < mos.w and 0 <= y < mos.h else np.zeros(mos.src.shape[:2], bool)
        if cache is not None:
            cache[key] = m
        out.append(m)
    return out
