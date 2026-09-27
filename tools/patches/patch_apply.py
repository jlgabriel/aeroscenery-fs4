#!/usr/bin/env python3
"""Write corrected copies of the stitched images: one colour offset per marked patch.

    python tools/patches/patch_apply.py <state.json> --out <folder> <stitched folder> [...]

<stitched folder> is a '<zoom>-stitched' folder of a grid square, as the app writes it:
<Working Folder>\\<square>\\<source>\\<zoom>-stitched. Give every folder the patches were marked on
in patch_editor.py, in any order.

The originals are not changed. For each square with a patch in it, the script writes
<out>\\<square>\\<source>\\<zoom>-stitched with the corrected PNGs, copies of the other files, and a
.tmc that points to the new folders. The squares without a patch are not written. Convert the new
.tmc with AeroSceneryConvert and install the tiles as usual.

The labels come from the wand at 1/16 (patchlib.level). At full resolution every straight run of a
label border is moved onto the exact column (or row) of the colour step, found by averaging the
whole run. The patch borders are straight lines, so this is exact where a pixel-by-pixel colour
test is noisy.
"""
import argparse
import glob
import os
import re
import shutil
import sys
import time

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import patchlib as pl

REACH = 24          # full-resolution px the border may move from where the 1/16 labels put it
TRIM = 8            # full-resolution px left out at each end of a run: the corners are mixed
MIN_STEP = 4.0      # below this the run has no clear step and stays where it is


def runs(D):
    """Runs of True down each column of D: (column, row0, row1 exclusive)."""
    res = []
    dd = np.diff(np.pad(D, ((1, 1), (0, 0))).astype(np.int8), axis=0)
    for c in np.nonzero(D.any(0))[0]:
        st = np.nonzero(dd[:, c] == 1)[0]
        en = np.nonzero(dd[:, c] == -1)[0]
        res += [(c, a, b) for a, b in zip(st, en)]
    return res


def vertical_runs(L):
    """Borders between column x and x+1, split wherever the pair of labels changes."""
    out = []
    Li = L.astype(np.int32)
    code = Li[:, :-1] * 256 + Li[:, 1:]
    for pair in np.unique(code[Li[:, :-1] != Li[:, 1:]]):
        out += [(x, a, b, pair // 256, pair % 256) for x, a, b in runs(code == pair)]
    return out


def snap_position(img, r0, r1, c_nom):
    """Column of the strongest colour step near c_nom, averaged over rows r0..r1 of img."""
    W = img.shape[1]
    lo, hi = max(c_nom - REACH - 4, 0), min(c_nom + REACH + 4, W)
    if r1 - r0 < 4 or hi - lo < 16:
        return c_nom
    prof = img[r0:r1, lo:hi, :3].astype(np.float32).mean(0)
    best, bc = 0.0, c_nom
    for c in range(max(c_nom - REACH, lo + 4), min(c_nom + REACH, hi - 4) + 1):
        s = np.linalg.norm(prof[c - lo:c - lo + 4].mean(0) - prof[c - lo - 4:c - lo].mean(0))
        if s > best:
            best, bc = s, c
    return bc if best >= MIN_STEP else c_nom


def correct_image(im, mos, L, off):
    """Correct one stitched image. Returns the corrected pixels, or None if no patch touches it."""
    S = pl.SCALE
    h16, w16 = (im.height + S - 1) // S, (im.width + S - 1) // S
    ox, oy = mos.offset(im)
    Lim = np.zeros((h16, w16), np.uint8)
    part = L[oy:oy + h16, ox:ox + w16]
    Lim[:part.shape[0], :part.shape[1]] = part
    if Lim.max() == 0:
        return None, 0
    with Image.open(im.png) as pic:
        img = np.asarray(pic).copy()
    H, W = img.shape[:2]
    Lf = np.repeat(np.repeat(Lim, S, 0), S, 1)[:H, :W]
    # vertical borders, then horizontal ones on the transposed picture
    for axis in (1, 0):
        Lc = Lim if axis == 1 else Lim.T
        pic = img if axis == 1 else img.transpose(1, 0, 2)
        Lfa = Lf if axis == 1 else Lf.T
        for x, a, b, left, right in vertical_runs(Lc):
            c_nom = (x + 1) * S
            r0, r1 = a * S, min(b * S, pic.shape[0])
            c = snap_position(pic, r0 + TRIM, r1 - TRIM, c_nom)
            if c < c_nom:
                Lfa[r0:r1, c:c_nom] = right
            elif c > c_nom:
                Lfa[r0:r1, c_nom:c] = left
    offs = off.astype(np.float32)
    changed = 0
    for r0 in range(0, H, 512):
        lab = Lf[r0:r0 + 512]
        if lab.max() == 0:
            continue
        rgb = img[r0:r0 + 512, :, :3]
        new = np.clip(np.rint(rgb.astype(np.float32) + offs[lab]), 0, 255).astype(np.uint8)
        new[rgb.max(2) == 0] = 0                    # exact black is unbuilt source: keep it
        changed += int((new != rgb).any(2).sum())
        img[r0:r0 + 512, :, :3] = new
    return img, changed


def write_tmc(src_tmc, dst_tmc, stitched_dir, ttc_dir):
    txt = open(src_tmc, encoding='utf-8', newline='').read()
    txt = re.sub(r'(\[folder_source_files\]\[)[^\]]*(\])', lambda m: m.group(1) + stitched_dir + '\\' + m.group(2), txt)
    txt = re.sub(r'(\[folder_destination_ttc\]\[)[^\]]*(\])', lambda m: m.group(1) + ttc_dir + '\\' + m.group(2), txt)
    open(dst_tmc, 'w', encoding='utf-8', newline='').write(txt)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('state')
    ap.add_argument('folders', nargs='+', help='the <zoom>-stitched folders')
    ap.add_argument('--out', required=True, help='folder for the corrected copies')
    args = ap.parse_args()

    t0 = time.time()
    folders = [pl.StitchedFolder(p) for p in args.folders]
    mos = pl.Mosaic(folders)
    state = pl.load_state(args.state)
    masks = pl.masks_of(mos, state)
    _, off, L = pl.level(mos, masks)
    print('%d clicks, %d patches' % (len(masks), int(L.max())))
    for i, o in enumerate(off[1:], 1):
        print('  patch %d: offset (%.1f, %.1f, %.1f)' % ((i,) + tuple(o)))

    for f in folders:
        results = [(im, correct_image(im, mos, L, off)) for im in f.images]
        if all(r[0] is None for _, r in results):
            print('%s: no patch, not written' % f.name)
            continue
        rel = os.path.relpath(f.path, f.square_dir)          # <source>\<zoom>-stitched
        out_st = os.path.join(os.path.abspath(args.out), f.name, rel)
        out_ttc = out_st[:-len('stitched')] + 'geoconvert-ttc'
        os.makedirs(out_st, exist_ok=True)
        os.makedirs(out_ttc, exist_ok=True)
        changed = 0
        for im, (pix, n) in results:
            dst = os.path.join(out_st, os.path.basename(im.png))
            if pix is None:
                shutil.copy2(im.png, dst)
            else:
                Image.fromarray(pix).save(dst, compress_level=1)
                changed += n
        for src in glob.glob(os.path.join(f.path, '*')):
            if src.lower().endswith('.png') or os.path.isdir(src):
                continue
            if src.lower().endswith('.tmc'):
                write_tmc(src, os.path.join(out_st, os.path.basename(src)), out_st, out_ttc)
            else:
                shutil.copy2(src, out_st)
        print('%s: %d px changed -> %s' % (f.name, changed, out_st))
    print('done in %.0f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
