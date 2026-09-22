using System;
using System.Diagnostics;
using System.IO;
using AeroScenery.AFS2;

/// <summary>
/// Runs the in-app converter over a real .tmc and reports what it produced.
///
///     convert.ps1 "path\to\g_15_stitch.tmc" "out\dir"
///
/// Then compare against the reference over the same input:
///
///     python tools/ttc/convert_tmc.py --tmc "...\g_15_stitch.tmc" --out out\python
///     python tools/ttc/hash_dir.py out\dir
///     python tools/ttc/hash_dir.py out\python
///
/// The two manifests must match. Peak memory is the other half of the point: the reference keeps
/// a whole level in RAM, which is exactly what this cannot do.
/// </summary>
internal static class ProbeConvert
{
    public static int Main(string[] rawArgs)
    {
        // The named options are pulled out first, so they can be given without the optional
        // positional ones in front of them. The positional four came first and are left alone.
        string waterFix = null;
        var list = new System.Collections.Generic.List<string>();
        for (int i = 0; i < rawArgs.Length; i++)
        {
            if (rawArgs[i] == "--water" && i + 1 < rawArgs.Length)
            {
                waterFix = rawArgs[++i];
            }
            else
            {
                list.Add(rawArgs[i]);
            }
        }
        string[] args = list.ToArray();

        if (args.Length < 2)
        {
            Console.Error.WriteLine(
                "usage: ProbeConvert <tmc> <outdir> [threads] [blackIsMissing] [coastline] [marginNm] [--water <awfx>]");
            return 2;
        }

        string tmc = args[0];
        string outDir = args[1];

        var converter = new TtcConverter();
        if (args.Length >= 3)
        {
            converter.MaxThreads = Int32.Parse(args[2]);
        }
        if (args.Length >= 4 && args[3] == "1")
        {
            converter.BlackIsMissing = true;
            Console.WriteLine("  black       treated as missing data");
        }
        if (args.Length >= 5 && args[4].Length > 0)
        {
            var coast = Coastline.Load(args[4]);
            if (coast.IsEmpty)
            {
                Console.Error.WriteLine("error: " + args[4] + " has no line in it");
                return 1;
            }
            if (args.Length >= 6)
            {
                double nm;
                if (Double.TryParse(args[5], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out nm) && nm > 0.0)
                {
                    coast.MarginKm = nm * 1.852;
                }
            }
            converter.Coast = coast;
            Console.WriteLine("  coast       {0} points, land to the {1}, cut at {2:0.##} NM",
                coast.Points.Count, coast.Land.ToString().ToLowerInvariant(), coast.MarginKm / 1.852);
        }
        if (waterFix != null)
        {
            var field = WaterFixField.Load(waterFix);
            converter.Water = field;
            Console.WriteLine("  water fix   {0} texels, lon {1:0.####}..{2:0.####}, lat {3:0.####}..{4:0.####}",
                field.Size, field.West, field.East, field.South, field.North);
        }
        Console.WriteLine("  threads     {0}{1}", converter.MaxThreads,
            converter.MaxThreads <= 0 ? " (all " + Environment.ProcessorCount + ")" : "");
        int lastLevel = -1;

        var progress = new Progress<TtcConversionProgress>(p =>
        {
            if (p.Level != lastLevel)
            {
                lastLevel = p.Level;
                Console.WriteLine("  level {0}  {1} tiles", p.Level, p.TilesTotal);
            }
        });

        TtcConversionResult result = converter.Convert(tmc, outDir, progress);

        Console.WriteLine();
        Console.WriteLine("  files       {0:n0}", result.FilesWritten.Count);
        Console.WriteLine("  bytes       {0:n1} MB", result.BytesWritten / (1024.0 * 1024.0));
        Console.WriteLine("  elapsed     {0:f1} s", result.Elapsed.TotalSeconds);
        Console.WriteLine("  restarts    {0}   (should be 0)", result.SourceRestarts);

        var proc = Process.GetCurrentProcess();
        Console.WriteLine("  peak wset   {0:n0} MB", proc.PeakWorkingSet64 / (1024 * 1024));
        return 0;
    }
}
