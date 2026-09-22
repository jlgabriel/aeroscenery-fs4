#!/usr/bin/env python3
"""Label connected bodies of water in a mask, with numpy alone.

There is no scipy on the machine this is written for, so `ndimage.label` is not available. This is
the classic two-pass algorithm, done over runs rather than over pixels: each row is reduced to its
runs of water, runs that overlap between adjacent rows are unioned, and a second pass turns the
union-find roots into dense labels. Over a water mask the run count is a few thousand where the
pixel count is millions, so it is fast enough in plain Python.

Why bodies at all: the water fix levels each body of water onto the tone of its own cleanest part.
Measured per SQUARE instead, two squares that share a lake pick different targets and leave a step
on a straight line where they meet - which is what `map_09_4c00_5f80` and `map_09_4c00_5f00` did to
lake Llanquihue, and the pilot saw it. Labelling also stops a lake and the sea being forced to the
same tone, which they should not be: they are genuinely different colours.

Connectivity is 4, which is what runs plus vertical overlap gives. A narrow channel can join two
bodies that ought to be separate - a river mouth at 57 m/px is a texel or two wide - so `erode`
opens the mask before labelling and the labels are then grown back over the original mask. That
keeps a lake off the sea's target without losing the lake's own outlet.
"""
import numpy as np


def _runs(row):
    """Start and end+1 of each run of True in a 1-D boolean row."""
    d = np.diff(np.concatenate(([False], row, [False])).astype(np.int8))
    return np.flatnonzero(d == 1), np.flatnonzero(d == -1)


class _Union(object):
    def __init__(self):
        self.parent = []

    def make(self):
        self.parent.append(len(self.parent))
        return len(self.parent) - 1

    def find(self, a):
        p = self.parent
        while p[a] != a:
            p[a] = p[p[a]]
            a = p[a]
        return a

    def union(self, a, b):
        ra, rb = self.find(a), self.find(b)
        if ra != rb:
            self.parent[max(ra, rb)] = min(ra, rb)


def label(mask):
    """Label 4-connected components of a boolean mask.

    Returns (labels, count). Label 0 is background; bodies are numbered from 1."""
    h, w = mask.shape
    out = np.zeros((h, w), np.int32)
    uf = _Union()
    uf.make()                                   # index 0, reserved for background
    prev = []

    for y in range(h):
        starts, ends = _runs(mask[y])
        cur = []
        for s, e in zip(starts, ends):
            here = None
            for ps, pe, pl in prev:
                if ps < e and s < pe:           # the runs overlap, so they are one body
                    if here is None:
                        here = pl
                    else:
                        uf.union(here, pl)
                elif ps >= e:
                    break
            if here is None:
                here = uf.make()
            cur.append((s, e, here))
            out[y, s:e] = here
        prev = cur

    # Second pass: collapse to roots, then renumber densely so the labels are 1..n.
    roots = np.array([uf.find(i) for i in range(len(uf.parent))], np.int32)
    used = np.unique(roots[np.unique(out)])
    used = used[used != 0]
    dense = np.zeros(len(uf.parent), np.int32)
    for i, r in enumerate(used, start=1):
        dense[roots == r] = i
    return dense[out], len(used)


def erode(mask, n=1):
    """Shrink a mask by n texels, 4-connected. Used to open narrow channels before labelling."""
    m = mask
    for _ in range(n):
        e = m.copy()
        e[1:, :] &= m[:-1, :]
        e[:-1, :] &= m[1:, :]
        e[:, 1:] &= m[:, :-1]
        e[:, :-1] &= m[:, 1:]
        m = e
    return m


def grow(labels, mask, rounds):
    """Spread labels outwards into `mask`, so the texels erosion removed get their body back.

    Ties are settled by whichever neighbour is looked at last; over a one or two texel rim of a
    lake that is not a distinction worth making."""
    out = labels.copy()
    for _ in range(rounds):
        empty = (out == 0) & mask
        if not empty.any():
            break
        for shift in range(4):
            src = np.zeros_like(out)
            if shift == 0:
                src[1:, :] = out[:-1, :]
            elif shift == 1:
                src[:-1, :] = out[1:, :]
            elif shift == 2:
                src[:, 1:] = out[:, :-1]
            else:
                src[:, :-1] = out[:, 1:]
            take = empty & (src != 0)
            out[take] = src[take]
            empty &= ~take
    return out


def bodies(mask, open_by=2):
    """Label a water mask, opening narrow channels first and growing the labels back after.

    Returns (labels, count) over the whole of `mask`, so every water texel belongs to a body."""
    core = erode(mask, open_by) if open_by > 0 else mask
    labels, n = label(core)
    if open_by > 0:
        # Enough rounds to close over what the erosion took, from both sides. A channel that was
        # erased entirely is filled from each end and meets in the middle, which is the right
        # answer: the two lakes keep separate targets and the channel belongs to whichever is
        # nearer. Too few rounds and the middle of it is orphaned into a body of its own, whose
        # target would then be measured over a handful of texels.
        labels = grow(labels, mask, 4 * open_by + 4)
        # An island of water thinner than the erosion vanished entirely. Give what is left its own
        # body rather than dropping it, or those texels would go uncorrected.
        left = mask & (labels == 0)
        if left.any():
            extra, m = label(left)
            labels = np.where(extra > 0, extra + n, labels)
            n += m
    return labels, n
