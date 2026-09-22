using AeroScenery.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AeroScenery.AFS2
{
    public class TMCRegion
    {
        public int Level { get; set; }
        public double LonMin { get; set; }
        public double LatMin { get; set; }
        public double LonMax { get; set; }
        public double LatMax { get; set; }
        public bool WriteImagesWithMask { get; set; }

        /// <summary>
        /// Whether the west and east edges are the outer edges of the grid square.
        /// A region that is one of several side by side strips has an inner edge where it meets its
        /// neighbour, and pulling that edge inwards would leave a strip of the square converted by
        /// nobody. Only outer edges get the shrink.
        /// </summary>
        public bool ShrinkWest { get; set; }

        public bool ShrinkEast { get; set; }

        public TMCRegion()
        {
            this.ShrinkWest = true;
            this.ShrinkEast = true;
        }

        private static double Shrink(double coordinate, bool apply, Direction direction)
        {
            if (!apply)
            {
                return coordinate;
            }

            return GeoCoordinatesHelper.CalculateOffset(coordinate, AeroSceneryManager.Instance.Settings.ShrinkTMCGridSquareCoords.Value, direction);
        }

        public string NorthWestLonLatStr
        {
            get
            {
                var westLonShrink = Shrink(LonMin, this.ShrinkWest, Direction.East);
                var northLatShrink = Shrink(LatMin, true, Direction.South);

                return String.Format("{0} {1}", westLonShrink.ToString("0.00######", CultureInfo.InvariantCulture), northLatShrink.ToString("0.00######", CultureInfo.InvariantCulture));
            }
        }

        public string SouthEastLonLatStr
        {
            get
            {
                var eastLonShrink = Shrink(LonMax, this.ShrinkEast, Direction.West);
                var southLatShrink = Shrink(LatMax, true, Direction.North);

                return String.Format("{0} {1}", eastLonShrink.ToString("0.00######", CultureInfo.InvariantCulture), southLatShrink.ToString("0.00######", CultureInfo.InvariantCulture));
            }
        }
    }

    public class TMCFile
    {
        public bool WriteImagesWithMask { get; set; }
        public bool WriteTTCFiles { get; set; }
        public bool AlwaysOverwrite { get; set; }
        public bool DoHeightmaps { get; set; }
        public string FolderDestinationTTC { get; set; }
        public string FolderSourceFiles { get; set; }

        public List<TMCRegion> Regions { get; set; }

        public TMCFile()
        {
            Regions = new List<TMCRegion>();
        }

        // Reading a .tmc back lives in TmcReader, not here. This class writes, and writing pulls
        // the shrink margin out of AeroSceneryManager.Instance.Settings; the converter must not
        // inherit that dependency, or it could not be built or tested without the whole app.

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<[file]" + "[]" + "[" + "]");
            sb.AppendLine("\t" + "<[tmcolormap_regions]" + "[]" + "[]");

            if (!String.IsNullOrEmpty(this.FolderSourceFiles))
            {
                sb.AppendLine("\t\t" + "<[string8]" + "[folder_source_files]" + "[" + FolderSourceFiles + "]>");
            }

            sb.AppendLine("\t\t" + "<[bool]" + "[write_ttc_files]" + "[" + WriteTTCFiles.ToString().ToLower() + "]>");
            sb.AppendLine("\t\t" + "<[string8]" + "[folder_destination_ttc]" + "[" + FolderDestinationTTC + "]>");
            sb.AppendLine("\t\t" + "<[bool]" + "[do_heightmaps]" + "[" + DoHeightmaps.ToString().ToLower() + "]>");
            sb.AppendLine("\t\t" + "<[bool]" + "[always_overwrite]" + "[" + AlwaysOverwrite.ToString().ToLower() + "]>");
            sb.AppendLine("\t\t" + "<[bool]" + "[write_images_with_mask]" + "[" + WriteImagesWithMask.ToString().ToLower() + "]>");
            sb.AppendLine("");
            sb.AppendLine("\t\t" + "<[list]" + "[region_list]" + "[]");
            sb.AppendLine("");

            if (this.Regions != null)
            {
                foreach (var region in this.Regions)
                {
                    sb.AppendLine("\t\t\t<[tmcolormap_region][element][0]");
                    sb.AppendLine(String.Format("\t\t\t\t<[uint32] [level] [{0}]>", region.Level));
                    sb.AppendLine(String.Format("\t\t\t\t<[vector2_float64] [lonlat_min] [{0}]>", region.NorthWestLonLatStr));
                    sb.AppendLine(String.Format("\t\t\t\t<[vector2_float64] [lonlat_max] [{0}]>", region.SouthEastLonLatStr));
                    sb.AppendLine(String.Format("\t\t\t\t<[bool] [write_images_with_mask] [{0}]>", region.WriteImagesWithMask.ToString().ToLower()));
                    sb.AppendLine("\t\t\t" + ">");
                    sb.AppendLine("");
                }
            }


            sb.AppendLine("");
            sb.AppendLine("\t\t" + ">");
            sb.AppendLine("\t" + ">");
            sb.AppendLine(">");

            return sb.ToString();

        }

    }
}
