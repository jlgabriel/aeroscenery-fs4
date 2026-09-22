"""
Build a landmark .tft for Aerofly FS 4 from a list of places.

    python make_tft.py --demo       write a demo tile over Santiago and install it
    python make_tft.py --restore    put the game's own file back

The format, all of it
---------------------
A .tft is a tmfile_properties_compressed container (see tft.py) whose payload is tm TEXT,
compressed with deflate carrying a zlib header. The text is:

    <[file][][]
        <[tmterrain_landmark_list][][]
            <[list_tmterrain_landmark][landmarks][]
                <[tmterrain_landmark][element][0]
                    <[string8][name][Nunoa]>
                    <[vector2_float64][lon_lat][-70.598 -33.456]>
                    <[uint32][type][0]>
                    <[uint32][priority][0]>
                    <[float64][height][0]>
                >
                ...

A tmterrain_landmark has exactly five members: **name, lon_lat, type, priority, height**. That is
not a guess. 47 candidate names were offered to the parser in one flight and it named the 42 it
rejected; these five are what was left. `extra` is not a member of the list either, though the
executable mentions it.

**`height` is altitude above sea level, not above the ground.** Flown 2026-08-27: over Santiago,
which stands at about 520 m, the H1000 and H2000 markers hung in the sky, H600 sat at roof height,
and H0, H300 and H520 never appeared at all -- they were under the terrain. This is what had made
every earlier round draw nothing: the control left height at 0. A place therefore needs its own
elevation, plus enough margin to clear the ground it sits on.

**Coordinates are degrees, longitude first.** Same flight: the RAD marker (the same place in
radians) and the SWAP marker (the pair reversed) both failed to appear, while the degree markers
did.

What `type` and `priority` do is still unknown. Both draw. The engine has a
`landmark_color_table` and a `landmark_size_factor`, so they plausibly pick a colour and decide
how far away a label survives, but that has not been measured.

The file name decides when a tile is read, never where its labels land: the coordinates inside are
absolute. Use AFS2Grid at level 7 to name the file for the place it covers.
"""

import argparse
import os
import shutil
import sys

import tft

# The game install. Set AEROFLY_FS4_DIR if it is not in the default Steam library.
GAME = os.environ.get("AEROFLY_FS4_DIR",
                      r"C:\Program Files (x86)\Steam\steamapps\common\Aerofly FS 4 Flight Simulator")
USER = os.path.join(os.path.expanduser("~"), "Documents", "Aerofly FS 4")
LANDMARKS = os.path.join(USER, "scenery", "landmarks")


def build(places):
    """places: a list of dicts with name, lon, lat, and optionally type, priority, height."""
    out = ['<[file][][]',
           '    <[tmterrain_landmark_list][][]',
           '        <[list_tmterrain_landmark][landmarks][]']
    for i, p in enumerate(places):
        out += [
            '            <[tmterrain_landmark][element][%d]' % i,
            '                <[string8][name][%s]>' % p['name'],
            '                <[vector2_float64][lon_lat][%.6f %.6f]>' % (p['lon'], p['lat']),
            '                <[uint32][type][%d]>' % p.get('type', 0),
            '                <[uint32][priority][%d]>' % p.get('priority', 0),
            '                <[float64][height][%g]>' % p.get('height', 0),
            '            >',
        ]
    out += ['        >', '    >', '>', '']
    return tft.build_deflate('\n'.join(out).encode('ascii'), zlib_header=True)


# --------------------------------------------------------------------------
# demo: the Santiago tile, lat -34.15..-31.82, lon -73.12..-70.31
# --------------------------------------------------------------------------

# Places Aerofly does not label today, with their elevation in metres. Both the coordinates and
# the elevations are approximate, good to a few hundred metres of position and a few tens of
# metres of height -- enough to put a readable label over the right town. A real dataset wants a
# proper source; GeoNames carries both fields.
#                     lon        lat       elevation
COMUNAS = [
    ('Nunoa',        -70.598,  -33.456,  570),
    ('Providencia',  -70.610,  -33.425,  590),
    ('Las Condes',   -70.568,  -33.408,  700),
    ('Vitacura',     -70.575,  -33.380,  650),
    ('La Reina',     -70.535,  -33.443,  720),
    ('Penalolen',    -70.542,  -33.487,  700),
    ('Macul',        -70.598,  -33.487,  600),
    ('La Florida',   -70.586,  -33.535,  650),
    ('Maipu',        -70.758,  -33.510,  490),
    ('Pudahuel',     -70.775,  -33.442,  470),
    ('Renca',        -70.727,  -33.404,  480),
    ('Recoleta',     -70.645,  -33.410,  550),
    ('San Miguel',   -70.652,  -33.497,  530),
    ('Cerrillos',    -70.717,  -33.492,  500),
    ('Lo Barnechea', -70.518,  -33.352,  900),
    ('Colina',       -70.674,  -33.203,  570),
    ('Buin',         -70.742,  -33.733,  500),
    ('Melipilla',    -71.215,  -33.688,  170),
    ('San Antonio',  -71.621,  -33.593,   20),
    ('Casablanca',   -71.410,  -33.319,  200),
    ('Quillota',     -71.249,  -32.880,  130),
    ('Los Andes',    -70.598,  -32.834,  820),
    ('San Felipe',   -70.725,  -32.750,  640),
]

# Two lines of markers over the flat ground south of the city, away from every real label, to see
# what type and priority actually change. The ground there is about 400 m, so 500 clears it.
def probes():
    out = []
    for t in range(12):
        out.append({'name': 'T%02d' % t, 'lon': -70.75, 'lat': -33.78 - 0.02 * t,
                    'type': t, 'priority': 0, 'height': 500})
    for pr in range(8):
        out.append({'name': 'P%d' % pr, 'lon': -70.95, 'lat': -33.78 - 0.02 * pr,
                    'type': 0, 'priority': pr, 'height': 500})
    return out


# --------------------------------------------------------------------------
# diagnosis: the five field names are right and the file parses, but nothing draws
# --------------------------------------------------------------------------
#
# Three candidate reasons, and one flight separates them:
#
#   height   the control left it at 0, and Santiago stands at about 520 m. A label at zero is
#            under the ground. This is the likeliest.
#   radians  Aerofly works in radians internally. The mission file writes lon_lat in degrees, but
#            that is a different class.
#   order    lon_lat could be read latitude first despite the name.
#
# Every group other than the height sweep sits at height 600, so if height is the whole problem
# they all appear at once and say so.

import math

def diagnosis():
    out = []
    # A: height sweep, degrees, lon first. If one of these appears, read the number off the label.
    for i, h in enumerate([0, 300, 520, 600, 1000, 2000]):
        out.append({'name': 'H%d' % h, 'lon': -70.75, 'lat': -33.30 - 0.05 * i, 'height': h})
    # B: the same place in radians
    out.append({'name': 'RAD', 'lon': math.radians(-70.66), 'lat': math.radians(-33.45),
                'height': 600})
    # C: the same place with the pair the other way round
    out.append({'name': 'SWAP', 'lon': -33.45, 'lat': -70.66, 'height': 600})
    # D and E: type and priority, in case a zero in either means "do not draw"
    for t in range(1, 4):
        out.append({'name': 'T%d' % t, 'lon': -70.55, 'lat': -33.30 - 0.05 * t,
                    'type': t, 'height': 600})
    for p in range(1, 4):
        out.append({'name': 'P%d' % p, 'lon': -70.85, 'lat': -33.30 - 0.05 * p,
                    'priority': p, 'height': 600})
    out.append({'name': 'TP', 'lon': -70.45, 'lat': -33.40, 'type': 1, 'priority': 1,
                'height': 600})
    return out


def demo(diag=False):
    if not os.path.isdir(LANDMARKS):
        sys.exit("%s does not exist. Run probe.ps1 -Populate first." % LANDMARKS)

    if diag:
        places = diagnosis()
    else:
        places = [{'name': n, 'lon': lo, 'lat': la, 'height': elev}
                  for n, lo, la, elev in COMUNAS] + probes()
    data = build(places)

    name = 'lm_07_4c00_6600.tft'
    open(os.path.join(LANDMARKS, name), 'wb').write(data)
    print("wrote %s: %d places, %d bytes" % (name, len(places), len(data)))
    for pl in places:
        print("    %-8s lon %10.5f  lat %10.5f  type %d  prio %d  height %g" %
              (pl['name'], pl['lon'], pl['lat'], pl.get('type', 0), pl.get('priority', 0),
               pl.get('height', 0)))

    log = os.path.join(USER, "tm.log")
    if os.path.exists(log):
        backup = log + ".before-demo"
        if os.path.exists(backup):
            os.remove(backup)
        os.rename(log, backup)
    print("\nRestart the simulator and fly over Santiago.")


def restore():
    name = 'lm_07_4c00_6600.tft'
    src = os.path.join(GAME, "scenery", "landmarks", name)
    shutil.copyfile(src, os.path.join(LANDMARKS, name))
    print("restored from the game: " + name)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--demo', action='store_true')
    ap.add_argument('--diag', action='store_true')
    ap.add_argument('--restore', action='store_true')
    a = ap.parse_args()
    if a.diag:
        demo(diag=True)
    elif a.demo:
        demo()
    elif a.restore:
        restore()
    else:
        ap.print_help()
