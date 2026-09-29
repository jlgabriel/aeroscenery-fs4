using AeroScenery.AFS2;
using AeroScenery.Common;
using AeroScenery.Controls;
using AeroScenery.Data;
using AeroScenery.Download;
using AeroScenery.ImageProcessing;
using AeroScenery.OrthophotoSources;
using AeroScenery.OrthophotoSources.Japan;
using AeroScenery.OrthophotoSources.NewZealand;
using AeroScenery.OrthophotoSources.Norway;
using AeroScenery.OrthophotoSources.Spain;
using AeroScenery.OrthophotoSources.Sweden;
using AeroScenery.OrthophotoSources.Switzerland;
using AeroScenery.OrthophotoSources.UnitedStates;
using AeroScenery.OrthoPhotoSources;
using AeroScenery.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//#MOD_f
using System.Globalization;

namespace AeroScenery
{
    public class AeroSceneryManager
    {
        private MainForm mainForm;

        private BingOrthophotoSource bingOrthophotoSource;
        private GoogleOrthophotoSource googleOrthophotoSource;
        private USGSOrthophotoSource usgsOrthophotoSource;
        private GSIOrthophotoSource gsiOrthophotoSource;
        private LinzOrthophotoSource linzOrthophotoSource;
        private NorgeBilderOrthophotoSource norgeBilderOrthophotoSource;
        private IDEIBOrthophotoSource ideibOrthophotoSource;
        private IGNOrthophotoSource ignOrthophotoSource;
        private LantmaterietOrthophotoSource lantmaterietOrthophotoSource;
        private GeoportalOrthophotoSource geoportalOrthophotoSource;
        private ArcGISOrthophotoSource arcGISOrthophotoSource;
        private HittaOrthophotoSource hittaOrthophotoSource;
        private HereWeGoOrthophotoSource hereWeGoOrthophotoSource;
        private GuleSiderOrthophotoSource guleSiderOrthophotoSource;
        //#MOD_e
        private MapboxOrthophotoSource mapboxOrthophotoSource;
        //#MOD_b 

        private DownloadManager downloadManager;

        private TtcConverterManager ttcConverterManager;

        //private DownloadFailedForm downloadFailedForm;

        private TileStitcher tileStitcher;

        private static AeroSceneryManager aeroSceneryManager;

        private ImageTileService imageTileService;

        private Common.Settings settings;

        private SettingsService settingsService;

        private AFSFileGenerator afsFileGenerator;

        private List<ImageTile> imageTiles;
        private readonly ILog log = LogManager.GetLogger("AeroScenery");
        private string version;

        public AeroSceneryManager()
        {
            downloadManager = new DownloadManager();
            ttcConverterManager = new TtcConverterManager();
            imageTileService = new ImageTileService();
            tileStitcher = new TileStitcher();
            settingsService = new SettingsService();
            afsFileGenerator = new AFSFileGenerator();

            imageTiles = null;
            version = "2.2";
        }

        public Settings Settings
        {
            get
            {
                return this.settings;
            }
        }

        public string Version
        {
            get
            {
                return this.version;
            }
        }

        public static AeroSceneryManager Instance
        {
            get
            {
                if (AeroSceneryManager.aeroSceneryManager == null)
                {
                    aeroSceneryManager = new AeroSceneryManager();
                }

                return aeroSceneryManager;
            }
        }

        public void Initialize()
        {
            // Create settings if required and read them
            this.settings = settingsService.GetSettings();
            settingsService.LogSettings(this.settings);
            settingsService.CheckConfiguredDirectories(this.settings);

            bingOrthophotoSource = new BingOrthophotoSource(settings.OrthophotoSourceSettings.BN_OrthophotoSourceUrlTemplate);
            googleOrthophotoSource = new GoogleOrthophotoSource(settings.OrthophotoSourceSettings.GM_OrthophotoSourceUrlTemplate);
            usgsOrthophotoSource = new USGSOrthophotoSource();
            gsiOrthophotoSource = new GSIOrthophotoSource();
            linzOrthophotoSource = new LinzOrthophotoSource();
            norgeBilderOrthophotoSource = new NorgeBilderOrthophotoSource();
            ideibOrthophotoSource = new IDEIBOrthophotoSource();
            ignOrthophotoSource = new IGNOrthophotoSource();
            lantmaterietOrthophotoSource = new LantmaterietOrthophotoSource();
            geoportalOrthophotoSource = new GeoportalOrthophotoSource();
            arcGISOrthophotoSource = new ArcGISOrthophotoSource();
            hittaOrthophotoSource = new HittaOrthophotoSource();
            hereWeGoOrthophotoSource = new HereWeGoOrthophotoSource();
            guleSiderOrthophotoSource = new GuleSiderOrthophotoSource();
            //#MOD_e
            mapboxOrthophotoSource = new MapboxOrthophotoSource();
            //#MOD_b

            this.mainForm = new MainForm();
            this.mainForm.StartStopClicked += async (sender, eventArgs) =>
            {
                //#MOD_g
                // Bug fix: Adding a delay for Start & Stops reduces the occurrence of an unhandled error when stopping the download (bug appears since the number of download threads has been increased from 4 to 8)
                await Task.Delay(600);

                if (this.mainForm.ActionsRunning)
                {
                    // A fault must not end the app, and it must not end silently either. Log it,
                    // stop what is still running and give the UI back, so that Start works again.
                    try
                    {
                        await StartSceneryGenerationProcessAsync(sender, eventArgs);
                    }
                    catch (Exception ex)
                    {
                        log.Error("The run stopped on an error", ex);
                        StopSceneryGenerationProcess(sender, eventArgs);

                        // Not running any more, so ActionsComplete logs the run as stopped, not finished
                        this.mainForm.ActionsRunning = false;
                        this.mainForm.ActionsComplete();
                    }

                }
                else
                {
                    StopSceneryGenerationProcess(sender, eventArgs);
                }
            };

            this.mainForm.Initialize();
            Application.Run(this.mainForm);

        }


        private string GetTileDownloadDirectory(string afsGridSquareDirectory)
        {
            var tileDownloadDirectory = afsGridSquareDirectory;

            switch (this.settings.OrthophotoSource)
            {
                case OrthophotoSource.Bing:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.Bing);
                    break;
                case OrthophotoSource.Google:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.Google);
                    break;
                case OrthophotoSource.ArcGIS:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.ArcGIS);
                    break;
                case OrthophotoSource.US_USGS:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.US_USGS);
                    break;
                case OrthophotoSource.NZ_Linz:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.NZ_Linz);
                    break;
                case OrthophotoSource.ES_IDEIB:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.ES_IDEIB);
                    break;
                case OrthophotoSource.CH_Geoportal:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.CH_Geoportal);
                    break;
                case OrthophotoSource.NO_NorgeBilder:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.NO_NorgeBilder);
                    break;
                case OrthophotoSource.SE_Lantmateriet:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.SE_Lantmateriet);
                    break;
                case OrthophotoSource.ES_IGN:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.ES_IGN);
                    break;
                case OrthophotoSource.JP_GSI:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.JP_GSI);
                    break;
                case OrthophotoSource.SE_Hitta:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.SE_Hitta);
                    break;
                case OrthophotoSource.HereWeGo:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.HereWeGo);
                    break;
                case OrthophotoSource.NO_GuleSider:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.NO_GuleSider);
                    break;
                //#MOD_e
                case OrthophotoSource.Mapbox:
                    tileDownloadDirectory += String.Format("\\{0}\\", OrthophotoSourceDirectoryName.Mapbox);
                    break;
                //#MOD_b
            }

            return tileDownloadDirectory;
        }

        public void StopSceneryGenerationProcess(object sender, EventArgs e)
        {
            downloadManager.StopDownloads();

            if (this.imageTiles != null)
            {
                this.imageTiles.Clear();
                this.imageTiles = null;
                System.GC.Collect();
            }

        }

        private void ActionsComplete()
        {
            this.mainForm.ActionsComplete();

            if (this.imageTiles != null)
            {
                this.imageTiles.Clear();
                this.imageTiles = null;
                System.GC.Collect();
            }

        }




        public async Task StartSceneryGenerationProcessAsync(object sender, EventArgs e)
        {
            try
            {
                // Set settings on orthophoto sources
                this.linzOrthophotoSource.ApiKey = settings.LinzApiKey;
                //#MOD_e
                this.mapboxOrthophotoSource.ApiKey = settings.MapboxApiKey;
                //#MOD_h
                this.hereWeGoOrthophotoSource.ApiKey = settings.HereWeGoApiKey;

                int i = 0;
                foreach (AFS2GridSquare afs2GridSquare in this.mainForm.SelectedAFS2GridSquares.Values.Select(x => x.AFS2GridSquare))
                {
                    var currentGrideSquareMessage = String.Format("Working on AFS4 Grid Square {0} of {1}", i + 1, this.mainForm.SelectedAFS2GridSquares.Count());
                    this.mainForm.UpdateParentTaskLabel(currentGrideSquareMessage);
                    log.Info(currentGrideSquareMessage);

                    var afsGridSquareDirectory = this.settings.WorkingDirectory + afs2GridSquare.Name;

                    var tileDownloadDirectory = GetTileDownloadDirectory(afsGridSquareDirectory) + this.settings.ZoomLevel + @"\";
                    var stitchedTilesDirectory = GetTileDownloadDirectory(afsGridSquareDirectory) + this.settings.ZoomLevel + @"-stitched\";

                    if ((this.Settings.DownloadImageTiles.Value || (this.Settings.DownloadImageTiles.Value || (this.Settings.StitchImageTiles.Value) || (this.Settings.GenerateAIDAndTMCFiles.Value) || (this.Settings.RunConverter.Value)) && this.mainForm.ActionsRunning))
                    {
                        // Do we have a directory for this afs grid square in our working directory?
                        //var afsGridSquareDirectory = this.settings.WorkingDirectory + afs2GridSquare.Name;

                        if (!Directory.Exists(this.settings.WorkingDirectory + afs2GridSquare.Name))
                        {
                            Directory.CreateDirectory(this.settings.WorkingDirectory + afs2GridSquare.Name);
                        }

                        if (!Directory.Exists(tileDownloadDirectory))
                        {
                            Directory.CreateDirectory(tileDownloadDirectory);
                        }

                        if (!Directory.Exists(stitchedTilesDirectory))
                        {
                            Directory.CreateDirectory(stitchedTilesDirectory);
                        }
                    }

                    // Download Imamge Tiles
                    if (this.Settings.DownloadImageTiles.Value && this.mainForm.ActionsRunning)
                    {
                        this.mainForm.UpdateChildTaskLabel("Calculating Image Tiles To Download");
                        log.Info("Calculating Image Tiles To Download");

                        GenericOrthophotoSource orthophotoSourceInstance = null;

                        var imageTilesTask = Task.Run(() => {

                            // Get a list of all the image tiles we need to download
                            switch (settings.OrthophotoSource)
                            {
                                case OrthophotoSource.Bing:
                                    imageTiles = bingOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = bingOrthophotoSource;
                                    break;
                                case OrthophotoSource.Google:
                                    imageTiles = googleOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = googleOrthophotoSource;
                                    break;
                                case OrthophotoSource.ArcGIS:
                                    imageTiles = arcGISOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = arcGISOrthophotoSource;
                                    break;
                                case OrthophotoSource.US_USGS:
                                    imageTiles = usgsOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = usgsOrthophotoSource;
                                    break;
                                case OrthophotoSource.NZ_Linz:
                                    imageTiles = linzOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = linzOrthophotoSource;
                                    break;
                                case OrthophotoSource.ES_IDEIB:
                                    imageTiles = ideibOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = ideibOrthophotoSource;
                                    break;
                                case OrthophotoSource.CH_Geoportal:
                                    imageTiles = geoportalOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = geoportalOrthophotoSource;
                                    break;
                                case OrthophotoSource.NO_NorgeBilder:
                                    imageTiles = norgeBilderOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = norgeBilderOrthophotoSource;
                                    break;
                                case OrthophotoSource.SE_Lantmateriet:
                                    imageTiles = lantmaterietOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = lantmaterietOrthophotoSource;
                                    break;
                                case OrthophotoSource.ES_IGN:
                                    imageTiles = ignOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = ignOrthophotoSource;
                                    break;
                                case OrthophotoSource.JP_GSI:
                                    imageTiles = gsiOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = gsiOrthophotoSource;
                                    break;
                                case OrthophotoSource.SE_Hitta:
                                    imageTiles = hittaOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = hittaOrthophotoSource;
                                    break;
                                case OrthophotoSource.HereWeGo:
                                    imageTiles = hereWeGoOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = hereWeGoOrthophotoSource;
                                    break;
                                case OrthophotoSource.NO_GuleSider:
                                    imageTiles = guleSiderOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = guleSiderOrthophotoSource;
                                    break;
                                //#MOD_e
                                case OrthophotoSource.Mapbox:
                                    imageTiles = mapboxOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    orthophotoSourceInstance = mapboxOrthophotoSource;
                                    break;
                                //#MOD_b
                            }
                        });

                        await imageTilesTask;

                        this.mainForm.UpdateChildTaskLabel("Downloading Image Tiles");
                        log.Info("Downloading Image Tiles");

                        // Capture the progress of each thread
                        var downloadThreadProgress = new Progress<DownloadThreadProgress>();
                        downloadThreadProgress.ProgressChanged += DownloadThreadProgress_ProgressChanged;

                        // Which tiles Bing has no imagery for are worth filling from a lower zoom.
                        // Off the main thread: the field takes a few seconds to build.
                        Func<ImageTile, bool> fillWanted = null;
                        if (settings.OrthophotoSource == OrthophotoSource.Bing && (settings.FillMissingTiles ?? true))
                        {
                            fillWanted = await Task.Run(() => this.FillWantedFor(afs2GridSquare));
                        }

                        // Send the image tiles to the download manager
                        //#MOD_g
                        //await downloadManager.DownloadImageTiles(settings.OrthophotoSource.Value, imageTiles, downloadThreadProgress, tileDownloadDirectory, orthophotoSourceInstance);
                        await downloadManager.DownloadImageTiles(settings.OrthophotoSource.Value, imageTiles, downloadThreadProgress, tileDownloadDirectory, orthophotoSourceInstance, Convert.ToInt16(settings.SimultaneousDownloads),
                            fillWanted);

                        // Only finalise if we weren't cancelled
                        if (this.mainForm.ActionsRunning)
                        {
                            this.mainForm.AddDownloadedGridSquare(afs2GridSquare);
                        }


                    }

                    // Stitch Image Tiles
                    if (this.Settings.StitchImageTiles.Value && this.mainForm.ActionsRunning)
                    {
                        this.mainForm.UpdateChildTaskLabel("Stitching Image Tiles");
                        log.Info("Stitching Image Tiles");

                        // Capture the progress of the tile stitcher
                        var tileStitcherProgress = new Progress<TileStitcherProgress>();
                        tileStitcherProgress.ProgressChanged += TileStitcherProgress_ProgressChanged;

                        //#MOD_h
                        //await this.tileStitcher.StitchImageTilesAsync(tileDownloadDirectory, stitchedTilesDirectory, true, tileStitcherProgress);
                        await this.tileStitcher.StitchImageTilesAsync(tileDownloadDirectory, stitchedTilesDirectory, true, settings.OrthophotoSource.Value, tileStitcherProgress);
                    }

                    // Generate AID and TMC Files
                    if (this.Settings.GenerateAIDAndTMCFiles.Value && this.mainForm.ActionsRunning)
                    {
                        this.mainForm.UpdateChildTaskLabel("Generating AFS Metadata Files");
                        log.Info("Generating AFS Metadata Files");

                        // Capture the progress of the tile stitcher
                        var afsFileGeneratorProgress = new Progress<AFSFileGeneratorProgress>();
                        afsFileGeneratorProgress.ProgressChanged += AFSFileGeneratorProgress_ProgressChanged;


                        // Generate AID files for the image tiles
                        await afsFileGenerator.GenerateAFSFilesAsync(afs2GridSquare, stitchedTilesDirectory, GetTileDownloadDirectory(afsGridSquareDirectory), afsFileGeneratorProgress);

                    }





                }

                //#MOD_h (End of)

                // If required, move on to converting the stitched images into ttc tiles
                if (this.settings.RunConverter.Value && this.mainForm.ActionsRunning)
                {
                    await this.StartConverterAsync();
                }


                //#Nickohod (not implemented)
                // Delete Stitched Immage Tiles
                //if (this.Settings.DeleteStitchedImageTiles)
                //{
                //    this.mainForm.UpdateChildTaskLabel("Deleting Stitched Image Tiles");

                //    // If we haven't just downloaded image tiles we need to load aero files to get image tile objects
                //    if (imageTiles == null)
                //    {
                //        imageTiles = await this.imageTileService.LoadImageTilesAsync(tileDownloadDirectory);
                //    }

                //}


                // Install Scenery
                if (this.settings.InstallScenery.Value && this.mainForm.ActionsRunning)
                {
                    await this.InstallSceneryAsync();
                }

                this.ActionsComplete();

            }
            finally
            {
                if (this.imageTiles != null)
                {
                    this.imageTiles.Clear();
                    this.imageTiles = null;
                    System.GC.Collect();
                }
            }

        }

        public async Task StartConverterAsync()
        {
            if (this.mainForm.ActionsRunning)
            {
                if (settings.AFSLevelsToGenerate.Count == 0)
                {
                    var messageBox = new CustomMessageBox("Please choose one or more AFS levels to generate before running the converter",
                        "AeroScenery",
                        MessageBoxIcon.Warning);

                    messageBox.ShowDialog();
                }
                else
                {
                    await RunConverterAsync();
                }
            }
        }

        /// <summary>
        /// The one coastline of the package: at the root of the working folder rather than inside a
        /// square, so neighbouring squares cannot disagree about where their shared edge is. Draw
        /// Coast on the Map tab writes it, and the converter reads it.
        /// </summary>
        public string CoastlinePath
        {
            get
            {
                return Path.Combine(this.settings.WorkingDirectory, "coastline.txt");
            }
        }

        /// <summary>
        /// The lakes of the package, beside the coastline and apart from it. Lake on the Map tab
        /// writes it. A separate file, so a version that knows nothing of lakes cannot read one as
        /// coast or island.
        /// </summary>
        public string LakesPath
        {
            get
            {
                return Path.Combine(this.settings.WorkingDirectory, "lakes.txt");
            }
        }

        /// <summary>
        /// The lakes to cut out this run, or null for none: the setting is off, or no lake has
        /// been drawn. Loaded once, like the coastline. A lake is cut in every square it reaches,
        /// with no coastline and no stretch rule: the ring says exactly what to cut.
        /// </summary>
        private Coastline LoadLakesForRun()
        {
            if (!(this.settings.CutOutLakes ?? true))
            {
                return null;
            }

            var path = this.LakesPath;

            if (!File.Exists(path))
            {
                return null;
            }

            Coastline lakes;

            try
            {
                lakes = Coastline.LoadLakes(path);
            }
            catch (Exception ex)
            {
                log.Error(String.Format("Could not read the lakes {0}, so no lake is cut out", path), ex);
                return null;
            }

            if (lakes.IsEmpty)
            {
                return null;
            }

            log.InfoFormat("Cutting out {0} lake(s) in {1}", lakes.Islands.Count, path);
            return lakes;
        }

        /// <summary>
        /// The coastline to cut this run at, or null for no cut: the setting is off, no line has
        /// been drawn, or the file has no line in it. Loaded once, because every square of a run
        /// must be cut at the same line.
        /// </summary>
        private Coastline LoadCoastlineForRun()
        {
            if (!(this.settings.CutAtCoastline ?? false))
            {
                return null;
            }

            var path = this.CoastlinePath;

            if (!File.Exists(path))
            {
                log.Info("No coastline has been drawn, so the photoscenery is not cut at the sea");
                return null;
            }

            Coastline coast;

            try
            {
                coast = Coastline.Load(path);
            }
            catch (Exception ex)
            {
                log.Error(String.Format("Could not read the coastline {0}, so nothing is cut", path), ex);
                return null;
            }

            if (coast.IsEmpty)
            {
                log.WarnFormat("{0} has no line in it, so nothing is cut", path);
                return null;
            }

            if (coast.HasCoast)
            {
                log.InfoFormat("Cutting at the coastline in {0}: {1} points, lat {2:0.00} to {3:0.00}, lon {4:0.00} to {5:0.00}, land to the {6}, {7:0.##} NM out to sea",
                    path, coast.Points.Count,
                    coast.Points.Min(p => p.Lat), coast.Points.Max(p => p.Lat),
                    coast.Points.Min(p => p.Lon), coast.Points.Max(p => p.Lon),
                    coast.Land.ToString().ToLowerInvariant(),
                    coast.MarginKm / 1.852);
            }

            if (coast.Islands.Count > 0)
            {
                log.InfoFormat("Cutting at {0} island(s) in {1}, {2:0.##} NM out to sea",
                    coast.Islands.Count, path, coast.MarginKm / 1.852);
            }

            return coast;
        }

        /// <summary>
        /// Which tiles with no imagery the download fills from a lower zoom, for one grid square:
        /// the tiles that reach inside the coastline cut. The rest are cut away, so filling them
        /// only costs time - 91% of the fill of the first Balearic build. Null, to fill every tile,
        /// when the square is not cut: then all of it shows.
        ///
        /// The test is the converter's own field and rule, so it cannot leave out a tile the
        /// converter keeps. RangeOver bounds the field over the tile exactly.
        /// </summary>
        private Func<ImageTile, bool> FillWantedFor(AFS2GridSquare afs2GridSquare)
        {
            if (!(this.settings.CutAtCoastline ?? false) || !File.Exists(this.CoastlinePath))
            {
                return null;
            }

            Coastline coast;
            try
            {
                coast = Coastline.Load(this.CoastlinePath);
            }
            catch (Exception)
            {
                return null;
            }

            if (coast.IsEmpty)
            {
                return null;
            }

            string why;
            var part = coast.ForSquare(afs2GridSquare.WestLongitude, afs2GridSquare.EastLongitude,
                afs2GridSquare.SouthLatitude, afs2GridSquare.NorthLatitude, out why);
            if (part == null)
            {
                return null;
            }

            // A little wider than the square, because the tiles to download reach past its edges.
            const double Pad = 0.03;
            var field = CoastlineField.Build(part,
                afs2GridSquare.WestLongitude - Pad, afs2GridSquare.EastLongitude + Pad,
                afs2GridSquare.SouthLatitude - Pad, afs2GridSquare.NorthLatitude + Pad);
            double margin = part.MarginKm;

            return tile =>
            {
                double lo, hi;
                field.RangeOver(tile.WestLongitude, tile.EastLongitude, tile.SouthLatitude, tile.NorthLatitude, out lo, out hi);
                return lo <= margin;
            };
        }

        /// <summary>
        /// The coastline for one grid square, or null to convert it without a cut. The rule is
        /// Coastline.ForSquare: the coast only where the square is wholly inside the stretch it
        /// covers, and an island only where it comes near the square.
        /// </summary>
        private Coastline CoastFor(AFS2GridSquare afs2GridSquare, Coastline coast)
        {
            if (coast == null)
            {
                return null;
            }

            string why;
            var part = coast.ForSquare(afs2GridSquare.WestLongitude, afs2GridSquare.EastLongitude,
                afs2GridSquare.SouthLatitude, afs2GridSquare.NorthLatitude, out why);

            if (part == null)
            {
                log.WarnFormat("Grid square {0} is converted without a cut: {1}. " +
                    "To cut it, draw the coastline past both of its edges, or draw its island.",
                    afs2GridSquare.Name, why);
            }

            return part;
        }

        /// <summary>
        /// Converts the stitched images of every selected grid square into .ttc tiles, one square
        /// at a time. The converter runs in this process; see TtcConverterManager.
        /// </summary>
        public async Task RunConverterAsync()
        {
            log.Info("Starting tmc to ttc conversion");

            var coast = this.LoadCoastlineForRun();
            var lakes = this.LoadLakesForRun();

            int i = 0;

            foreach (AFS2GridSquare afs2GridSquare in this.mainForm.SelectedAFS2GridSquares.Values.Select(x => x.AFS2GridSquare))
            {
                if (this.mainForm.ActionsRunning)
                {
                    var currentGrideSquareMessage = String.Format("Working on AFS4 Grid Square {0} of {1}", i + 1, this.mainForm.SelectedAFS2GridSquares.Count());
                    this.mainForm.UpdateParentTaskLabel(currentGrideSquareMessage);
                    log.Info(currentGrideSquareMessage);

                    // Do we have a directory for this afs grid square in our working directory?
                    var afsGridSquareDirectory = this.settings.WorkingDirectory + afs2GridSquare.Name;

                    if (Directory.Exists(afsGridSquareDirectory))
                    {
                        var stitchedTilesDirectory = GetTileDownloadDirectory(afsGridSquareDirectory) + this.settings.ZoomLevel + @"-stitched\";

                        if (Directory.Exists(stitchedTilesDirectory))
                        {
                            // Create the ttc directory if required. It could have been deleted manually.
                            var ttcDirectory = GetTileDownloadDirectory(afsGridSquareDirectory) + this.settings.ZoomLevel + @"-geoconvert-ttc\";

                            if (!Directory.Exists(ttcDirectory))
                            {
                                Directory.CreateDirectory(ttcDirectory);
                            }

                            await this.ttcConverterManager.ConvertAllAsync(stitchedTilesDirectory, ttcDirectory, this.mainForm,
                                this.CoastFor(afs2GridSquare, coast), lakes);
                        }
                        else
                        {
                            var messageBox = new CustomMessageBox(String.Format("Could not find any stitched images for the grid square {0}", afs2GridSquare.Name),
                                "AeroScenery",
                                MessageBoxIcon.Error);

                            messageBox.ShowDialog();
                        }

                    }

                    i++;
                }
            }
        }

        /// <summary>
        /// Installs the generated ttc files of every selected grid square into the Aerofly scenery
        /// folder. Choosing the action is the confirmation, so the user is not prompted per square -
        /// a run left unattended has to be able to finish on its own.
        /// </summary>
        private async Task InstallSceneryAsync()
        {
            this.mainForm.UpdateChildTaskLabel("Installing Scenery");
            log.Info("Installing Scenery");

            int i = 0;

            foreach (AFS2GridSquare afs2GridSquare in this.mainForm.SelectedAFS2GridSquares.Values.Select(x => x.AFS2GridSquare))
            {
                if (this.mainForm.ActionsRunning)
                {
                    var currentGrideSquareMessage = String.Format("Working on AFS4 Grid Square {0} of {1}", i + 1, this.mainForm.SelectedAFS2GridSquares.Count());
                    this.mainForm.UpdateParentTaskLabel(currentGrideSquareMessage);
                    log.Info(currentGrideSquareMessage);

                    await this.mainForm.InstallSceneryForGridSquareAsync(afs2GridSquare, false);

                    i++;
                }
            }
        }



        private void DownloadThreadProgress_ProgressChanged(object sender, DownloadThreadProgress progress)
        {
            if (this.mainForm.ActionsRunning)
            {
                var progressControl = this.mainForm.GetDownloadThreadProgressControl(progress.DownloadThreadNumber);
                var percentageProgress = (int)Math.Floor(((double)progress.FilesDownloaded / (double)progress.TotalFiles) * 100);

                // There are only eight progress controls on the form, so any SimultaneousDownloads
                // above eight reports from threads that have none and this returns null. The
                // overall percentage below still counts them; the per-thread bars show the first
                // eight as a sample.
                if (progressControl != null)
                {
                    progressControl.SetProgressPercentage(percentageProgress);

                    progressControl.SetImageTileCount(progress.FilesDownloaded, progress.TotalFiles);
                }

                var downloadActionProgressPercentage = this.mainForm.CurrentActionProgressPercentage;

                if (percentageProgress > downloadActionProgressPercentage)
                {
                    this.mainForm.CurrentActionProgressPercentage = percentageProgress;
                }
            }

        }


        private void TileStitcherProgress_ProgressChanged(object sender, TileStitcherProgress progress)
        {
            if (this.mainForm.ActionsRunning)
            {

                var currentStitchedImagePercentage = ((double)progress.CurrentStitchedImage / (double)progress.TotalStitchedImages);
                var nextStitchedImagePercentage = ((double)(progress.CurrentStitchedImage + 1) / (double)progress.TotalStitchedImages);

                var tilesPercentage = ((double)(progress.CurrentTilesRenderedForCurrentStitchedImage) / (double)progress.TotalImageTilesForCurrentStitchedImage);

                var percentageIncreaseBetweenThisStitchedImageAndNext = nextStitchedImagePercentage - currentStitchedImagePercentage;

                var finalPercentageDbl = (currentStitchedImagePercentage + (percentageIncreaseBetweenThisStitchedImageAndNext * tilesPercentage)) * 100;
                //Debug.WriteLine(finalPercentageDbl);

                var finalPercentage = (int)Math.Floor(finalPercentageDbl);

                if (finalPercentage > 100)
                {
                    finalPercentage = 100;
                }

                this.mainForm.CurrentActionProgressPercentage = finalPercentage;

            }

        }

        private void AFSFileGeneratorProgress_ProgressChanged(object sender, AFSFileGeneratorProgress progress)
        {
            if (this.mainForm.ActionsRunning)
            {
                var precentDone = ((double)progress.FilesCreated / (double)progress.TotalFiles) * 100;

                this.mainForm.CurrentActionProgressPercentage = (int)precentDone;

            }
        }

        public bool AllImageTilesDownloaded(List<ImageTile> imageTiles)
        {
            return true;
        }

        public void SaveSettings()
        {
            this.settingsService.SaveSettings(this.settings);
        }

        public string ApplicationPath
        {
            get
            {
                var applicationUri = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                var applicationLocalPath = new Uri(Path.GetDirectoryName(applicationUri)).LocalPath;
                return applicationLocalPath;

            }
        }


    }
}
