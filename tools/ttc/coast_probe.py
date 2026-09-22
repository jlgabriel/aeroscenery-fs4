#!/usr/bin/env python3
"""How far offshore does the imagery reach, measured over a whole installed package?

The question this answers is where photoscenery should stop at the sea. Cutting at a margin
offshore only works if there is real imagery out to that margin; otherwise the margin is full of
the black that the source left behind. So: for every raster row of the package, how far west of
the coast does coverage actually go, and how much of the sea inside a candidate margin is black.

Measured from the BUILT SCENERY, not from the network. Black in our own `.ttc` is exactly where
the source served nothing, it is what the pilot flies over, and it needs no requests and no
stitched sources on disk. Our tiles are stored rather than compressed, so ttc.py reads them.

    python tools/ttc/coast_probe.py "<...>/addons/scenery/<package>/images"
    python tools/ttc/coast_probe.py "<...>/images" --mosaic block.png --probe-bing 8

Method, and the three places it could go wrong
----------------------------------------------
Each square's own level-9 tile is decoded: 2048 px over ~65 km, so 32 m/px, and a level-9 pixel is
only pure black when all 1024 of its level-14 children were. That makes the black boundary
conservative by about 32 m, which is nothing against a margin measured in kilometres, and it means
one 2.8 MB file per square instead of the 1365 it stands for.

  water vs land   B - R. On the Chile block, open ocean measures +44 (p1 +20) and land -31
                  (p99 0), so the threshold at +10 sits in a wide empty gap rather than on a
                  guess. Cloud over water measures +16, i.e. it stays on the water side, which is
                  what we want since the patches are all offshore. Cloud over LAND would read as
                  water; that is the known weakness, and it is why the coast needs a run.
  the coast       walking east from the west end, the first column where the next 32 px (1 km)
                  are at least 75% land. A run rather than a single pixel, so a bright shoal or a
                  sandbar cannot fake a coastline.
  offshore        every water column west of that, with its distance from the coast and whether
                  it is black. An island counts as a coast, which is correct for coverage but
                  makes "distance from the coast" mean distance from the island.

A row is only counted as RESOLVED if black is actually found west of the coast inside built data.
A row that runs off the west edge of the package while still on imagery tells you the coverage is
at least that wide, not how wide it is, and averaging those in would understate the reach.

Result on the 45-square Chile block, 2026-08-08
-----------------------------------------------
Coverage west of the coast, over 20,682 resolved rows: min 1.11 km, p1 5.14, p5 8.26, median 17.81,
max 53.79. **99.69% of all the black in the block is at sea**; 0.31% is inland, in mountain shadow.
Cutting 3 NM offshore leaves 0.354% of the sea band black and removes 99.60% of the block's black,
so a coastline cut at that margin needs no low-zoom fill under it.

That corrects an earlier note, which read a single probe as meaning the
coverage edge sits at the coast. The probe was right and the reading was wrong: at lat -32.05 the
edge is at lon -71.605 as it said, but the coast there is at -71.51, so that edge is 9 km out.
"""
import argparse
import io
import math
import os
import re
import sys
import urllib.request

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ttc
from convert_tmc import lat_of_grid_y, lon_of_grid_x

SQUARE_RE = re.compile(r"^map_(\d{2})_([0-9a-f]{4})_([0-9a-f]{4})$")

TILE = 2048
BR_WATER = 10           # B - R at or above this is water
BR_LAND = 0             # at or below this is land; between the two is nobody's
RUN = 32                # 1 km of mostly-land is what makes a coast
RUN_FRAC = 0.75


def find_squares(images_dir):
    """The package's square folders, keyed by (grid x, grid y). Level comes from the names."""
    out, level = {}, None
    for name in sorted(os.listdir(images_dir)):
        m = SQUARE_RE.match(name)
        if not m or not os.path.isdir(os.path.join(images_dir, name)):
            continue
        lv = int(m.group(1))
        if level is None:
            level = lv
        elif lv != level:
            sys.exit("mixed square levels in %s: %d and %d" % (images_dir, level, lv))
        step = 1 << (16 - lv)
        out[(int(m.group(2), 16) // step, int(m.group(3), 16) // step)] = name
    if not out:
        sys.exit("no map_NN_XXXX_YYYY square folders in " + images_dir)
    return out, level


def load_square(images_dir, name):
    """The square's own top-level tile, decoded and flipped north-up."""
    path = os.path.join(images_dir, name, name + ".ttc")
    with open(path, "rb") as f:
        hdr, _, codec = ttc.read_ttc(f.read())
    if hdr["format"] != ttc.FORMAT_DXT1:
        sys.exit("%s is format %d, not DXT1 - a compressed or Basis tile needs a decoder we "
                 "do not have" % (name, hdr["format"]))
    w, h = hdr["width"], hdr["height"]
    if not hdr["stored"]:
        sys.exit("%s is compressed; only tiles this converter wrote can be read back" % name)
    return ttc.decode_dxt1(codec[: (w // 4) * (h // 4) * 8], w, h)[::-1]


GAP = 32                # 1 km of black is where coverage ends; less is a hole in it


def coverage_edge(black_row, have, coast):
    """Walking west from the coast, where coverage ends — and whether that was actually seen.

    Two traps, both of which cost a measurement before they were noticed.

    A run of two or three black pixels is not the end of coverage, it is a hole in it, so short
    runs are stepped over rather than treated as the edge. And a package boundary is not a
    coverage edge: where the built squares simply stop, all we know is that coverage reaches at
    least that far. Worse, the last pixel or two before a boundary is often black, so a naive walk
    stops on that sliver and reports the package edge as if the imagery had run out there. Rows
    like that are returned unresolved and must be excluded from any statistic about reach.
    """
    k = coast - 1
    while k >= 0 and have[k]:
        if not black_row[k]:
            k -= 1
            continue
        end = k
        while end >= 0 and have[end] and black_row[end]:
            end -= 1
        if k - end >= GAP:
            return k + 1, True
        k = end                     # a hole, not the edge - keep going west
    return k + 1, False             # ran out of built data


def coast_column(land_row, have):
    """First column with a sustained land run, or None."""
    lrow = land_row & have
    cs = np.concatenate(([0], np.cumsum(lrow)))
    hit = np.nonzero((cs[RUN:] - cs[:-RUN]) / float(RUN) >= RUN_FRAC)[0]
    if len(hit) == 0 or hit[0] == 0:
        return None
    return int(hit[0])


def analyse(images_dir, bin_km, max_km, mosaic_path=None, shrink=16):
    squares, level = find_squares(images_dir)
    xs = sorted({x for x, _ in squares})
    ys = sorted({y for _, y in squares})
    X0, X1, Y0, Y1 = xs[0], xs[-1], ys[0], ys[-1]
    W = (X1 - X0 + 1) * TILE
    print("%d squares at level %d, x %d..%d, y %d..%d"
          % (len(squares), level, X0, X1, Y0, Y1))

    nbin = int(max_km / bin_km)
    blk = np.zeros(nbin, np.int64)
    tot = np.zeros(nbin, np.int64)
    unbroken, lats, resolved, edge_lons = [], [], [], []
    px_built = px_black = black_inland = black_nocoast = 0

    mosaic = None
    if mosaic_path:
        from PIL import Image
        mosaic = np.zeros(((Y1 - Y0 + 1) * TILE // shrink, W // shrink, 3), np.uint8)

    lon_w = lon_of_grid_x(X0, level)
    lon_e = lon_of_grid_x(X1 + 1, level)
    lons = lon_w + (np.arange(W) + 0.5) * (lon_e - lon_w) / W

    for gy in sorted(ys, reverse=True):
        strip = np.zeros((TILE, W, 3), np.uint8)
        have = np.zeros(W, bool)
        for x in xs:
            if (x, gy) not in squares:
                continue
            off = (x - X0) * TILE
            strip[:, off:off + TILE] = load_square(images_dir, squares[(x, gy)])
            have[off:off + TILE] = True

        if mosaic is not None:
            from PIL import Image
            my = (Y1 - gy) * TILE // shrink
            mosaic[my:my + TILE // shrink] = np.array(
                Image.fromarray(strip).resize((W // shrink, TILE // shrink), Image.BOX))

        r = strip[:, :, 0].astype(np.int16)
        b = strip[:, :, 2].astype(np.int16)
        black = np.all(strip == 0, axis=2)
        land = ((b - r) <= BR_LAND) & ~black

        px_built += int(have.sum()) * TILE
        px_black += int(black[:, have].sum())

        for j in range(TILE):
            lat = lat_of_grid_y((gy + 1) - (j + 0.5) / TILE, level)
            kmdeg = 111.320 * math.cos(math.radians(lat))

            coast = coast_column(land[j], have)
            if coast is None:
                black_nocoast += int((black[j] & have).sum())
                continue
            black_inland += int((black[j][coast:] & have[coast:]).sum())

            sea = np.arange(coast)
            m = have[sea]
            if not m.any():
                continue
            d = (lons[coast] - lons[sea]) * kmdeg
            idx = np.minimum((d[m] / bin_km).astype(int), nbin - 1)
            np.add.at(tot, idx, 1)
            np.add.at(blk, idx, black[j][sea][m].astype(np.int64))

            edge, res = coverage_edge(black[j], have, coast)
            unbroken.append(float((lons[coast] - lons[edge]) * kmdeg))
            edge_lons.append(float(lons[edge]))
            lats.append(lat)
            resolved.append(res)

        del strip, r, b, black, land
        print("  y=%d" % gy)

    if mosaic is not None:
        from PIL import Image
        Image.fromarray(mosaic).save(mosaic_path)
        print("\nwrote %s (%d x %d, %.0f m/px)"
              % (mosaic_path, mosaic.shape[1], mosaic.shape[0], 32.0 * shrink))

    return dict(level=level, blk=blk, tot=tot, bin_km=bin_km,
                unbroken=np.array(unbroken), lat=np.array(lats),
                resolved=np.array(resolved), edge_lon=np.array(edge_lons),
                px_built=px_built, px_black=px_black,
                black_inland=black_inland, black_nocoast=black_nocoast)


def report(a):
    blk, tot, bin_km = a["blk"], a["tot"], a["bin_km"]
    cb, ct = np.cumsum(blk), np.cumsum(tot)
    u = a["unbroken"][a["resolved"]]
    lat = a["lat"][a["resolved"]]

    print("\n=== the block: %d built pixels, %.2f%% pure black"
          % (a["px_built"], 100.0 * a["px_black"] / a["px_built"]))
    for tag, n in (("inland of the coast", a["black_inland"]),
                   ("in rows with no coast", a["black_nocoast"]),
                   ("at sea", int(blk.sum()))):
        print("  black %-22s %12d   %6.2f%% of all black"
              % (tag, n, 100.0 * n / max(a["px_black"], 1)))

    print("\n=== if photoscenery stops M offshore")
    print("   M              inside the margin        beyond it, i.e. removed")
    print(" NM     km       black px    of the band     of all block black")
    for nm in (1, 2, 3, 4, 5, 6, 8, 10, 15, 20):
        km = nm * 1.852
        i = min(int(km / bin_km), len(blk) - 1)
        inside = cb[i]
        print("%3d  %6.2f  %11d   %8.3f%%       %8.2f%%"
              % (nm, km, inside, 100.0 * inside / max(ct[i], 1),
                 100.0 * (blk.sum() - inside) / max(a["px_black"], 1)))

    print("\n=== coverage west of the coast, holes under 1 km ignored (%d resolved rows of %d)"
          % (a["resolved"].sum(), len(a["unbroken"])))
    print("  min %.2f  p1 %.2f  p5 %.2f  p25 %.2f  median %.2f  p75 %.2f  max %.2f km"
          % (u.min(), *[np.percentile(u, q) for q in (1, 5, 25, 50, 75)], u.max()))
    print("  rows whose coverage ends closer in than:")
    for nm in (1, 2, 3, 4, 5, 6):
        print("    %d NM (%5.2f km): %6.3f%%" % (nm, nm * 1.852, 100.0 * (u < nm * 1.852).mean()))

    un = a["unbroken"][~a["resolved"]]
    if len(un):
        print("\n=== %d rows where the PACKAGE ends before the imagery does" % len(un))
        print("  built sea west of the coast: p5 %.2f  median %.2f  p95 %.2f km"
              % (np.percentile(un, 5), np.median(un), np.percentile(un, 95)))
        print("  these bound the reach from below and say nothing about where the source stops;")
        print("  where the figure is small, a margin cut would land on the package edge anyway")

    print("\n=== by latitude, 1 degree bands")
    print("  band          rows     min      p5   median     max   (km)")
    lo = int(math.floor(lat.min()))
    while lo < lat.max():
        m = (lat >= lo) & (lat < lo + 1)
        if m.sum() >= 50:
            v = u[m]
            print("  %+d..%+d  %8d  %6.2f  %6.2f  %7.2f  %6.2f"
                  % (lo, lo + 1, m.sum(), v.min(), np.percentile(v, 5), np.median(v), v.max()))
        lo += 1

    stretches(a, 3 * 1.852)


def stretches(a, limit_km, gap_deg=0.02):
    """Where coverage ends inside the margin, and whether that edge is a coast or a rectangle.

    An edge that holds one longitude over hundreds of consecutive rows is a straight north-south
    line, which is what a dropped download looks like - and also, as it turns out, what the tile
    source's own acquisition footprints look like. --probe-bing is what tells the two apart.
    """
    m = a["resolved"] & (a["unbroken"] < limit_km)
    if not m.any():
        print("\nno row loses coverage inside %.2f km" % limit_km)
        return
    lat, elon = a["lat"][m], a["edge_lon"][m]
    order = np.argsort(-lat)
    lat, elon = lat[order], elon[order]

    cut = np.nonzero(np.diff(lat) < -gap_deg)[0] + 1
    print("\n=== stretches losing coverage inside %.2f km (%d rows, %.1f%% of resolved)"
          % (limit_km, m.sum(), 100.0 * m.sum() / a["resolved"].sum()))
    print("  lat band            rows   edge lon           spread   rows sharing one lon")
    for group in np.split(np.arange(len(lat)), cut):
        if len(group) < 20:
            continue
        e, l = elon[group], lat[group]
        _, cnt = np.unique(np.round(e, 3), return_counts=True)
        print("  %+7.3f..%+7.3f  %6d   %8.3f..%8.3f  %6.3f   %d of %d"
              % (l.max(), l.min(), len(group), e.min(), e.max(),
                 e.max() - e.min(), cnt.max(), len(group)))


def probe_bing(a, n, zoom):
    """Ask the source, now, whether it has anything where our tiles are black near the coast.

    A coverage boundary that follows the coast is geography. One made of axis-aligned rectangles
    could equally be the source's acquisition footprint or our own download dropping tiles, and
    those want opposite fixes - so ask. Two controls bracket the answer: a point over land, which
    must come back as a real JPEG, and one far offshore, which must come back empty.
    """
    from ocean_fill import quadkey, merc_y, TEMPLATE

    def ask(lat, lon):
        m = 1 << zoom
        tx = int((lon + 180.0) / 360.0 * m)
        ty = int(merc_y(lat) * m)
        url = TEMPLATE.format(quadkey(tx, ty, zoom))
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(req, timeout=30) as r:
                data = r.read()
        except urllib.error.HTTPError as e:
            return "HTTP %d" % e.code
        if not data:
            return "empty - no imagery"
        return "%d bytes of imagery" % len(data)

    thin = a["resolved"] & (a["unbroken"] < 6.0)
    if not thin.any():
        print("\nno row has coverage ending within 6 km, nothing worth probing")
        return
    idx = np.nonzero(thin)[0]
    idx = idx[:: max(1, len(idx) // n)][:n]

    print("\n=== asking the source at zoom %d, %d points just outside the coverage edge" % (zoom, n))
    lat0 = float(a["lat"][idx[0]])
    lon0 = float(a["edge_lon"][idx[0]])
    print("  control, over land        : %s" % ask(lat0, lon0 + 0.5))
    print("  control, 200 km offshore  : %s" % ask(lat0, lon0 - 2.1))
    for i in idx:
        print("  lat %8.4f lon %8.4f (%.2f km out): %s"
              % (a["lat"][i], a["edge_lon"][i] - 0.005, a["unbroken"][i],
                 ask(float(a["lat"][i]), float(a["edge_lon"][i]) - 0.005)))


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("images", help="a package's scenery/images folder, holding map_NN_XXXX_YYYY dirs")
    ap.add_argument("--bin-km", type=float, default=0.25)
    ap.add_argument("--max-km", type=float, default=400.0,
                    help="cap on distance from the coast, wide enough to hold every sea pixel")
    ap.add_argument("--mosaic", help="also write a downscaled overview PNG of the whole block")
    ap.add_argument("--probe-bing", type=int, metavar="N", default=0,
                    help="ask the tile server about N points where coverage ends near the coast")
    ap.add_argument("--zoom", type=int, default=17, help="zoom to probe (default 17, 1 m/px)")
    ap.add_argument("--npz", help="save the raw per-row arrays here")
    args = ap.parse_args()

    a = analyse(args.images, args.bin_km, args.max_km, args.mosaic)
    report(a)
    if args.probe_bing:
        probe_bing(a, args.probe_bing, args.zoom)
    if args.npz:
        np.savez(args.npz, **{k: v for k, v in a.items() if isinstance(v, np.ndarray)})
        print("\nwrote " + args.npz)


if __name__ == "__main__":
    main()
