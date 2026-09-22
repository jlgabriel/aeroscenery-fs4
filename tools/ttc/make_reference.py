"""
Regenerate the .ttc reference fixtures by driving a real GeoConvert install.

    python tools/ttc/make_reference.py --geoconvert "D:\\path\\to\\aerofly_fs_2_geoconvert"

Builds a synthetic noise aerial image plus its AID and a one-level TMC, runs
GeoConvert in a temp directory, and copies the resulting .ttc files into testdata/.
Pass --keep-raw to also copy the 2048x2048 raw tile (9 MB, not committed by default).

Noise, not a gradient: incompressible like real photography, so the compressor
behaves the way it would on real input.
"""

import argparse
import os
import shutil
import subprocess
import tempfile
import time

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, 'testdata')

TOP_LEFT_LON = -82.5
TOP_LEFT_LAT = 25.25
SPAN = 1.25
N = 4096

AID = """<[file][][]
    <[tm_aerial_image_definition][][]
        <[string8][image][test.png]>
        <[string8][mask][]>
        <[vector2_float64][steps_per_pixel][{step!r} {nstep!r}]>
        <[vector2_float64][top_left][{lon!r} {lat!r}]>
        <[string8][coordinate_system][lonlat]>
        <[bool][flip_vertical][false]>
    >
>
"""

TMC = """<[file][][]
    <[tmcolormap_regions][][]
        <[string] [folder_source_files][./input_aerial_images/]>
        <[bool]   [write_images_with_mask][true]>
        <[bool]   [write_ttc_files][true]>
        <[string8][folder_destination_ttc][./scenery/images/]>
        <[bool]   [write_raw_files][true]>
        <[string8][folder_destination_raw][./raw/]>
        <[bool]   [always_overwrite][true]>

        <[list][region_list][]
            <[tmcolormap_region][element][0]
                <[uint32]          [level]       [9]>
                <[vector2_float64] [lonlat_min]  [-82.2 24.3]>
                <[vector2_float64] [lonlat_max]  [-81.6 24.9]>
            >
        >
    >
>
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--geoconvert', required=True,
                    help='folder containing aerofly_fs_2_geoconvert.exe, shader_dx11/ and texture/')
    ap.add_argument('--keep-raw', action='store_true')
    ap.add_argument('--timeout', type=int, default=300)
    args = ap.parse_args()

    exe = os.path.join(args.geoconvert, 'aerofly_fs_2_geoconvert.exe')
    if not os.path.exists(exe):
        raise SystemExit(f'not found: {exe}')

    run = tempfile.mkdtemp(prefix='ttcref_')
    print(f'working in {run}')
    for sub in ('shader_dx11', 'texture'):
        shutil.copytree(os.path.join(args.geoconvert, sub), os.path.join(run, sub))
    shutil.copy2(exe, run)

    src = os.path.join(run, 'input_aerial_images')
    os.makedirs(src)
    os.makedirs(os.path.join(run, 'scenery', 'images'))
    os.makedirs(os.path.join(run, 'raw'))

    step = SPAN / N
    rng = np.random.default_rng(12345)
    img = rng.integers(0, 256, size=(N, N, 3), dtype=np.uint8)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    tint = (np.sin(xx / 260.0) * np.cos(yy / 320.0) * 28.0).astype(np.int16)
    img = np.clip(img.astype(np.int16) + tint[:, :, None], 0, 255).astype(np.uint8)
    Image.fromarray(img).save(os.path.join(src, 'test.png'), compress_level=1)

    open(os.path.join(src, 'test.aid'), 'w').write(
        AID.format(step=step, nstep=-step, lon=TOP_LEFT_LON, lat=TOP_LEFT_LAT))
    open(os.path.join(run, 'ref.tmc'), 'w').write(TMC)

    # GeoConvert is a GUI app: no stdout, no exit code, and it waits for Escape
    # when finished. Completion is detected by counting output files, which is
    # also what a production wrapper should do (never screenshot for green text).
    print('running GeoConvert...')
    p = subprocess.Popen([exe, 'ref.tmc'], cwd=run)
    out = os.path.join(run, 'scenery', 'images')
    t0 = time.time()
    stable = 0
    last = -1
    while time.time() - t0 < args.timeout:
        try:
            p.wait(timeout=3)
            break
        except subprocess.TimeoutExpired:
            pass
        n = len([f for f in os.listdir(out) if f.endswith('.ttc')])
        stable = stable + 1 if n == last and n >= 2 else 0
        last = n
        print(f'  t={time.time()-t0:5.1f}s  ttc={n}')
        if stable >= 3:
            break
    if p.poll() is None:
        p.kill()
    print(f'done in {time.time()-t0:.1f}s')

    os.makedirs(DATA, exist_ok=True)
    for f in sorted(os.listdir(out)):
        if f.endswith('.ttc'):
            shutil.copy2(os.path.join(out, f), os.path.join(DATA, f))
            print(f'  -> testdata/{f}  ({os.path.getsize(os.path.join(out, f)):,} bytes)')
    if args.keep_raw:
        raw = os.path.join(run, 'raw', 'map_09_4580_9180.png')
        if os.path.exists(raw):
            shutil.copy2(raw, os.path.join(DATA, 'map_09_4580_9180_raw.png'))
            print('  -> testdata/map_09_4580_9180_raw.png')

    print(f'\ntemp dir left in place for inspection: {run}')


if __name__ == '__main__':
    main()
