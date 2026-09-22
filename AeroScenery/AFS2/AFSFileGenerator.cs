using AeroScenery.Common;
using AeroScenery.Controls;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace AeroScenery.AFS2
{
    public class AFSFileGenerator
    {
        //#TRY_j: Not used?
        // The prefix of each image tile and aero file, e.g. g_19_
        //private string filenamePrefix;

        private XmlSerializer xmlSerializer;

        private readonly ILog log = LogManager.GetLogger("AeroScenery");

        public AFSFileGenerator()
        {
            this.xmlSerializer = new XmlSerializer(typeof(StitchedImage));
        }

        public async Task GenerateAFSFilesAsync(AFS2GridSquare afs2GridSquare, string stitchedTilesDirectory, string afsGridSquareDirectory, IProgress<AFSFileGeneratorProgress> progress)
        {
            await Task.Run(() =>
            {
                var afsFileGeneratorProgress = new AFSFileGeneratorProgress();

                StitchedImage firstStitchedImageAeroFile = null;

                // The number of stiched tiles should always be pretty manageable so we can get a list of filenames

                if (Directory.Exists(stitchedTilesDirectory))
                {
                    string[] stitchedImagesAeroFiles = Directory.GetFiles(stitchedTilesDirectory, "*.aero");

                    int i = 0;

                    foreach (string aeroFilename in stitchedImagesAeroFiles)
                    {
                        try
                        {
                            StitchedImage stitchedImageAeroFile;

                            using (StreamReader reader = new StreamReader(aeroFilename))
                            {
                                stitchedImageAeroFile = (StitchedImage)xmlSerializer.Deserialize(reader);
                                reader.Close();
                            }

                            if (i == 0)
                            {
                                firstStitchedImageAeroFile = stitchedImageAeroFile;
                            }

                            double stepsPerPixelX = Math.Abs((stitchedImageAeroFile.WestLongitude - stitchedImageAeroFile.EastLongitude) / stitchedImageAeroFile.Width);
                            double stepsPerPixelY = -Math.Abs((stitchedImageAeroFile.NorthLatitude - stitchedImageAeroFile.SouthLatitude) / stitchedImageAeroFile.Height);

                            var aidFile = new AIDFile();

                            aidFile.ImageFile = stitchedImageAeroFile.FileName + "." + stitchedImageAeroFile.ImageExtension;
                            aidFile.FlipVertical = false;
                            aidFile.StepsPerPixelX = stepsPerPixelX;
                            aidFile.StepsPerPixelY = stepsPerPixelY;
                            aidFile.X = stitchedImageAeroFile.WestLongitude;
                            aidFile.Y = stitchedImageAeroFile.NorthLatitude;

                            var aidFileStr = aidFile.ToString();

                            string path = stitchedTilesDirectory + stitchedImageAeroFile.FileName + ".aid";

                            log.InfoFormat("Writing AID file {0}", path);
                            File.WriteAllText(path, aidFileStr);
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex.Message);
                        }

                        i++;
                    }

                    if (firstStitchedImageAeroFile != null)
                    {
                        this.GenerateTMCFile(afs2GridSquare, stitchedTilesDirectory, afsGridSquareDirectory, firstStitchedImageAeroFile);
                    }
                    else
                    {
                        var messageBox = new CustomMessageBox("No stiched images found for this grid square and this image detail (zoom) level.\nRun the 'Download Image Tiles' and 'Stitch Image Tiles' actions first.",
                            "AeroScenery",
                            MessageBoxIcon.Error);

                        messageBox.ShowDialog();
                    }



                }
            });


        }

        private void GenerateTMCFile(AFS2GridSquare afs2GridSquare, string stitchedTilesDirectory, string afsGridSquareDirectory, StitchedImage firstStitchedImageAeroFile)
        {
            // The converter writes its tiles here. The folder name is the one the tiles have
            // always had, so a working directory built by an earlier version still installs.
            var ttcDirectory = String.Format("{0}-geoconvert-ttc\\", firstStitchedImageAeroFile.ZoomLevel);
            var ttcPath = afsGridSquareDirectory + ttcDirectory;

            if (!Directory.Exists(ttcPath))
            {
                Directory.CreateDirectory(ttcPath);
            }

            var filenameParts = firstStitchedImageAeroFile.FileName.Split('_');
            var tmcFilename = String.Format("{0}_{1}_{2}", filenameParts[0], filenameParts[1], filenameParts[2]);

            // The converter reads every tmc file in this directory. An earlier version could split
            // a square into several, so remove them all before the one that is wanted is written.
            // This method is the only writer of tmc files here, so that is safe.
            foreach (var staleTMCFile in Directory.EnumerateFiles(stitchedTilesDirectory, "*.tmc"))
            {
                File.Delete(staleTMCFile);
            }

            var tmcFile = new TMCFile();

            tmcFile.AlwaysOverwrite = true;
            tmcFile.DoHeightmaps = false;
            tmcFile.FolderDestinationTTC = ttcPath;
            tmcFile.FolderSourceFiles = stitchedTilesDirectory;
            tmcFile.WriteImagesWithMask = AeroSceneryManager.Instance.Settings.WriteImagesWithMask.Value;
            tmcFile.WriteTTCFiles = true;

            tmcFile.Regions = this.GenerateTMCFileRegions(afs2GridSquare);

            if (tmcFile.Regions.Count == 0)
            {
                return;
            }

            string path = String.Format("{0}{1}.tmc", stitchedTilesDirectory, tmcFilename);

            log.InfoFormat("Writing TMC file {0}", path);
            File.WriteAllText(path, tmcFile.ToString());
        }

        /// <summary>
        /// The regions of a grid square, one per AFS level.
        /// </summary>
        private List<TMCRegion> GenerateTMCFileRegions(AFS2GridSquare afs2GridSquare)
        {
            var settings = AeroSceneryManager.Instance.Settings;

            List<TMCRegion> regions = new List<TMCRegion>();

            foreach (int afsLevel in settings.AFSLevelsToGenerate)
            {
                var region = new TMCRegion();

                // Really the NW corner, and LatMax really the SE one
                region.LatMin = afs2GridSquare.NorthLatitude;
                region.LatMax = afs2GridSquare.SouthLatitude;
                region.LonMin = afs2GridSquare.WestLongitude;
                region.LonMax = afs2GridSquare.EastLongitude;
                region.ShrinkWest = true;
                region.ShrinkEast = true;
                region.Level = afsLevel;
                region.WriteImagesWithMask = settings.WriteImagesWithMask.Value;

                regions.Add(region);
            }

            return regions;
        }
    }
}
