"""
Write .tft landmark files we compress ourselves, install them, and read the verdict from tm.log.

    python write_probe.py --install
    python write_probe.py --read
    python write_probe.py --restore

How it got here
---------------
Round 1: plain text in the file. Silence, and no labels -- a .tft is a container and the text had
no container around it.

Round 2: the text inside a correct container, stored rather than compressed, the trick that
removed LZHAM from the .ttc write path. Refused, and the refusal named the way in:

    tmcompress: ERROR:  tinfl_decompress() failed with status -1!

tinfl_decompress is miniz. So the container was read, a stored payload is not allowed here, and
tmcompress falls back to inflate when the blob does not open with a tmcompress chunk. IPACS
compress with LZHAM, which we cannot write; we do not have to.

Round 3: deflate, raw in two files and zlib-wrapped in one. Inflate failures fell from two per
tick to one, and the zlib-wrapped file got all the way through the parser:

    tmfile_properties: WARNING: property 'Landmarks' is not a member of
    type 'tmterrain_landmark_list'  hash=15279641139067209878

That one line settled four things. **Deflate must carry a zlib header.** The payload really is
**tm text**. The parser knows `tmterrain_landmark_list`. And the list field is `landmarks`, not
`Landmarks` -- which the executable had already said.

It also handed over an oracle: the parser names every property it does not recognise, and the
hash it prints is our own FNV-1a, confirmed exactly. So field names need not be guessed one per
flight. Offer many; whatever draws no warning is real.

Round 4, this one
-----------------
A sweep. Each landmark carries one group of candidate field names plus the best guess for the
others, so a group that guesses right also draws its label. Element 5 is the control: `name` and
`lon_lat` alone. If that one appears in the sky, the format is finished.
"""

import argparse
import os
import re
import shutil
import sys
import zlib

import tft

# The game install. Set AEROFLY_FS4_DIR if it is not in the default Steam library.
GAME = os.environ.get("AEROFLY_FS4_DIR",
                      r"C:\Program Files (x86)\Steam\steamapps\common\Aerofly FS 4 Flight Simulator")
USER = os.path.join(os.path.expanduser("~"), "Documents", "Aerofly FS 4")
LANDMARKS = os.path.join(USER, "scenery", "landmarks")
LOG = os.path.join(USER, "tm.log")

SANTIAGO = 'lm_07_4c00_6600.tft'
TALCA    = 'lm_07_4c00_6400.tft'
SERENA   = 'lm_07_4c00_6800.tft'

LON = -70.70

# The best guess for each role, used to fill in whatever a sweep is not sweeping.
BEST_NAME = '<[string8][name][{label}]>'
BEST_POS  = '<[vector2_float64][lon_lat][{lon} {lat}]>'

NAME_CANDIDATES_LOWER = ['name', 'text', 'label', 'title', 'id', 'place', 'city',
                         'description', 'caption', 'string']
NAME_CANDIDATES_UPPER = ['Name', 'Text', 'Label', 'Title', 'Place', 'City', 'Description']
POS_CANDIDATES        = ['lon_lat', 'position', 'pos', 'location', 'coordinates',
                         'lonlat', 'LonLat', 'Position', 'lat_lon', 'geo_position']
UINT_CANDIDATES       = ['type', 'class', 'category', 'kind', 'level', 'priority',
                         'importance', 'rank', 'color', 'flags', 'population', 'index']
FLOAT_CANDIDATES      = ['size', 'scale', 'height', 'altitude', 'elevation', 'radius',
                         'font_size', 'distance']


def build_santiago():
    """Six landmarks, each sweeping one group, spread north to south over the city."""
    return [
        # candidate label field names, lower case, with the best-guess position
        ('SW-LOWER', -33.30,
         ['<[string8][%s][SW-LOWER]>' % c for c in NAME_CANDIDATES_LOWER] + [BEST_POS]),
        # the same in PascalCase
        ('SW-UPPER', -33.37,
         ['<[string8][%s][SW-UPPER]>' % c for c in NAME_CANDIDATES_UPPER] + [BEST_POS]),
        # candidate position field names, with the best-guess label
        ('SW-POS', -33.44,
         [BEST_NAME] + ['<[vector2_float64][%s][{lon} {lat}]>' % c for c in POS_CANDIDATES]),
        # candidate integer attributes
        ('SW-UINT', -33.51,
         [BEST_NAME, BEST_POS] + ['<[uint32][%s][0]>' % c for c in UINT_CANDIDATES]),
        # candidate float attributes
        ('SW-FLOAT', -33.58,
         [BEST_NAME, BEST_POS] + ['<[float64][%s][1]>' % c for c in FLOAT_CANDIDATES]),
        # the control: nothing but the two best guesses
        ('TEST-CTRL', -33.65, [BEST_NAME, BEST_POS]),
    ]


def make_text(items, list_name='landmarks', extra=None):
    out = ['<[file][][]', '    <[tmterrain_landmark_list][][]']
    if extra:
        out.append('        ' + extra)
    out.append('        <[list_tmterrain_landmark][%s][]' % list_name)
    for i, (label, lat, fields) in enumerate(items):
        out.append('            <[tmterrain_landmark][element][%d]' % i)
        for f in fields:
            out.append('                ' + f.format(lon=LON, lat=lat, label=label))
        out.append('            >')
    out += ['        >', '    >', '>', '']
    return '\n'.join(out).encode('ascii')


def build_files():
    return {
        SANTIAGO: make_text(build_santiago()),
        # `extra` is a member of the list according to the executable. If it is not, the parser
        # will say so, and asking costs nothing.
        TALCA:    make_text([('TEST-TALCA', -35.43, [BEST_NAME, BEST_POS])],
                            extra='<[uint32][extra][0]>'),
        SERENA:   make_text([('TEST-SERENA', -30.03, [BEST_NAME, BEST_POS])]),
    }


def install():
    if not os.path.isdir(LANDMARKS):
        sys.exit("%s does not exist. Run probe.ps1 -Populate first." % LANDMARKS)

    for name, content in build_files().items():
        # zlib header, settled in round 3: the raw-deflate files kept failing to inflate and the
        # zlib-wrapped one parsed.
        data = tft.build_deflate(content, zlib_header=True)

        got = tft.read(data)
        assert got['SizeUncompressed'] == len(content), name
        assert got['SizeCompressed'] == len(got['payload']), name
        assert got['ChecksumUncompressed'] == zlib.crc32(content) & 0xffffffff, name
        assert got['ChecksumCompressed'] == zlib.crc32(got['payload']) & 0xffffffff, name
        assert zlib.decompressobj(15).decompress(got['payload']) == content, name

        open(os.path.join(LANDMARKS, name), 'wb').write(data)
        print("wrote %-22s container=%4d  deflate=%4d  text=%4d, verified by inflating" %
              (name, len(data), len(got['payload']), len(content)))

    if os.path.exists(LOG):
        backup = LOG + ".before-sweep"
        if os.path.exists(backup):
            os.remove(backup)
        os.rename(LOG, backup)
        print("moved the old tm.log aside")
    print("\nRestart the simulator, fly near Santiago, then: python write_probe.py --read")


def read():
    if not os.path.exists(LOG):
        sys.exit("no tm.log yet")
    lines = open(LOG, encoding='utf-8', errors='replace').read().splitlines()

    inflate = [l for l in lines if 'tinfl_decompress' in l]
    print("inflate failures: %d" % len(inflate))
    if inflate:
        print("  -> a file still did not inflate. A tick carries one line per failing file.")

    warn = {}
    for l in lines:
        m = re.search(r"property '([^']+)' is not a member of type '([^']+)'", l)
        if m:
            warn.setdefault(m.group(2), set()).add(m.group(1))

    print("\n=== properties the parser REJECTED ===")
    if not warn:
        print("  (none)")
    for type_name, props in sorted(warn.items()):
        print("  %s:" % type_name)
        for p in sorted(props):
            print("      %s" % p)

    offered = [
        ('label, lower case', NAME_CANDIDATES_LOWER),
        ('label, PascalCase', NAME_CANDIDATES_UPPER),
        ('position',          POS_CANDIDATES),
        ('uint32 attribute',  UINT_CANDIDATES),
        ('float64 attribute', FLOAT_CANDIDATES),
    ]
    rejected = set()
    for props in warn.values():
        rejected |= props

    print("\n=== the answer: offered but NOT rejected, so real ===")
    for group, names in offered:
        good = [n for n in names if n not in rejected]
        print("  %-19s %s" % (group + ':', ', '.join(good) if good else '(all rejected)'))
    print("\n  A group showing every name it was offered means the file never reached the parser.")
    print("  Check the inflate count above before believing it.")


def restore():
    for name in build_files():
        src = os.path.join(GAME, "scenery", "landmarks", name)
        dst = os.path.join(LANDMARKS, name)
        if os.path.exists(src):
            shutil.copyfile(src, dst)
            print("restored from the game: " + name)
        elif os.path.exists(dst):
            os.remove(dst)
            print("removed: " + name)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--install', action='store_true')
    ap.add_argument('--read', action='store_true')
    ap.add_argument('--restore', action='store_true')
    a = ap.parse_args()
    if a.install:
        install()
    elif a.read:
        read()
    elif a.restore:
        restore()
    else:
        ap.print_help()
