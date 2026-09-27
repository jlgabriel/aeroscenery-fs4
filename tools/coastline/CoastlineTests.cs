using System;
using System.Collections.Generic;
using AeroScenery.AFS2;

class CoastTest
{
    static int failed = 0;

    static void Check(string what, bool ok, string detail)
    {
        Console.WriteLine("{0}  {1}{2}", ok ? "  ok  " : "FAILED", what,
                          detail == null ? "" : "   " + detail);
        if (!ok) failed++;
    }

    static Coastline Straight(int n, double lat0, double lat1, double lon)
    {
        var c = new Coastline();
        c.BeginStroke();
        for (int i = 0; i < n; i++)
        {
            c.AddPoint(lat0 + (lat1 - lat0) * i / (n - 1.0), lon);
        }
        c.EndStroke(0.0);
        return c;
    }

    static void Main()
    {
        // ---- distance to a straight north-south line ----
        var line = Straight(50, -33.0, -32.0, -71.5);
        double kmDeg = Coastline.KmPerDegLon(-32.5);
        double d = line.DistanceKm(-32.5, -71.5 - 0.1);
        Check("distance west of a meridian coast", Math.Abs(d - 0.1 * kmDeg) < 0.01,
              String.Format("{0:F4} km, expected {1:F4}", d, 0.1 * kmDeg));

        // beyond the end it must measure to the endpoint, not to the infinite line
        double e = line.DistanceKm(-34.0, -71.5);
        Check("distance past the end clamps to the endpoint", Math.Abs(e - 1.0 * 110.9) < 0.5,
              String.Format("{0:F3} km", e));

        // ---- the invariant that defines the whole thing ----
        // Every point of the cut line must sit at exactly the margin from the coast. If that
        // holds on a shape with capes and bays, the rounding and the fold culling are both right.
        var wiggly = new Coastline();
        wiggly.Land = LandSide.East;
        wiggly.MarginKm = 5.556;
        wiggly.BeginStroke();
        var rnd = new Random(12345);
        for (int i = 0; i < 400; i++)
        {
            double lat = -33.0 + i * 0.0025;
            // capes and bays of a few km, plus hand jitter
            double lon = -71.5
                       + 0.05 * Math.Sin(i * 0.09)
                       + 0.012 * Math.Sin(i * 0.41)
                       + (rnd.NextDouble() - 0.5) * 0.002;
            wiggly.AddPoint(lat, lon);
        }
        wiggly.EndStroke(0.3);

        var cut = wiggly.CutLine();
        double worst = 0.0, mean = 0.0;
        foreach (var p in cut)
        {
            double dd = Math.Abs(wiggly.DistanceKm(p.Lat, p.Lon) - wiggly.MarginKm);
            mean += dd;
            if (dd > worst) worst = dd;
        }
        mean /= Math.Max(cut.Count, 1);
        Check("every cut point sits at the margin", worst < 0.05,
              String.Format("{0} points, worst {1:F4} km, mean {2:F5} km", cut.Count, worst, mean));

        // ---- and the SEGMENTS between those points, not just the points ----
        // Culling a fold leaves a straight chord across the gap, and a chord between two points
        // that are both at the margin can dip well inside it. No vertex test can see that.
        double chordWorst = 0.0;
        double chordAt = 0.0;
        var cutPts = cut;
        for (int i = 0; i + 1 < cutPts.Count; i++)
        {
            for (int k = 1; k < 20; k++)
            {
                double t = k / 20.0;
                double lat = cutPts[i].Lat + (cutPts[i + 1].Lat - cutPts[i].Lat) * t;
                double lon = cutPts[i].Lon + (cutPts[i + 1].Lon - cutPts[i].Lon) * t;
                double inside = wiggly.MarginKm - wiggly.DistanceKm(lat, lon);
                if (inside > chordWorst) { chordWorst = inside; chordAt = t; }
            }
        }
        Check("the drawn cut never cuts inside the margin", chordWorst < 0.1,
              String.Format("worst {0:F3} km inside, {1:F0}% along a segment",
                            chordWorst, chordAt * 100));

        // ---- and it must not run OUTSIDE the margin either, which is the harder half ----
        // Culling a fold used to leave the two surviving points joined by a chord across a corner
        // that no vertex was ever generated for, and the chord sailed past it out to sea. On the
        // pilot's own line that put the drawn cut 2.25 km outside a 1.852 km margin - 120% wrong -
        // and on the map it read as the red line wandering off and losing whole stretches. Every
        // vertex was exactly right the whole time, so nothing here could see it until the segments
        // between them were measured as well.
        double outWorst = 0.0;
        for (int i = 0; i + 1 < cutPts.Count; i++)
        {
            for (int k = 1; k < 20; k++)
            {
                double t = k / 20.0;
                double lat = cutPts[i].Lat + (cutPts[i + 1].Lat - cutPts[i].Lat) * t;
                double lon = cutPts[i].Lon + (cutPts[i + 1].Lon - cutPts[i].Lon) * t;
                double outside = wiggly.DistanceKm(lat, lon) - wiggly.MarginKm;
                if (outside > outWorst) outWorst = outside;
            }
        }
        Check("the drawn cut never runs outside the margin", outWorst < 0.4,
              String.Format("worst {0:F3} km outside a {1:F3} km margin", outWorst, wiggly.MarginKm));

        // ---- the fold that found it, which is a stretch of the pilot's own coast ----
        // A long segment across a bay mouth meeting a short one heading north: the two offsets
        // cross, the whole run between them is culled, and everything depends on the crossing
        // point being put back.
        var fold = new Coastline();
        fold.Land = LandSide.East;
        fold.MarginKm = 1.852;
        fold.BeginStroke();
        fold.AddPoint(-32.990, -71.548);
        fold.AddPoint(-32.985, -71.547);
        fold.AddPoint(-32.929, -71.554);
        fold.AddPoint(-32.920, -71.514);   // 3.9 km east-north-east, across the bay
        fold.AddPoint(-32.909, -71.507);
        fold.AddPoint(-32.874, -71.512);   // and back to due north
        fold.AddPoint(-32.834, -71.523);
        fold.EndStroke(0.0);

        var foldCut = fold.CutLine();
        double foldIn = 0.0, foldOut = 0.0;
        for (int i = 0; i + 1 < foldCut.Count; i++)
        {
            for (int k = 0; k <= 20; k++)
            {
                double t = k / 20.0;
                double lat = foldCut[i].Lat + (foldCut[i + 1].Lat - foldCut[i].Lat) * t;
                double lon = foldCut[i].Lon + (foldCut[i + 1].Lon - foldCut[i].Lon) * t;
                double off = fold.DistanceKm(lat, lon) - fold.MarginKm;
                if (off > foldOut) foldOut = off;
                if (-off > foldIn) foldIn = -off;
            }
        }
        Check("a fold sharper than the margin keeps the cut on the margin",
              foldOut < 0.25 && foldIn < 0.1,
              String.Format("{0} points, {1:F3} km outside, {2:F3} km inside, margin {3:F3}",
                            foldCut.Count, foldOut, foldIn, fold.MarginKm));

        // ---- the cut must be on the seaward side, whichever way the line was drawn ----
        foreach (bool reversed in new bool[] { false, true })
        {
            var c = new Coastline();
            c.Land = LandSide.East;
            c.MarginKm = 5.556;
            c.BeginStroke();
            for (int i = 0; i < 30; i++)
            {
                double lat = reversed ? -32.0 - i * 0.03 : -33.0 + i * 0.03;
                c.AddPoint(lat, -71.5 + 0.01 * Math.Sin(i * 0.5));
            }
            c.EndStroke(0.05);

            var cl = c.CutLine();
            double maxLon = Double.MinValue;
            foreach (var p in cl) if (p.Lon > maxLon) maxLon = p.Lon;
            Check("cut lands west of the coast, drawn " + (reversed ? "N to S" : "S to N"),
                  maxLon < -71.5, String.Format("easternmost cut lon {0:F4}", maxLon));
        }

        // ---- a cape must come out rounded, not spiked ----
        var cape = new Coastline();
        cape.Land = LandSide.East;
        cape.MarginKm = 5.0;
        cape.BeginStroke();
        cape.AddPoint(-33.00, -71.50);
        cape.AddPoint(-32.90, -71.50);
        cape.AddPoint(-32.85, -71.62);   // a sharp cape pointing west
        cape.AddPoint(-32.80, -71.50);
        cape.AddPoint(-32.70, -71.50);
        cape.EndStroke(0.0);

        var capeCut = cape.CutLine();
        double far = 0.0;
        foreach (var p in capeCut)
        {
            double dd = cape.DistanceKm(p.Lat, p.Lon);
            if (dd > far) far = dd;
        }
        Check("a sharp cape rounds rather than spikes", far < cape.MarginKm * 1.01,
              String.Format("furthest cut point {0:F3} km from the coast, margin {1:F3}",
                            far, cape.MarginKm));

        // ---- simplification keeps the shape ----
        var dense = new Coastline();
        dense.BeginStroke();
        for (int i = 0; i < 3000; i++)
        {
            dense.AddPoint(-33.0 + i * 0.0002, -71.5 + 0.03 * Math.Sin(i * 0.01));
        }
        int before = dense.Points.Count;
        dense.EndStroke(0.2);
        int after = dense.Points.Count;

        double drift = 0.0;
        for (int i = 0; i < 3000; i++)
        {
            double lat = -33.0 + i * 0.0002, lon = -71.5 + 0.03 * Math.Sin(i * 0.01);
            double dd = dense.DistanceKm(lat, lon);
            if (dd > drift) drift = dd;
        }
        Check("thinning keeps the line where it was", after < before / 10 && drift <= 0.2,
              String.Format("{0} points to {1}, worst drift {2:F4} km", before, after, drift));

        // ---- save and load round trip ----
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "coast_rt.txt");
        wiggly.Save(path);
        var back = Coastline.Load(path);
        var a = wiggly.Points; var b = back.Points;
        bool same = a.Count == b.Count && back.Land == wiggly.Land
                    && Math.Abs(back.MarginKm - wiggly.MarginKm) < 1e-12;
        for (int i = 0; same && i < a.Count; i++)
        {
            same = a[i].Lat == b[i].Lat && a[i].Lon == b[i].Lon;
        }
        Check("save and load round trip exactly", same,
              String.Format("{0} points, {1} strokes", b.Count, back.StrokeCount));

        // ---- undo ----
        var u = new Coastline();
        u.BeginStroke(); u.AddPoint(-33, -71); u.AddPoint(-32.9, -71); u.EndStroke(0);
        u.BeginStroke(); u.AddPoint(-32.9, -71); u.AddPoint(-32.8, -71); u.EndStroke(0);
        int n0 = u.Points.Count;
        u.UndoStroke();
        Check("undo drops the last stroke whole", u.StrokeCount == 1 && u.Points.Count == n0 - 2,
              String.Format("{0} strokes, {1} points", u.StrokeCount, u.Points.Count));

        // ---- the strokes order themselves along the coast, whatever order they were drawn in ----
        // Joining them in drawing order made the drawing order part of the data: pick the south end
        // up after the north end and the line gains a straight segment between the two, which the
        // converter reads as coast. It also meant a coast could only ever grow at one end.
        // Here the middle is drawn first, then the north piece backwards, then the south piece
        // backwards - about as awkward as it gets.
        var pieces = new Coastline();
        pieces.BeginStroke();
        for (int i = 0; i <= 10; i++) pieces.AddPoint(-33.0 + i * 0.01, -71.5);
        pieces.EndStroke(0.0);
        pieces.BeginStroke();
        for (int i = 10; i >= 0; i--) pieces.AddPoint(-32.9 + i * 0.01, -71.5);
        pieces.EndStroke(0.0);
        pieces.BeginStroke();
        for (int i = 10; i >= 0; i--) pieces.AddPoint(-33.1 + i * 0.01, -71.5);
        pieces.EndStroke(0.0);

        var chained = pieces.Points;
        bool rising = true, falling = true;
        for (int i = 0; i + 1 < chained.Count; i++)
        {
            if (chained[i + 1].Lat < chained[i].Lat - 1e-9) rising = false;
            if (chained[i + 1].Lat > chained[i].Lat + 1e-9) falling = false;
        }
        Check("strokes drawn out of order still make one line",
              (rising || falling) && pieces.LongestJoinKm < 0.01,
              String.Format("{0} points, {1:F4} km worst join, {2}", chained.Count,
                            pieces.LongestJoinKm, rising ? "south to north" : "north to south"));

        // ---- but a piece of coast that was never drawn is still a gap, and has to be visible ----
        var gapped = new Coastline();
        gapped.BeginStroke(); gapped.AddPoint(-33.0, -71.5); gapped.AddPoint(-32.95, -71.5); gapped.EndStroke(0.0);
        gapped.BeginStroke(); gapped.AddPoint(-32.75, -71.5); gapped.AddPoint(-32.70, -71.5); gapped.EndStroke(0.0);
        Check("a gap in the coast is measured, not hidden",
              Math.Abs(gapped.LongestJoinKm - 0.20 * 110.9) < 0.5,
              String.Format("{0:F2} km, expected {1:F2}", gapped.LongestJoinKm, 0.20 * 110.9));

        Console.WriteLine();
        FieldTests();

        Console.WriteLine();
        IslandTests();

        Console.WriteLine();
        Console.WriteLine(failed == 0 ? "ALL PASSED" : failed + " FAILED");
        Environment.Exit(failed == 0 ? 0 : 1);
    }

    // =========================================================================================
    // CoastlineField - the rasterised cut, which is what the converter actually reads
    // =========================================================================================

    /// <summary>
    /// Where the line is at one latitude, for a coast drawn single valued in latitude.
    ///
    /// Beyond either end it holds the end point's longitude, which is exactly how the field
    /// extends the line for its ray cast. That is the whole point of this function: it decides
    /// which side of the coast a point is on while sharing no code with the thing under test.
    /// </summary>
    static double OracleLon(double[] lats, double[] lons, double lat)
    {
        int n = lats.Length;
        if (lat <= lats[0]) return lons[0];
        if (lat >= lats[n - 1]) return lons[n - 1];

        for (int i = 0; i + 1 < n; i++)
        {
            if (lat >= lats[i] && lat <= lats[i + 1])
            {
                double t = (lat - lats[i]) / (lats[i + 1] - lats[i]);
                return lons[i] + (lons[i + 1] - lons[i]) * t;
            }
        }
        return lons[n - 1];
    }

    static void FieldTests()
    {
        // A coast that is single valued in latitude, so the oracle above can decide the side of
        // every point without borrowing anything from the field.
        var coast = new Coastline();
        coast.Land = LandSide.East;
        coast.MarginKm = 5.556;
        coast.BeginStroke();
        for (int i = 0; i < 150; i++)
        {
            coast.AddPoint(-33.0 + i * 0.004,
                           -71.5 + 0.06 * Math.Sin(i * 0.11) + 0.015 * Math.Sin(i * 0.53));
        }
        coast.EndStroke(0.0);

        // Read the vertices back rather than trusting that thinning kept them all, so the oracle
        // describes what is stored and not what was drawn.
        var stored = coast.Points;
        var lats = new double[stored.Count];
        var lons = new double[stored.Count];
        for (int i = 0; i < stored.Count; i++)
        {
            lats[i] = stored[i].Lat;
            lons[i] = stored[i].Lon;
        }

        double west = -71.85, east = -71.15, south = -33.15, north = -32.25;
        double texel = 0.08;
        var field = CoastlineField.Build(coast, west, east, south, north, texel);
        Check("field builds over a square", field.Width > 100 && field.Height > 100,
              String.Format("{0} x {1} texels at {2:F3} km", field.Width, field.Height, field.TexelKm));

        var rnd = new Random(4242);

        // ---- the one that matters: coverage agrees with the rule, point by point ----
        // Expected coverage is landward-or-within-the-margin, computed from the oracle side and
        // from Coastline.DistanceKm. Points too near the decision boundary to call are skipped and
        // counted, because a test that quietly skipped everything would still say ok.
        int wrong = 0, tested = 0, skipped = 0;
        for (int k = 0; k < 20000; k++)
        {
            // Latitudes the user actually drew. Past either end the coast carries straight on, and
            // Coastline.DistanceKm knows nothing about that, so the oracle would be measuring to a
            // different line. Beyond the ends gets its own test below.
            double lat = lats[0] + rnd.NextDouble() * (lats[lats.Length - 1] - lats[0]);
            double lon = west + rnd.NextDouble() * (east - west);

            bool land = lon > OracleLon(lats, lons, lat);
            double signed = (land ? -1.0 : 1.0) * coast.DistanceKm(lat, lon);

            if (Math.Abs(signed - coast.MarginKm) < 3.0 * texel)
            {
                skipped++;
                continue;
            }

            tested++;
            if (field.IsCovered(lat, lon) != (signed <= coast.MarginKm))
            {
                wrong++;
            }
        }
        Check("coverage matches landward-or-within-the-margin", wrong == 0 && tested > 15000,
              String.Format("{0} wrong of {1} tested, {2} too near the cut to call", wrong, tested, skipped));

        // ---- past the end of the drawn line, where the first version of this was wrong ----
        // The side flips across the line's continuation whether or not the distance follows it.
        // When it did not, the flip was a step of twice the clamp between two neighbouring texels,
        // and interpolating across that step laid a hairline of scenery down the middle of the
        // sea. So the tests are Lipschitz continuity, and the rule measured to the same
        // continuation the field uses.
        double endLat = lats[lats.Length - 1];
        double endLon = lons[lons.Length - 1];
        double kmPerDegLon = Coastline.KmPerDegLon(endLat);

        double stepKm = 0.02;
        double worstJump = 0.0;
        double previous = field.SignedDistanceKm(endLat + 0.05, endLon - 10.0 / kmPerDegLon);
        for (int k = 1; k <= 1000; k++)
        {
            double v = field.SignedDistanceKm(endLat + 0.05,
                                              endLon + (-10.0 + k * stepKm) / kmPerDegLon);
            double jump = Math.Abs(v - previous);
            if (jump > worstJump) worstJump = jump;
            previous = v;
        }
        Check("the field stays continuous past the end of the line", worstJump < 2.0 * stepKm,
              String.Format("worst {0:F4} km over {1:F3} km of ground", worstJump, stepKm));

        var endPoint = new GeoPoint(endLat, endLon);
        var endRay = new GeoPoint(north + 0.05, endLon);
        int wrongPastEnd = 0, testedPastEnd = 0;
        for (int k = 0; k < 5000; k++)
        {
            double lat = endLat + 0.005 + rnd.NextDouble() * 0.06;
            double lon = west + rnd.NextDouble() * (east - west);

            bool land = lon > endLon;
            double d = Math.Min(coast.DistanceKm(lat, lon),
                                Coastline.SegmentDistanceKm(new GeoPoint(lat, lon), endPoint, endRay));
            double signed = (land ? -1.0 : 1.0) * d;

            if (Math.Abs(signed - coast.MarginKm) < 3.0 * texel) continue;

            testedPastEnd++;
            if (field.IsCovered(lat, lon) != (signed <= coast.MarginKm)) wrongPastEnd++;
        }
        Check("the cut carries straight on past the end of the line",
              wrongPastEnd == 0 && testedPastEnd > 4000,
              String.Format("{0} wrong of {1} tested", wrongPastEnd, testedPastEnd));

        // ---- the distance itself, at a texel fine enough to hold the field to it ----
        var fine = CoastlineField.Build(coast, -71.60, -71.40, -32.75, -32.65, 0.02);
        double worstKm = 0.0;
        int wrongSide = 0;
        for (int k = 0; k < 20000; k++)
        {
            double lat = -32.75 + rnd.NextDouble() * 0.10;
            double lon = -71.60 + rnd.NextDouble() * 0.20;

            double exact = coast.DistanceKm(lat, lon);
            double got = fine.SignedDistanceKm(lat, lon);

            if (exact < 8.0)
            {
                double err = Math.Abs(Math.Abs(got) - exact);
                if (err > worstKm) worstKm = err;
            }
            if (exact > 3.0 * 0.02 && (got < 0.0) != (lon > OracleLon(lats, lons, lat)))
            {
                wrongSide++;
            }
        }
        Check("the field measures the same distance the line does", worstKm < 0.02,
              String.Format("worst {0:F5} km, one texel is 0.020", worstKm));
        Check("the field puts every point on the right side", wrongSide == 0,
              String.Format("{0} of 20000 on the wrong side", wrongSide));

        // ---- RangeOver has to bracket the lookups, or the sampler's early-out is a lie ----
        int notBracketed = 0;
        for (int k = 0; k < 300; k++)
        {
            double w = west + rnd.NextDouble() * (east - west - 0.02);
            double s = south + rnd.NextDouble() * (north - south - 0.02);
            double e = w + 0.002 + rnd.NextDouble() * 0.018;
            double n = s + 0.002 + rnd.NextDouble() * 0.018;

            double lo, hi;
            field.RangeOver(w, e, s, n, out lo, out hi);

            for (int q = 0; q < 40; q++)
            {
                double v = field.SignedDistanceKm(s + rnd.NextDouble() * (n - s),
                                                  w + rnd.NextDouble() * (e - w));
                if (v < lo - 1e-6 || v > hi + 1e-6) notBracketed++;
            }
        }
        Check("RangeOver brackets every lookup inside the box", notBracketed == 0,
              String.Format("{0} of 12000 outside the range", notBracketed));

        // ---- and the two answers the sampler short circuits on ----
        double inlandLo, inlandHi, oceanLo, oceanHi;
        field.RangeOver(-71.25, -71.20, -32.70, -32.65, out inlandLo, out inlandHi);
        field.RangeOver(-71.84, -71.80, -32.70, -32.65, out oceanLo, out oceanHi);
        Check("a tile well inland reads as wholly covered", inlandHi <= coast.MarginKm,
              String.Format("furthest {0:F3} km, margin {1:F3}", inlandHi, coast.MarginKm));
        Check("a tile well out to sea reads as wholly cut", oceanLo > coast.MarginKm,
              String.Format("nearest {0:F3} km, margin {1:F3}", oceanLo, coast.MarginKm));

        // ---- the line the editor DRAWS and the line the converter BUILDS must be the same ----
        // The editor exists so the pilot can see the cut before spending two minutes on it. If
        // CutLine and CoastlineField disagree, the picture is a lie and drawing it is pointless.
        // Vertices are held tight; the segments between them are allowed the chord sag that
        // Main's own checks bound, because the field is the one that decides what gets built.
        var drawn = coast.CutLine();
        double vertexGap = 0.0, alongGap = 0.0;
        foreach (var p in drawn)
        {
            double g = Math.Abs(field.SignedDistanceKm(p.Lat, p.Lon) - coast.MarginKm);
            if (g > vertexGap) vertexGap = g;
        }
        for (int i = 0; i + 1 < drawn.Count; i++)
        {
            for (int k = 0; k <= 20; k++)
            {
                double t = k / 20.0;
                double g = Math.Abs(field.SignedDistanceKm(
                    drawn[i].Lat + (drawn[i + 1].Lat - drawn[i].Lat) * t,
                    drawn[i].Lon + (drawn[i + 1].Lon - drawn[i].Lon) * t) - coast.MarginKm);
                if (g > alongGap) alongGap = g;
            }
        }
        Check("the drawn cut sits on the rasterised one", vertexGap < 0.05 && alongGap < 0.5,
              String.Format("{0} points, vertices off by {1:F4} km, segments by {2:F3} km",
                            drawn.Count, vertexGap, alongGap));

        // ---- all four land sides, on coasts that run the way each one is meant for ----
        foreach (bool landEast in new bool[] { true, false })
        {
            var c = new Coastline();
            c.Land = landEast ? LandSide.East : LandSide.West;
            c.MarginKm = 5.556;
            c.BeginStroke();
            for (int i = 0; i < 20; i++) c.AddPoint(-33.0 + i / 19.0, -71.5);
            c.EndStroke(0.0);

            var g = CoastlineField.Build(c, -71.7, -71.3, -33.1, -31.7, 0.1);
            double toEast = g.SignedDistanceKm(-32.5, -71.4);
            double toWest = g.SignedDistanceKm(-32.5, -71.6);

            Check("land to the " + (landEast ? "east" : "west") + " puts the sea on the other side",
                  landEast ? (toEast < 0.0 && toWest > 0.0) : (toEast > 0.0 && toWest < 0.0),
                  String.Format("east {0:F3} km, west {1:F3} km", toEast, toWest));

            // Past the end of the drawn line the coast has to carry straight on, or the last few
            // kilometres of a package come out cut off at both ends.
            double beyondSea = g.SignedDistanceKm(-31.8, landEast ? -71.6 : -71.4);
            double beyondLand = g.SignedDistanceKm(-31.8, landEast ? -71.4 : -71.6);
            Check("beyond the end of the line the sides still hold",
                  beyondSea > 0.0 && beyondLand < 0.0,
                  String.Format("sea side {0:F3} km, land side {1:F3} km", beyondSea, beyondLand));
        }

        foreach (bool landNorth in new bool[] { true, false })
        {
            var c = new Coastline();
            c.Land = landNorth ? LandSide.North : LandSide.South;
            c.MarginKm = 5.556;
            c.BeginStroke();
            for (int i = 0; i < 20; i++) c.AddPoint(-32.5, -71.5 + i / 19.0);
            c.EndStroke(0.0);

            var g = CoastlineField.Build(c, -71.6, -70.4, -32.7, -32.3, 0.1);
            double toNorth = g.SignedDistanceKm(-32.4, -71.0);
            double toSouth = g.SignedDistanceKm(-32.6, -71.0);

            Check("land to the " + (landNorth ? "north" : "south") + " puts the sea on the other side",
                  landNorth ? (toNorth < 0.0 && toSouth > 0.0) : (toNorth > 0.0 && toSouth < 0.0),
                  String.Format("north {0:F3} km, south {1:F3} km", toNorth, toSouth));
        }
    }

    // =========================================================================================
    // Islands - closed rings with the land inside, alone or together with a coast
    // =========================================================================================

    /// <summary>
    /// A ring round a centre, in points, with a wobbly radius in km. Anticlockwise unless
    /// clockwise is set.
    /// </summary>
    static List<GeoPoint> Blob(double lat, double lon, double radiusKm, int n, bool clockwise,
        double wobble)
    {
        var pts = new List<GeoPoint>();
        for (int i = 0; i < n; i++)
        {
            double t = 2.0 * Math.PI * i / n * (clockwise ? -1.0 : 1.0);
            double r = radiusKm * (1.0 + wobble * (0.15 * Math.Sin(3.0 * t) + 0.07 * Math.Sin(7.0 * t)));
            pts.Add(new GeoPoint(lat + r * Math.Sin(t) / Coastline.KmPerDegLat,
                                 lon + r * Math.Cos(t) / Coastline.KmPerDegLon(lat)));
        }
        return pts;
    }

    /// <summary>Draws a ring as one island, in the given number of strokes.</summary>
    static void DrawIsland(Coastline c, List<GeoPoint> ring, int strokes)
    {
        c.StartIsland();
        int per = ring.Count / strokes;
        for (int k = 0; k < strokes; k++)
        {
            int from = k * per;
            int to = (k == strokes - 1) ? ring.Count : from + per + 1;
            c.BeginStroke();
            for (int i = from; i < to; i++)
            {
                c.AddPoint(ring[i].Lat, ring[i].Lon);
            }
            c.EndStroke(0.0);
        }
        c.EndIsland();
    }

    /// <summary>
    /// Whether a point is inside a ring, by the winding number. The field uses a ray cast, so
    /// this shares no code and no method with the thing under test.
    /// </summary>
    static bool InsideRing(List<GeoPoint> ring, double lat, double lon)
    {
        double sum = 0.0;
        for (int i = 0; i < ring.Count; i++)
        {
            GeoPoint a = ring[i];
            GeoPoint b = ring[(i + 1) % ring.Count];
            double a0 = Math.Atan2(a.Lat - lat, a.Lon - lon);
            double a1 = Math.Atan2(b.Lat - lat, b.Lon - lon);
            double d = a1 - a0;
            while (d > Math.PI) d -= 2.0 * Math.PI;
            while (d < -Math.PI) d += 2.0 * Math.PI;
            sum += d;
        }
        return Math.Abs(sum) > Math.PI;
    }

    static void IslandTests()
    {
        // An island the size of Mallorca, drawn clockwise in three strokes, and two islets. Its
        // radius runs from 23.4 km (north) to 36.6 km (south). One islet is about 15 km off its
        // south shore, so their margins do not meet. The other is 2 km off its north shore,
        // inside the margin.
        var big = Blob(39.6, 2.9, 30.0, 400, true, 1.0);
        var far = Blob(39.6 - 55.0 / Coastline.KmPerDegLat, 2.9, 3.0, 40, false, 0.5);
        var close = Blob(39.6 + 26.4 / Coastline.KmPerDegLat, 2.9, 1.0, 20, false, 0.0);

        var c = new Coastline();
        c.MarginKm = 5.556;
        DrawIsland(c, big, 3);
        DrawIsland(c, far, 1);
        DrawIsland(c, close, 2);

        Check("three islands and no coast",
              c.Islands.Count == 3 && !c.HasCoast && !c.IsEmpty,
              String.Format("{0} islands, {1} coast points", c.Islands.Count, c.Points.Count));
        Check("an island drawn all the way round closes with no gap", c.LongestJoinKm < 1.0,
              String.Format("widest join {0:F3} km", c.LongestJoinKm));

        // Half an island is an open ring, and its closing join says so.
        var half = new Coastline();
        half.StartIsland();
        half.BeginStroke();
        for (int i = 0; i < 200; i++) half.AddPoint(big[i].Lat, big[i].Lon);
        half.EndStroke(0.0);
        Check("an island still being drawn shows its closing gap", half.LongestJoinKm > 40.0,
              String.Format("{0:F1} km", half.LongestJoinKm));

        // A large island takes more than one sitting: saved half drawn and loaded again, it carries
        // on as the same island, and so does clicking Island again after closing it too early.
        string halfPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aeroscenery_half_island.txt");
        half.Save(halfPath);
        var resumed = Coastline.Load(halfPath);
        resumed.BeginStroke();
        for (int i = 199; i < 400; i++) resumed.AddPoint(big[i].Lat, big[i].Lon);
        resumed.EndStroke(0.0);
        Check("a half-drawn island carries on after a save and load",
              resumed.Islands.Count == 1 && resumed.LongestJoinKm < 1.0,
              String.Format("{0} islands, widest join {1:F2} km", resumed.Islands.Count, resumed.LongestJoinKm));

        half.EndIsland();
        half.StartIsland();
        int again = half.DrawingIsland;
        resumed.EndIsland();
        resumed.StartIsland();
        Check("Island again continues an open island, and starts a new one after a closed one",
              again == 0 && resumed.DrawingIsland == 1,
              String.Format("open: island {0}, closed: island {1}", again, resumed.DrawingIsland));

        // ---- save, load, undo ----
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aeroscenery_islands_test.txt");
        c.Save(path);
        var back = Coastline.Load(path);
        bool same = back.Islands.Count == c.Islands.Count && back.StrokeCount == c.StrokeCount;
        for (int r = 0; same && r < c.Islands.Count; r++)
        {
            same = back.Islands[r].Count == c.Islands[r].Count;
            for (int i = 0; same && i < c.Islands[r].Count; i++)
            {
                same = back.Islands[r][i].Lat == c.Islands[r][i].Lat
                    && back.Islands[r][i].Lon == c.Islands[r][i].Lon;
            }
        }
        Check("islands save and load round trip exactly", same,
              String.Format("{0} islands, {1} strokes", back.Islands.Count, back.StrokeCount));

        back.UndoStroke();
        back.UndoStroke();
        Check("undo takes the islet's strokes back, last drawn first",
              back.Islands.Count == 2 && back.StrokeCount == c.StrokeCount - 2,
              String.Format("{0} islands, {1} strokes", back.Islands.Count, back.StrokeCount));

        back.StartIsland();
        back.BeginStroke(); back.AddPoint(39.0, 2.0); back.AddPoint(39.01, 2.0); back.EndStroke(0.0);
        back.EndIsland();
        back.BeginStroke(); back.AddPoint(39.0, 3.0); back.AddPoint(39.1, 3.0); back.EndStroke(0.0);
        back.Save(path);
        var mixed = Coastline.Load(path);
        Check("coast and island strokes keep their line through a save",
              mixed.Points.Count == 2 && mixed.StrokeCount == back.StrokeCount,
              String.Format("{0} coast points, {1} strokes", mixed.Points.Count, mixed.StrokeCount));

        // ---- the cut round an island ----
        var cuts = c.CutLines();
        double worst = 0.0;
        int closedCount = 0;
        int cutPoints = 0;
        foreach (var cut in cuts)
        {
            if (cut.Count > 2 && cut[0].Lat == cut[cut.Count - 1].Lat
                              && cut[0].Lon == cut[cut.Count - 1].Lon)
            {
                closedCount++;
            }
            foreach (var p in cut)
            {
                worst = Math.Max(worst, Math.Abs(c.DistanceKm(p.Lat, p.Lon) - c.MarginKm));
                cutPoints++;
            }
        }
        Check("every island cut point sits at the margin", worst < 0.05,
              String.Format("{0} points, worst {1:F4} km", cutPoints, worst));

        // The islet 2 km off shares the big island's margin, so each of the two cuts stops where
        // it runs into the other's margin: two open pieces. The far islet is one closed piece.
        Check("an islet near its island makes open pieces, a lone islet a closed one",
              cuts.Count == 3 && closedCount == 1,
              String.Format("{0} pieces, {1} closed", cuts.Count, closedCount));

        // Between its points, no piece may cut through the covered area: that is the chord a
        // join across another margin would draw.
        double chordInside = 0.0;
        foreach (var cut in cuts)
        {
            for (int i = 0; i + 1 < cut.Count; i++)
            {
                for (int k = 1; k < 8; k++)
                {
                    double lat = cut[i].Lat + (cut[i + 1].Lat - cut[i].Lat) * k / 8.0;
                    double lon = cut[i].Lon + (cut[i + 1].Lon - cut[i].Lon) * k / 8.0;
                    chordInside = Math.Max(chordInside, c.MarginKm - c.DistanceKm(lat, lon));
                }
            }
        }
        Check("no piece of the island cut runs through the covered area", chordInside < 0.1,
              String.Format("worst {0:F3} km inside the margin", chordInside));

        bool outside = true;
        foreach (var cut in cuts)
        {
            foreach (var p in cut)
            {
                foreach (var ring in c.Islands)
                {
                    if (InsideRing(ring, p.Lat, p.Lon)) outside = false;
                }
            }
        }
        Check("the cut lies out at sea, whichever way round the island was drawn", outside, null);

        // ---- the field over the big island's box ----
        double west = 2.9 - 45.0 / Coastline.KmPerDegLon(39.6);
        double east = 2.9 + 45.0 / Coastline.KmPerDegLon(39.6);
        double south = 39.6 - 70.0 / Coastline.KmPerDegLat;
        double north = 39.6 + 45.0 / Coastline.KmPerDegLat;

        var field = CoastlineField.Build(c, west, east, south, north, 0.05);
        var rnd = new Random(5);
        int wrong = 0, tested = 0;
        for (int i = 0; i < 20000; i++)
        {
            double lat = south + rnd.NextDouble() * (north - south);
            double lon = west + rnd.NextDouble() * (east - west);
            double d = c.DistanceKm(lat, lon);
            if (Math.Abs(d - c.MarginKm) < 0.05 || d < 0.05)
            {
                continue;
            }
            bool land = false;
            foreach (var ring in c.Islands) land |= InsideRing(ring, lat, lon);
            bool want = land || d <= c.MarginKm;
            if (field.IsCovered(lat, lon) != want) wrong++;
            if ((field.SignedDistanceKm(lat, lon) < 0.0) != land) wrong++;
            tested++;
        }
        Check("island coverage matches inside-or-within-the-margin", wrong == 0 && tested > 19000,
              String.Format("{0} wrong of {1} tested", wrong, tested));

        double onCut = 0.0;
        foreach (var cut in cuts)
        {
            foreach (var p in cut)
            {
                onCut = Math.Max(onCut, Math.Abs(field.SignedDistanceKm(p.Lat, p.Lon) - c.MarginKm));
            }
        }
        Check("the drawn island cut sits on the rasterised one", onCut < 0.05,
              String.Format("worst {0:F4} km", onCut));

        // ---- two islands drawn overlapping stay land where they overlap ----
        var twin = new Coastline();
        DrawIsland(twin, Blob(39.6, 2.9, 5.0, 60, false, 0.0), 1);
        DrawIsland(twin, Blob(39.6, 2.9 + 6.0 / Coastline.KmPerDegLon(39.6), 5.0, 60, true, 0.0), 1);
        var tf = CoastlineField.Build(twin, 2.7, 3.1, 39.5, 39.7, 0.05);
        double overlap = tf.SignedDistanceKm(39.6, 2.9 + 3.0 / Coastline.KmPerDegLon(39.6));
        Check("where two islands overlap it is land", overlap < 0.0,
              String.Format("{0:F3} km", overlap));

        // ---- a coast and an island in one file ----
        var both = new Coastline();
        both.Land = LandSide.East;
        both.MarginKm = 5.556;
        both.BeginStroke();
        for (int i = 0; i < 60; i++) both.AddPoint(-33.3 + i * 0.01, -71.5);
        both.EndStroke(0.0);
        var isle = Blob(-33.0, -71.5 - 20.0 / Coastline.KmPerDegLon(-33.0), 4.0, 50, false, 0.5);
        DrawIsland(both, isle, 1);

        var bf = CoastlineField.Build(both, -71.9, -71.3, -33.2, -32.8, 0.05);
        int bothWrong = 0, bothTested = 0;
        for (int i = 0; i < 10000; i++)
        {
            double lat = -33.2 + rnd.NextDouble() * 0.4;
            double lon = -71.9 + rnd.NextDouble() * 0.6;
            double d = both.DistanceKm(lat, lon);
            if (Math.Abs(d - both.MarginKm) < 0.05 || d < 0.05)
            {
                continue;
            }
            bool land = lon > -71.5 || InsideRing(isle, lat, lon);
            if (bf.IsCovered(lat, lon) != (land || d <= both.MarginKm)) bothWrong++;
            bothTested++;
        }
        Check("a coast and an island together: landward of the coast or inside the island",
              bothWrong == 0 && bothTested > 9000,
              String.Format("{0} wrong of {1} tested", bothWrong, bothTested));

        // ---- which squares are cut ----
        string why;
        var part = c.ForSquare(2.1, 2.8, 39.15, 39.69, out why);
        Check("a square an island reaches is cut at that island",
              part != null && part.Islands.Count >= 1 && !part.HasCoast, why);

        part = c.ForSquare(10.0, 10.7, 39.15, 39.69, out why);
        Check("a square no island comes near is not cut", part == null && why != null, why);

        part = both.ForSquare(-72.0, -71.0, -33.25, -32.75, out why);
        Check("inside the coast's stretch: the coast and the island near it",
              part != null && part.HasCoast && part.Islands.Count == 1, why);

        part = both.ForSquare(-72.0, -71.0, -33.25, -32.5, out why);
        Check("outside the coast's stretch: no cut, not even at the island", part == null, why);

        // ---- how long a field over one grid square takes, at the size of Mallorca ----
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var square = CoastlineField.Build(c, 2.109, 2.812, 39.151, 39.686);
        sw.Stop();
        Check("a field over one grid square next to a large island builds in reasonable time",
              sw.Elapsed.TotalSeconds < 30.0,
              String.Format("{0} x {1} texels in {2:F1} s", square.Width, square.Height,
                            sw.Elapsed.TotalSeconds));
    }
}
