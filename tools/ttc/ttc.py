"""
Reference reader/writer for Aerofly .ttc colormap tiles.

Derived by black-box analysis of GeoConvert 1.4.5 output. Every constant here was
read off real files, not guessed.

Layout
------
  0x000  u32  magic              0x0000303A
  0x004  u32  version            0x00000100
  0x008  u32  level
  0x00C  u32  size_compressed    bytes after the header (== chunk total)
  0x010  u32  size_uncompressed
  0x014  u32  width
  0x018  u32  height
  0x01C  u32  num_mips
  0x020  u32  format             0 = L8, 10 = DXT1/BC1, 0x12345671/2 = Basis Universal
  0x024  u32  unknown            FFFFFFFF colour / 00FFFFFF mask
  0x028  u32  unknown            FFFFFFFF colour / 00000000 mask
  0x02C..0x0FF  zero padding
  0x100  payload: a tmcompress chunk, or the raw mip chain (see below)

Compressed or stored
--------------------
The payload does not have to be compressed. Aerofly FS 4 loads a format 10 tile whose payload
is the raw DXT1 mip chain sitting straight at 0x100 with size_compressed == size_uncompressed
and no tmcompress chunk at all -- verified in the simulator, see docs/ttc-format.md.
That is what build_ttc_stored() writes, and it is why nothing here needs an LZHAM encoder.

IPACS ship both shapes themselves: of 205,490 .ttc files on a full install, 59,637 carry a
Basis Universal payload with no chunk (format 0x12345671) and 45,802 carry one wrapped in a
chunk (0x12345672). Every one of the 99,169 format 10 files they ship is compressed, but the
engine reads a stored one regardless.

tmcompress chunk
----------------
  +0x00  u32  header size        0x40  (== offset of payload)
  +0x04  u32  magic              0xA810BEF4
  +0x08  u64  size_uncompressed
  +0x10  u64  size_total         chunk header + payload
  +0x18  16 zero bytes
  +0x28  u64  magic2             0x17F34DF32797945C
  +0x30  u32  param_a            21
  +0x34  u32  param_b            20
  +0x38  8 zero bytes
  +0x40  LZHAM stream
"""

import struct
import numpy as np

TTC_MAGIC      = 0x0000303A
TTC_VERSION    = 0x00000100
TTC_HEADER_LEN = 0x100

FORMAT_L8   = 0
FORMAT_DXT1 = 10

# Sentinels, not texture types: the payload is a Basis Universal file rather than a raw mip
# chain. ...71 is stored, ...72 is wrapped in a tmcompress chunk. Read only, we do not write these.
FORMAT_BASIS_STORED = 0x12345671
FORMAT_BASIS_CHUNKED = 0x12345672

CHUNK_HEADER_LEN = 0x40
CHUNK_MAGIC1     = 0xA810BEF4
CHUNK_MAGIC2     = 0x17F34DF32797945C
CHUNK_PARAM_A    = 21
CHUNK_PARAM_B    = 20

BYTES_PER_BLOCK = {FORMAT_DXT1: 8}


# --------------------------------------------------------------------------
# size maths
# --------------------------------------------------------------------------

def mip_chain_size(width, height, num_mips, fmt):
    """Total bytes of a concatenated mip chain, no per-level headers."""
    total = 0
    for i in range(num_mips):
        w = max(1, width >> i)
        h = max(1, height >> i)
        if fmt == FORMAT_L8:
            total += w * h
        else:
            bpb = BYTES_PER_BLOCK[fmt]
            total += max(1, (w + 3) // 4) * max(1, (h + 3) // 4) * bpb
    return total


def full_mip_count(width, height):
    return max(width, height).bit_length()


# --------------------------------------------------------------------------
# container
# --------------------------------------------------------------------------

def build_chunk(payload, size_uncompressed):
    """Wrap an already-compressed payload in a tmcompress chunk."""
    total = CHUNK_HEADER_LEN + len(payload)
    h = bytearray(CHUNK_HEADER_LEN)
    struct.pack_into('<II', h, 0x00, CHUNK_HEADER_LEN, CHUNK_MAGIC1)
    struct.pack_into('<Q',  h, 0x08, size_uncompressed)
    struct.pack_into('<Q',  h, 0x10, total)
    struct.pack_into('<Q',  h, 0x28, CHUNK_MAGIC2)
    struct.pack_into('<II', h, 0x30, CHUNK_PARAM_A, CHUNK_PARAM_B)
    return bytes(h) + payload


def build_ttc(level, width, height, num_mips, fmt, chunk,
              size_uncompressed, unk24=0xFFFFFFFF, unk28=0xFFFFFFFF):
    """Assemble a complete .ttc from a finished tmcompress chunk."""
    h = bytearray(TTC_HEADER_LEN)
    struct.pack_into('<11I', h, 0,
                     TTC_MAGIC, TTC_VERSION, level,
                     len(chunk), size_uncompressed,
                     width, height, num_mips, fmt,
                     unk24, unk28)
    return bytes(h) + chunk


def build_ttc_stored(level, width, height, num_mips, fmt, chain,
                     unk24=0xFFFFFFFF, unk28=0xFFFFFFFF):
    """Assemble a .ttc whose payload is stored, not compressed.

    `chain` goes straight at 0x100 and both size fields carry its length, which is how the
    engine is told there is nothing to decompress. Aerofly FS 4 renders the result; see the
    module docstring.
    """
    h = bytearray(TTC_HEADER_LEN)
    struct.pack_into('<11I', h, 0,
                     TTC_MAGIC, TTC_VERSION, level,
                     len(chain), len(chain),
                     width, height, num_mips, fmt,
                     unk24, unk28)
    return bytes(h) + chain


def read_ttc(data):
    """Parse a .ttc into its header fields plus the payload.

    Returns (hdr, payload_at_0x100, codec_data). For a compressed file the second value is the
    whole tmcompress chunk and the third is the codec stream inside it; for a stored file the
    two are the same bytes and hdr['chunk'] is None.
    """
    f = struct.unpack_from('<11I', data, 0)
    if f[0] != TTC_MAGIC:
        raise ValueError(f'bad magic {f[0]:#x}')
    hdr = dict(zip(
        ['magic', 'version', 'level', 'size_compressed', 'size_uncompressed',
         'width', 'height', 'num_mips', 'format', 'unk24', 'unk28'], f))

    payload = data[TTC_HEADER_LEN:TTC_HEADER_LEN + hdr['size_compressed']]
    cmagic = struct.unpack_from('<I', payload, 4)[0] if len(payload) >= 8 else 0
    if cmagic != CHUNK_MAGIC1:
        # Stored. Do not treat this as an error: IPACS ship 59,637 such files themselves.
        hdr['chunk'] = None
        hdr['stored'] = True
        return hdr, payload, payload

    hdr['stored'] = False
    chdr_len = struct.unpack_from('<I', payload, 0)[0]
    c_unc, c_total = struct.unpack_from('<QQ', payload, 0x08)
    hdr['chunk'] = {
        'header_len': chdr_len,
        'size_uncompressed': c_unc,
        'size_total': c_total,
        'magic2': struct.unpack_from('<Q', payload, 0x28)[0],
        'params': struct.unpack_from('<II', payload, 0x30),
    }
    return hdr, payload, payload[chdr_len:]


# --------------------------------------------------------------------------
# BC1 / DXT1
# --------------------------------------------------------------------------

def _to565(c):
    return (((c[..., 0] >> 3) & 0x1F) << 11) | \
           (((c[..., 1] >> 2) & 0x3F) << 5) | \
           ((c[..., 2] >> 3) & 0x1F)


def _from565(v):
    r = (v >> 11) & 0x1F
    g = (v >> 5) & 0x3F
    b = v & 0x1F
    return np.stack([(r << 3) | (r >> 2),
                     (g << 2) | (g >> 4),
                     (b << 3) | (b >> 2)], axis=-1).astype(np.int32)


def _pad_to_4(img):
    h, w = img.shape[:2]
    ph, pw = (-h) % 4, (-w) % 4
    if ph or pw:
        img = np.pad(img, ((0, ph), (0, pw), (0, 0)), mode='edge')
    return img


def encode_dxt1(rgb):
    """Bounding-box range-fit BC1 encoder, always 4-colour (opaque) mode."""
    rgb = _pad_to_4(np.asarray(rgb, dtype=np.uint8))
    h, w = rgb.shape[:2]
    bh, bw = h // 4, w // 4

    blocks = (rgb.reshape(bh, 4, bw, 4, 3)
                 .transpose(0, 2, 1, 3, 4)
                 .reshape(-1, 16, 3).astype(np.int32))

    lo = blocks.min(axis=1)
    hi = blocks.max(axis=1)
    c0 = _to565(hi).astype(np.uint32)
    c1 = _to565(lo).astype(np.uint32)

    # 4-colour mode requires c0 > c1; if they collapse the block is flat anyway
    swap = c0 < c1
    c0, c1 = np.where(swap, c1, c0), np.where(swap, c0, c1)

    e0 = _from565(c0)
    e1 = _from565(c1)
    pal = np.stack([e0, e1, (2 * e0 + e1) // 3, (e0 + 2 * e1) // 3], axis=1)

    d = blocks[:, :, None, :] - pal[:, None, :, :]
    idx = (d * d).sum(axis=-1).argmin(axis=2).astype(np.uint32)

    bits = np.zeros(len(blocks), dtype=np.uint32)
    for i in range(16):
        bits |= (idx[:, i] & 3) << (2 * i)

    out = np.empty((len(blocks), 8), dtype=np.uint8)
    out[:, 0] = c0 & 0xFF
    out[:, 1] = (c0 >> 8) & 0xFF
    out[:, 2] = c1 & 0xFF
    out[:, 3] = (c1 >> 8) & 0xFF
    for i in range(4):
        out[:, 4 + i] = (bits >> (8 * i)) & 0xFF
    return out.tobytes()


def decode_dxt1(data, width, height):
    """Inverse of encode_dxt1, for quality checking."""
    bw, bh = max(1, (width + 3) // 4), max(1, (height + 3) // 4)
    b = np.frombuffer(data, dtype=np.uint8)[:bw * bh * 8].reshape(-1, 8)
    c0 = b[:, 0].astype(np.uint32) | (b[:, 1].astype(np.uint32) << 8)
    c1 = b[:, 2].astype(np.uint32) | (b[:, 3].astype(np.uint32) << 8)
    bits = sum(b[:, 4 + i].astype(np.uint32) << (8 * i) for i in range(4))

    e0, e1 = _from565(c0), _from565(c1)
    pal = np.stack([e0, e1, (2 * e0 + e1) // 3, (e0 + 2 * e1) // 3], axis=1)

    idx = np.stack([(bits >> (2 * i)) & 3 for i in range(16)], axis=1)
    px = np.take_along_axis(pal, idx[:, :, None], axis=1)

    img = (px.reshape(bh, bw, 4, 4, 3)
             .transpose(0, 2, 1, 3, 4)
             .reshape(bh * 4, bw * 4, 3).astype(np.uint8))
    return img[:height, :width]


# --------------------------------------------------------------------------
# mip chain
# --------------------------------------------------------------------------

def _halve(img):
    h, w = img.shape[:2]
    h2, w2 = max(1, h // 2), max(1, w // 2)
    if h < 2 or w < 2:
        return img[:h2, :w2]
    a = img[:h2 * 2, :w2 * 2].astype(np.uint16)
    return (((a[0::2, 0::2] + a[1::2, 0::2] + a[0::2, 1::2] + a[1::2, 1::2]) + 2) // 4
            ).astype(np.uint8)


def build_mip_chain(img, fmt, num_mips=None):
    """Concatenated mip chain, level 0 first, no per-level headers."""
    img = np.asarray(img)
    if img.ndim == 2:
        img = img[:, :, None]
    h, w = img.shape[:2]
    if num_mips is None:
        num_mips = full_mip_count(w, h)

    out = []
    cur = img
    for _ in range(num_mips):
        if fmt == FORMAT_L8:
            out.append(cur[:, :, 0].tobytes())
        else:
            out.append(encode_dxt1(cur))
        cur = _halve(cur)
    return b''.join(out), num_mips


def mip_offsets(width, height, num_mips, fmt):
    """Byte offset and length of every level inside the chain."""
    res, off = [], 0
    for i in range(num_mips):
        w, h = max(1, width >> i), max(1, height >> i)
        n = (w * h if fmt == FORMAT_L8
             else max(1, (w + 3) // 4) * max(1, (h + 3) // 4) * BYTES_PER_BLOCK[fmt])
        res.append((i, w, h, off, n))
        off += n
    return res


# --------------------------------------------------------------------------
# tile naming
# --------------------------------------------------------------------------

# The name carries a position in a fixed 2^16 x 2^16 world grid, not the tile index, so the
# multiplier shrinks as the level deepens: 128 at level 9, 16 at level 12, 4 at level 14.
# Verified against six levels of real GeoConvert output. Treating 128 as constant produces
# correct names at level 9 only, which is why it survived a level-9-only fixture set.
WORLD_GRID_BITS = 16


def tile_step(level):
    return 1 << (WORLD_GRID_BITS - level)


def tile_name(level, tile_x, tile_y, mask=False):
    s = tile_step(level)
    return (f'map_{level:02d}_{tile_x * s:04x}_'
            f'{tile_y * s:04x}{"_mask" if mask else ""}.ttc')


def parse_tile_name(name):
    stem = name.split('.')[0]
    mask = stem.endswith('_mask')
    if mask:
        stem = stem[:-5]
    _, level, hx, hy = stem.split('_')
    s = tile_step(int(level))
    return int(level), int(hx, 16) // s, int(hy, 16) // s, mask
