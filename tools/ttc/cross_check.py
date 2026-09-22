"""
Prints SHA-256 hashes of the Python reference over a fixed test pattern, for comparison with
the C# port.

    python tools/ttc/cross_check.py

The C# validation suite (tools/ttc/csharp) prints the same list. Every hash must match, which
is a much stronger statement than "both look reasonable": a BC1 encoder can differ from its
reference in tie-breaking, rounding or padding and still produce perfectly good-looking tiles
with a healthy PSNR. Those differences are invisible to a quality metric and would mean the
Python suite no longer validates what the app actually ships.

The pattern is generated with pure integer arithmetic in both languages - a numpy RNG could not
be reproduced in C#, and a single content type would leave encoder paths untested. Its four
quadrants are a smooth gradient, a flat colour off the RGB565 lattice, incompressible noise and
hard block edges.
"""

import hashlib
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ttc

M32 = 0xFFFFFFFF


def test_pattern(n=256):
    """Mirror of Validate.TestPattern. Must stay identical to the C# version, byte for byte."""
    img = np.zeros((n, n, 3), np.uint8)
    half = n // 2
    d = max(1, half - 1)

    # top-left: smooth gradient
    ys, xs = np.mgrid[0:half, 0:half]
    r = (xs * 255) // d
    g = (ys * 255) // d
    img[:half, :half, 0] = r
    img[:half, :half, 1] = g
    img[:half, :half, 2] = (r + g) // 2

    # top-right: flat, deliberately not on the RGB565 lattice
    img[:half, half:] = (37, 121, 200)

    # bottom-left: integer-hash noise, wrapped to 32 bits exactly as C# does
    ys, xs = np.mgrid[half:n, 0:half]
    h = (xs.astype(np.int64) * 374761393 + ys.astype(np.int64) * 668265263) & M32
    h = ((h ^ (h >> 13)) * 1274126177) & M32
    h ^= h >> 16
    img[half:, :half, 0] = h & 255
    img[half:, :half, 1] = (h >> 8) & 255
    img[half:, :half, 2] = (h >> 16) & 255

    # bottom-right: 8-pixel checkerboard, hard edges on block boundaries
    ys, xs = np.mgrid[half:n, half:n]
    odd = (((xs // 8) + (ys // 8)) & 1).astype(bool)
    img[half:, half:, 0] = 255
    img[half:, half:, 1] = np.where(odd, 0, 255)
    img[half:, half:, 2] = np.where(odd, 255, 0)

    return img


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    print('=' * 74)
    print('CROSS-LANGUAGE  -  hashes to compare with the C# suite')
    print('=' * 74)
    print('  Run:  tools/ttc/csharp/run.ps1')
    print('  Every hash below must match the C# port exactly.\n')

    p = test_pattern(256)
    print(f'  pattern-256      {sha(p.tobytes())}')
    print(f'  bc1-256          {sha(ttc.encode_dxt1(p))}')
    print(f'  halve-256        {sha(ttc._halve(p).tobytes())}')

    chain, mips = ttc.build_mip_chain(p, ttc.FORMAT_DXT1)
    print(f'  mipchain-256     {sha(chain)}   ({mips} mips, {len(chain):,} bytes)')

    stored = ttc.build_ttc_stored(9, 256, 256, mips, ttc.FORMAT_DXT1, chain)
    print(f'  stored-ttc-256   {sha(stored)}   ({len(stored):,} bytes)')

    ys, xs = np.mgrid[0:256, 0:256]
    l8 = ((xs * 7 + ys * 13) & 0xFF).astype(np.uint8)
    lchain, lmips = ttc.build_mip_chain(l8, ttc.FORMAT_L8)
    print(f'  l8-mipchain-256  {sha(lchain)}   ({lmips} mips, {len(lchain):,} bytes)')


if __name__ == '__main__':
    main()
