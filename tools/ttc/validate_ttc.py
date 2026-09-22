"""
Validation suite for ttc.py, run against real GeoConvert output.

    python tools/ttc/validate_ttc.py

Fixtures live in testdata/. The raw 2048x2048 source tile is not committed (9 MB);
when it is missing a deterministic synthetic tile is used instead, which exercises
every size and layout check identically. Run make_reference.py to regenerate the
real fixtures from a GeoConvert install.
"""

import os
import sys
import time

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ttc

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, 'testdata')

passed = failed = 0


def check(label, cond, detail=''):
    global passed, failed
    if cond:
        passed += 1
        print(f"  PASS  {label}" + (f"   {detail}" if detail else ""))
    else:
        failed += 1
        print(f"  FAIL  {label}" + (f"   {detail}" if detail else ""))


def synthetic_tile(n=2048, seed=12345):
    """Same recipe as the input that produced the committed fixtures."""
    rng = np.random.default_rng(seed)
    img = rng.integers(0, 256, size=(n, n, 3), dtype=np.uint8)
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    tint = (np.sin(xx / 260.0) * np.cos(yy / 320.0) * 28.0).astype(np.int16)
    return np.clip(img.astype(np.int16) + tint[:, :, None], 0, 255).astype(np.uint8)


REFS = [
    ('map_09_4580_9180.ttc',      ttc.FORMAT_DXT1, 2048, 2048, 12),
    ('map_09_4580_9180_mask.ttc', ttc.FORMAT_L8,    512,  512, 10),
]

print("=" * 74)
print("1. ROUND TRIP  -  rebuild each reference file from its own parts")
print("=" * 74)
for name, fmt, w, h, mips in REFS:
    path = os.path.join(DATA, name)
    if not os.path.exists(path):
        print(f"\n  SKIP  {name} missing - run make_reference.py")
        continue
    ref = open(path, 'rb').read()
    hdr, chunk, payload = ttc.read_ttc(ref)
    print(f"\n{name}  ({len(ref):,} bytes)")

    check("header fields parse", hdr['width'] == w and hdr['height'] == h
          and hdr['num_mips'] == mips and hdr['format'] == fmt,
          f"{hdr['width']}x{hdr['height']} x{hdr['num_mips']} fmt={hdr['format']}")
    check("chunk header len is 0x40", hdr['chunk']['header_len'] == 0x40)
    check("chunk magic2 matches", hdr['chunk']['magic2'] == ttc.CHUNK_MAGIC2)
    check("chunk params (21, 20)", hdr['chunk']['params'] == (21, 20))
    check("chunk size_total == size_compressed",
          hdr['chunk']['size_total'] == hdr['size_compressed'])
    check("size_compressed accounts for whole file",
          ttc.TTC_HEADER_LEN + hdr['size_compressed'] == len(ref),
          f"0x100 + {hdr['size_compressed']:,} == {len(ref):,}")

    expect = ttc.mip_chain_size(w, h, mips, fmt)
    check("size_uncompressed == exact mip chain", hdr['size_uncompressed'] == expect,
          f"{hdr['size_uncompressed']:,} vs computed {expect:,}")

    rebuilt_chunk = ttc.build_chunk(payload, hdr['size_uncompressed'])
    check("chunk rebuilt byte-exact", rebuilt_chunk == chunk, f"{len(rebuilt_chunk):,} bytes")

    rebuilt = ttc.build_ttc(hdr['level'], w, h, mips, fmt, rebuilt_chunk,
                            hdr['size_uncompressed'], hdr['unk24'], hdr['unk28'])
    check("FILE rebuilt byte-exact", rebuilt == ref, f"{len(rebuilt):,} bytes")

print("\n" + "=" * 74)
print("2. NAMING  -  filename <-> tile index")
print("=" * 74)
# Levels other than 9 matter: the multiplier is 2^(16-level), not a constant 128. These
# names are real GeoConvert output from the Santiago and Easter Island squares, and every
# one of them nests inside its level-9 parent - x 155, y 205 and x 100, y 215 respectively.
for name, exp in [('map_09_4580_9180.ttc', (9, 139, 291, False)),
                  ('map_09_4580_9200.ttc', (9, 139, 292, False)),
                  ('map_09_4580_9180_mask.ttc', (9, 139, 291, True)),
                  ('map_09_4d80_6680.ttc', (9, 155, 205, False)),
                  ('map_10_4d80_6680.ttc', (10, 310, 410, False)),
                  ('map_11_4d80_6680.ttc', (11, 620, 820, False)),
                  ('map_12_4d80_6680.ttc', (12, 1240, 1640, False)),
                  ('map_13_4d80_6680.ttc', (13, 2480, 3280, False)),
                  ('map_14_4dc0_6680.ttc', (14, 4976, 6560, False)),
                  ('map_12_3230_6bd0.ttc', (12, 803, 1725, False))]:
    got = ttc.parse_tile_name(name)
    check(f"parse {name}", got == exp, f"{got}")
    if not got[3]:
        check(f"emit  {name}", ttc.tile_name(*exp[:3]) == name, ttc.tile_name(*exp[:3]))

for lvl, parent in ((9, (155, 205)), (14, (4976, 6560))):
    f = 2 ** (lvl - 9)
    check(f"level {lvl} tile nests in its level-9 parent",
          parent[0] // f == 155 and parent[1] // f == 205, f"{parent} / {f}")

print("\n" + "=" * 74)
print("3. PIPELINE  -  our own DXT1 + mip chain")
print("=" * 74)
raw_path = os.path.join(DATA, 'map_09_4580_9180_raw.png')
if os.path.exists(raw_path):
    raw = np.array(Image.open(raw_path).convert('RGB'))
    print(f"source: GeoConvert raw tile {raw.shape}")
else:
    raw = synthetic_tile()
    print(f"source: synthetic tile {raw.shape}  (real raw tile not committed)")

t0 = time.perf_counter()
chain, mips = ttc.build_mip_chain(raw, ttc.FORMAT_DXT1)
dt = time.perf_counter() - t0

ref_path = os.path.join(DATA, 'map_09_4580_9180.ttc')
if os.path.exists(ref_path):
    ref_hdr, _, _ = ttc.read_ttc(open(ref_path, 'rb').read())
    check("mip count matches GeoConvert", mips == ref_hdr['num_mips'],
          f"{mips} vs {ref_hdr['num_mips']}")
    check("chain size matches GeoConvert exactly",
          len(chain) == ref_hdr['size_uncompressed'],
          f"{len(chain):,} vs {ref_hdr['size_uncompressed']:,}")
else:
    check("chain size == computed", len(chain) == ttc.mip_chain_size(2048, 2048, mips, ttc.FORMAT_DXT1))
print(f"        encode time (pure numpy, 1 thread): {dt:.2f}s for 2048^2 + mips")

print("\n  per-level layout:")
for i, w, h, off, n in ttc.mip_offsets(2048, 2048, mips, ttc.FORMAT_DXT1):
    print(f"     L{i:<2} {w:>4}x{h:<4} offset {off:>9,}  {n:>9,} bytes")

print("\n" + "=" * 74)
print("3b. ENCODER QUALITY  -  BC1 is content-dependent, so test three contents")
print("=" * 74)


def psnr_of(img):
    enc = ttc.encode_dxt1(img)
    dec = ttc.decode_dxt1(enc, img.shape[1], img.shape[0])
    mse = ((dec.astype(np.float64) - img.astype(np.float64)) ** 2).mean()
    return (float('inf') if mse == 0 else 10 * np.log10(255 * 255 / mse)), dec


# (33,121,206) is the RGB565 lattice point nearest (37,121,200)
flat565 = np.full((64, 64, 3), (33, 121, 206), dtype=np.uint8)
p, dec = psnr_of(flat565)
check("flat 565-exact colour is lossless", np.array_equal(dec, flat565), f"PSNR {p}")

flat_any = np.full((64, 64, 3), (37, 121, 200), dtype=np.uint8)
_, dec = psnr_of(flat_any)
err = np.abs(dec.astype(int) - flat_any.astype(int)).max()
check("arbitrary flat colour within 565 quantisation", err <= 7,
      f"max channel error {err} (565 step is 8 for R/B, 4 for G)")
check("flat block decodes to a single colour",
      len(np.unique(dec.reshape(-1, 3), axis=0)) == 1)

yy, xx = np.mgrid[0:256, 0:256].astype(np.float32)
grad = np.stack([xx, yy, (xx + yy) / 2], axis=-1).astype(np.uint8)
p, _ = psnr_of(grad)
check("smooth gradient >= 40 dB", p >= 40, f"PSNR {p:.2f} dB")

rng = np.random.default_rng(7)
base = np.zeros((512, 512, 3), np.float32)
for f, a in ((3, 60), (7, 30), (17, 14), (41, 6)):
    ph = rng.random(3) * 6.28
    yy, xx = np.mgrid[0:512, 0:512].astype(np.float32)
    for c in range(3):
        base[:, :, c] += a * np.sin(xx / 512 * f * 6.28 + ph[c]) * np.cos(yy / 512 * f * 6.28 + ph[c])
photo = np.clip(base + 128 + rng.normal(0, 4, (512, 512, 3)), 0, 255).astype(np.uint8)
p, _ = psnr_of(photo)
check("photo-like >= 32 dB", p >= 32, f"PSNR {p:.2f} dB")

p, _ = psnr_of(raw[:512, :512])
print(f"  NOTE  uniform-noise tile: PSNR {p:.2f} dB - BC1 cannot beat this on "
      f"random data (4 colours on a line vs 16 independent pixels)")

print("\n" + "=" * 74)
print("4. MASK PIPELINE  -  L8 chain")
print("=" * 74)
mask_small = np.zeros((512, 512), dtype=np.uint8)
mchain, mmips = ttc.build_mip_chain(mask_small, ttc.FORMAT_L8)
mref_path = os.path.join(DATA, 'map_09_4580_9180_mask.ttc')
if os.path.exists(mref_path):
    mref, _, _ = ttc.read_ttc(open(mref_path, 'rb').read())
    check("mask mip count", mmips == mref['num_mips'], f"{mmips} vs {mref['num_mips']}")
    check("mask chain size exact", len(mchain) == mref['size_uncompressed'],
          f"{len(mchain):,} vs {mref['size_uncompressed']:,}")

print("\n" + "=" * 74)
print("5. STORED PAYLOAD  -  a .ttc with no tmcompress chunk")
print("=" * 74)
print("  Aerofly FS 4 rendered a tile in this shape; see docs/ttc-format.md.")

# 8-pixel squares, so every 4x4 BC1 block is one flat colour, and both colours sit on the
# RGB565 lattice. Only then is the round trip lossless -- a 1-pixel checkerboard would put
# three non-collinear colours in a block and BC1 cannot carry that.
yy, xx = np.mgrid[0:256, 0:256]
odd = (((xx // 8) + (yy // 8)) % 2).astype(bool)
small = np.where(odd[:, :, None], np.array([255, 0, 255], np.uint8),
                 np.array([255, 255, 0], np.uint8)).astype(np.uint8)
schain, smips = ttc.build_mip_chain(small, ttc.FORMAT_DXT1)
stored = ttc.build_ttc_stored(9, 256, 256, smips, ttc.FORMAT_DXT1, schain)

shdr, spayload, scodec = ttc.read_ttc(stored)
check("read_ttc reports it stored", shdr['stored'] is True and shdr['chunk'] is None)
check("both size fields carry the chain length",
      shdr['size_compressed'] == shdr['size_uncompressed'] == len(schain),
      f"{len(schain):,}")
check("0x100 + size_compressed accounts for whole file",
      ttc.TTC_HEADER_LEN + shdr['size_compressed'] == len(stored),
      f"0x100 + {shdr['size_compressed']:,} == {len(stored):,}")
check("payload is the chain itself, no chunk header",
      spayload == scodec == schain)
check("header still parses as format 10",
      shdr['format'] == ttc.FORMAT_DXT1 and shdr['num_mips'] == smips
      and shdr['width'] == 256, f"fmt={shdr['format']} mips={shdr['num_mips']}")
check("mip 0 decodes straight out of the file",
      np.array_equal(ttc.decode_dxt1(stored[0x100:0x100 + 64 * 64 * 8], 256, 256), small))
check("stored costs exactly the mip chain",
      len(stored) == 0x100 + ttc.mip_chain_size(256, 256, smips, ttc.FORMAT_DXT1))

# A compressed file must still be recognised as compressed by the same reader.
if os.path.exists(ref_path):
    chdr, _, _ = ttc.read_ttc(open(ref_path, 'rb').read())
    check("a real GeoConvert tile is still read as compressed",
          chdr['stored'] is False and chdr['chunk'] is not None,
          f"ratio {chdr['size_uncompressed'] / chdr['size_compressed']:.3f}x")

# ---------------------------------------------------------------------------
# Connected bodies of water. The water fix levels each body onto the tone of its
# own cleanest part, so a lake that crosses a square border keeps ONE tone and a
# lake and the sea keep different ones. See water_bodies.py and water_fix.py.
# ---------------------------------------------------------------------------

print("\n" + "=" * 74)
print("6. BODIES OF WATER  -  labelling, with numpy alone")
print("=" * 74)

import water_bodies


def body_sizes(labels, n):
    return sorted((int((labels == i).sum()) for i in range(1, n + 1)), reverse=True)


m = np.zeros((20, 20), bool)
m[2:8, 2:8] = True
m[12:18, 12:18] = True
lab, n = water_bodies.bodies(m, open_by=0)
check("two separate blobs are two bodies", n == 2, str(body_sizes(lab, n)))

m = np.zeros((20, 20), bool)
m[2:18, 2:6] = True
m[2:18, 14:18] = True
m[14:18, 2:18] = True
lab, n = water_bodies.bodies(m, open_by=0)
check("a U is one body", n == 1, str(body_sizes(lab, n)))

m = np.zeros((10, 10), bool)
m[2:5, 2:5] = True
m[5:8, 5:8] = True
lab, n = water_bodies.bodies(m, open_by=0)
check("touching only at a corner is two bodies", n == 2,
      "4-connected, so a diagonal does not join them")

# The case this exists for: a river mouth must not weld a lake onto the sea.
m = np.zeros((30, 40), bool)
m[5:25, 3:15] = True
m[5:25, 25:37] = True
m[14:15, 15:25] = True
lab, n = water_bodies.bodies(m, open_by=0)
check("a one-texel channel joins them if nothing is opened", n == 1)
lab, n = water_bodies.bodies(m, open_by=2)
check("opening by two texels separates them", n == 2, str(body_sizes(lab, n)))
check("and every water texel still belongs to a body",
      not (m & (lab == 0)).any(),
      "the channel is filled from both ends rather than orphaned")

m = np.zeros((30, 40), bool)
m[5:25, 3:15] = True
m[5:25, 25:37] = True
m[10:20, 15:25] = True
lab, n = water_bodies.bodies(m, open_by=2)
check("a wide strait still leaves one body", n == 1, str(body_sizes(lab, n)))

lab, n = water_bodies.bodies(np.zeros((12, 12), bool), open_by=2)
check("an empty mask has no bodies", n == 0)
lab, n = water_bodies.bodies(np.ones((12, 12), bool), open_by=2)
check("an all-water mask is one body", n == 1 and not (lab == 0).any())

print("\n" + "=" * 74)
print(f"RESULT:  {passed} passed, {failed} failed")
print("=" * 74)
sys.exit(1 if failed else 0)
