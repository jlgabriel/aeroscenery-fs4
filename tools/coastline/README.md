# tools/coastline — checks for the hand-drawn coastline geometry

```bash
tools/coastline/run.ps1
```

Checks on [`AeroScenery/AFS2/Coastline.cs`](../../AeroScenery/AFS2/Coastline.cs) — the line the user
traces on the map and the cut it produces — and on
[`CoastlineField.cs`](../../AeroScenery/AFS2/CoastlineField.cs), the rasterised version of that cut
which is what the converter actually reads. Expected result is **ALL PASSED**. Needs nothing but
`csc` — no solution, no MSBuild, no NuGet.

Compiling those two files on their own is also a standing check that the geometry never grows a
dependency on WinForms or GMap. It has to stay clean: `CoastlineEditor` is only one of its callers,
and the converter uses it with no UI anywhere in sight.

## classify.ps1 — which squares the cut touches

```bash
tools/coastline/classify.ps1 <working folder>\coastline.txt <working folder>
```

Run this **first** when the line has grown and squares have to be re-cut. It answers, per square,
whether the cut touches it at all — `LAND` (leave it alone, a rebuild gives back the same bytes),
`CUT` (rebuild it), `SEA` (delete it, there is no photoscenery there) or `OUTSIDE` (the square is
not wholly inside the stretch of coast the line covers, so it must not be cut at all — the same
rule the app and `water_convert.ps1` apply). It costs seconds per
square and opens no source image, and most squares need nothing: the whole-package pass of
2026-08-09 came out 17 to rebuild out of 45, which is fifty minutes of converting instead of two
and a half hours.

The verdicts are **exact**, not indicative, and that rests on two things:

- The box comes from `TtcConverter.CoastFieldBox`, which is the call the converter itself makes.
  That box is the **work range**, about a tile wider than what the `.tmc` asked for, because the
  work range is widened so every parent has four children. Bounding the `.tmc` box instead reads a
  square as landward that the converter would still shave at its seaward edge — `map_09_4d00_6880`
  bounds to 5.2 km against a 5.556 km margin that way, and it carries 17 masks. Ask for the box,
  never reproduce it.
- Bilinear interpolation never leaves the range of the four texels it reads, so a maximum inside
  the margin means every lookup in the square is inside it.

Unlike `run.ps1` this compiles the whole converter, for the one call above. The alternative was to
copy six lines of tile arithmetic into the tool and let the two drift.

Run over the 57 squares of the Chile package, its verdicts agree with what is installed on every
one: **20 CUT, and those are exactly the 20 squares that carry masks** — 1,418 of them — while all
36 LAND squares carry none and the one SEA square is the one that was deleted. That is the tool
checked against built scenery rather than against itself.

`OUTSIDE` was added later, when the package had grown past both ends of the line. Without it the
classifier called seven such squares `CUT`, because the field carries the coast straight on past
the ends — and cutting against that guess is the mistake that once took a square from 1,365 tiles
to 435. Over the 81 squares it now reads 20 CUT, 38 LAND, 1 SEA and 22 OUTSIDE: the 20 are exactly
the squares that carry masks, and the 22 are exactly the squares the app leaves uncut.

## Why these checks and not others

The line the user draws is the **waterline**; the cut sits a margin out to sea and is derived. So
the thing to test is not that the drawing works — that is visible the moment you drag — but that
the derivation is what it claims:

> every point of the cut is exactly `MarginKm` from the coast

If that holds on a shape with capes, bays and hand jitter, then the corner rounding and the fold
culling are both right, and no amount of looking at the map adds to it. Measured on a synthetic
coast of 400 points: **worst 1.8 m, mean 0.7 m**.

Five more exist because of a specific way this can look fine and be wrong:

- **The cut's *segments*, not only its vertices.** Culling a fold leaves a straight chord across
  the gap, and a chord between two points that are both at the margin can dip well inside it. No
  vertex test can see that. Measured worst: 47 m inside a 5,556 m margin.
- **And the same chord running *outside* the margin, which is the half that was missing.** See
  below — this is the one the pilot found by looking at the map.
- **A sharp cape must round, not spike.** Offsetting each segment and meeting the results in a
  point grows a spike whose length runs away as the angle sharpens. The check is that no cut point
  is further from the coast than the margin.
- **Drawn north to south and south to north must give the same cut.** Which side the sea is on
  cannot be decided per segment — a stretch running east-west has one normal north and one south,
  and "land is east" says nothing about either — so it is settled once for the whole line, and
  this is what catches it being settled wrongly.
- **Thinning must not move the line.** Douglas-Peucker at a fifth of the margin takes a stroke
  from 3,000 points to 33; the check is that the original points are all still within the
  tolerance of the thinned line.

## The fold, and why culling was not enough — found on the map, 2026-08-09

The cut used to be built by offsetting every segment, rounding the convex corners, and then
**dropping** any point that came out nearer the coast than the margin. The claim was that what
survived was the true contour. It is not, and the difference is not subtle.

Where the coast turns concave more sharply than the margin, the offset folds over itself and the
whole folded run is dropped. The two points either side are both exactly at the margin — every
vertex test passes — but **the contour turns a corner between them that no vertex was ever
generated for**, and the chord left behind sails straight past it out to sea. On the pilot's own
line that put the drawn cut **2.25 km outside a 1.852 km margin**, 120% wrong, and on the map it
read as the red line wandering off and losing whole stretches of coast.

It took two passes to fix, and the second only became visible once the first was in.

**One bisection per fold.** Where consecutive raw points straddle the margin, find the crossing and
put it in. That is the corner that was missing, and it stops the cut leaving the margin by
kilometres.

**Then fill in the steps and pull them onto the contour.** Bounding the error is not the same as
getting the shape right: between two crossings the cut is still a straight chord where the true
contour curves, and around a headland with a bay on each side that draws a rectangular notch in
what should be an arc. Every step longer than a quarter of the margin is subdivided, and each new
point is walked onto the contour along the gradient of the distance — found by differences, so it
never has to hunt for the nearest segment. A point already at the margin does not move, so the long
straight stretches cost only the points.

Measured on the pilot's 900 km line, 617 vertices, at a 5.556 km margin:

| | vertices only | + crossings | + contour |
|---|---:|---:|---:|
| worst point outside the margin | — | 1.684 km | **0.356 km** |
| worst point inside the margin | — | 0.048 km | **0.044 km** |
| points in the cut | — | 1003 | 1315 |

The 1.68 km was at Punta Hualpén, where the coast turns twice inside the margin; everywhere else on
that line was already under 500 m. On the short test line the first pass alone took 2.252 km down
to 0.192 km against a 1.852 km margin.

What remains is 6% of the margin, over open water. Nothing downstream reads `CutLine` at all — the
converter uses `CoastlineField`, which is exact — so this is entirely about the picture telling the
truth.

**The cut stopped being O(n²).** Answering "does the coast come nearer than the margin" walked every
segment, and `CutLine` asks it thousands of times: a 2,000-vertex line took **1.9 s on every release
of the mouse button**, and 4,000 took 5.8 s. Bucketing the segments by latitude takes those to
**42 ms and 127 ms** with the contour pass included. That cost, not any argument about the cut, is
what had been keeping the drawing coarse.

## The thinning tolerance is not a fraction of the margin any more

It was a fifth of it. Drawing at 3 NM therefore straightened every bay under 1.1 km, and the pilot
traced round a bay and watched their own line go straight. Worse: **the thinning is permanent and the
margin is not**, so a number documented as re-tunable was quietly deciding how much of the coast
survived, and re-cutting at 2 NM afterwards could not bring back a headland the tolerance had
eaten.

It is now a fixed **200 m**. That keeps every inlet worth seeing on the map, sits well under the
one vertex per kilometre the cut actually needs, and is affordable because of the index above.

A line drawn before this stays as thin as it was made. There is no way to recover detail that was
never stored — those stretches have to be drawn again.

What decides the detail now is the **zoom**, so the toolbar shows it. The map had a zoom control
and no zoom readout, which made "draw at level 12" advice with no way to follow it: the user could
only go in and out and guess. It reports metres per pixel rather than the level, because that is
the number that decides anything — one point every 4 px against a 200 m tolerance means **50 m/px
is the coarsest worth drawing at**, and the label says `too coarse` below it. The threshold is
derived from those two constants rather than written down, so it cannot drift from them.

At lat −33 that lands on map zoom 12, at 32.1 m/px. Zoom 13 is 16.0 and stores nothing more, for
twice the panning.

## The field, and why it is tested against an oracle rather than against itself

A distance field that is merely plausible looks perfect on a map and cuts holes in the scenery, so
the field is checked against a rule written a completely different way: the test coast is drawn
single valued in latitude, so which side a point is on is simply whether it lies east of the line
at that latitude, and the distance comes from `Coastline.DistanceKm`, which the checks above
already pin down. 20,000 random points, and the two have to agree on every one.

That is what caught the only real defect in it. The field decides the side by an even-odd ray cast,
which needs the line carried on past either end or a ray beyond the drawn coast crosses nothing and
calls open ocean land. The first version carried it on **for the side only** and kept measuring the
distance to the drawn segments — and a side that flips where the distance does not is a step of
twice the clamp between two neighbouring texels. Interpolating across that step lays a hairline of
photoscenery down the middle of the sea. It failed 2 points of 20,000, which is exactly what a
hairline looks like from a random sample, and nothing about a picture of the cut would have shown
it. There is now a Lipschitz-continuity check on the same spot as well, because that names the
defect directly.

## Drawing order stopped being part of the data

The strokes used to be joined end to end in the order they were drawn. That quietly made the
drawing order part of the coastline: pick the south end up after the north end and the line gained
a straight segment between the two, tens of kilometres long, which the converter reads as coast.
It also meant a coast could only ever grow at one end — "carry on southward tomorrow" meant
starting again, which is the question that surfaced it.

Each stroke is now attached to whichever end of the chain it is nearest, reversed if it faces the
wrong way. A coast is one line, so its pieces have exactly one sensible arrangement and the nearest
ends find it. Draw in any sequence, in either direction, start anywhere, extend either end.

Two things follow that are worth knowing:

- **Undo still drops the stroke drawn most recently**, which after ordering need not be at either
  end of the line. Undo means "take back what I just did".
- **A gap is still a gap.** Nothing can invent a piece that was never drawn, so a stretch left out
  gets a straight line across it. `LongestJoinKm` measures the widest join and the toolbar shows
  `GAP n km` when it exceeds **the margin** — visible beats silent, and that is all that can be
  done about it.

  The threshold is the margin rather than a flat kilometre because that is where a gap starts to
  matter: a straight line across a gap of length L puts the coast at most L/2 from where it runs,
  so a gap shorter than the margin cannot move the cut by more than half the tolerance the design
  already accepts. The flat kilometre was tried first and cried wolf on the first 900 km line ever
  drawn — its widest join was 1.86 km, shorter than nine of its own ordinary segments, the longest
  of which is 8.2 km of genuinely straight beach north of Los Vilos.

## One thing the map will show you that is not a bug

The gap between the drawn line and the cut is **not** the perpendicular distance where the coast
runs diagonally — on screen it is wider by `1/sin(angle)`. And in a bay narrower than twice the
margin the cut runs straight past the mouth, because the nearest land to a point out there is the
headland and not the head of the bay. Both are correct, and both look like the margin varying.
