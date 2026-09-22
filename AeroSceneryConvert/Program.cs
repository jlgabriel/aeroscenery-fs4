using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using AeroScenery.AFS2;

namespace AeroSceneryConvert
{
    /// <summary>
    /// Converts a .tmc and its stitched images into .ttc tiles from the command line, with no app
    /// and no window. It is for the jobs that are easier as a script: converting many squares,
    /// two or three at a time, or converting again with a different coastline.
    ///
    /// Everything else - where the images come from, where the tiles go - is inside the .tmc,
    /// which is the file the app's Generate AID / TMC Files action writes.
    ///
    /// The conversion itself is not implemented here. It is the same AeroScenery.AFS2 code the app
    /// runs in process, linked into this project rather than copied, so the two cannot drift.
    /// </summary>
    internal static class Program
    {
        private const int ExitOk = 0;
        private const int ExitError = 1;
        private const int ExitUsage = 2;

        /// <summary>
        /// Every field name this converter understands. Anything else in a .tmc is something
        /// GeoConvert may have acted on and this does not, so it gets said out loud rather than
        /// silently ignored - a quiet difference in output is the worst way to find that out.
        /// The empty name and the two structural ones are the container syntax, not settings.
        /// </summary>
        private static readonly HashSet<string> KnownFields = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "", "region_list", "element",
            "folder_source_files", "folder_destination_ttc", "folder_destination_raw",
            "write_ttc_files", "write_raw_files", "do_heightmaps", "always_overwrite",
            "write_images_with_mask",
            "level", "lonlat_min", "lonlat_max"
        };

        public static int Main(string[] args)
        {
            string tmcPath = null;
            string outDir = null;
            string coastPath = null;
            double marginNm = 0.0;
            int threads = TtcConverter.DefaultThreads();
            bool quiet = false;
            var positional = new List<string>();

            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    switch (a.ToLowerInvariant())
                    {
                        case "-h":
                        case "--help":
                        case "/?":
                            Usage(Console.Out);
                            return ExitOk;
                        case "--out":
                            outDir = Next(args, ref i, "--out");
                            break;
                        case "--threads":
                            threads = Int32.Parse(Next(args, ref i, "--threads"),
                                CultureInfo.InvariantCulture);
                            break;
                        case "--coast":
                            coastPath = Next(args, ref i, "--coast");
                            break;
                        case "--margin-nm":
                            marginNm = Double.Parse(Next(args, ref i, "--margin-nm"),
                                CultureInfo.InvariantCulture);
                            break;
                        case "--quiet":
                            quiet = true;
                            break;
                        default:
                            positional.Add(a);
                            break;
                    }
                }

                tmcPath = ResolveTmcArgument(positional);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                return ExitUsage;
            }

            if (tmcPath == null)
            {
                Usage(Console.Error);
                return ExitUsage;
            }

            try
            {
                return Run(tmcPath, outDir, coastPath, marginNm, threads, quiet);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("error: " + ex.Message);
                if (Environment.GetEnvironmentVariable("AEROSCENERYCONVERT_TRACE") == "1")
                {
                    Console.Error.WriteLine(ex.ToString());
                }
                return ExitError;
            }
        }

        /// <summary>
        /// Works out which of the loose arguments is the .tmc.
        ///
        /// AeroScenery passes the path <em>unquoted</em> - `processArguments = tmcFilename` - so a
        /// working directory with a space in it arrives here split across several arguments. That
        /// is not a hypothetical: the default SDK folder is "Aerofly FS 2 SDK". Rejoining when the
        /// pieces name a real file costs nothing and turns a baffling failure into a non-event.
        /// </summary>
        private static string ResolveTmcArgument(List<string> positional)
        {
            if (positional.Count == 0)
            {
                return null;
            }

            if (positional.Count == 1)
            {
                return positional[0];
            }

            string joined = String.Join(" ", positional.ToArray());
            if (File.Exists(joined))
            {
                return joined;
            }

            // Not a split path - the caller really did pass several things.
            throw new ArgumentException("expected one .tmc file, got " + positional.Count
                + " arguments and \"" + joined + "\" is not a file");
        }

        private static string Next(string[] args, ref int i, string option)
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException(option + " needs a value");
            }
            return args[++i];
        }

        private static int Run(string tmcPath, string outDir, string coastPath, double marginNm,
            int threads, bool quiet)
        {
            if (!File.Exists(tmcPath))
            {
                Console.Error.WriteLine("error: tmc not found: " + tmcPath);
                return ExitError;
            }

            tmcPath = Path.GetFullPath(tmcPath);
            TmcDocument doc = TmcReader.Parse(tmcPath);

            if (!CheckSupported(tmcPath, doc))
            {
                return ExitError;
            }

            if (String.IsNullOrEmpty(outDir))
            {
                outDir = doc.FolderDestinationTtc;
            }
            if (String.IsNullOrEmpty(outDir))
            {
                Console.Error.WriteLine(
                    "error: the tmc has no folder_destination_ttc, and no --out was given");
                return ExitError;
            }

            string sourceFolder = doc.FolderSourceFiles;
            if (String.IsNullOrEmpty(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                // Same fallback the converter itself applies: a .tmc that was moved still works if
                // its images travelled with it.
                sourceFolder = Path.GetDirectoryName(tmcPath);
            }

            Coastline coast = null;
            if (!String.IsNullOrEmpty(coastPath))
            {
                if (!File.Exists(coastPath))
                {
                    Console.Error.WriteLine("error: coastline not found: " + coastPath);
                    return ExitError;
                }

                coast = Coastline.Load(coastPath);
                if (coast.IsEmpty)
                {
                    // Said out loud rather than ignored. A coastline that failed to parse looks
                    // exactly like one that was never asked for, and the difference is a square
                    // with its sea cut off and one without.
                    Console.Error.WriteLine("error: " + coastPath + " has no line in it");
                    return ExitError;
                }
                if (marginNm > 0.0)
                {
                    coast.MarginKm = marginNm * 1.852;
                }
            }
            else if (marginNm > 0.0)
            {
                Console.Error.WriteLine("note: --margin-nm does nothing without --coast.");
            }

            if (!quiet)
            {
                Console.WriteLine("AeroSceneryConvert {0}", Version());
                Console.WriteLine();
                Console.WriteLine("  tmc         {0}", tmcPath);
                Console.WriteLine("  source      {0}", sourceFolder);
                Console.WriteLine("  output      {0}", outDir);
                Console.WriteLine("  levels      {0}", DescribeLevels(doc));
                Console.WriteLine("  threads     {0}{1}", threads,
                    threads <= 0 ? " (all " + Environment.ProcessorCount + ")" : "");
                if (coast != null)
                {
                    Console.WriteLine("  coast       {0} points, land to the {1}, cut at {2:0.##} NM",
                        coast.Points.Count, coast.Land.ToString().ToLowerInvariant(),
                        coast.MarginKm / 1.852);
                }
                Console.WriteLine();
            }

            var converter = new TtcConverter { MaxThreads = threads, Coast = coast };
            var reporter = quiet ? null : new ConsoleProgress();

            TtcConversionResult result = converter.Convert(tmcPath, outDir, reporter);

            if (reporter != null)
            {
                reporter.Finish();
            }

            if (!quiet)
            {
                Console.WriteLine();
                Console.WriteLine("  files       {0:n0}", result.FilesWritten.Count);
                Console.WriteLine("  bytes       {0:n1} MB", result.BytesWritten / (1024.0 * 1024.0));
                Console.WriteLine("  elapsed     {0:hh\\:mm\\:ss}", result.Elapsed);
            }

            if (result.FilesWritten.Count == 0)
            {
                // With a coastline this is an answer, not a failure: a square that lies wholly
                // past the cut has no photoscenery in it, and the right output is nothing at all.
                // Measured on map_09_4c80_6700, which is 3.56 GB of installed black 20 km
                // offshore - the converter reached that verdict in 30 s without opening a source.
                if (coast != null)
                {
                    Console.WriteLine();
                    Console.WriteLine("  The whole square lies past the cut. There is no photoscenery");
                    Console.WriteLine("  here, so nothing was written - delete the installed square.");
                    return ExitOk;
                }

                Console.Error.WriteLine("error: no tiles were written");
                return ExitError;
            }

            return ExitOk;
        }

        /// <summary>
        /// Says what this converter will and will not honour in a .tmc, before spending an hour on
        /// it. Heightmaps and raw output are GeoConvert features that were never reimplemented;
        /// the difference between the two answers is whether the caller still gets what it asked
        /// for. Older versions of the app asked for raw files, so that one is a note, not a stop.
        /// </summary>
        private static bool CheckSupported(string tmcPath, TmcDocument doc)
        {
            bool ok = true;

            foreach (var f in TmFields.Parse(File.ReadAllText(tmcPath, System.Text.Encoding.UTF8)))
            {
                if (String.Equals(f.Name, "do_heightmaps", StringComparison.OrdinalIgnoreCase)
                    && TmFields.Bool(f.Value))
                {
                    Console.Error.WriteLine(
                        "error: do_heightmaps is true. This converter only makes colour tiles; "
                        + "use IPACS' geoconvert for elevation.");
                    ok = false;
                }
                else if (String.Equals(f.Name, "always_overwrite", StringComparison.OrdinalIgnoreCase)
                    && !TmFields.Bool(f.Value))
                {
                    Console.Error.WriteLine(
                        "note: always_overwrite is false, but this converter always overwrites.");
                }
                else if (!KnownFields.Contains(f.Name))
                {
                    Console.Error.WriteLine(
                        "note: unknown tmc field \"{0}\" - ignored.", f.Name);
                }
            }

            if (!doc.WriteTtcFiles)
            {
                Console.Error.WriteLine(
                    "error: write_ttc_files is false, and .ttc tiles are all this converter makes.");
                ok = false;
            }

            if (doc.WriteRawFiles)
            {
                Console.Error.WriteLine(
                    "note: write_raw_files is true; raw output is not produced. AeroScenery does "
                    + "not read it.");
            }

            return ok;
        }

        private static string DescribeLevels(TmcDocument doc)
        {
            int min = Int32.MaxValue, max = Int32.MinValue;
            foreach (var r in doc.Regions)
            {
                if (r.Level < min) min = r.Level;
                if (r.Level > max) max = r.Level;
            }
            return min == max
                ? min.ToString(CultureInfo.InvariantCulture)
                : min.ToString(CultureInfo.InvariantCulture) + " - "
                    + max.ToString(CultureInfo.InvariantCulture);
        }

        private static string Version()
        {
            return typeof(Program).Assembly.GetName().Version.ToString(3);
        }

        private static void Usage(TextWriter w)
        {
            w.WriteLine("usage: AeroSceneryConvert <file.tmc> [--out <dir>] [--threads <n>]");
            w.WriteLine("                          [--coast <file>] [--margin-nm <n>] [--quiet]");
            w.WriteLine();
            w.WriteLine("  Converts a .tmc and its stitched images into Aerofly .ttc tiles.");
            w.WriteLine("  With no options it writes to the folder_destination_ttc named in the .tmc,");
            w.WriteLine("  which is where the app looks for them.");
            w.WriteLine();
            w.WriteLine("  --out <dir>      write tiles here instead");
            w.WriteLine("  --threads <n>    BC1 encoder threads; 0 means all cores. Default is half.");
            w.WriteLine("  --coast <file>   stop the photoscenery at this hand-drawn coastline.");
            w.WriteLine("                   The file is the one the Map tab's Draw Coast button saves.");
            w.WriteLine("                   Without it, everything a source reaches is kept.");
            w.WriteLine("  --margin-nm <n>  how far out to sea to cut, overriding the file. Default 3.");
            w.WriteLine("  --quiet          no progress, only errors");
        }

        /// <summary>
        /// Progress on one line, redrawn in place.
        ///
        /// Only the deepest level reports - the shallower ones are halved out of it as it goes -
        /// so its tile count is the honest measure of how far along the whole job is. Throttled,
        /// because at a few hundred tiles a second the writing costs more than the converting, and
        /// falls back to plain lines when output is a file rather than a console.
        /// </summary>
        private sealed class ConsoleProgress : IProgress<TtcConversionProgress>
        {
            private readonly Stopwatch sinceLastDraw = Stopwatch.StartNew();
            private readonly bool interactive = !Console.IsOutputRedirected;
            private readonly object gate = new object();
            private int lastPercent = -1;
            private bool drewSomething;

            public void Report(TtcConversionProgress p)
            {
                lock (gate)
                {
                    if (p.TilesTotal <= 0)
                    {
                        return;
                    }

                    int percent = (int)(100L * p.TilesDone / p.TilesTotal);

                    if (interactive)
                    {
                        if (sinceLastDraw.ElapsedMilliseconds < 200 && p.TilesDone < p.TilesTotal)
                        {
                            return;
                        }
                        sinceLastDraw.Restart();
                        Console.Write("\r  level {0}   {1:n0} / {2:n0} tiles   {3}%   ",
                            p.Level, p.TilesDone, p.TilesTotal, percent);
                    }
                    else
                    {
                        if (percent == lastPercent)
                        {
                            return;
                        }
                        Console.WriteLine("  level {0}   {1:n0} / {2:n0} tiles   {3}%",
                            p.Level, p.TilesDone, p.TilesTotal, percent);
                    }

                    lastPercent = percent;
                    drewSomething = true;
                }
            }

            public void Finish()
            {
                lock (gate)
                {
                    if (drewSomething && interactive)
                    {
                        Console.WriteLine();
                    }
                }
            }
        }
    }
}
