"""
Reader and writer for the .tft container Aerofly FS 4 uses for landmarks.

Derived by reading real files and the executable's own strings. Every constant here was checked
against the 2,229 files IPACS ship, not guessed.

What a .tft is
--------------
Not a landmark file. A generic **tmfile_properties_compressed** container holding one payload.
The executable names its five fields, and they line up one for one with the nodes in the file:

    SizeUncompressed      uint64
    SizeCompressed        uint64
    ChecksumUncompressed  uint32
    ChecksumCompressed    uint32
    Compressed            blob

Node layout
-----------
Each node is:

    +0x00  8 bytes  type hash
    +0x08  8 bytes  name hash
    +0x10  u64      size, this node's header and value together
    +0x18  u64      0x20 for a container (its children start there),
                    else the size rounded up to 8
    +0x20  value

Nodes are laid out back to back, each starting on an 8-byte boundary.

The hashes are **FNV-1a 64 over the name plus its NUL terminator**, stored little-endian. Verified
against all five field names above, five for five. The type hashes are kept as constants because
the names behind them have not been identified; they never vary.

The checksums are **plain CRC32**. Verified on the payload of all 2,229 files, 2,229 for 2,229.

Compressed or stored
--------------------
The payload of `Compressed` is a tmcompress chunk: a 0x40-byte header, then an LZHAM stream. It
is the same chunk a .ttc carries, and the same magic numbers. All 2,229 shipped files are
compressed; not one is stored.

Whether the engine reads a stored one is **not known**. It does for a .ttc, which is why
build_stored() exists: it writes the payload straight into `Compressed` with
SizeCompressed == SizeUncompressed and no chunk. If the engine rejects that, tm.log says so --
either `checksums don't match` or an lzham complaint -- which is what makes the experiment worth
running.
"""

import struct
import zlib

HEADER = 0x20                     # bytes before a node's value
FNV_OFFSET = 0xcbf29ce484222325
FNV_PRIME  = 0x100000001b3
M64 = (1 << 64) - 1

# Type hashes, copied from the shipped files. The names behind them are not known; they are
# constant across every .tft, and across the .tms files the simulator writes itself.
TYPE_CONTAINER = bytes.fromhex('b5fe24c7b73cebb7')
TYPE_U64       = bytes.fromhex('bc628ed22a7082f3')
TYPE_U32       = bytes.fromhex('9e8caf8498703ec2')
TYPE_BLOB      = bytes.fromhex('9e76560202020202')

# The root node's name hash. Every .tft and .tms opens with it.
ROOT_NAME = bytes.fromhex('c3e386cb206d5fec')


def name_hash(name):
    """FNV-1a 64 over the name and its NUL terminator, little-endian, as stored in the file."""
    h = FNV_OFFSET
    for c in name.encode('ascii') + b'\0':
        h = ((h ^ c) * FNV_PRIME) & M64
    return h.to_bytes(8, 'little')


def align8(n):
    return (n + 7) & ~7


# --------------------------------------------------------------------------
# reading
# --------------------------------------------------------------------------

def read(data):
    """Pull the five fields out of a .tft. Returns a dict, with `payload` as raw bytes."""
    out = {}
    def walk(off, end):
        while off < end - HEADER:
            type_hash = data[off:off + 8]
            name_h    = data[off + 8:off + 16]
            size, second = struct.unpack_from('<QQ', data, off + 16)
            if size < HEADER or off + size > len(data):
                return
            if second == HEADER and size > HEADER + 8 and type_hash == TYPE_CONTAINER:
                walk(off + HEADER, off + size)
            else:
                value = data[off + HEADER:off + size]
                for field in ('SizeUncompressed', 'SizeCompressed',
                              'ChecksumUncompressed', 'ChecksumCompressed', 'Compressed'):
                    if name_h == name_hash(field):
                        if type_hash == TYPE_U64:
                            out[field] = struct.unpack('<Q', value[:8])[0]
                        elif type_hash == TYPE_U32:
                            out[field] = struct.unpack('<I', value[:4])[0]
                        else:
                            out[field] = value
            off += align8(size)
    walk(0, len(data))
    out['payload'] = out.pop('Compressed', b'')
    return out


# --------------------------------------------------------------------------
# writing
# --------------------------------------------------------------------------

def _leaf(type_hash, name, value):
    size = HEADER + len(value)
    node = type_hash + name_hash(name) + struct.pack('<QQ', size, align8(size)) + value
    return node + b'\0' * (align8(size) - size)


def build(payload, size_uncompressed, checksum_uncompressed):
    """Assemble a .tft around an already-formed payload.

    `payload` goes into the Compressed field verbatim. For a normal file that is a tmcompress
    chunk; for a stored one it is the content itself, and then the two sizes and the two
    checksums are equal.
    """
    body = b''.join([
        _leaf(TYPE_U64,  'SizeUncompressed',     struct.pack('<Q', size_uncompressed)),
        _leaf(TYPE_U64,  'SizeCompressed',       struct.pack('<Q', len(payload))),
        _leaf(TYPE_U32,  'ChecksumUncompressed', struct.pack('<I', checksum_uncompressed)),
        _leaf(TYPE_U32,  'ChecksumCompressed',   struct.pack('<I', zlib.crc32(payload) & 0xffffffff)),
        _leaf(TYPE_BLOB, 'Compressed',           payload),
    ])
    obj  = TYPE_CONTAINER + name_hash('object') + struct.pack('<QQ', HEADER + len(body), HEADER) + body
    root = TYPE_CONTAINER + ROOT_NAME + struct.pack('<QQ', HEADER + len(obj), HEADER) + obj
    return root


def build_stored(content):
    """Write a .tft whose payload is NOT compressed: no chunk, both sizes equal.

    REFUSED by the engine. Flown 2026-08-27: tm.log answered
    `tmcompress: ERROR: tinfl_decompress() failed with status -1!`, over and over. The container
    was read -- it got as far as decompressing -- but a stored payload is not a thing here, unlike
    in a .ttc. Kept because that error is what identified the fallback codec.
    """
    crc = zlib.crc32(content) & 0xffffffff
    return build(content, len(content), crc)


def build_deflate(content, zlib_header=False):
    """Write a .tft whose payload is DEFLATE, with no tmcompress chunk.

    `tinfl_decompress` is miniz, so tmcompress falls back to inflate when the blob does not open
    with a tmcompress chunk. That is the way in: IPACS compress their own files with LZHAM, which
    we cannot write, but we do not have to -- we can hand the engine deflate instead.

    Which wrapping miniz expects is the open question. raw (no header) is the default in miniz and
    is tried first; zlib_header=True is the other candidate.
    """
    c = zlib.compressobj(9, zlib.DEFLATED, 15 if zlib_header else -15)
    payload = c.compress(content) + c.flush()
    return build(payload, len(content), zlib.crc32(content) & 0xffffffff)


# --------------------------------------------------------------------------
# self-test: rebuild every shipped file from its own payload
# --------------------------------------------------------------------------

if __name__ == '__main__':
    import glob, os, sys
    game = os.environ.get("AEROFLY_FS4_DIR", r"C:\Program Files (x86)\Steam\steamapps\common\Aerofly FS 4 Flight Simulator")
    folder = sys.argv[1] if len(sys.argv) > 1 else os.path.join(game, "scenery", "landmarks")
    files = sorted(glob.glob(os.path.join(folder, "*.tft")))
    if not files:
        sys.exit("no .tft files in " + folder)

    same = 0
    bad_crc = 0
    for path in files:
        original = open(path, 'rb').read()
        f = read(original)
        if (zlib.crc32(f['payload']) & 0xffffffff) != f['ChecksumCompressed']:
            bad_crc += 1
        rebuilt = build(f['payload'], f['SizeUncompressed'], f['ChecksumUncompressed'])
        if rebuilt == original:
            same += 1
        elif same + 1 == len(files):
            print("DIFF", os.path.basename(path))

    print("files                              : %d" % len(files))
    print("rebuilt byte for byte from payload : %d" % same)
    print("ChecksumCompressed is CRC32        : %d" % (len(files) - bad_crc))
    print("PASS" if same == len(files) and bad_crc == 0 else "FAIL")
