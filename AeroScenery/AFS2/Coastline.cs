using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AeroScenery.AFS2
{
    /// <summary>Which side of the drawn line the land is on.</summary>
    public enum LandSide
    {
        East,
        West,
        North,
        South
    }

    public struct GeoPoint
    {
        public double Lat;
        public double Lon;

        public GeoPoint(double lat, double lon)
        {
            Lat = lat;
            Lon = lon;
        }
    }

    /// <summary>
    /// A hand-drawn coastline, and the rule that turns it into where photoscenery stops.
    ///
    /// The line is the WATERLINE, not the cut. The cut sits a margin out to sea, and the user
    /// never draws it: they trace something they can actually see, and the margin is applied
    /// afterwards. Two things fall out of that split, and they are the whole reason for it.
    ///
    /// The margin becomes a parameter rather than a redraw - re-cut at 2, 3 or 5 NM without
    /// touching the line. And precision stops mattering: with the cut 5.5 km offshore, being 500 m
    /// wrong about the waterline moves the cut 500 m over open water, which is invisible. Drawing
    /// the cut directly would need tens of metres, because there Aerofly's coast and the imagery's
    /// coast disagree and any error shows as a fringe of the wrong texture along every beach.
    ///
    /// Coverage is:
    ///
    ///     landward of the line, OR within MarginKm of the line
    ///
    /// which is just "every point within MarginKm of the land". Stating it as a distance rather
    /// than as an offset polygon is what makes headlands come out ROUNDED, radius = the margin -
    /// the set of points within d of a point is a disc. IPACS' own sea cut has exactly that
    /// rounding, which is the signature of the same operation, and it means the radius of one of
    /// their arcs reads off their margin directly. Offsetting each segment and joining the results
    /// in a point would instead grow a spike at every cape.
    ///
    /// Distances are in kilometres through a local equirectangular scale rather than a great
    /// circle: over the few kilometres a margin spans, the difference is far below the metre and
    /// the saving is that everything stays plain arithmetic.
    ///
    /// The file holds two kinds of line. The COAST is one open line with the land on one side,
    /// as on a mainland. An ISLAND is a closed ring with the land inside. The coast can be absent,
    /// and there can be any number of islands. A point is land when it is landward of the coast
    /// or inside an island, and the margin is measured to the nearest line of either kind.
    ///
    /// A file of LAKES (IsLakes, lakes.txt) uses the same class for the opposite job. It holds only
    /// closed rings, and Islands lists them. The photoscenery is cut out INSIDE each ring, so
    /// Aerofly draws its own imagery on the lake. The cut is the drawn line itself: the margin is
    /// 0. Water has no detail to lose, and Bing's water is where its imagery is worst.
    /// </summary>
    public class Coastline
    {
        public const double KmPerDegLat = 110.9;

        /// <summary>The island number of a stroke that belongs to the coast.</summary>
        public const int CoastStroke = -1;

        /// <summary>
        /// How near the end of a lake must come to its start for the lake to be closed, in km.
        /// An island uses its margin for this, but a lake has a margin of 0. Half a kilometre is
        /// easy to hit by hand at the zoom the editor asks for, and the straight join is on the
        /// map, where it can be seen.
        /// </summary>
        public const double LakeCloseKm = 0.5;

        /// <summary>One drag of the mouse, and the line it belongs to.</summary>
        private sealed class Stroke
        {
            public List<GeoPoint> Points = new List<GeoPoint>();

            /// <summary>CoastStroke, or the number of the island.</summary>
            public int Island = CoastStroke;
        }

        private readonly List<Stroke> strokes = new List<Stroke>();
        private Stroke current;
        private List<GeoPoint> pointsCache;
        private List<List<GeoPoint>> islandsCache;
        private double longestJoinKm;

        public Coastline()
        {
            Land = LandSide.East;
            MarginKm = 3.0 * 1.852;
            DrawingIsland = CoastStroke;
        }

        /// <summary>
        /// Where the next stroke goes: CoastStroke for the coast, or the number of an island.
        /// StartIsland and EndIsland set it.
        /// </summary>
        public int DrawingIsland { get; private set; }

        /// <summary>
        /// Starts an island. The strokes drawn from now until EndIsland make one closed ring.
        ///
        /// If the last stroke drawn belongs to an island that is still open, that island carries
        /// on instead of a new one starting. A large island takes more than one sitting, and
        /// starting a second island on the way round would leave two broken rings.
        /// </summary>
        public void StartIsland()
        {
            int open = OpenIsland();
            if (open != CoastStroke)
            {
                DrawingIsland = open;
                return;
            }

            int next = 0;
            foreach (var s in strokes)
            {
                if (s.Island >= next)
                {
                    next = s.Island + 1;
                }
            }
            DrawingIsland = next;
        }

        /// <summary>Ends the island. The next strokes go to the coast again.</summary>
        public void EndIsland()
        {
            DrawingIsland = CoastStroke;
        }

        /// <summary>
        /// The island of the last stroke drawn, if it is still open: the join from its last point
        /// back to its first is longer than CloseKm, which is where the toolbar shows GAP. Else
        /// CoastStroke. A finished island closes within CloseKm, or GAP would say otherwise.
        /// </summary>
        public int OpenIsland()
        {
            if (strokes.Count == 0)
            {
                return CoastStroke;
            }

            int island = strokes[strokes.Count - 1].Island;
            if (island == CoastStroke)
            {
                return CoastStroke;
            }

            var ring = new List<GeoPoint>();
            foreach (var s in Chain(StrokesOf(island)))
            {
                ring.AddRange(s);
            }
            if (ring.Count < 3)
            {
                return island;
            }
            return PointDistanceKm(ring[ring.Count - 1], ring[0]) > CloseKm ? island : CoastStroke;
        }

        /// <summary>
        /// True for a file of lakes: rings only, the photoscenery cut out inside each one, at a
        /// margin of 0. See NewLakes and LoadLakes.
        /// </summary>
        public bool IsLakes { get; private set; }

        /// <summary>An empty file of lakes.</summary>
        public static Coastline NewLakes()
        {
            var c = new Coastline();
            c.IsLakes = true;
            c.MarginKm = 0.0;
            return c;
        }

        /// <summary>
        /// How near the last point of a ring must come to its first for the ring to be closed:
        /// the margin for an island, LakeCloseKm for a lake.
        /// </summary>
        public double CloseKm { get { return IsLakes ? LakeCloseKm : MarginKm; } }

        /// <summary>Which side of the line is land. East for a west-facing coast like Chile's.</summary>
        public LandSide Land { get; set; }

        /// <summary>How far out to sea the cut sits. 3 NM by default; see docs/user-guide.md, section 5.</summary>
        public double MarginKm { get; set; }

        public int StrokeCount { get { return strokes.Count; } }

        /// <summary>True when there is neither a coast nor an island to cut at.</summary>
        public bool IsEmpty { get { return !HasCoast && Islands.Count == 0; } }

        /// <summary>True when the file holds an open coast, and not only islands.</summary>
        public bool HasCoast { get { return Points.Count >= 2; } }

        /// <summary>
        /// Each island as a closed ring: its strokes joined as the coast's are, and closed from the
        /// last point back to the first. The first point is not repeated at the end. An island with
        /// fewer than three points encloses nothing and is left out.
        ///
        /// Treat the lists as read only, as for Points.
        /// </summary>
        public List<List<GeoPoint>> Islands
        {
            get
            {
                if (pointsCache == null)
                {
                    Rebuild();
                }
                return islandsCache;
            }
        }

        /// <summary>
        /// The coast's strokes' points end to end, in order along the coast, which is the polyline
        /// the margin is measured from. The islands are not in it; see Islands.
        ///
        /// Strokes are kept separate only so an undo can drop the last one whole. They are joined
        /// rather than treated as separate lines because the coast is one line: the user releases
        /// the button to pan and carries on, and a gap between where they let go and where they
        /// picked up is a gap in the coast, not a new one.
        ///
        /// Treat the list as read only. It is cached, so the same list comes back until the line
        /// changes, and a caller that edits it edits the coastline.
        /// </summary>
        public List<GeoPoint> Points
        {
            get
            {
                if (pointsCache == null)
                {
                    Rebuild();
                }
                return pointsCache;
            }
        }

        /// <summary>
        /// The widest gap between one stroke and the next, in km, once they are in order.
        ///
        /// A few hundred metres is the ordinary business of letting go of the button to pan.
        /// Kilometres mean a piece of coast was never drawn, and the straight line laid across the
        /// gap counts as coast to the converter. Nothing can invent the missing piece, so the only
        /// useful thing to do with this number is show it.
        ///
        /// An island counts its closing join too, from its last point back to its first. While an
        /// island is still being drawn round, that join is long, and the number says so.
        /// </summary>
        public double LongestJoinKm
        {
            get
            {
                if (pointsCache == null)
                {
                    Rebuild();
                }
                return longestJoinKm;
            }
        }

        private void Rebuild()
        {
            longestJoinKm = 0.0;
            pointsCache = Join(StrokesOf(CoastStroke));

            // Islands in the order they were first drawn, so their numbers need not be dense.
            var order = new List<int>();
            foreach (var s in strokes)
            {
                if (s.Island != CoastStroke && !order.Contains(s.Island))
                {
                    order.Add(s.Island);
                }
            }

            islandsCache = new List<List<GeoPoint>>();
            foreach (int island in order)
            {
                List<GeoPoint> ring = Join(StrokesOf(island));
                if (ring.Count < 3)
                {
                    continue;
                }

                double closing = PointDistanceKm(ring[ring.Count - 1], ring[0]);
                if (closing > longestJoinKm)
                {
                    longestJoinKm = closing;
                }
                islandsCache.Add(ring);
            }
        }

        private List<List<GeoPoint>> StrokesOf(int island)
        {
            var found = new List<List<GeoPoint>>();
            foreach (var s in strokes)
            {
                if (s.Island == island)
                {
                    found.Add(s.Points);
                }
            }
            return found;
        }

        /// <summary>Chains strokes into one line and records the widest join between them.</summary>
        private List<GeoPoint> Join(List<List<GeoPoint>> pieces)
        {
            var all = new List<GeoPoint>();

            foreach (var s in Chain(pieces))
            {
                if (all.Count > 0)
                {
                    double gap = PointDistanceKm(all[all.Count - 1], s[0]);
                    if (gap > longestJoinKm)
                    {
                        longestJoinKm = gap;
                    }
                }
                all.AddRange(s);
            }
            return all;
        }

        /// <summary>
        /// The strokes put in order along the coast, each one turned to face the same way.
        ///
        /// They used to be joined in the order they were DRAWN, which quietly made the drawing
        /// order part of the data. Draw the south end after the north end and the line gained a
        /// straight segment between the two, tens of kilometres long, which the converter reads as
        /// coast. That is a silent way to lose an afternoon, and it also meant a coast could only
        /// ever be extended at one end - carrying on southward tomorrow meant starting again.
        ///
        /// Each stroke is now attached to whichever end of the chain it is nearest, reversed if it
        /// faces the wrong way. A coast is one line, so its pieces have exactly one sensible
        /// arrangement and the nearest ends find it. Draw in any order, in either direction, and
        /// start anywhere.
        ///
        /// What this cannot do is invent a piece that was never drawn. A gap is still a gap and
        /// still gets a straight line across it; LongestJoinKm is what makes that visible rather
        /// than silent.
        ///
        /// An island's strokes are chained the same way, each island on its own.
        /// </summary>
        private static List<List<GeoPoint>> Chain(List<List<GeoPoint>> strokes)
        {
            var ordered = new List<List<GeoPoint>>();
            if (strokes.Count == 0)
            {
                return ordered;
            }

            var used = new bool[strokes.Count];
            ordered.Add(new List<GeoPoint>(strokes[0]));
            used[0] = true;

            for (int placed = 1; placed < strokes.Count; placed++)
            {
                List<GeoPoint> lastStroke = ordered[ordered.Count - 1];
                GeoPoint head = ordered[0][0];
                GeoPoint tail = lastStroke[lastStroke.Count - 1];

                double best = Double.MaxValue;
                int bestStroke = -1;
                bool atTail = true;
                bool flip = false;

                for (int i = 0; i < strokes.Count; i++)
                {
                    if (used[i])
                    {
                        continue;
                    }

                    List<GeoPoint> s = strokes[i];
                    GeoPoint a = s[0];
                    GeoPoint b = s[s.Count - 1];

                    // Four ways to attach it: either end of the chain, either way round.
                    double d = PointDistanceKm(tail, a);
                    if (d < best) { best = d; bestStroke = i; atTail = true; flip = false; }

                    d = PointDistanceKm(tail, b);
                    if (d < best) { best = d; bestStroke = i; atTail = true; flip = true; }

                    d = PointDistanceKm(head, b);
                    if (d < best) { best = d; bestStroke = i; atTail = false; flip = false; }

                    d = PointDistanceKm(head, a);
                    if (d < best) { best = d; bestStroke = i; atTail = false; flip = true; }
                }

                if (bestStroke < 0)
                {
                    break;
                }

                var add = new List<GeoPoint>(strokes[bestStroke]);
                if (flip)
                {
                    add.Reverse();
                }
                if (atTail)
                {
                    ordered.Add(add);
                }
                else
                {
                    ordered.Insert(0, add);
                }
                used[bestStroke] = true;
            }

            return ordered;
        }

        public static double KmPerDegLon(double lat)
        {
            return 111.320 * Math.Cos(lat * Math.PI / 180.0);
        }

        public void BeginStroke()
        {
            current = new Stroke { Island = DrawingIsland };
            strokes.Add(current);
            pointsCache = null;
        }

        public void AddPoint(double lat, double lon)
        {
            if (current == null)
            {
                BeginStroke();
            }
            current.Points.Add(new GeoPoint(lat, lon));
            pointsCache = null;
        }

        /// <summary>
        /// Ends a stroke and thins it, because a drag produces a point every few pixels and the
        /// margin cannot resolve them. Tolerance is in km and belongs well under the margin: at a
        /// fifth of it, the thinning is invisible in the cut and the vertex count drops by orders
        /// of magnitude.
        /// </summary>
        public void EndStroke(double toleranceKm)
        {
            if (current == null)
            {
                return;
            }
            if (current.Points.Count < 2)
            {
                strokes.Remove(current);
            }
            else
            {
                current.Points = Simplify(current.Points, toleranceKm);
            }
            current = null;
            pointsCache = null;
        }

        /// <summary>
        /// Drops the stroke drawn most recently, which is not necessarily the one at either end of
        /// the coast now that the strokes are ordered geographically. Undo means "take back what I
        /// just did", so it stays the last one DRAWN.
        /// </summary>
        public bool UndoStroke()
        {
            if (strokes.Count == 0)
            {
                return false;
            }
            strokes.RemoveAt(strokes.Count - 1);
            current = null;
            pointsCache = null;
            return true;
        }

        public void Clear()
        {
            strokes.Clear();
            current = null;
            pointsCache = null;
            DrawingIsland = CoastStroke;
        }

        /// <summary>
        /// The part of the file that may cut one grid square, or null to convert the square
        /// without a cut. Why says the reason when it is null.
        ///
        /// The coast is used only if the square lies wholly inside the stretch it covers. Past the
        /// end of the coast the field carries it straight on, so a square outside that stretch
        /// would be cut against a guess. This was learnt on a real square 2.1 degrees past the end
        /// of the line: it lost two thirds of its tiles to a sea that was not there.
        ///
        /// An island has no ends, so there is no guess. An island is used if the square comes
        /// within the margin of its box. An island further away changes nothing in the square.
        ///
        /// Two cases give no cut. A square outside the coast's stretch is not cut at all, not even
        /// at an island: the mainland in it is unknown, and a cut at the island alone would remove
        /// that mainland. And when the file holds only islands, a square that no island comes near
        /// is not cut. Both are the safe reading of a line that was not drawn there.
        /// </summary>
        public Coastline ForSquare(double west, double east, double south, double north,
            out string why)
        {
            why = null;

            if (HasCoast)
            {
                bool alongLat;
                double from, to;
                CoastStretch(out alongLat, out from, out to);

                double squareFrom = alongLat ? south : west;
                double squareTo = alongLat ? north : east;

                if (!(squareFrom >= from && squareTo <= to))
                {
                    why = String.Format(CultureInfo.InvariantCulture,
                        "it is not wholly inside the stretch the coast covers ({0} {1:0.00} to {2:0.00})",
                        alongLat ? "lat" : "lon", from, to);
                    return null;
                }
            }

            var part = new Coastline();
            part.IsLakes = IsLakes;
            part.Land = Land;
            part.MarginKm = MarginKm;

            if (HasCoast)
            {
                foreach (var s in strokes)
                {
                    if (s.Island == CoastStroke)
                    {
                        part.strokes.Add(new Stroke { Points = new List<GeoPoint>(s.Points) });
                    }
                }
            }

            // Only closed rings, as in Islands. A little slack beyond the margin, because the
            // converter samples a box a few texels larger than the square.
            double reachKm = MarginKm + 1.0;
            var rings = Islands;
            for (int r = 0; r < rings.Count; r++)
            {
                double lo = Double.MaxValue, hi = Double.MinValue;
                double w = Double.MaxValue, e = Double.MinValue;
                foreach (var p in rings[r])
                {
                    if (p.Lat < lo) lo = p.Lat;
                    if (p.Lat > hi) hi = p.Lat;
                    if (p.Lon < w) w = p.Lon;
                    if (p.Lon > e) e = p.Lon;
                }

                double padLat = reachKm / KmPerDegLat;
                double widest = Math.Max(Math.Abs(lo), Math.Abs(hi)) + padLat;
                double padLon = reachKm / KmPerDegLon(Math.Min(widest, 89.0));

                bool near = hi + padLat >= south && lo - padLat <= north
                         && e + padLon >= west && w - padLon <= east;
                if (near)
                {
                    // One ring, one stroke: it is already chained and closes by itself.
                    part.strokes.Add(new Stroke { Points = new List<GeoPoint>(rings[r]), Island = r });
                }
            }

            if (part.IsEmpty)
            {
                why = "no island comes within the margin of it, and there is no coast";
                return null;
            }
            return part;
        }

        /// <summary>
        /// The stretch the coast covers, along the way it runs: latitude when the land is east or
        /// west, longitude when it is north or south. CoastlineField makes the same choice.
        /// </summary>
        public void CoastStretch(out bool alongLat, out double from, out double to)
        {
            alongLat = Land == LandSide.East || Land == LandSide.West;
            from = Double.MaxValue;
            to = Double.MinValue;
            foreach (var p in Points)
            {
                double v = alongLat ? p.Lat : p.Lon;
                if (v < from) from = v;
                if (v > to) to = v;
            }
        }

        // -----------------------------------------------------------------------------------
        // geometry
        // -----------------------------------------------------------------------------------

        /// <summary>Distance in km between two points.</summary>
        private static double PointDistanceKm(GeoPoint a, GeoPoint b)
        {
            double x, y;
            Delta(a, b, out x, out y);
            return Math.Sqrt(x * x + y * y);
        }

        /// <summary>Local km offsets of b from a, x east and y north.</summary>
        private static void Delta(GeoPoint a, GeoPoint b, out double x, out double y)
        {
            double midLat = 0.5 * (a.Lat + b.Lat);
            x = (b.Lon - a.Lon) * KmPerDegLon(midLat);
            y = (b.Lat - a.Lat) * KmPerDegLat;
        }

        /// <summary>
        /// Shortest distance in km from a point to the nearest line: the coast or any island.
        /// </summary>
        public double DistanceKm(double lat, double lon)
        {
            var p = new GeoPoint(lat, lon);
            double best = Double.MaxValue;

            foreach (var s in Segments())
            {
                double d = SegmentDistanceKm(p, s.A, s.B);
                if (d < best)
                {
                    best = d;
                }
            }
            return best;
        }

        /// <summary>One straight piece of a line.</summary>
        public struct Segment
        {
            public GeoPoint A;
            public GeoPoint B;

            public Segment(GeoPoint a, GeoPoint b)
            {
                A = a;
                B = b;
            }
        }

        /// <summary>
        /// Every segment of every line: the coast's, then each island's, with the segment that
        /// closes the island from its last point back to its first.
        /// </summary>
        public List<Segment> Segments()
        {
            var segs = new List<Segment>();

            var pts = Points;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                segs.Add(new Segment(pts[i], pts[i + 1]));
            }

            foreach (var ring in Islands)
            {
                for (int i = 0; i < ring.Count; i++)
                {
                    segs.Add(new Segment(ring[i], ring[(i + 1) % ring.Count]));
                }
            }
            return segs;
        }

        /// <summary>
        /// Shortest distance in km from a point to one segment. Public because CoastlineField
        /// measures the same thing a few million times and has to get the same answer this class
        /// would: one definition of the distance, or the rasterised cut and the drawn one drift.
        /// </summary>
        public static double SegmentDistanceKm(GeoPoint p, GeoPoint a, GeoPoint b)
        {
            double abx, aby, apx, apy;
            Delta(a, b, out abx, out aby);
            Delta(a, p, out apx, out apy);

            double len2 = abx * abx + aby * aby;
            double t = (len2 <= 0.0) ? 0.0 : (apx * abx + apy * aby) / len2;
            if (t < 0.0) t = 0.0;
            else if (t > 1.0) t = 1.0;

            double dx = apx - t * abx;
            double dy = apy - t * aby;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Which hand the sea is on, as +1 or -1, decided once for the whole line.
        ///
        /// Per segment it cannot be decided at all: a stretch of coast running exactly east-west
        /// has one normal north and one south, and "land is east" says nothing about either. So
        /// the handedness is settled from the line's overall run and then applied to every
        /// segment, which is also what keeps a wiggly coast from flipping its cut inside out at
        /// each wiggle.
        /// </summary>
        private int SeaHand(List<GeoPoint> pts)
        {
            double x, y;
            Delta(pts[0], pts[pts.Count - 1], out x, out y);
            if (Math.Abs(x) < 1e-9 && Math.Abs(y) < 1e-9)
            {
                return 1;
            }

            // Right of travel, i.e. the direction rotated -90 degrees.
            double rx = y, ry = -x;

            switch (Land)
            {
                case LandSide.East: return rx < 0 ? 1 : -1;
                case LandSide.West: return rx > 0 ? 1 : -1;
                case LandSide.North: return ry < 0 ? 1 : -1;
                default: return ry > 0 ? 1 : -1;
            }
        }

        /// <summary>
        /// Which hand the sea is on for an island, from the way round it was drawn. The land is
        /// inside, so the sea is on the right when the ring runs anticlockwise - positive area.
        /// </summary>
        private static int RingSeaHand(List<GeoPoint> ring)
        {
            double area2 = 0.0;
            for (int i = 0; i < ring.Count; i++)
            {
                double x0, y0, x1, y1;
                Delta(ring[0], ring[i], out x0, out y0);
                Delta(ring[0], ring[(i + 1) % ring.Count], out x1, out y1);
                area2 += x0 * y1 - x1 * y0;
            }
            return area2 >= 0.0 ? 1 : -1;
        }

        /// <summary>
        /// The seaward cut line: where photoscenery stops, at the current margin.
        ///
        /// Built by offsetting each segment and rounding every convex corner into an arc, then
        /// dropping any point that came out closer to the coast than the margin. That last pass is
        /// what makes it agree with the distance rule rather than merely resemble it: where the
        /// coast bends tighter than the margin the raw offset crosses itself, and the points
        /// inside those loops are exactly the ones that are too close. Cull them and what is left
        /// is the true contour, with no polygon clipping anywhere.
        ///
        /// This is the coast's cut alone, in one piece. To draw the cut of a file that also holds
        /// islands, use CutLines.
        /// </summary>
        public List<GeoPoint> CutLine()
        {
            var pts = Points;
            if (pts.Count < 2 || MarginKm <= 0.0)
            {
                return pts;
            }

            var near = new NearIndex(Segments(), MarginKm * 0.999);
            var joined = new List<GeoPoint>();
            foreach (var piece in Contour(pts, false, SeaHand(pts), near, MarginKm))
            {
                joined.AddRange(piece);
            }
            return Smooth(joined, near, MarginKm);
        }

        /// <summary>
        /// Every piece of the cut: the coast's, then each island's. An island that no other line
        /// comes near gives one closed piece, whose last point is its first again.
        ///
        /// Each cut is culled against every line, not only its own. Where two islands, or an
        /// island and the coast, are nearer than twice the margin, their margins overlap, and the
        /// part of one cut that falls inside the other margin is not a boundary at all. So what is
        /// drawn is the edge of the whole covered area, which is what the field cuts at, and a cut
        /// that runs into another margin stops there and becomes an open piece. An island wholly
        /// inside another line's margin has no cut of its own.
        /// </summary>
        public List<List<GeoPoint>> CutLines()
        {
            var cuts = new List<List<GeoPoint>>();
            var rings = Islands;

            if (MarginKm <= 0.0)
            {
                if (HasCoast)
                {
                    cuts.Add(Points);
                }
                foreach (var ring in rings)
                {
                    var closed = new List<GeoPoint>(ring);
                    closed.Add(ring[0]);
                    cuts.Add(closed);
                }
                return cuts;
            }

            var near = new NearIndex(Segments(), MarginKm * 0.999);
            var pieces = new List<List<GeoPoint>>();
            if (HasCoast)
            {
                pieces.AddRange(Contour(Points, false, SeaHand(Points), near, MarginKm));
            }
            foreach (var ring in rings)
            {
                pieces.AddRange(Contour(ring, true, RingSeaHand(ring), near, MarginKm));
            }

            foreach (var piece in pieces)
            {
                if (piece.Count >= 2)
                {
                    cuts.Add(Smooth(piece, near, MarginKm));
                }
            }
            return cuts;
        }

        /// <summary>
        /// The contour at the margin on the sea side of one line, open or closed, before it is
        /// smoothed. See CutLine for how it is built. A closed line also gets the segment and the
        /// corner that close it.
        ///
        /// It comes back in pieces, because another line's margin can cover part of it. Where the
        /// contour goes into a margin and comes out again, the two crossings are joined only if
        /// the chord between them runs along the edge of the covered area, as at a fold of this
        /// line: there both crossings are at the same corner. Where the chord runs through the
        /// covered area, as across the margin of an islet, the contour is broken there instead.
        /// </summary>
        private static List<List<GeoPoint>> Contour(List<GeoPoint> pts, bool closed, int hand,
            NearIndex near, double m)
        {
            int n = pts.Count;
            int segCount = closed ? n : n - 1;
            var raw = new List<GeoPoint>();

            for (int i = 0; i < segCount; i++)
            {
                GeoPoint a = pts[i];
                GeoPoint b = pts[(i + 1) % n];

                double dx, dy;
                Delta(a, b, out dx, out dy);
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-9)
                {
                    continue;
                }

                double nx = hand * dy / len;
                double ny = -hand * dx / len;

                raw.Add(Offset(a, m * nx, m * ny));
                raw.Add(Offset(b, m * nx, m * ny));

                // Round the corner into the next segment. Only the seaward turn needs arc points;
                // the landward one is where the offset self-crosses and gets culled below anyway.
                if (closed || i + 2 < n)
                {
                    double ex, ey;
                    Delta(b, pts[(i + 2) % n], out ex, out ey);
                    double elen = Math.Sqrt(ex * ex + ey * ey);
                    if (elen < 1e-9)
                    {
                        continue;
                    }

                    double a0 = Math.Atan2(ny, nx);
                    double a1 = Math.Atan2(-hand * ex / elen, hand * ey / elen);
                    double sweep = NormaliseAngle(a1 - a0);

                    int steps = (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 12.0));
                    for (int k = 1; k < steps; k++)
                    {
                        double t = a0 + sweep * k / steps;
                        raw.Add(Offset(b, m * Math.Cos(t), m * Math.Sin(t)));
                    }
                }
            }

            double keep = m * 0.999;

            // A closed contour has no start. Start it at a point that is on the contour, and end
            // it at the same point, so the loop below sees every change of state once.
            if (closed)
            {
                int start = -1;
                for (int i = 0; i < raw.Count; i++)
                {
                    if (!near.AnyNearerThan(raw[i].Lat, raw[i].Lon, keep))
                    {
                        start = i;
                        break;
                    }
                }
                if (start < 0)
                {
                    return new List<List<GeoPoint>>();
                }

                var turned = new List<GeoPoint>(raw.Count + 1);
                for (int i = 0; i < raw.Count; i++)
                {
                    turned.Add(raw[(start + i) % raw.Count]);
                }
                turned.Add(turned[0]);
                raw = turned;
            }

            // Anything nearer the coast than the margin is inside a fold, not on the contour.
            // The slack keeps the offset's own points, which sit at exactly the margin.
            //
            // Dropping them is not enough on its own, and believing it was is what put the drawn
            // cut 2.25 km OUTSIDE a 1.852 km margin on the pilot's own line. The two points either
            // side of a dropped run are both at the margin, but the chord between them is not:
            // where the offset folds, the contour turns a corner that no vertex was ever generated
            // for, and the chord sails straight past it out to sea. A vertex test cannot see it -
            // every vertex is exactly where it should be - and on the map it reads as the cut
            // wandering off and losing whole stretches of coast.
            //
            // So each crossing gets a point of its own, found on the raw segment that straddles
            // it. That is the missing corner, and it costs one bisection per fold.
            var pieces = new List<List<GeoPoint>>();
            var cut = new List<GeoPoint>();
            bool wasOut = false;
            GeoPoint wentIn = new GeoPoint();

            for (int i = 0; i < raw.Count; i++)
            {
                bool isOut = !near.AnyNearerThan(raw[i].Lat, raw[i].Lon, keep);

                if (i > 0 && isOut != wasOut)
                {
                    if (!isOut)
                    {
                        wentIn = Crossing(near, raw[i - 1], raw[i], keep);
                        cut.Add(wentIn);
                    }
                    else
                    {
                        GeoPoint cameOut = Crossing(near, raw[i], raw[i - 1], keep);
                        var mid = new GeoPoint(0.5 * (wentIn.Lat + cameOut.Lat),
                                               0.5 * (wentIn.Lon + cameOut.Lon));
                        if (cut.Count > 0 && near.NearestKm(mid.Lat, mid.Lon, m) < 0.9 * m)
                        {
                            pieces.Add(cut);
                            cut = new List<GeoPoint>();
                        }
                        cut.Add(cameOut);
                    }
                }
                if (isOut)
                {
                    cut.Add(raw[i]);
                }
                wasOut = isOut;
            }
            if (cut.Count > 0)
            {
                pieces.Add(cut);
            }

            // A closed contour broken somewhere: its last piece runs on into its first.
            if (closed && pieces.Count > 1)
            {
                var last = pieces[pieces.Count - 1];
                last.AddRange(pieces[0].GetRange(1, pieces[0].Count - 1));
                pieces.RemoveAt(0);
            }
            return pieces;
        }

        /// <summary>
        /// Fills in the long steps the culling leaves behind, and pulls each new point onto the
        /// contour.
        ///
        /// The crossing points above stop the cut leaving the margin by kilometres, but they only
        /// bound the ERROR - the shape between them is still a straight chord where the contour
        /// curves, and around a headland with a bay on each side that reads as a rectangular notch
        /// in what should be an arc. Measured at 1.68 km outside a 5.556 km margin at Hualpen, on
        /// a 900 km line whose worst point anywhere else was under 500 m.
        ///
        /// Every step longer than a quarter of the margin is subdivided, and each new point is
        /// walked onto the contour along the gradient of the distance - which is just "straight
        /// away from the nearest coast", found by differences rather than by hunting for the
        /// nearest segment. A point already at the margin does not move, so the long straight
        /// stretches of a cut cost only the points.
        /// </summary>
        private static List<GeoPoint> Smooth(List<GeoPoint> cut, NearIndex near, double m)
        {
            double step = m * 0.25;
            var outPts = new List<GeoPoint>(cut.Count * 2);

            for (int i = 0; i < cut.Count; i++)
            {
                outPts.Add(cut[i]);
                if (i + 1 >= cut.Count)
                {
                    break;
                }

                double gap = PointDistanceKm(cut[i], cut[i + 1]);
                if (gap <= step)
                {
                    continue;
                }

                int pieces = (int)Math.Ceiling(gap / step);
                for (int k = 1; k < pieces; k++)
                {
                    double t = (double)k / pieces;
                    var mid = new GeoPoint(cut[i].Lat + (cut[i + 1].Lat - cut[i].Lat) * t,
                                           cut[i].Lon + (cut[i + 1].Lon - cut[i].Lon) * t);
                    outPts.Add(OntoContour(near, mid, m));
                }
            }
            return outPts;
        }

        /// <summary>Walks a point onto the margin contour, along the gradient of the distance.</summary>
        private static GeoPoint OntoContour(NearIndex near, GeoPoint p, double m)
        {
            double limit = 2.0 * m;
            const double Eps = 0.02;

            for (int i = 0; i < 4; i++)
            {
                double d = near.NearestKm(p.Lat, p.Lon, limit);
                if (Math.Abs(d - m) < 0.01)
                {
                    break;
                }

                double dLat = Eps / KmPerDegLat;
                double dLon = Eps / KmPerDegLon(p.Lat);
                double gy = (near.NearestKm(p.Lat + dLat, p.Lon, limit)
                           - near.NearestKm(p.Lat - dLat, p.Lon, limit)) / (2.0 * Eps);
                double gx = (near.NearestKm(p.Lat, p.Lon + dLon, limit)
                           - near.NearestKm(p.Lat, p.Lon - dLon, limit)) / (2.0 * Eps);

                double g = Math.Sqrt(gx * gx + gy * gy);
                if (g < 1e-6)
                {
                    break;
                }

                double move = m - d;
                p = Offset(p, move * gx / g, move * gy / g);
            }
            return p;
        }

        /// <summary>
        /// Where the straight line from a point at the margin to one inside it crosses the margin.
        ///
        /// Bisection rather than algebra, because the margin is a distance to a whole polyline and
        /// the crossing has no closed form. Twenty halvings take any raw segment to under a metre,
        /// which is four orders of magnitude below the thing being decided.
        /// </summary>
        private static GeoPoint Crossing(NearIndex near, GeoPoint outside, GeoPoint inside,
            double keep)
        {
            for (int i = 0; i < 20; i++)
            {
                var mid = new GeoPoint(0.5 * (outside.Lat + inside.Lat),
                                       0.5 * (outside.Lon + inside.Lon));
                if (near.AnyNearerThan(mid.Lat, mid.Lon, keep))
                {
                    inside = mid;
                }
                else
                {
                    outside = mid;
                }
            }
            return outside;
        }

        /// <summary>
        /// The segments bucketed by latitude, so asking "does the coast come nearer than the
        /// margin" walks a handful of them instead of all of them.
        ///
        /// CutLine asks that once per raw offset point plus twenty per fold, and answering it the
        /// plain way made a 2000-vertex line take nearly two seconds - on every release of the
        /// mouse button, which is what a coast worth drawing costs. That cost, not any argument
        /// about the cut, is what used to keep the thinning tolerance coarse.
        ///
        /// A segment further away in latitude than the limit is further away outright, so
        /// bucketing on latitude alone can drop a segment but never the nearest one. A coast
        /// running exactly east-west lands in a single bucket and gets no faster, which is exactly
        /// where it started.
        /// </summary>
        private sealed class NearIndex
        {
            private const int MaxBands = 4096;

            private readonly List<Segment> segs;
            private readonly List<int>[] bands;
            private readonly double south;
            private readonly double step;

            public NearIndex(List<Segment> segs, double limitKm)
            {
                this.segs = segs;

                double lo = Double.MaxValue, hi = Double.MinValue;
                for (int i = 0; i < segs.Count; i++)
                {
                    lo = Math.Min(lo, Math.Min(segs[i].A.Lat, segs[i].B.Lat));
                    hi = Math.Max(hi, Math.Max(segs[i].A.Lat, segs[i].B.Lat));
                }
                if (segs.Count == 0)
                {
                    lo = hi = 0.0;
                }

                south = lo;
                step = Math.Max(limitKm, 0.01) / KmPerDegLat;

                int count = (int)((hi - lo) / step) + 1;
                if (count > MaxBands)
                {
                    step = (hi - lo) / MaxBands + 1e-12;
                    count = MaxBands + 1;
                }

                bands = new List<int>[count];
                for (int i = 0; i < count; i++)
                {
                    bands[i] = new List<int>();
                }

                for (int i = 0; i < segs.Count; i++)
                {
                    int a = Band(Math.Min(segs[i].A.Lat, segs[i].B.Lat));
                    int b = Band(Math.Max(segs[i].A.Lat, segs[i].B.Lat));
                    for (int k = a; k <= b; k++)
                    {
                        bands[k].Add(i);
                    }
                }
            }

            private int Band(double lat)
            {
                int b = (int)((lat - south) / step);
                if (b < 0) return 0;
                if (b > bands.Length - 1) return bands.Length - 1;
                return b;
            }

            public bool AnyNearerThan(double lat, double lon, double limitKm)
            {
                return NearestKm(lat, lon, limitKm) < limitKm;
            }

            /// <summary>
            /// Distance to the nearest segment, or limitKm if nothing comes that close. The cap is
            /// what lets the bands do their work; past it the answer is only ever compared.
            /// </summary>
            public double NearestKm(double lat, double lon, double limitKm)
            {
                double slack = limitKm / KmPerDegLat;
                int first = Band(lat - slack);
                int last = Band(lat + slack);
                var p = new GeoPoint(lat, lon);
                double best = limitKm;

                for (int b = first; b <= last; b++)
                {
                    List<int> band = bands[b];
                    for (int j = 0; j < band.Count; j++)
                    {
                        int s = band[j];
                        double d = SegmentDistanceKm(p, segs[s].A, segs[s].B);
                        if (d < best)
                        {
                            best = d;
                        }
                    }
                }
                return best;
            }
        }

        private static double NormaliseAngle(double a)
        {
            while (a > Math.PI) a -= 2.0 * Math.PI;
            while (a < -Math.PI) a += 2.0 * Math.PI;
            return a;
        }

        private static GeoPoint Offset(GeoPoint p, double eastKm, double northKm)
        {
            return new GeoPoint(p.Lat + northKm / KmPerDegLat,
                                p.Lon + eastKm / KmPerDegLon(p.Lat));
        }

        /// <summary>Douglas-Peucker, iterative so a long drag cannot blow the stack.</summary>
        public static List<GeoPoint> Simplify(List<GeoPoint> pts, double toleranceKm)
        {
            if (pts.Count < 3)
            {
                return new List<GeoPoint>(pts);
            }

            var keep = new bool[pts.Count];
            keep[0] = true;
            keep[pts.Count - 1] = true;

            var work = new Stack<int[]>();
            work.Push(new int[] { 0, pts.Count - 1 });

            while (work.Count > 0)
            {
                int[] range = work.Pop();
                int first = range[0], last = range[1];

                double worst = -1.0;
                int worstAt = -1;
                for (int i = first + 1; i < last; i++)
                {
                    double d = SegmentDistanceKm(pts[i], pts[first], pts[last]);
                    if (d > worst)
                    {
                        worst = d;
                        worstAt = i;
                    }
                }

                if (worst > toleranceKm && worstAt > 0)
                {
                    keep[worstAt] = true;
                    work.Push(new int[] { first, worstAt });
                    work.Push(new int[] { worstAt, last });
                }
            }

            var outPts = new List<GeoPoint>();
            for (int i = 0; i < pts.Count; i++)
            {
                if (keep[i])
                {
                    outPts.Add(pts[i]);
                }
            }
            return outPts;
        }

        // -----------------------------------------------------------------------------------
        // storage - plain text, one point per line, so it can be read and hand-edited
        // -----------------------------------------------------------------------------------

        public void Save(string path)
        {
            using (var w = new StreamWriter(path, false))
            {
                if (IsLakes)
                {
                    // No land side and no margin: a lake has neither. The marker is "# lake N".
                    w.WriteLine("# aeroscenery lakes - lat lon, one point per line, blank line ends a stroke");
                }
                else
                {
                    w.WriteLine("# aeroscenery coastline - lat lon, one point per line, blank line ends a stroke");
                    w.WriteLine("# land {0}", Land);
                    w.WriteLine("# margin_km {0}", MarginKm.ToString("R", CultureInfo.InvariantCulture));
                }

                // The strokes stay in the order they were drawn, because undo takes the last one.
                // A marker goes before a stroke only where the line changes: "# island N" before
                // the strokes of island N, "# coast" before the strokes of the coast. A file
                // without markers is all coast, which is every file written before islands.
                string ringMarker = IsLakes ? "lake" : "island";
                int line = CoastStroke;
                foreach (var s in strokes)
                {
                    if (s.Island != line)
                    {
                        line = s.Island;
                        if (line == CoastStroke)
                        {
                            w.WriteLine("# coast");
                        }
                        else
                        {
                            w.WriteLine("# {0} {1}", ringMarker, line);
                        }
                    }

                    foreach (var p in s.Points)
                    {
                        w.WriteLine("{0} {1}",
                            p.Lat.ToString("R", CultureInfo.InvariantCulture),
                            p.Lon.ToString("R", CultureInfo.InvariantCulture));
                    }
                    w.WriteLine();
                }
            }
        }

        public static Coastline Load(string path)
        {
            return Load(path, false);
        }

        /// <summary>
        /// Loads a file of lakes. Its rings are marked "# lake N". The margin stays 0 whatever the
        /// file says, because the cut of a lake is the drawn line.
        /// </summary>
        public static Coastline LoadLakes(string path)
        {
            return Load(path, true);
        }

        private static Coastline Load(string path, bool lakes)
        {
            var c = lakes ? NewLakes() : new Coastline();
            if (!File.Exists(path))
            {
                return c;
            }

            string ringMarker = lakes ? "lake" : "island";
            Stroke stroke = null;
            int island = CoastStroke;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    stroke = null;
                    continue;
                }

                if (line[0] == '#')
                {
                    string[] meta = line.Substring(1).Trim().Split(' ');
                    int n;
                    if (meta.Length == 1 && meta[0] == "coast")
                    {
                        island = CoastStroke;
                        stroke = null;
                    }
                    else if (meta.Length == 2 && meta[0] == ringMarker
                             && Int32.TryParse(meta[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)
                             && n >= 0)
                    {
                        island = n;
                        stroke = null;
                    }
                    else if (meta.Length == 2 && meta[0] == "land")
                    {
                        try { c.Land = (LandSide)Enum.Parse(typeof(LandSide), meta[1], true); }
                        catch (ArgumentException) { }
                    }
                    else if (meta.Length == 2 && meta[0] == "margin_km" && !lakes)
                    {
                        double km;
                        if (Double.TryParse(meta[1], NumberStyles.Float,
                                            CultureInfo.InvariantCulture, out km))
                        {
                            c.MarginKm = km;
                        }
                    }
                    continue;
                }

                string[] parts = line.Split(' ');
                double lat, lon;
                if (parts.Length == 2
                    && Double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
                    && Double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lon))
                {
                    if (stroke == null)
                    {
                        stroke = new Stroke { Island = island };
                        c.strokes.Add(stroke);
                    }
                    stroke.Points.Add(new GeoPoint(lat, lon));
                }
            }

            c.strokes.RemoveAll(delegate (Stroke s) { return s.Points.Count < 2; });

            // An island left half drawn carries on where it stopped.
            c.DrawingIsland = c.OpenIsland();
            return c;
        }
    }
}
