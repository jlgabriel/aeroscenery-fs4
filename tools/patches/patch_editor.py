#!/usr/bin/env python3
"""Mark colour patches with a magic wand, in the browser.

    python tools/patches/patch_editor.py <state.json> <stitched folder> [...] [--port 8765]

Then open http://localhost:8765. The page shows the stitched images of the folders at 1/16.

- Click inside a patch: the wand marks all of it (magenta). Right-click a marked patch to remove it.
- If a mark runs into good terrain, lower the tolerance. If it stops short, raise it.
- Preview shows the result: one colour offset per patch. V switches before / after.
- Save writes the clicks to <state.json>, in degrees. patch_apply.py reads the same file.

The server only listens on this computer (127.0.0.1).
"""
import argparse
import io
import json
import os
import sys
import time
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import patchlib as pl


def png_bytes(arr):
    buf = io.BytesIO()
    Image.fromarray(arr).save(buf, 'PNG')
    return buf.getvalue()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('state')
    ap.add_argument('folders', nargs='+', help='the <zoom>-stitched folders')
    ap.add_argument('--port', type=int, default=8765)
    args = ap.parse_args()

    print('reading the stitched images...', flush=True)
    mos = pl.Mosaic([pl.StitchedFolder(p) for p in args.folders])
    base = png_bytes(mos.src.astype(np.uint8))
    cache = {}
    preview = {'png': None}

    def clicks_to_state(clicks):
        out = []
        for c in clicks:
            lon, lat = mos.lonlat(c['x'], c['y'])
            out.append({'lon': round(lon, 7), 'lat': round(lat, 7), 'tol': int(c['tol'])})
        return {'version': 1, 'clicks': out}

    def state_to_clicks(state):
        out = []
        for c in state.get('clicks', []):
            x, y = mos.xy(c['lon'], c['lat'])
            out.append({'x': x, 'y': y, 'tol': int(c['tol'])})
        return out

    def masks(clicks):
        return pl.masks_of(mos, clicks_to_state(clicks), cache)

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *a):
            pass

        def send(self, body, ctype='application/json', code=200):
            if isinstance(body, (dict, list)):
                body = json.dumps(body).encode()
            self.send_response(code)
            self.send_header('Content-Type', ctype)
            self.send_header('Cache-Control', 'no-store')
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            p = self.path.split('?')[0]
            if p == '/':
                return self.send(open(os.path.join(HERE, 'editor.html'), 'rb').read(), 'text/html; charset=utf-8')
            if p == '/base.png':
                return self.send(base, 'image/png')
            if p == '/preview.png' and preview['png']:
                return self.send(preview['png'], 'image/png')
            if p == '/state':
                return self.send({'clicks': state_to_clicks(pl.load_state(args.state))})
            self.send({'error': 'not found'}, code=404)

        def do_POST(self):
            body = json.loads(self.rfile.read(int(self.headers['Content-Length'])) or b'{}')
            clicks = body.get('clicks', [])
            if self.path == '/overlay':
                rgba = np.zeros(mos.src.shape[:2] + (4,), np.uint8)
                for m in masks(clicks):
                    rgba[m] = (255, 0, 255, 90)
                    edge = m & ~(np.roll(m, 1, 0) & np.roll(m, -1, 0) & np.roll(m, 1, 1) & np.roll(m, -1, 1))
                    rgba[edge] = (255, 0, 255, 255)
                return self.send(png_bytes(rgba), 'image/png')
            if self.path == '/which':
                x, y = int(body['x']), int(body['y'])
                ms = masks(clicks)
                for i in range(len(ms) - 1, -1, -1):
                    if 0 <= y < mos.h and 0 <= x < mos.w and ms[i][y, x]:
                        return self.send({'index': i})
                return self.send({'index': -1})
            if self.path == '/preview':
                t = time.time()
                out, off, lab = pl.level(mos, masks(clicks))
                preview['png'] = png_bytes(out.astype(np.uint8))
                return self.send({'ms': int((time.time() - t) * 1000), 'patches': int(lab.max())})
            if self.path == '/save':
                pl.save_state(args.state, clicks_to_state(clicks))
                return self.send({'saved': len(clicks)})
            self.send({'error': 'not found'}, code=404)

    print('ready on http://localhost:%d' % args.port, flush=True)
    ThreadingHTTPServer(('127.0.0.1', args.port), Handler).serve_forever()


if __name__ == '__main__':
    main()
