"""
Samples one tile with the Python reference and prints hashes, for comparison with the C# port.

    python tools/ttc/cross_sample.py "C:\\...\\g_15_stitch.tmc" 12 1240 1647

and on the other side:

    tools/ttc/csharp/sample.ps1 "C:\\...\\g_15_stitch.tmc" 12 1240 1647

Both must print the same two hashes. Resampling is where a georeferencing port goes quietly
wrong - half a pixel of offset, a floor that should have been a round, latitude treated as
linear. None of that looks like a bug in the output; it just puts the imagery slightly in the
wrong place, which is the same class of mistake as the row order that already cost a session.
Only a pixel-for-pixel comparison catches it.

Needs the real stitched source, so it is not part of validate_ttc.py. Note this loads the whole
image, which is the thing the C# side exists to avoid - fine for one tile on a desktop, and it
is the reference, not the product.
"""

import hashlib
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import convert_tmc as ct


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2

    tmc_path = sys.argv[1]
    level = int(sys.argv[2])

    tmc = ct.parse_tmc(tmc_path)
    folder = tmc.get('folder_source_files') or os.path.dirname(os.path.abspath(tmc_path))
    if not os.path.isdir(folder):
        folder = os.path.dirname(os.path.abspath(tmc_path))

    region = next((r for r in tmc['regions'] if r['level'] == level), None)
    if region is None:
        print(f'no region at level {level}')
        return 2

    tiles = ct.tiles_for(region, level)
    xs = sorted({t[0] for t in tiles})
    ys = sorted({t[1] for t in tiles})
    print(f'  tiles       x {xs[0]}..{xs[-1]}  y {ys[0]}..{ys[-1]}   ({len(xs)} x {len(ys)})')

    if len(sys.argv) >= 5:
        tx, ty = int(sys.argv[3]), int(sys.argv[4])
    else:
        tx, ty = xs[0], ys[-1]      # northernmost row, matching the C# default

    sources = [ct.Source(folder, f) for f in sorted(os.listdir(folder)) if f.endswith('.aid')]
    print(f'  sources     {len(sources)}')
    for s in sources:
        print(f'     {s.w} x {s.h}   W {s.lon_w:.6f} E {s.lon_e:.6f} '
              f'S {s.lat_s:.6f} N {s.lat_n:.6f}')

    rgb, cov = ct.sample_tile(sources, level, tx, ty)

    print()
    print(f'  tile        level {level}  ({tx}, {ty})')
    print(f'  covered     {int(cov.sum()):,} of {cov.size:,} px  ({100.0 * cov.mean():.1f}%)')
    print()
    print(f'  rgb         {sha(rgb.tobytes())}')
    print(f'  covered     {sha(cov.astype("uint8").tobytes())}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
