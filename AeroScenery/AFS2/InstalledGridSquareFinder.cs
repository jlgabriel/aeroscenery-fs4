using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AeroScenery.AFS2
{
    /// <summary>
    /// The level 9 grid squares that have tiles installed in Aerofly. Aerofly loads every .ttc under
    /// addons\scenery and scenery\images, whatever the folders are called, and the name of a tile gives
    /// its position. So the tile names, not the folder names, tell what is installed. This also finds
    /// scenery that another tool installed.
    /// </summary>
    public class InstalledGridSquareFinder
    {
        private readonly ILog log = LogManager.GetLogger("AeroScenery");

        // map_14_4bbc_5f3c.ttc, map_12_4b80_5f00_mask.ttc
        private static readonly Regex TileFileName =
            new Regex(@"^map_(\d{2})_([0-9a-fA-F]{4})_([0-9a-fA-F]{4})(_mask)?\.ttc$", RegexOptions.IgnoreCase);

        // A level 9 square is 2^(16 - 9) = 128 units of the world grid that tile names count in
        private const int Level9Step = 128;

        private static readonly string[] SceneryFolders = { @"addons\scenery", @"scenery\images" };

        public List<AFS2GridSquare> FindAll(string afsUserDirectory)
        {
            var gridSquares = new List<AFS2GridSquare>();

            if (String.IsNullOrEmpty(afsUserDirectory) || !Directory.Exists(afsUserDirectory))
            {
                return gridSquares;
            }

            var afs2Grid = new AFS2Grid();
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int tiles = 0;

            foreach (var sceneryFolder in SceneryFolders)
            {
                var root = Path.Combine(afsUserDirectory, sceneryFolder);

                if (!Directory.Exists(root))
                {
                    continue;
                }

                try
                {
                    foreach (var file in Directory.EnumerateFiles(root, "map_*.ttc", SearchOption.AllDirectories))
                    {
                        var match = TileFileName.Match(Path.GetFileName(file));

                        // Levels 7 and 8 are larger than a level 9 square, so they do not name one
                        if (!match.Success || int.Parse(match.Groups[1].Value) < 9)
                        {
                            continue;
                        }

                        tiles++;

                        int x = Convert.ToInt32(match.Groups[2].Value, 16) / Level9Step * Level9Step;
                        int y = Convert.ToInt32(match.Groups[3].Value, 16) / Level9Step * Level9Step;
                        found.Add(String.Format("{0:x4}_{1:x4}", x, y));
                    }
                }
                catch (Exception ex)
                {
                    // A folder that cannot be read must not stop the map from showing the rest
                    log.Warn(String.Format("Could not read all of {0}: {1}", root, ex.Message));
                }
            }

            foreach (var tile in found)
            {
                var gridSquare = afs2Grid.GetGridSquareName(tile, 9);

                if (gridSquare != null)
                {
                    gridSquares.Add(gridSquare);
                }
            }

            log.Info(String.Format("Found {0} installed grid squares ({1} tiles of level 9 and deeper)",
                gridSquares.Count, tiles));

            return gridSquares;
        }
    }
}
