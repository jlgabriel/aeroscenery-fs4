"""
Build the Chilean landmark tiles from GeoNames, and install them.

    python build_chile.py --data <folder with CL.txt AR.txt PE.txt BO.txt>
    python build_chile.py --data <folder> --install

Get the data from https://download.geonames.org/export/dump/ -- CL.zip and, for the reason below,
AR.zip, PE.zip and BO.zip. The format is one tab-separated row per place:

    0 geonameid  1 name  2 asciiname  3 alternatenames  4 latitude  5 longitude
    6 feature_class  7 feature_code  ...  14 population  15 elevation  16 dem  ...

Why the neighbours are needed
-----------------------------
A level-7 tile is 2.8 degrees of longitude wide, so the eastern Chilean columns reach well into
Argentina, Peru and Bolivia. **We can write a .tft but not read one** -- IPACS compress theirs
with LZHAM -- so a rebuilt tile loses every label IPACS had put in it. Rebuilding the Santiago
tile is what dropped `Santiago` and `Puente Alto`. Any tile we touch has to carry everything that
belongs in it, not only the Chilean half.

Choices made here
-----------------
- **Cities and comunas only.** The first build took all 6,934 populated places and flying it was
  a mess -- 885 labels over Santiago, most of them farms and historical sections, and 13 places
  called `San Miguel` so the label on the horizon was rarely the comuna. See the note by
  ADM_CODE.
- **Elevation comes from `dem`**, filled for 6,961 of the 6,967 Chilean places, where the
  `elevation` field is filled for 30. Plus a margin, because `height` is above sea level and a
  label at ground level can sink into terrain the simulator models differently.
- **`asciiname`, not `name`.** Whether the label font and the string8 field carry accented UTF-8
  is not known yet, and a whole country is the wrong place to find out. The Santiago tile carries
  three test labels that answer it; once they are read, switch ACCENTS to True.
- **type 0 and priority 0 for everything.** What those two change has not been measured, and a
  wrong priority might silently drop labels.
"""

import argparse
import glob
import shutil
import collections
import math
import os
import sys

import tft

# The game install. Set AEROFLY_FS4_DIR if it is not in the default Steam library.
GAME = os.environ.get("AEROFLY_FS4_DIR",
                      r"C:\Program Files (x86)\Steam\steamapps\common\Aerofly FS 4 Flight Simulator")
USER = os.path.join(os.path.expanduser("~"), "Documents", "Aerofly FS 4")
LANDMARKS = os.path.join(USER, "scenery", "landmarks")

LEVEL = 7
K = 2.3311223704144          # the AFS2 world grid constant, see AFS2Grid.cs

# Cities and comunas, nothing else.
#
# The first build took every populated place, all 6,934, and flying it was a mess: 885 labels in
# the Santiago tile, most of them farms and historical sections. Worse, GeoNames holds 13 places
# called `San Miguel` and 9 called `Cerrillos`, all real hamlets with no population, so the label
# on the horizon was rarely the comuna of that name.
#
# So: **class A / ADM3 gives the 346 comunas of Chile exactly**, and it is the only source here
# that has Las Condes, Vitacura, Penalolen and La Florida, which GeoNames files as PPLX with no
# population. Cities and towns come from class P, but only where a population is recorded -- that
# is what separates a town from a farm.
ADM_CODE = 'ADM3'            # in Chile, a comuna
CAPITALS = {'PPLC', 'PPLA', 'PPLA2', 'PPLA3'}
POPULATED = {'PPL', 'PPLA', 'PPLA2', 'PPLA3', 'PPLC', 'PPLL', 'PPLX'}
DEDUPE_KM = 8                # a city and its comuna, same name, this close: keep one

HEIGHT_MARGIN = 50           # metres above the DEM, so a label clears ground the sim models higher
ACCENTS = False              # see the note above


def cell(lat, lon, level=LEVEL):
    n = 2 ** level
    return (math.floor(n * (0.5 + 0.5 * (lon / 180.0))),
            math.floor(n * (0.5 + 0.5 * (math.tan(K * (lat / 180.0)) / K))))


def tile_name(x, y, level=LEVEL):
    step = 65536 // 2 ** level
    return "lm_%02d_%04x_%04x" % (level, x * step, y * step)


def _height(f):
    # `elevation` is filled for 30 Chilean places, `dem` for 6,961. Where dem is -9999, its
    # no-data value, fall back to sea level rather than dropping the place: all six Chilean
    # cases are on the coast, and one of them is Punta Arenas.
    for field in (f[15], f[16]):
        try:
            v = int(field)
        except ValueError:
            continue
        if -400 <= v <= 7000:
            return v
    return 0


def load(path, comunas=True):
    """Cities and towns that have a population, plus comunas, plus every capital."""
    out = []
    for line in open(path, encoding='utf-8'):
        f = line.rstrip('\n').split('\t')
        if len(f) < 19:
            continue
        pop = int(f[14]) if f[14].lstrip('-').isdigit() else 0
        if f[6] == 'A':
            if not comunas or f[7] != ADM_CODE:
                continue
            # GeoNames writes some of them 'Comuna de X'
            name = f[2]
            for prefix in ('Comuna de ', 'Comuna '):
                if name.startswith(prefix):
                    name = name[len(prefix):]
            out.append({'name': name.strip(), 'lat': float(f[4]), 'lon': float(f[5]),
                        'height': _height(f) + HEIGHT_MARGIN, 'pop': pop, 'adm': True})
            continue
        if f[6] != 'P' or f[7] not in POPULATED:
            continue
        if pop <= 0 and f[7] not in CAPITALS:
            continue                       # no population and not a capital: a farm or a hamlet
        out.append({'name': (f[1] if ACCENTS else f[2]).strip(),
                    'lat': float(f[4]), 'lon': float(f[5]),
                    'height': _height(f) + HEIGHT_MARGIN, 'pop': pop, 'adm': False})
    return dedupe(out)


def dedupe(places):
    """A city and the comuna named after it are one label, not two."""
    out = []
    by_name = collections.defaultdict(list)
    # Population first, so the city's own coordinates win over the comuna centroid.
    for p in sorted(places, key=lambda p: (-p['pop'], p['adm'])):
        near = False
        for q in by_name[p['name'].lower()]:
            dlat = (p['lat'] - q['lat']) * 111.32
            dlon = (p['lon'] - q['lon']) * 111.32 * math.cos(math.radians(p['lat']))
            if math.hypot(dlat, dlon) < DEDUPE_KM:
                near = True
                break
        if not near:
            by_name[p['name'].lower()].append(p)
            out.append(p)
    return out


# Three labels over Santiago that answer the accent question on the next flight: whether string8
# carries UTF-8, whether string8u does, and whether latin-1 does. Whichever renders correctly says
# which encoding to use; then set ACCENTS = True and rebuild.
ACCENT_TESTS = [
    ('ACC-U8',   '<[string8][name][ACC-U8 \u00d1u\u00f1oa Vicu\u00f1a]>',   'utf-8'),
    ('ACC-8U',   '<[string8u][name][ACC-8U \u00d1u\u00f1oa Vicu\u00f1a]>',  'utf-8'),
    ('ACC-L1',   '<[string8][name][ACC-L1 \u00d1u\u00f1oa Vicu\u00f1a]>',   'latin-1'),
]


def landmark_lines(p, index):
    return [
        '            <[tmterrain_landmark][element][%d]' % index,
        '                <[string8][name][%s]>' % p['name'],
        '                <[vector2_float64][lon_lat][%.6f %.6f]>' % (p['lon'], p['lat']),
        '                <[uint32][type][0]>',
        '                <[uint32][priority][0]>',
        '                <[float64][height][%g]>' % p['height'],
        '            >',
    ]


def build_tile(places, accent_tests=False):
    out = ['<[file][][]',
           '    <[tmterrain_landmark_list][][]',
           '        <[list_tmterrain_landmark][landmarks][]']
    i = 0
    for p in places:
        out += landmark_lines(p, i)
        i += 1

    chunks = ['\n'.join(out).encode('ascii', 'replace')]
    if accent_tests:
        extra = []
        for k, (label, line, enc) in enumerate(ACCENT_TESTS):
            extra += [
                '            <[tmterrain_landmark][element][%d]' % (i + k),
                '                ' + line,
                '                <[vector2_float64][lon_lat][%.6f %.6f]>' % (-70.95, -33.35 - 0.04 * k),
                '                <[uint32][type][0]>',
                '                <[uint32][priority][0]>',
                '                <[float64][height][600]>',
                '            >',
            ]
        # each test line is encoded the way it is meant to be tested
        blob = b''
        for line in extra:
            enc = 'utf-8'
            for _, probe_line, probe_enc in ACCENT_TESTS:
                if probe_line in line:
                    enc = probe_enc
            blob += line.encode(enc, 'replace') + b'\n'
        chunks.append(b'\n' + blob.rstrip(b'\n'))

    chunks.append('\n        >\n    >\n>\n'.encode('ascii'))
    return tft.build_deflate(b''.join(chunks), zlib_header=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--data', required=True, help='folder holding CL.txt and the neighbours')
    ap.add_argument('--install', action='store_true')
    a = ap.parse_args()

    chile = load(os.path.join(a.data, 'CL.txt'))
    print("Chile: %d places" % len(chile))

    # Which tiles Chile needs. Only these are touched.
    wanted = {cell(p['lat'], p['lon']) for p in chile}
    print("tiles: %d" % len(wanted))

    by_tile = collections.defaultdict(list)
    for p in chile:
        by_tile[cell(p['lat'], p['lon'])].append(p)

    for code in ('AR', 'PE', 'BO'):
        path = os.path.join(a.data, code + '.txt')
        if not os.path.exists(path):
            print("  no %s.txt -- shared tiles will lose that country's labels" % code)
            continue
        n = 0
        for p in load(path, comunas=False):
            c = cell(p['lat'], p['lon'])
            if c in wanted:
                by_tile[c].append(p)
                n += 1
        print("  %s: %d places fall in those tiles" % (code, n))

    santiago = cell(-33.45, -70.66)
    total = 0
    written = []
    for c in sorted(by_tile):
        places = sorted(by_tile[c], key=lambda p: p['name'])
        data = build_tile(places, accent_tests=(c == santiago))
        written.append((tile_name(*c), len(places), len(data), data))
        total += len(places)

    print("\n%-18s %7s %8s" % ("tile", "places", "bytes"))
    for name, n, size, _ in written:
        print("%-18s %7d %8d" % (name, n, size))
    print("%-18s %7d %8d" % ("TOTAL", total, sum(s for _, _, s, _ in written)))

    if not a.install:
        print("\n(dry run -- pass --install to write them)")
        return

    if not os.path.isdir(LANDMARKS):
        sys.exit("%s does not exist. Run probe.ps1 -Populate first." % LANDMARKS)

    # A tile we wrote on an earlier run that this run no longer covers would otherwise be left
    # behind holding stale data. Ours are recognisable: IPACS payloads open with a tmcompress
    # chunk, ours with a zlib header.
    keep = {name + '.tft' for name, _, _, _ in written}
    reverted = removed = 0
    for path in glob.glob(os.path.join(LANDMARKS, "*.tft")):
        base = os.path.basename(path)
        if base in keep:
            continue
        head = open(path, 'rb').read(0x102)[0x100:]
        if head not in (b'\x78\xda', b'\x78\x9c', b'\x78\x01'):
            continue                                    # not ours, leave it alone
        original = os.path.join(GAME, "scenery", "landmarks", base)
        if os.path.exists(original):
            shutil.copyfile(original, path)
            reverted += 1
        else:
            os.remove(path)
            removed += 1
    if reverted or removed:
        print("stale tiles from an earlier run: %d put back from the game, %d removed"
              % (reverted, removed))

    for name, n, size, data in written:
        open(os.path.join(LANDMARKS, name + '.tft'), 'wb').write(data)
    print("\ninstalled %d tiles into %s" % (len(written), LANDMARKS))

    log = os.path.join(USER, "tm.log")
    if os.path.exists(log):
        backup = log + ".before-chile"
        if os.path.exists(backup):
            os.remove(backup)
        os.rename(log, backup)
    print("Restart the simulator.")


if __name__ == '__main__':
    main()
