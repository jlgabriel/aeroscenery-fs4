using AeroScenery.AFS2;
using AeroScenery.Common;
using AeroScenery.Controls;
using AeroScenery.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AeroScenery.FileManagement
{
    public class SceneryInstaller
    {
        private AFS2Grid afsGrid;

        private readonly ILog log = LogManager.GetLogger("AeroScenery");


        public SceneryInstaller()
        {
            this.afsGrid = new AFS2Grid();
        }

        /// <summary>
        /// Checks that there is somewhere to install to, and asks the user to confirm unless the
        /// caller has already taken that as read.
        /// </summary>
        public DialogResult ConfirmSceneryInstallation(AFS2GridSquare afs2GridSquare, bool promptUser)
        {
            var gridSquareDirectory = AeroSceneryManager.Instance.Settings.WorkingDirectory + afs2GridSquare.Name;

            DialogResult result = DialogResult.No;

            // Does this grid square exist
            if (Directory.Exists(gridSquareDirectory))
            {
                // Do we have an Aerofly folder to install into?
                string afsSceneryInstallDirectory = DirectoryHelper.FindAFSSceneryInstallDirectory(AeroSceneryManager.Instance.Settings);

                if (afsSceneryInstallDirectory != null)
                {
                    if (!promptUser)
                    {
                        return DialogResult.Yes;
                    }

                    // Confirm that the user does want to install scenery
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("Are you sure you want to install all scenery for this grid square?");
                    sb.AppendLine("Any existing files in the same destination folder will be overwritten.");
                    sb.AppendLine("");
                    sb.AppendLine(String.Format("Destination: {0}", afsSceneryInstallDirectory));

                    var messageBox = new CustomMessageBox(sb.ToString(),
                        "AeroScenery",
                        MessageBoxIcon.Question);

                    messageBox.SetButtons(
                        new string[] { "Yes", "No" },
                        new DialogResult[] { DialogResult.Yes, DialogResult.No });

                    result = messageBox.ShowDialog();
                }
                else
                {
                    // Can't find anywhere to install
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("Could not find a location to install to.");
                    sb.AppendLine("");
                    sb.AppendLine("AeroScenery looks for 'Aerofly FS 4' and then 'Aerofly FS 2' in your Documents folder.");
                    sb.AppendLine("If your Aerofly user folder is somewhere else, set it as the AFS User Folder in Settings.");

                    var messageBox = new CustomMessageBox(sb.ToString(),
                        "AeroScenery",
                        MessageBoxIcon.Error);

                    result = messageBox.ShowDialog();
                }
            }
            else
            {

            }

            return result;
        }

        public DialogResult? CheckForDuplicateTTCFiles(AFS2GridSquare afs2GridSquare, out List<string> ttcFiles)
        {
            // A null dialog result means that there are no duplicates
            DialogResult? result = null;

            var gridSquareDirectory = AeroSceneryManager.Instance.Settings.WorkingDirectory + afs2GridSquare.Name;

            ttcFiles = this.EnumerateFilesRecursive(gridSquareDirectory, "*.ttc").ToList();

            List<string> ttcFileNames = new List<string>();

            foreach (var ttcFile in ttcFiles)
            {
                var tccFileName = Path.GetFileName(ttcFile);
                ttcFileNames.Add(tccFileName);
            }

            // Check for duplicate ttc files
            if (ttcFileNames.Count != ttcFileNames.Distinct().Count())
            {
                StringBuilder sbDuplicates = new StringBuilder();

                sbDuplicates.AppendLine(String.Format("Duplicate ttc files were found in the folder for grid square ({0})", afs2GridSquare.Name));
                sbDuplicates.AppendLine("This may be because you have downloaded this grid square with multiple map image providers.");
                sbDuplicates.AppendLine("If you continue with the install you may get a mismatched set of ttc files.");

                var duplicatesMessageBox = new CustomMessageBox(sbDuplicates.ToString(),
                    "AeroScenery",
                    MessageBoxIcon.Warning);

                duplicatesMessageBox.SetButtons(
                    new string[] { "Continue", "Cancel" },
                    new DialogResult[] { DialogResult.OK, DialogResult.Cancel });

                result = duplicatesMessageBox.ShowDialog();
            }

            return result;
        }

        public async Task InstallSceneryAsync(AFS2GridSquare afs2GridSquare, List<string> ttcFiles)
        {
            var task = Task.Run(() =>
            {
                var gridSquareDirectory = AeroSceneryManager.Instance.Settings.WorkingDirectory + afs2GridSquare.Name;

                // Does this grid square exist
                if (Directory.Exists(gridSquareDirectory))
                {
                    // Do we have an Aerofly folder to install into?
                    string afsSceneryInstallDirectory = DirectoryHelper.FindAFSSceneryInstallDirectory(AeroSceneryManager.Instance.Settings);

                    if (afsSceneryInstallDirectory != null)
                    {
                        // We install ttc files into a folder of the level 9 grid square containing the selected grid square
                        var level9GridSquare = afs2GridSquare;

                        if (afs2GridSquare.Level != 9)
                        {
                            level9GridSquare = this.afsGrid.GetGridSquareAtLatLon(afs2GridSquare.GetCenter().Lat, afs2GridSquare.GetCenter().Lng, 9);
                        }

                        // This is now the level9 grid square that contains the selected grid square
                        var afsSceneryFinalInstallDirectory = String.Format(@"{0}\{1}", afsSceneryInstallDirectory, level9GridSquare.Name);

                        if (ttcFiles.Count == 0)
                        {
                            // Most often the whole square lies past the coastline cut. Leave what is
                            // installed alone rather than guess: an empty conversion can also mean
                            // that something failed.
                            log.WarnFormat("There are no ttc files to install for grid square {0}, so {1} was not changed. " +
                                "If the whole square lies out at sea, past the coastline cut, delete that folder.",
                                afs2GridSquare.Name, afsSceneryFinalInstallDirectory);
                            return;
                        }

                        if (!Directory.Exists(afsSceneryFinalInstallDirectory))
                        {
                            Directory.CreateDirectory(afsSceneryFinalInstallDirectory);
                        }

                        // A level 9 square owns its install folder, so remove the tiles this build
                        // did not make. A square cut at the coastline has fewer tiles than one that
                        // was not, and a tile left from the earlier install would still show as sea.
                        // A smaller square shares its level 9 folder with its neighbours, so it
                        // only ever adds and replaces.
                        if (afs2GridSquare.Level == 9)
                        {
                            var building = new HashSet<string>(ttcFiles.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
                            int removed = 0;

                            foreach (var installed in Directory.EnumerateFiles(afsSceneryFinalInstallDirectory, "*.ttc").ToList())
                            {
                                if (!building.Contains(Path.GetFileName(installed)))
                                {
                                    File.Delete(installed);
                                    removed++;
                                }
                            }

                            if (removed > 0)
                            {
                                log.InfoFormat("Removed {0} installed ttc files that this build of grid square {1} no longer makes",
                                    removed, afs2GridSquare.Name);
                            }
                        }

                        // Copy the files over
                        foreach(var ttcFilePath in ttcFiles)
                        {
                            var filename = Path.GetFileName(ttcFilePath);
                            var destinationPath = String.Format(@"{0}/{1}", afsSceneryFinalInstallDirectory, filename);

                            // We want to overwrite files so that users can install updated files again
                            if (File.Exists(destinationPath))
                            {
                                File.Delete(destinationPath);
                            }
                            File.Copy(ttcFilePath, destinationPath);
                        }

                        log.Info(String.Format("Installed {0} ttc files for grid square {1} into {2}",
                            ttcFiles.Count,
                            afs2GridSquare.Name,
                            afsSceneryFinalInstallDirectory));
                    }
                    else
                    {
                        log.Error("Could not find an Aerofly folder to install scenery into");
                    }

                }

            });

            await task;
        }

        /// <summary>
        /// Recursively enumerates files. Silently fails if it doesn't have access to any files.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        private IEnumerable<string> EnumerateFilesRecursive(string root, string pattern = "*")
        {
            var todo = new Queue<string>();
            todo.Enqueue(root);
            while (todo.Count > 0)
            {
                string dir = todo.Dequeue();
                string[] subdirs = new string[0];
                string[] files = new string[0];
                try
                {
                    subdirs = Directory.GetDirectories(dir);
                    files = Directory.GetFiles(dir, pattern);
                }
                catch (IOException)
                {
                }
                catch (System.UnauthorizedAccessException)
                {
                }

                foreach (string subdir in subdirs)
                {
                    todo.Enqueue(subdir);
                }
                foreach (string filename in files)
                {
                    yield return filename;
                }
            }
        }


    }
}
