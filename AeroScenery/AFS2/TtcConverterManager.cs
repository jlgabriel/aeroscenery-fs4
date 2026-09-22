using AeroScenery.Controls;
using AeroScenery.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AeroScenery.AFS2
{
    /// <summary>
    /// Runs the built-in converter over every .tmc in a stitched tiles directory.
    ///
    /// The conversion runs in this process. There is no external tool to launch, no GPU and no
    /// desktop session: it reports its own progress and either returns or throws.
    /// </summary>
    public class TtcConverterManager
    {
        private readonly ILog log = LogManager.GetLogger("AeroScenery");

        /// <param name="coast">Where to stop the photoscenery at the sea, or null for no cut.</param>
        public async Task<TtcConversionResult> ConvertAllAsync(string stitchedTilesDirectory,
            string ttcDirectory, MainForm mainForm, Coastline coast = null)
        {
            var total = new TtcConversionResult();

            if (!Directory.Exists(stitchedTilesDirectory))
            {
                var messageBox = new CustomMessageBox("No stiched images found for this grid square and this image detail (zoom) level.\nRun the 'Download Image Tiles' and 'Stitch Image Tiles' actions first.",
                    "AeroScenery",
                    MessageBoxIcon.Error);

                messageBox.ShowDialog();
                return total;
            }

            var tmcFiles = Directory.EnumerateFiles(stitchedTilesDirectory, "*.tmc").ToList();

            if (tmcFiles.Count == 0)
            {
                var messageBox = new CustomMessageBox("No TCM file was found for this grid square and this image detail (zoom) level.\nRun the 'Download Image Tiles', 'Stitch Image Tiles' and 'Generate AID / TMC Files' actions first.",
                    "AeroScenery",
                    MessageBoxIcon.Error);

                messageBox.ShowDialog();
                return total;
            }

            // Start from an empty folder. A conversion cut at the coastline writes fewer tiles than
            // one that is not, and a tile left over from an earlier run would be installed as sea.
            var stale = Directory.Exists(ttcDirectory)
                ? Directory.EnumerateFiles(ttcDirectory, "*.ttc").ToList()
                : new List<string>();

            foreach (var file in stale)
            {
                File.Delete(file);
            }

            if (stale.Count > 0)
            {
                log.InfoFormat("Deleted {0} ttc files from an earlier conversion in {1}", stale.Count, ttcDirectory);
            }

            foreach (string tmcFilename in tmcFiles)
            {
                log.Info(String.Format("Converting {0}", tmcFilename));
                mainForm.UpdateChildTaskLabel("Converting");

                var stopwatch = Stopwatch.StartNew();
                TtcConversionResult result;

                try
                {
                    // One conversion stays on one thread for its whole life. The PNG decoder
                    // underneath is a WPF BitmapDecoder, which is bound to the thread that created
                    // it, so this must not be split across awaits.
                    int configured = AeroSceneryManager.Instance.Settings.ConverterThreads ?? 0;
                    int threads = configured > 0 ? configured : TtcConverter.DefaultThreads();

                    result = await Task.Run(() =>
                    {
                        var converter = new TtcConverter { MaxThreads = threads, Coast = coast };
                        var progress = new Progress<TtcConversionProgress>(p =>
                            mainForm.UpdateChildTaskProgress(String.Format(
                                "Converting level {0} - {1} of {2} tiles",
                                p.Level, p.TilesDone, p.TilesTotal)));

                        return converter.Convert(tmcFilename, ttcDirectory, progress);
                    });
                }
                catch (Exception ex)
                {
                    log.Error(String.Format("Could not convert {0}", tmcFilename), ex);

                    var messageBox = new CustomMessageBox(String.Format("{0} could not be converted.\n\n{1}",
                            Path.GetFileName(tmcFilename), ex.Message),
                        "AeroScenery",
                        MessageBoxIcon.Error);

                    messageBox.ShowDialog();
                    continue;
                }

                stopwatch.Stop();

                total.FilesWritten.AddRange(result.FilesWritten);
                total.BytesWritten += result.BytesWritten;
                total.SourceRestarts += result.SourceRestarts;
                total.Elapsed += result.Elapsed;

                log.Info(String.Format("Converted {0} in {1:hh\\:mm\\:ss}, {2} files, {3:n1} MB in {4}",
                    Path.GetFileName(tmcFilename),
                    stopwatch.Elapsed,
                    result.FilesWritten.Count,
                    result.BytesWritten / (1024.0 * 1024.0),
                    ttcDirectory));

                if (result.SourceRestarts > 0)
                {
                    // Not a failure, but it means the source was re-decoded from the top one or
                    // more times, which is the difference between one pass and several.
                    log.WarnFormat("{0} source decoder restarts while converting {1}",
                        result.SourceRestarts, Path.GetFileName(tmcFilename));
                }

                if (result.FilesWritten.Count == 0 && coast != null)
                {
                    // An answer, not a failure: the whole square lies past the cut, so there is
                    // no photoscenery in it and the right output is nothing at all.
                    log.WarnFormat("{0} produced no tiles - the whole grid square lies out at sea, past the coastline cut",
                        Path.GetFileName(tmcFilename));
                }
                else if (result.FilesWritten.Count == 0)
                {
                    log.WarnFormat("{0} produced no tiles - no source image covers its regions",
                        Path.GetFileName(tmcFilename));
                }
            }

            return total;
        }
    }
}
