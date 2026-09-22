using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using AeroScenery.AFS2;

/// <summary>
/// Samples one tile out of a real .tmc and prints hashes of the result, to be compared against
/// tools/ttc/cross_sample.py running the Python reference over the same inputs.
///
///     sample.ps1 "path\to\g_15_stitch.tmc" 12
///     sample.ps1 "path\to\g_15_stitch.tmc" 12 1240 1379
///
/// Resampling is where a port goes quietly wrong. Half a pixel of offset, a floor that should
/// have been a round, latitude read as linear - none of that looks like a bug in the output, it
/// just puts the imagery slightly in the wrong place, which is exactly the class of mistake that
/// already cost this project a session (see the row-order correction in the handoff). Comparing
/// the two implementations pixel for pixel is the only check that catches it.
/// </summary>
internal static class ProbeSampler
{
    private const int TileSize = 2048;

    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: ProbeSampler <tmc> <level> [tx ty]");
            return 2;
        }

        string tmcPath = args[0];
        int level = Int32.Parse(args[1]);

        TmcDocument doc = TmcReader.Parse(tmcPath);
        string folder = doc.FolderSourceFiles;
        if (String.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            folder = Path.GetDirectoryName(Path.GetFullPath(tmcPath));
        }

        TmcRegion region = null;
        foreach (var r in doc.Regions)
        {
            if (r.Level == level) { region = r; break; }
        }
        if (region == null)
        {
            Console.Error.WriteLine("no region at level " + level);
            return 2;
        }

        int x0, x1, y0, y1;
        AFS2World.TileRange(region.West, region.East, region.South, region.North, level,
            out x0, out x1, out y0, out y1);
        Console.WriteLine("  region      W {0} E {1} S {2} N {3}",
            region.West, region.East, region.South, region.North);
        Console.WriteLine("  tiles       x {0}..{1}  y {2}..{3}   ({4} x {5})",
            x0, x1 - 1, y0, y1 - 1, x1 - x0, y1 - y0);

        int tx = args.Length >= 4 ? Int32.Parse(args[2]) : x0;
        int ty = args.Length >= 4 ? Int32.Parse(args[3]) : y1 - 1;   // northernmost row

        var sources = SourceImage.OpenFolder(folder);
        try
        {
            Console.WriteLine("  sources     {0}", sources.Count);
            foreach (var s in sources)
            {
                Console.WriteLine("     {0} x {1}   W {2:f6} E {3:f6} S {4:f6} N {5:f6}",
                    s.Width, s.Height, s.LonWest, s.LonEast, s.LatSouth, s.LatNorth);
            }

            var rgb = new byte[TileSize * TileSize * 3];
            var covered = new bool[TileSize * TileSize];

            var sw = Stopwatch.StartNew();
            bool any = TileSampler.Sample(sources, level, tx, ty, rgb, covered, TileSize);
            sw.Stop();

            int n = 0;
            var coveredBytes = new byte[covered.Length];
            for (int i = 0; i < covered.Length; i++)
            {
                coveredBytes[i] = covered[i] ? (byte)1 : (byte)0;
                if (covered[i]) n++;
            }

            Console.WriteLine();
            Console.WriteLine("  tile        level {0}  ({1}, {2})  -> {3}",
                level, tx, ty, TtcTileName.ForTile(level, tx, ty));
            Console.WriteLine("  any         {0}", any);
            Console.WriteLine("  covered     {0:n0} of {1:n0} px  ({2:f1}%)",
                n, covered.Length, 100.0 * n / covered.Length);
            Console.WriteLine("  elapsed     {0:f2} s", sw.Elapsed.TotalSeconds);
            Console.WriteLine();
            Console.WriteLine("  rgb         {0}", Sha(rgb));
            Console.WriteLine("  covered     {0}", Sha(coveredBytes));

            int restarts = 0;
            foreach (var s in sources) restarts += s.Restarts;
            Console.WriteLine();
            Console.WriteLine("  restarts    {0}   (should be 0 for a north-to-south pass)", restarts);

            var p = Process.GetCurrentProcess();
            Console.WriteLine("  peak wset   {0:n0} MB", p.PeakWorkingSet64 / (1024 * 1024));
        }
        finally
        {
            foreach (var s in sources) s.Dispose();
        }
        return 0;
    }

    private static string Sha(byte[] data)
    {
        using (var sha = SHA256.Create())
        {
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}
