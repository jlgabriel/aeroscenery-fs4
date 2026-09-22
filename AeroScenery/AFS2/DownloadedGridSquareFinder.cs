using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AeroScenery.AFS2
{
    /// <summary>
    /// The grid squares we have worked on are the directories in the working directory. Aerofly names
    /// a square after its level and the hex of its south west corner, so the name on its own gives
    /// back the coordinates and there is nothing left to store.
    /// </summary>
    public class DownloadedGridSquareFinder
    {
        private readonly ILog log = LogManager.GetLogger("AeroScenery");

        // map_09_4d80_6680
        private static readonly Regex GridSquareDirectoryName =
            new Regex(@"^map_(\d{2})_([0-9a-fA-F]{4})_([0-9a-fA-F]{4})$");

        public List<AFS2GridSquare> FindAll(string workingDirectory)
        {
            var gridSquares = new List<AFS2GridSquare>();

            if (String.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory))
            {
                return gridSquares;
            }

            var afs2Grid = new AFS2Grid();

            foreach (var directory in Directory.GetDirectories(workingDirectory))
            {
                var directoryName = Path.GetFileName(directory);
                var match = GridSquareDirectoryName.Match(directoryName);

                if (!match.Success)
                {
                    continue;
                }

                var level = int.Parse(match.Groups[1].Value);
                var tile = String.Format("{0}_{1}", match.Groups[2].Value, match.Groups[3].Value);

                var gridSquare = afs2Grid.GetGridSquareName(tile, level);

                // A hex that does not come back as the name we started from is not a square of this
                // level, whatever the directory is called
                if (gridSquare == null || !String.Equals(gridSquare.Name, directoryName, StringComparison.OrdinalIgnoreCase))
                {
                    log.Warn(String.Format("{0} is not the name of a grid square, ignoring it", directoryName));
                    continue;
                }

                gridSquares.Add(gridSquare);
            }

            return gridSquares;
        }
    }
}
