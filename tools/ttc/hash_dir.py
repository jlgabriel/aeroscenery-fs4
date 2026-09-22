"""
Prints a manifest of a directory of .ttc files: one line per file, plus a total.

    python tools/ttc/hash_dir.py out_csharp
    python tools/ttc/hash_dir.py out_python

Deliberately implementation-agnostic - it hashes whatever is on disk, so the same script checks
the C# converter and the Python reference, and the two outputs can just be diffed. That is the
end-to-end check: not "does each piece work" but "do the two produce the same tiles".

The manifest line is the file name and its SHA-256, so a mismatch says which tile diverged
rather than only that something did.
"""

import hashlib
import os
import sys


def sha(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    folder = sys.argv[1]
    names = sorted(f for f in os.listdir(folder) if f.endswith('.ttc'))

    total = hashlib.sha256()
    nbytes = 0
    for n in names:
        p = os.path.join(folder, n)
        d = sha(p)
        nbytes += os.path.getsize(p)
        total.update((n + ' ' + d + '\n').encode())
        print(f'{n:32} {d}')

    print()
    print(f'{len(names)} files, {nbytes / 2**20:,.1f} MB')
    print(f'manifest {total.hexdigest()}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
