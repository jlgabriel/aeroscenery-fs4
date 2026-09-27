// Says which squares the drawn coastline actually touches, without converting any of them.
//
// See classify.ps1 for what it is for and how to run it.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AeroScenery.AFS2;

internal static class Classify
{
    /// <summary>
    /// How deep to look for a .tmc under a square. A working square keeps it at
    /// <c>&lt;square&gt;\b\17-stitched\b_17_stitch.tmc</c>, so three is enough - and a limit is the
    /// point, not a detail. The tile folders beside it hold tens of thousands of files, and an
    /// unbounded search spends minutes walking them to find a file it already knows the shape of.
    /// </summary>
    private const int MaxDepth = 3;

    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine(
                "usage: Classify <coastline file> <working root or .tmc> [margin NM]");
            return 2;
        }

        Coastline line;
        try
        {
            line = Coastline.Load(args[0]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: cannot read " + args[0] + ": " + ex.Message);
            return 1;
        }

        if (line.IsEmpty)
        {
            Console.Error.WriteLine("error: " + args[0] + " has no line in it");
            return 1;
        }

        if (args.Length > 2)
        {
            line.MarginKm = Double.Parse(args[2], CultureInfo.InvariantCulture) * 1.852;
        }

        List<string> tmcs = Collect(args[1]);
        if (tmcs.Count == 0)
        {
            Console.Error.WriteLine("error: no .tmc found under " + args[1]);
            return 1;
        }

        Console.WriteLine("coast    {0:n0} points, {1} strokes, land to the {2}, {3} island(s), cut at {4:0.##} NM",
            line.Points.Count, line.StrokeCount, line.Land.ToString().ToLowerInvariant(),
            line.Islands.Count, line.MarginKm / 1.852);
        Console.WriteLine();
        Console.WriteLine(
            "square                west      east     south     north    min km   max km  verdict");

        // Which part of the file may cut a square is Coastline.ForSquare, the rule the app uses:
        // the coast only where the square is wholly inside the stretch it covers, and an island
        // only where it comes near. water_convert.ps1 applies the same stretch rule.
        bool alongLat;
        double from, to;
        line.CoastStretch(out alongLat, out from, out to);

        int cut = 0, land = 0, sea = 0, outside = 0, far = 0, failed = 0;

        foreach (string tmc in tmcs)
        {
            string name = SquareName(tmc);

            double west, east, south, north;
            try
            {
                TtcConverter.CoastFieldBox(tmc, out west, out east, out south, out north);
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0,-18}  cannot be read: {1}", name, ex.Message);
                failed++;
                continue;
            }

            // The square's own edges when its name gives them, as in the app; the sampled box when not.
            double sw = west, se = east, ss = south, sn = north;
            SquareEdges(name, ref sw, ref se, ref ss, ref sn);

            string why;
            Coastline part = line.ForSquare(sw, se, ss, sn, out why);

            double min = Double.NaN, max = Double.NaN;
            if (part != null)
            {
                CoastlineField field = CoastlineField.Build(part, west, east, south, north);
                field.RangeOver(west, east, south, north, out min, out max);
            }

            string verdict;
            if (part == null && line.HasCoast) { verdict = "OUTSIDE"; outside++; }
            else if (part == null) { verdict = "FAR"; far++; }
            else if (max <= line.MarginKm) { verdict = "LAND"; land++; }
            else if (min > line.MarginKm) { verdict = "SEA"; sea++; }
            else { verdict = "CUT"; cut++; }

            Console.WriteLine(
                "{0,-18}  {1,8:0.000}  {2,8:0.000}  {3,8:0.000}  {4,8:0.000}  {5,7:0.0}  {6,7:0.0}  {7}",
                name, west, east, south, north, min, max, verdict);
        }

        Console.WriteLine();
        Console.WriteLine("  CUT      {0,3}  the cut crosses them - rebuild with --coast", cut);
        Console.WriteLine("  LAND     {0,3}  wholly landward - rebuilding gives back the same bytes", land);
        Console.WriteLine("  SEA      {0,3}  wholly past the cut - no photoscenery there, delete", sea);
        if (line.HasCoast)
        {
            Console.WriteLine("  OUTSIDE  {0,3}  not wholly inside the stretch the coast covers ({1} {2:0.00} to {3:0.00}) -",
                outside, alongLat ? "lat" : "lon", from, to);
            Console.WriteLine("                never give them --coast; draw the line past them first");
        }
        else
        {
            Console.WriteLine("  FAR      {0,3}  no island comes near - the app converts them without a cut", far);
        }
        if (failed > 0)
        {
            Console.WriteLine("  ?     {0,3}  could not be read", failed);
        }

        return failed > 0 ? 1 : 0;
    }

    /// <summary>
    /// The .tmc files to answer for: either the one that was named, or one per square under a
    /// working root.
    /// </summary>
    private static List<string> Collect(string path)
    {
        var found = new List<string>();

        if (File.Exists(path))
        {
            found.Add(Path.GetFullPath(path));
            return found;
        }

        if (!Directory.Exists(path))
        {
            return found;
        }

        var squares = new List<string>(Directory.GetDirectories(path, "map_*"));
        if (squares.Count == 0)
        {
            // A single square was named rather than the root above it.
            squares.Add(path);
        }
        squares.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (string square in squares)
        {
            string tmc = FindTmc(square, MaxDepth);
            if (tmc != null)
            {
                found.Add(tmc);
            }
        }

        return found;
    }

    private static string FindTmc(string dir, int depth)
    {
        string[] here = Directory.GetFiles(dir, "*.tmc");
        if (here.Length > 0)
        {
            Array.Sort(here, StringComparer.OrdinalIgnoreCase);
            return here[0];
        }

        if (depth <= 0)
        {
            return null;
        }

        foreach (string child in Directory.GetDirectories(dir))
        {
            string tmc = FindTmc(child, depth - 1);
            if (tmc != null)
            {
                return tmc;
            }
        }

        return null;
    }

    /// <summary>
    /// The square a .tmc belongs to, which is the folder the search started from rather than the
    /// stitched folder the file sits in.
    /// </summary>
    /// <summary>
    /// The edges of a level 9 square, from its name, as in the app. A name that is not a level 9
    /// square leaves the edges as they were given.
    /// </summary>
    private static void SquareEdges(string name, ref double west, ref double east,
        ref double south, ref double north)
    {
        Match m = Regex.Match(name, "^map_09_([0-9a-f]{4})_([0-9a-f]{4})$", RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            return;
        }

        int gx = Convert.ToInt32(m.Groups[1].Value, 16) / 128;
        int gy = Convert.ToInt32(m.Groups[2].Value, 16) / 128;
        west = AFS2World.LonOfGridX(gx, 9);
        east = AFS2World.LonOfGridX(gx + 1, 9);
        south = AFS2World.LatOfGridY(gy, 9);
        north = AFS2World.LatOfGridY(gy + 1, 9);
    }

    private static string SquareName(string tmc)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(tmc)));
        for (int i = 0; i < MaxDepth && dir != null; i++)
        {
            if (dir.Name.StartsWith("map_", StringComparison.OrdinalIgnoreCase))
            {
                return dir.Name;
            }
            dir = dir.Parent;
        }
        return Path.GetFileNameWithoutExtension(tmc);
    }
}
