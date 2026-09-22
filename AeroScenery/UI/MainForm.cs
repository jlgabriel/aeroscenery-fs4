using AeroScenery.AFS2;
using AeroScenery.Common;
using AeroScenery.Controls;
using AeroScenery.Data;
using AeroScenery.FileManagement;
using AeroScenery.OrthophotoSources;
using AeroScenery.OrthoPhotoSources;
using AeroScenery.Resources;
using AeroScenery.UI;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
//#MOD_j
using System.Net.Sockets;
using GMap.NET.WindowsForms.Markers;
using System.Net.NetworkInformation;
//using SmartFormat.Core.Output;
//using AForge.Imaging.Filters;
//using System.Drawing.Imaging;

namespace AeroScenery
{
    public partial class MainForm : Form
    {
        public event EventHandler StartStopClicked;
        public Dictionary<string, GridSquareViewModel> SelectedAFS2GridSquares;
        public Dictionary<string, GridSquareViewModel> DownloadedAFS2GridSquares;
        public AFS2GridSquare SelectedAFS2GridSquare;

        //private bool mouseDownOnMap;
        private AFS2Grid afs2Grid;
        private List<DownloadThreadProgressControl> downloadThreadProgressControls;
        private AeroScenery.Common.Point mapMouseDownLocation;
        private DownloadedGridSquareFinder downloadedGridSquareFinder;
        private GMapOverlay activeGridSquareOverlay;
        private bool actionsRunning;
        private readonly ILog log = LogManager.GetLogger("AeroScenery");
        private GMapControlManager gMapControlManager;

        //#MOD_k
        private CoastlineEditor coastlineEditor;

        // True while the land side dropdown is being set to match the line, so that showing the
        // value is not taken for the user choosing it.
        private bool showingCoastlineLand;
        private bool coastlineSaveWarned;

        private SceneryInstaller sceneryInstaller;
        private FileManager fileManager;

        // Whether we have finished initially updating the UI with settings
        // We can therefore ignore control events until this is true
        private bool uiSetFromSettings;

        private int afsGridSquareSelectionSize;

        // Whether the user should be shown a dialog about how changing the selection size
        // removes any current selections.
        private bool shownSelectionSizeChangeInfo;

        private List<AFSLevel> afsLevels;

        private bool processCheckBoxListEvents;

        private List<ImageComboItem> orthophotoSourceItems;
        private ImageList orthophotoSourceImages;

        // A run can take hours with nothing on screen changing, so we time the run as a whole and the
        // step being worked on now. Without this there is no way to tell a slow step from a stuck one
        private readonly Stopwatch runStopwatch = new Stopwatch();
        private readonly Stopwatch stepStopwatch = new Stopwatch();
        private System.Windows.Forms.Timer elapsedTimer;
        private string currentStepName;

        // True from Stop until the stopped run has really ended
        private bool stopping;

        public MainForm()
        {
            InitializeComponent();

            // This build targets Aerofly FS 4, so the progress heading should read AFS4, not AFS2.
            this.parentTaskLabel.Text = "Working On AFS4 Grid Square - of -";

            this.elapsedTimer = new System.Windows.Forms.Timer(this.components);
            this.elapsedTimer.Interval = 1000;
            this.elapsedTimer.Tick += ElapsedTimer_Tick;

            this.afs2Grid = new AFS2Grid();
            this.downloadedGridSquareFinder = new DownloadedGridSquareFinder();
            this.gMapControlManager = new GMapControlManager();
            this.sceneryInstaller = new SceneryInstaller();
            this.fileManager = new FileManager();

            this.actionsRunning = false;

            mainMap.MinZoom = 2;
            mainMap.MaxZoom = 23;
            mainMap.DragButton = MouseButtons.Left;
            mainMap.IgnoreMarkerOnMouseWheel = true;

            //#MOD_k
            // After the map's own handlers, so those get first refusal and can bow out while a
            // stroke is in progress rather than selecting a grid square under the pen.
            this.coastlineEditor = new CoastlineEditor(mainMap);
            this.coastlineEditor.Changed += CoastlineEditor_Changed;
            this.coastlineMarginToolStripTextBox.Text =
                (this.coastlineEditor.MarginKm / 1.852).ToString("0.#");
            this.ShowCoastlineLand();

            SelectedAFS2GridSquares = new Dictionary<string, GridSquareViewModel>();
            DownloadedAFS2GridSquares = new Dictionary<string, GridSquareViewModel>();

            this.downloadThreadProgressControls = new List<DownloadThreadProgressControl>();
            this.uiSetFromSettings = false;

            this.afsGridSquareSelectionSize = 9;
            this.gridSquareSelectionSizeToolstripCombo.SelectedIndex = 0;

            // TODO - Make this dynamic
            this.downloadThreadProgressControls.Add(this.downloadThreadProgress1);
            this.downloadThreadProgressControls.Add(this.downloadThreadProgress2);
            this.downloadThreadProgressControls.Add(this.downloadThreadProgress3);
            this.downloadThreadProgressControls.Add(this.downloadThreadProgress4);
            //#MOD_g
            this.downloadThreadProgressControls.Add(this.downloadThreadProgress5);
            this.downloadThreadProgressControls.Add(this.downloadThreadProgress6);
            this.downloadThreadProgressControls.Add(this.downloadThreadProgress7);
            this.downloadThreadProgressControls.Add(this.downloadThreadProgress8);

            this.downloadThreadProgress1.SetDownloadThreadNumber(1);
            this.downloadThreadProgress2.SetDownloadThreadNumber(2);
            this.downloadThreadProgress3.SetDownloadThreadNumber(3);
            this.downloadThreadProgress4.SetDownloadThreadNumber(4);
            //#MOD_g
            this.downloadThreadProgress5.SetDownloadThreadNumber(5);
            this.downloadThreadProgress6.SetDownloadThreadNumber(6);
            this.downloadThreadProgress7.SetDownloadThreadNumber(7);
            this.downloadThreadProgress8.SetDownloadThreadNumber(8);

            this.gridSquareLabel.Text = "";
            //#MOD_f
            this.gridSquareBoundaryBox.Text = "";

            this.gMapControlManager.GMapControl = this.mainMap;

            this.shownSelectionSizeChangeInfo = true;

        }

        public void Initialize()
        {
            ToolTip toolTip1 = new ToolTip();
            toolTip1.IsBalloon = true;
            toolTip1.InitialDelay = 500;
            //#MOD_i
            toolTip1.SetToolTip(this.generateAFS2LevelsHelpImage, "Select first the desired image resulution using the 'Image Detail (Zoom Level)' slider and then press [Choose for me].\nAeroScenery automatically selects the needed levels to be converted for your Aerofly scenery.\nRecommended to use is level 16 with 2.389m resolution covering the whole 'Size 9' area (use higher resolutions for smaller areas).");

            //#MOD_i
            ToolTip toolTip2 = new ToolTip();
            toolTip2.IsBalloon = true;
            toolTip2.InitialDelay = 500;
            toolTip2.SetToolTip(this.chooseActionsToRunHelpImage, "Select 'Run Default actions' to automatically execute all the required steps sequentially.\nWhen the conversion is complete, each selected tile can be installed using 'Install Scenery' to the path set under 'Settings'.\nBy selecting 'Choose actions to run' the steps can be executed separately resp. be done again, e.g. after editing of the stiched images.");

            //#MOD_j
            ToolTip toolTip3 = new ToolTip();
            toolTip2.IsBalloon = true;
            toolTip2.InitialDelay = 500;
            // Initialize the AFS Levels CheckBoxLists
            afsLevels = new List<AFSLevel>();
            afsLevels.Add(new AFSLevel("Level 9", 9));
            afsLevels.Add(new AFSLevel("Level 10", 10));
            afsLevels.Add(new AFSLevel("Level 11", 11));
            afsLevels.Add(new AFSLevel("Level 12", 12));
            afsLevels.Add(new AFSLevel("Level 13", 13));
            afsLevels.Add(new AFSLevel("Level 14", 14));
            afsLevels.Add(new AFSLevel("Level 15", 15));

            //#MOD_i
            afsLevels.Add(new AFSLevel("Level 7", 7));
            afsLevels.Add(new AFSLevel("Level 8", 8));

            this.afsLevelsCheckBoxList.DataSource = afsLevels;
            this.afsLevelsCheckBoxList.DisplayMember = "Name";
            this.afsLevelsCheckBoxList.ValueMember = "Level";
            this.afsLevelsCheckBoxList.ClearSelected();

            imageSourceComboBox.DisplayMember = "Text";
            imageSourceComboBox.ValueMember = "Value";

            this.orthophotoSourceImages = new ImageList();
            this.orthophotoSourceImages.TransparentColor = System.Drawing.Color.Transparent;
            this.orthophotoSourceImages.Images.Add(AeroSceneryImages.world_icon); //0
            this.orthophotoSourceImages.Images.Add(AeroSceneryImages.ch_flag); //1
            this.orthophotoSourceImages.Images.Add(AeroSceneryImages.es_flag); //2
            this.orthophotoSourceImages.Images.Add(AeroSceneryImages.jp_flag); //3
            this.orthophotoSourceImages.Images.Add(AeroSceneryImages.no_flag); //4
            this.orthophotoSourceImages.Images.Add(AeroSceneryImages.nz_flag); //5
            this.orthophotoSourceImages.Images.Add(AeroSceneryImages.se_flag); //6
            this.orthophotoSourceImages.Images.Add(AeroSceneryImages.us_flag); //7
            //#MOD_b
            this.orthophotoSourceImages.Images.Add(AeroSceneryImages.world_map); //8

            orthophotoSourceItems = new List<ImageComboItem>() {
                new ImageComboItem() { Text = "Bing", Value = OrthophotoSource.Bing, ImageIndex = 0 },
                new ImageComboItem() { Text = "Google", Value = OrthophotoSource.Google, ImageIndex = 0  },
                new ImageComboItem() { Text = "ArcGIS", Value = OrthophotoSource.ArcGIS, ImageIndex = 0  },
                new ImageComboItem() { Text = "Here WeGo", Value = OrthophotoSource.HereWeGo, ImageIndex = 0  },
                //#MOD_e
                new ImageComboItem() { Text = "Mapbox", Value = OrthophotoSource.Mapbox, ImageIndex = 0  },

                new ImageComboItem() { Text = "Geoportal (Switzerland)", Value = OrthophotoSource.CH_Geoportal, ImageIndex = 1  },
                new ImageComboItem() { Text = "GSI (Japan)", Value = OrthophotoSource.JP_GSI, ImageIndex = 3  },
                new ImageComboItem() { Text = "Gule Sider (Norway)", Value = OrthophotoSource.NO_GuleSider, ImageIndex = 4  },
                new ImageComboItem() { Text = "Hitta (Sweden)", Value = OrthophotoSource.SE_Hitta, ImageIndex = 6  },
                new ImageComboItem() { Text = "IDEIB (Balearics)", Value = OrthophotoSource.ES_IDEIB, ImageIndex = 2  },
                new ImageComboItem() { Text = "IGN (Spain)", Value = OrthophotoSource.ES_IGN, ImageIndex = 2  },
                new ImageComboItem() { Text = "Lantmateriet (Sweden)", Value = OrthophotoSource.SE_Lantmateriet, ImageIndex = 6  },
                new ImageComboItem() { Text = "Linz (New Zealand)", Value = OrthophotoSource.NZ_Linz, ImageIndex = 5  },
                new ImageComboItem() { Text = "Norge i Bilder (Norway)", Value = OrthophotoSource.NO_NorgeBilder, ImageIndex = 4  },
                new ImageComboItem() { Text = "USGS (US)", Value = OrthophotoSource.US_USGS, ImageIndex = 7  },

                //#MOD_b
                //Currently no use of the additional maps 

                //MOD_h - No more need in the selection due to direct download vie "Action to Run" checkbox
            };

            imageSourceComboBox.ImageList = this.orthophotoSourceImages;
            imageSourceComboBox.DataSource = orthophotoSourceItems;

            var settings = AeroSceneryManager.Instance.Settings;

            //#MOD_h
            // Hide the boxes resp. options for running Download Elevation if no API-Key is set in the Settings

            //#MOD_i
            // Hide the boxes resp. options for enabling Download OSM Data if Option is Set under Settings

            this.UpdateUIFromSettings();

            this.LoadDownloadedGridSquares();

            this.processCheckBoxListEvents = true;

        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            TextBoxAppender.ConfigureTextBoxAppender(this.logTextBox);

            log.Info(String.Format("AeroScenery v{0} Started", AeroSceneryManager.Instance.Version));
        }



        public void UpdateUIFromSettings()
        {
            log.Info("Updating UI from settings");
            var settings = AeroSceneryManager.Instance.Settings;

            // Orthophoto Source
            if (settings.OrthophotoSource == OrthophotoSource.USGS)
            {
                settings.OrthophotoSource = OrthophotoSource.US_USGS;
            }

            this.imageSourceComboBox.SelectedValue = settings.OrthophotoSource;

            // Zoom Level
            this.zoomLevelTrackBar.Value = settings.ZoomLevel.Value;
            this.setZoomLevelLabelText();


            // AFS Levels To Generate
            for (int i = 0; i < afsLevelsCheckBoxList.Items.Count; i++)
            {
                AFSLevel level = (AFSLevel)afsLevelsCheckBoxList.Items[i];

                //#MOD_i
                //if (settings.AFSLevelsToGenerate.Contains(level.Level))
                if ((settings.AFSLevelsToGenerate.Contains(level.Level)) && level.Level >= 9)
                {
                    level.IsChecked = true;
                    afsLevelsCheckBoxList.SetItemChecked(i, level.IsChecked);
                }

            }

            // Action set
            switch (settings.ActionSet)
            {
                case Common.ActionSet.Custom:
                    this.actionSetComboBox.SelectedIndex = 1;
                    this.SetCustomActions();
                    break;
                case Common.ActionSet.Default:
                    this.actionSetComboBox.SelectedIndex = 0;
                    this.SetDefaultActions();
                    break;
            }

            // Map stuff
            mainMap.MapProvider = GMapProviderHelper.GetGMapProvider(settings.MapControlLastMapType);
            //#MOD_l
            this.ShowActiveMapType();
            if (settings.MapControlLastZoomLevel.HasValue && settings.MapControlLastZoomLevel > 1)
            {
                mainMap.Zoom = settings.MapControlLastZoomLevel.Value;
            }
            else
            {
                mainMap.Zoom = 5;
            }

            if (settings.MapControlLastX.HasValue && settings.MapControlLastY.HasValue)
            {
                mainMap.Position = new PointLatLng(settings.MapControlLastX.Value, settings.MapControlLastY.Value);
            }


            //#MOD_g
            // Hide not used downloaders/ threads
            if (settings.SimultaneousDownloads < 8)
            {
                this.downloadThreadProgress8.Visible = false;
                this.downloadThreadProgress7.Visible = false;
            }
            if (settings.SimultaneousDownloads < 6)
            {
                this.downloadThreadProgress6.Visible = false;
                this.downloadThreadProgress5.Visible = false;
            }
            if (settings.SimultaneousDownloads < 4)
            {
                this.downloadThreadProgress4.Visible = false;
                this.downloadThreadProgress3.Visible = false;
            }

            if (settings.SimultaneousDownloads < 2)
                this.downloadThreadProgress2.Visible = false;

            this.uiSetFromSettings = true;

        }

        private void SetDefaultActions()
        {
            this.downloadImageTileCheckBox.Checked = true;
            this.stitchImageTilesCheckBox.Checked = true;
            this.generateAFSFilesCheckBox.Checked = true;
            this.runConverterCheckBox.Checked = true;
            this.installSceneryIntoAFSCheckBox.Checked = true;
            //#MOD_h
            //#MOD_g

            this.downloadImageTileCheckBox.Enabled = false;
            this.stitchImageTilesCheckBox.Enabled = false;
            this.generateAFSFilesCheckBox.Enabled = false;
            this.runConverterCheckBox.Enabled = false;
            this.installSceneryIntoAFSCheckBox.Enabled = false;
            //#MOD_h
            //#MOD_g
            

        }

        private void SetCustomActions()
        {
            var settings = AeroSceneryManager.Instance.Settings;
            // Actions
            this.downloadImageTileCheckBox.Checked = settings.DownloadImageTiles.Value;
            this.stitchImageTilesCheckBox.Checked = settings.StitchImageTiles.Value;
            this.generateAFSFilesCheckBox.Checked = settings.GenerateAIDAndTMCFiles.Value;
            this.runConverterCheckBox.Checked = settings.RunConverter.Value;
            this.deleteStitchedImagesCheckBox.Checked = settings.DeleteStitchedImageTiles.Value;
            this.installSceneryIntoAFSCheckBox.Checked = settings.InstallScenery.Value;

            //#MOD_h
            //#MOD_g

            this.downloadImageTileCheckBox.Enabled = true;
            this.stitchImageTilesCheckBox.Enabled = true;
            this.generateAFSFilesCheckBox.Enabled = true;
            this.runConverterCheckBox.Enabled = true;
            this.installSceneryIntoAFSCheckBox.Enabled = true;

            //#MOD_h
            //#MOD_g

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            mainMap.Manager.CancelTileCaching();
            mainMap.Dispose();

            AeroSceneryManager.Instance.SaveSettings();
        }

        private void ButtonStart_Click(object sender, EventArgs e)
        {
            // A stopped run is still ending. A new run must not start over it: the two would share
            // the download and the working folders.
            if (this.stopping)
            {
                return;
            }

            // Are we currently running actions
            if (this.ActionsRunning)
            {
                // Stop. The run ends in its own time, and ActionsComplete then unlocks the UI and
                // gives the button back.
                this.mainTabControl.SelectedIndex = 0;
                this.ActionsRunning = false;
                this.stopping = true;
                this.startStopButton.Text = "Stopping";
                this.startStopButton.Enabled = false;
            }
            else
            {
                if (SceneryGenerationProcessCanStart())
                {
                    // Start
                    this.mainTabControl.SelectedIndex = 1;
                    this.ActionsRunning = true;
                    this.LockUI();
                    this.StartElapsedClock();
                }

            }

            StartStopClicked(this, e);
        }

        private void LockUI()
        {
            this.imageSourceComboBox.Enabled = false;
            this.zoomLevelTrackBar.Enabled = false;
            this.autoSelectAFSLevelsButton.Enabled = false;
            this.afsLevelsCheckBoxList.Enabled = false;
            this.actionSetComboBox.Enabled = false;
            this.shutdownCheckbox.Enabled = false;

            this.downloadImageTileCheckBox.Enabled = false;
            this.stitchImageTilesCheckBox.Enabled = false;
            this.generateAFSFilesCheckBox.Enabled = false;
            this.runConverterCheckBox.Enabled = false;
            //#MOD_h
            //#MOD_g
            this.deleteStitchedImagesCheckBox.Enabled = false;
            this.installSceneryIntoAFSCheckBox.Enabled = false;
        }

        private void UnlockUI()
        {
            this.imageSourceComboBox.Enabled = true;
            this.zoomLevelTrackBar.Enabled = true;
            this.autoSelectAFSLevelsButton.Enabled = true;
            this.afsLevelsCheckBoxList.Enabled = true;
            this.actionSetComboBox.Enabled = true;
            //this.shutdownCheckbox.Enabled = true;

            // Only re-enable these if run custom actions is selected
            if(AeroSceneryManager.Instance.Settings.ActionSet == ActionSet.Custom)
            {
                this.downloadImageTileCheckBox.Enabled = true;
                this.stitchImageTilesCheckBox.Enabled = true;
                this.generateAFSFilesCheckBox.Enabled = true;
                this.runConverterCheckBox.Enabled = true;
                //#MOD_h
                //#MOD_g
                this.deleteStitchedImagesCheckBox.Enabled = true;
                this.installSceneryIntoAFSCheckBox.Enabled = true;
            }
        }

        private void ResetProgress()
        {
            this.downloadThreadProgress1.Reset();
            this.downloadThreadProgress2.Reset();
            this.downloadThreadProgress3.Reset();
            this.downloadThreadProgress4.Reset();
            //#MOD_g
            this.downloadThreadProgress5.Reset();
            this.downloadThreadProgress6.Reset();
            this.downloadThreadProgress7.Reset();
            this.downloadThreadProgress8.Reset();

            this.currentActionProgressBar.Value = 0;
        }

        public DownloadThreadProgressControl GetDownloadThreadProgressControl(int downloadThread)
        {
            if (downloadThread < this.downloadThreadProgressControls.Count)
            {
                return this.downloadThreadProgressControls[downloadThread];
            }

            return null;
        }

        private bool SceneryGenerationProcessCanStart()
        {
            switch (AeroSceneryManager.Instance.Settings.OrthophotoSource)
            {
                case OrthophotoSource.US_USGS:

                    if (AeroSceneryManager.Instance.Settings.ZoomLevel.HasValue && AeroSceneryManager.Instance.Settings.ZoomLevel > 16)
                    {
                        var messageBox = new CustomMessageBox("USGS only provides image tile services up to zoom level 16.\nHigher resolution images are available by manual download.\n" +
                            "A way to automate the processing of these manual downloads is being researched for AeroScenery.",
                            "AeroScenery",
                            MessageBoxIcon.Information);

                        messageBox.ShowDialog();
                        return false;
                    }

                    break;
                case OrthophotoSource.NZ_Linz:

                    if (String.IsNullOrEmpty(AeroSceneryManager.Instance.Settings.LinzApiKey))
                    {
                        var messageBox = new CustomMessageBox("A Linz API key must be set before using the Linz image source.\nThis can be set in Settings > Image Source Accounts",
                            "AeroScenery",
                            MessageBoxIcon.Information);

                        messageBox.ShowDialog();
                        return false;
                    }

                    break;
                //MOD_e
                case OrthophotoSource.Mapbox:

                    if (String.IsNullOrEmpty(AeroSceneryManager.Instance.Settings.MapboxApiKey))
                    {
                        var messageBox = new CustomMessageBox("Mapbox Access token must be set before using the Mapbox image source.\nThis can be set in Settings > Image Source Accounts",
                            "AeroScenery",
                            MessageBoxIcon.Information);

                        messageBox.ShowDialog();
                        return false;
                    }

                    break;

            }

            //#MOD_k
            // The selection survives a finished run, so squares from a previous run carry over
            // silently and get rebuilt. Show exactly what is about to be processed, and flag the
            // ones whose tiles are already on disk - that is what a carried-over square looks like.
            const int maxGridSquaresListed = 10;

            var selectedNames = this.SelectedAFS2GridSquares.Keys.OrderBy(name => name).ToList();

            var confirmation = new StringBuilder();
            confirmation.AppendLine(selectedNames.Count == 1
                ? "About to process 1 grid square:"
                : String.Format("About to process {0} grid squares:", selectedNames.Count));
            confirmation.AppendLine();

            foreach (var name in selectedNames.Take(maxGridSquaresListed))
            {
                confirmation.AppendLine(this.DownloadedAFS2GridSquares.ContainsKey(name)
                    ? "     " + name + "     (already downloaded)"
                    : "     " + name);
            }

            if (selectedNames.Count > maxGridSquaresListed)
            {
                confirmation.AppendLine(String.Format("     ... and {0} more", selectedNames.Count - maxGridSquaresListed));
            }

            confirmation.AppendLine();
            confirmation.Append("Continue?");

            var confirmationBox = new CustomMessageBox(confirmation.ToString(), "AeroScenery", MessageBoxIcon.Question);
            confirmationBox.SetButtons(new string[] { "Start", "Cancel" },
                new DialogResult[] { DialogResult.OK, DialogResult.Cancel }, 1);

            if (confirmationBox.ShowDialog() != DialogResult.OK)
            {
                return false;
            }

            return true;
        }

        private void SelectAFSGridSquare(int x, int y)
        {
            double lat = mainMap.FromLocalToLatLng(x, y).Lat;
            double lon = mainMap.FromLocalToLatLng(x, y).Lng;

            // Get the grid square for this lat and lon
            var gridSquare = afs2Grid.GetGridSquareAtLatLon(lat, lon, this.afsGridSquareSelectionSize);

            gridSquareLabel.Text = gridSquare.Name;

            //#MOD_f
            // Create a boundary box using "NWlng, NWlat, SElng, SElat" for use in AFS2 Editor from Nabeelamjad 
            gridSquareBoundaryBox.Text = gridSquare.WestLongitude.ToString("#.#######", CultureInfo.InvariantCulture) + "," + gridSquare.NorthLatitude.ToString("#.#######", CultureInfo.InvariantCulture) + ",";
            gridSquareBoundaryBox.Text = gridSquareBoundaryBox.Text + gridSquare.EastLongitude.ToString("#.#######", CultureInfo.InvariantCulture) + "," + gridSquare.SouthLatitude.ToString("#.#######", CultureInfo.InvariantCulture);

            // Set the map overlay of any previously selected grid square to visisble
            if (this.SelectedAFS2GridSquare != null)
            {
                if (this.SelectedAFS2GridSquares.ContainsKey(this.SelectedAFS2GridSquare.Name))
                {
                    var previouslySelectedGridSquare = this.SelectedAFS2GridSquares[this.SelectedAFS2GridSquare.Name];
                    previouslySelectedGridSquare.GMapOverlay.IsVisibile = true;
                }

            }

            // Clear the previous active overlay
            if (this.activeGridSquareOverlay != null)
            {
                this.activeGridSquareOverlay.Clear();
                this.activeGridSquareOverlay.Dispose();
                this.activeGridSquareOverlay = null;
            }


            // Is this a grid square that is already selected
            if (!this.SelectedAFS2GridSquares.ContainsKey(gridSquare.Name))
            {
                // Add the selected map overlay but make it invislbe for now
                var selectedGridSquare = this.gMapControlManager.DrawGridSquare(gridSquare, GridSquareDisplayType.Selected);
                selectedGridSquare.IsVisibile = false;

                // Add the AFS2 Grid Squrea and the GMapOverlay to the selected grid squares dictionary
                var gridSquareViewModel = new GridSquareViewModel();
                gridSquareViewModel.GMapOverlay = selectedGridSquare;
                gridSquareViewModel.AFS2GridSquare = gridSquare;

                this.SelectedAFS2GridSquares.Add(gridSquare.Name, gridSquareViewModel);

                // Create the active grid square map overlay, let it be visible
                this.activeGridSquareOverlay = this.gMapControlManager.DrawGridSquare(gridSquare, GridSquareDisplayType.Active);
            }
            else
            {
                // Create the active grid square map overlay, let it be visible
                this.activeGridSquareOverlay = this.gMapControlManager.DrawGridSquare(gridSquare, GridSquareDisplayType.Active);
            }

            this.SelectedAFS2GridSquare = gridSquare;
            this.UpdateStatusStrip();
            this.UpdateToolStrip();

            log.InfoFormat("Grid square {0} selected", gridSquare.Name);
        }

        private void DeselectAFSGridSquare(int x, int y)
        {
            double lat = mainMap.FromLocalToLatLng(x, y).Lat;
            double lon = mainMap.FromLocalToLatLng(x, y).Lng;

            // Get the grid square for this lat and lon
            var gridSquare = afs2Grid.GetGridSquareAtLatLon(lat, lon, this.afsGridSquareSelectionSize);

            if (gridSquare != null)
            {
                // If this grid square is already selected, deselect it
                if (this.SelectedAFS2GridSquares.ContainsKey(gridSquare.Name))
                {
                    var squareAndOverlay = this.SelectedAFS2GridSquares[gridSquare.Name];

                    mainMap.Overlays.Remove(squareAndOverlay.GMapOverlay);
                    this.SelectedAFS2GridSquares.Remove(gridSquare.Name);
                    this.SelectedAFS2GridSquare = null;
                }

                this.SelectedAFS2GridSquare = null;
                gridSquareLabel.Text = "";
                //MOD_f
                gridSquareBoundaryBox.Text = "";

                this.activeGridSquareOverlay.Clear();
                this.activeGridSquareOverlay.Dispose();
                this.activeGridSquareOverlay = null;

                this.UpdateStatusStrip();
                this.UpdateToolStrip();
            }

        }

        /// <summary>
        /// Clears any currently selected AFSGridSquares
        /// </summary>
        private void ClearAllSelectedAFSGridSquares()
        {
            foreach (var gridSquare in this.SelectedAFS2GridSquares.Values)
            {
                mainMap.Overlays.Remove(gridSquare.GMapOverlay);
            }


            if (this.activeGridSquareOverlay != null)
            {
                this.activeGridSquareOverlay.Clear();
                this.activeGridSquareOverlay.Dispose();
                this.activeGridSquareOverlay = null;
            }

            mainMap.Refresh();

            this.SelectedAFS2GridSquares.Clear();
            this.SelectedAFS2GridSquare = null;

            this.UpdateStatusStrip();
        }



        private void UpdateStatusStrip()
        {
            if (this.SelectedAFS2GridSquares.Count == 1)
            {
                this.statusStripLabel1.Text = String.Format("1 Grid Square Selected");
            }
            else
            {
                this.statusStripLabel1.Text = String.Format("{0} Grid Squares Selected", this.SelectedAFS2GridSquares.Count);
            }

            if (this.SelectedAFS2GridSquares.Count > 0 && !this.stopping)
            {
                this.startStopButton.Enabled = true;
            }
            else
            {
                this.startStopButton.Enabled = false;
            }

        }

        private void UpdateToolStrip()
        {
            if (this.SelectedAFS2GridSquare != null)
            {
                if (!this.DownloadedAFS2GridSquares.ContainsKey(this.SelectedAFS2GridSquare.Name))
                {
                    this.toolStripDownloadedLabel.Text = "Not Downloaded";
                    toolStripDownloadedLabel.Image = imageList1.Images[0];
                }
                else
                {
                    this.toolStripDownloadedLabel.Text = "Downloaded";
                    toolStripDownloadedLabel.Image = imageList1.Images[1];
                }
            }

            if (this.SelectedAFS2GridSquare != null)
            {
                this.openImageFolderToolstripButton.Enabled = true;
                this.deleteImagesToolStripButton.Enabled = true;
                this.openMapToolStripDropDownButton.Enabled = true;
                this.installSceneryToolStripButton.Enabled = true;
                //#MOD_f
                this.copyToClipboardToolStripButton.Enabled = true;
            }
            else
            {
                this.openImageFolderToolstripButton.Enabled = false;
                this.deleteImagesToolStripButton.Enabled = false;
                this.openMapToolStripDropDownButton.Enabled = false;
                this.installSceneryToolStripButton.Enabled = false;
                //#MOD_f
                this.copyToClipboardToolStripButton.Enabled = false;
            }

        }

        public void LoadDownloadedGridSquares()
        {
            var workingDirectory = AeroSceneryManager.Instance.Settings.WorkingDirectory;

            foreach (AFS2GridSquare afs2GridSqure in this.downloadedGridSquareFinder.FindAll(workingDirectory))
            {
                this.AddDownloadedGridSquare(afs2GridSqure);
            }
        }

        public void AddDownloadedGridSquare(AFS2GridSquare afs2GridSqure)
        {
            // Drawing a square that is already on the map would leak the overlay underneath it
            if (this.DownloadedAFS2GridSquares.ContainsKey(afs2GridSqure.Name))
            {
                return;
            }

            var polygonOverlay = this.gMapControlManager.DrawGridSquare(afs2GridSqure, GridSquareDisplayType.Downloaded);

            var gridSquareViewModel = new GridSquareViewModel();
            gridSquareViewModel.GMapOverlay = polygonOverlay;
            gridSquareViewModel.AFS2GridSquare = afs2GridSqure;

            this.DownloadedAFS2GridSquares[afs2GridSqure.Name] = gridSquareViewModel;

        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            var settingsForm = new SettingsForm();
            settingsForm.Show();
            if (settingsForm.StartPosition == FormStartPosition.CenterParent)
            {
                var x = Location.X + (Width - settingsForm.Width) / 2;
                var y = Location.Y + (Height - settingsForm.Height) / 2;
                settingsForm.Location = new System.Drawing.Point(Math.Max(x, 0), Math.Max(y, 0));
            }

        }

        //#MOD_k
        #region Coastline

        /// <summary>
        /// One line for the whole package. See AeroSceneryManager.CoastlinePath.
        /// </summary>
        private string CoastlinePath()
        {
            return AeroSceneryManager.Instance.CoastlinePath;
        }

        private void coastlineDrawToolStripButton_Click(object sender, EventArgs e)
        {
            this.coastlineEditor.Active = this.coastlineDrawToolStripButton.Checked;
            this.mainMap.Focus();
            CoastlineEditor_Changed(this, EventArgs.Empty);
        }

        private void coastlineUndoToolStripButton_Click(object sender, EventArgs e)
        {
            this.coastlineEditor.Undo();
            this.mainMap.Focus();
        }

        private void coastlineSaveToolStripButton_Click(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AeroSceneryManager.Instance.Settings.WorkingDirectory);
                this.coastlineEditor.Save(CoastlinePath());
                this.statusStripLabel1.Text = String.Format("Coastline saved to {0}", CoastlinePath());
            }
            catch (Exception ex)
            {
                log.Error("Could not save the coastline", ex);
                MessageBox.Show(this, ex.Message, "Could not save the coastline",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void coastlineMarginToolStripTextBox_Leave(object sender, EventArgs e)
        {
            ApplyCoastlineMargin();
        }

        private void coastlineMarginToolStripTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ApplyCoastlineMargin();
                e.SuppressKeyPress = true;
                this.mainMap.Focus();
            }
        }

        /// <summary>
        /// Which side of the line is land. The dropdown lists the sides in the order of the
        /// LandSide enum, so the index is the value.
        ///
        /// Chile's coast faces west, so the land is east, and that is the default. A coast that
        /// faces east needs west, and one that runs east-west needs north or south. The side is one
        /// setting for the whole line: the cut and the field are built on it, so changing it moves
        /// the cut to the other side at once.
        /// </summary>
        private void coastlineLandToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.showingCoastlineLand || this.coastlineLandToolStripComboBox.SelectedIndex < 0)
            {
                return;
            }

            var land = (AeroScenery.AFS2.LandSide)this.coastlineLandToolStripComboBox.SelectedIndex;

            if (land != this.coastlineEditor.Land)
            {
                this.coastlineEditor.Land = land;
                log.Info(String.Format("Coastline land side set to {0}", land));
            }
        }

        private void ShowCoastlineLand()
        {
            this.showingCoastlineLand = true;

            try
            {
                this.coastlineLandToolStripComboBox.SelectedIndex = (int)this.coastlineEditor.Land;
            }
            finally
            {
                this.showingCoastlineLand = false;
            }
        }

        /// <summary>
        /// The margin is typed in nautical miles because that is the unit the decision was made
        /// in - 3 NM is what IPACS themselves leave. It is held in km, which is what the geometry
        /// works in.
        /// </summary>
        private void ApplyCoastlineMargin()
        {
            double nm;
            if (Double.TryParse(this.coastlineMarginToolStripTextBox.Text,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.CurrentCulture, out nm)
                && nm >= 0.0 && nm <= 100.0)
            {
                this.coastlineEditor.MarginKm = nm * 1.852;
            }
            else
            {
                this.coastlineMarginToolStripTextBox.Text =
                    (this.coastlineEditor.MarginKm / 1.852).ToString("0.#");
            }
        }

        /// <summary>
        /// Saves on every change, and says so.
        ///
        /// Hand-drawn work that evaporates unless the user remembers a button is a trap, and it
        /// caught its author on the first stroke ever drawn. The file is a few KB of text and this
        /// fires on stroke end rather than on mouse move, so writing it every time costs nothing
        /// worth counting. The Save button stays because being told where the file went is worth
        /// something, but nothing depends on it any more.
        /// </summary>
        private void CoastlineEditor_Changed(object sender, EventArgs e)
        {
            // Drawing pans the map by moving Position directly, which is not a drag, so the zoom
            // readout would otherwise go stale exactly while it is being used.
            UpdateCoastlineZoomLabel();

            int points = this.coastlineEditor.Line.Points.Count;
            if (points == 0)
            {
                this.coastlineLabel.Text = "no coastline";
                return;
            }

            string state = "saved";
            try
            {
                Directory.CreateDirectory(AeroSceneryManager.Instance.Settings.WorkingDirectory);
                this.coastlineEditor.Save(CoastlinePath());
            }
            catch (Exception ex)
            {
                state = "NOT SAVED";
                if (!this.coastlineSaveWarned)
                {
                    this.coastlineSaveWarned = true;
                    log.Error("Could not autosave the coastline", ex);
                }
            }

            // The widest gap between strokes, once they are ordered, and it is worth reporting only
            // when it can actually move the cut.
            //
            // A straight line laid across a gap of length L puts the coast at most L/2 from where
            // it really runs, so a gap shorter than the margin cannot shift the cut by more than
            // half the tolerance the whole design is built on. Warning at a flat kilometre cried
            // wolf on the first 900 km line drawn: its widest join was 1.86 km, shorter than nine
            // of its ordinary segments, the longest of which is 8.2 km of genuinely straight
            // beach. Comparing against the margin says nothing on that line and still catches a
            // bay that was never traced.
            double gapKm = this.coastlineEditor.Line.LongestJoinKm;
            double margin = this.coastlineEditor.MarginKm;

            this.coastlineLabel.Text = String.Format("{0} points, {1} strokes, cut at {2:0.#} NM - {3}{4}",
                                                     points, this.coastlineEditor.Line.StrokeCount,
                                                     margin / 1.852, state,
                                                     gapKm > margin ? String.Format("   GAP {0:0.#} km", gapKm) : "");
        }

        /// <summary>
        /// Arrow keys pan and Ctrl+Z undoes, but only while drawing - outside that the map has no
        /// business swallowing them.
        ///
        /// This has to be ProcessCmdKey rather than a KeyDown handler: WinForms treats the arrows
        /// as navigation keys and moves focus between controls with them, so they never reach a
        /// normal handler at all. That failure is silent and looks like the map ignoring the
        /// keyboard for no reason.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (this.coastlineEditor != null && this.coastlineEditor.Active)
            {
                switch (keyData)
                {
                    case Keys.Left:
                    case Keys.Right:
                    case Keys.Up:
                    case Keys.Down:
                        this.coastlineEditor.PanByArrow(keyData);
                        return true;
                    case Keys.Control | Keys.Z:
                        this.coastlineEditor.Undo();
                        return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

        private void MainMap_MouseDown(object sender, MouseEventArgs e)
        {
            //#MOD_k
            // While drawing, the drag is a pen stroke and must not also start a square selection.
            if (this.coastlineEditor != null && this.coastlineEditor.Active)
            {
                return;
            }

            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                this.mapMouseDownLocation = new AeroScenery.Common.Point(e.X, e.Y);
            }
        }

        private void MainMap_MouseUp(object sender, MouseEventArgs e)
        {
            //#MOD_k
            if (this.coastlineEditor != null && this.coastlineEditor.Active)
            {
                return;
            }

            if (this.mapMouseDownLocation != null)
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    var mouseUpLocation = new System.Drawing.Point(e.X, e.Y);

                    var dx = Math.Abs(mouseUpLocation.X - this.mapMouseDownLocation.X);
                    var dy = Math.Abs(mouseUpLocation.Y - this.mapMouseDownLocation.Y);

                    // If there was little movement it was probably meant as a click
                    // rather than a drag
                    if (dx < 10 && dy < 10)
                    {
                        if (!this.mainMap.IsMouseOverMarker)
                        {
                            this.SelectAFSGridSquare(e.X, e.Y);
                        }
                    }
                }

            }

        }

        private void mainMap_DoubleClick(object sender, EventArgs e)
        {
            //#MOD_k
            if (this.coastlineEditor != null && this.coastlineEditor.Active)
            {
                return;
            }

            var evt = (MouseEventArgs)e;
            this.mapMouseDownLocation = null;

            double lat = mainMap.FromLocalToLatLng(evt.X, evt.Y).Lat;
            double lon = mainMap.FromLocalToLatLng(evt.X, evt.Y).Lng;

            // Get the grid square for this lat and lon
            var gridSquare = afs2Grid.GetGridSquareAtLatLon(lat, lon, this.afsGridSquareSelectionSize);

            if (this.SelectedAFS2GridSquares.ContainsKey(gridSquare.Name))
            {
                this.DeselectAFSGridSquare(evt.X, evt.Y);
            }
        }

        private void openInGoogleMapsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.SelectedAFS2GridSquare != null)
            {
                var selectedGridSquare = this.SelectedAFS2GridSquare;
                var googleMapsUrl = "https://www.google.com/maps/@{0},{1},60000m/data=!3m1!1e3";

                string latStr = selectedGridSquare.GetCenter().Lat.ToString("#.####################", CultureInfo.InvariantCulture);
                string lngStr = selectedGridSquare.GetCenter().Lng.ToString("#.####################", CultureInfo.InvariantCulture);

                System.Diagnostics.Process.Start(String.Format(googleMapsUrl, latStr, lngStr));

                //#MOD_f
                // Additionally copy the center coordinates to the clipoard as "<lon> <lat>" for use in TSC-Files of Aerofly
                var centerCoodinateStr = selectedGridSquare.GetCenter().Lng.ToString("#.########", CultureInfo.InvariantCulture) + " " + selectedGridSquare.GetCenter().Lat.ToString("#.########", CultureInfo.InvariantCulture);
                Clipboard.SetData(DataFormats.Text, (Object)centerCoodinateStr);
            }
        }

        private void openInBingMApsToolStripMenuItem_Click(object sender, EventArgs e)
        {       
            if (this.SelectedAFS2GridSquare != null)
            {
                var selectedGridSquare = this.SelectedAFS2GridSquare;
                var bingMapsUrl = "https://www.bing.com/maps/default.aspx?cp={0}~{1}&lvl=10&style=h";

                string latStr = selectedGridSquare.GetCenter().Lat.ToString("#.####################", CultureInfo.InvariantCulture);
                string lngStr = selectedGridSquare.GetCenter().Lng.ToString("#.####################", CultureInfo.InvariantCulture);

                System.Diagnostics.Process.Start(String.Format(bingMapsUrl, latStr, lngStr));

                //#MOD_f
                // Additionally copy the center coordinates to the clipoard as "<lon> <lat>" for use in TSC-Files of Aerofly
                var centerCoodinateStr = selectedGridSquare.GetCenter().Lng.ToString("#.########", CultureInfo.InvariantCulture) + " " + selectedGridSquare.GetCenter().Lat.ToString("#.########", CultureInfo.InvariantCulture);
                Clipboard.SetData(DataFormats.Text, (Object)centerCoodinateStr);
            }
        }
        //#MOD_f
        // Additional "Open in Map" type for Google Earth (Web-version only)
        private void openInGoogleEarthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.SelectedAFS2GridSquare != null)
            {
                var selectedGridSquare = this.SelectedAFS2GridSquare;
                var googleEarthUrl = "https://earth.google.com/web/@{0},{1},2000a,40000d,40y,0h,80t,0r";

                string latStr = selectedGridSquare.GetCenter().Lat.ToString("#.####################", CultureInfo.InvariantCulture);
                string lngStr = selectedGridSquare.GetCenter().Lng.ToString("#.####################", CultureInfo.InvariantCulture);

                System.Diagnostics.Process.Start(String.Format(googleEarthUrl, latStr, lngStr));

                //#MOD_f
                // Additionally copy the center coordinates to the clipoard as "<lon> <lat>" for use in TSC-Files of Aerofly
                var centerCoodinateStr = selectedGridSquare.GetCenter().Lng.ToString("#.########", CultureInfo.InvariantCulture) + " " + selectedGridSquare.GetCenter().Lat.ToString("#.########", CultureInfo.InvariantCulture);
                Clipboard.SetData(DataFormats.Text, (Object)centerCoodinateStr);
            }
        }
        

        private void openImageFolderToolstripButton_Click(object sender, EventArgs e)
        {
            if (this.SelectedAFS2GridSquare != null)
            {
                var gridSquareDirectory = AeroSceneryManager.Instance.Settings.WorkingDirectory + this.SelectedAFS2GridSquare.Name;

                if (Directory.Exists(gridSquareDirectory))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = gridSquareDirectory,
                        UseShellExecute = true,
                        Verb = "open"
                    });

                    //#MOD_f
                    // Additionally copy the name of the selected gridsquare to the clipboard
                    Clipboard.SetData(DataFormats.Text, (Object)this.SelectedAFS2GridSquare.Name);

                }
                else
                {
                    var messageBox = new CustomMessageBox(String.Format("There is no image folder yet for grid square {0}", this.SelectedAFS2GridSquare.Name), 
                        "AeroScenery", 
                        MessageBoxIcon.Information);

                    messageBox.ShowDialog();
                }
            }
        }

        private async void deleteImagesToolStripButton_ClickAsync(object sender, EventArgs e)
        {
            if (this.SelectedAFS2GridSquare != null)
            {
                var gridSquareDirectory = AeroSceneryManager.Instance.Settings.WorkingDirectory + this.SelectedAFS2GridSquare.Name;

                if (Directory.Exists(gridSquareDirectory))
                {
                    using (var deleteSquareOptionsForm = new DeleteSquareOptionsForm())
                    {
                        var result = deleteSquareOptionsForm.ShowDialog();
                        if (result == DialogResult.OK)
                        {
                            var deleteTask = this.fileManager.DeleteGridSquareFilesAsync(gridSquareDirectory, deleteSquareOptionsForm.DeleteMapImageTiles, deleteSquareOptionsForm.DeleteStitchedImages,
                                deleteSquareOptionsForm.DeleteTTCFiles);

                            var fileOperationProgressForm = new FileOperationProgressForm();
                            fileOperationProgressForm.MessageText = "Deleting Files";
                            fileOperationProgressForm.Title = "Deleting Files";

                            fileOperationProgressForm.FileOperationTask = deleteTask;
                            await fileOperationProgressForm.DoTaskAsync();
                            fileOperationProgressForm = null;

                            //#DEVL
                            // Additionally delete the OSM folder in the root folder of the tile (seperate treatment needed) & also the new trees folder should be added as option!
                        }
                    }

                }
                else
                {
                    var messageBox = new CustomMessageBox(String.Format("There is no image folder yet for grid square {0}", this.SelectedAFS2GridSquare.Name),
                        "AeroScenery",
                        MessageBoxIcon.Information);

                    messageBox.ShowDialog();
                }
            }
        }

        private void imageSourceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.uiSetFromSettings)
            {
                var settings = AeroSceneryManager.Instance.Settings;
                settings.OrthophotoSource = (OrthophotoSource)this.imageSourceComboBox.SelectedValue;

                AeroSceneryManager.Instance.SaveSettings();
            }

        }

        private void actionSetComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.uiSetFromSettings)
            {
                switch (this.actionSetComboBox.SelectedIndex)
                {
                    // Default
                    case 0:
                        AeroSceneryManager.Instance.Settings.ActionSet = Common.ActionSet.Default;
                        this.SetDefaultActions();
                        break;
                    // Custom
                    case 1:
                        AeroSceneryManager.Instance.Settings.ActionSet = Common.ActionSet.Custom;
                        this.SetCustomActions();
                        break;
                }

                AeroSceneryManager.Instance.SaveSettings();
            }

        }

        private void setZoomLevelLabelText()
        {
            double metersPerPixel = 0;

            switch (this.zoomLevelTrackBar.Value)
            {
                case 12:
                    metersPerPixel = 38.2185;
                    break;
                case 13:
                    metersPerPixel = 19.1093;
                    break;
                case 14:
                    metersPerPixel = 9.5546;
                    break;
                case 15:
                    metersPerPixel = 4.7773;
                    break;
                case 16:
                    metersPerPixel = 2.3887;
                    break;
                case 17:
                    metersPerPixel = 1.1943;
                    break;
                case 18:
                    metersPerPixel = 0.5972;
                    break;
                case 19:
                    metersPerPixel = 0.2986;
                    break;
                case 20:
                    metersPerPixel = 0.1493;
                    break;
            }

            this.zoomLevelLabel.Text = String.Format("{0} - {1} meters/pixel", this.zoomLevelTrackBar.Value, metersPerPixel.ToString("0.000"));
        }

        private void zoomLevelTrackBar_Scroll(object sender, EventArgs e)
        {
            this.setZoomLevelLabelText();
            AeroSceneryManager.Instance.Settings.ZoomLevel = this.zoomLevelTrackBar.Value;
            AeroSceneryManager.Instance.SaveSettings();
        }

        private void gridSquareLevelsCheckBoxList_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (uiSetFromSettings && processCheckBoxListEvents)
            {
                var settings = AeroSceneryManager.Instance.Settings;

                //var checkedLevel = e.Index + 9;
                var afsLevel = (AFSLevel)this.afsLevelsCheckBoxList.Items[e.Index];                
                var checkedLevel = afsLevel.Level;

                if (e.NewValue == CheckState.Checked)
                {
                    afsLevel.IsChecked = true;
                }
                else
                {
                    afsLevel.IsChecked = false;
                }

                // Don't let anyone select levels that are smaller than the grid square selection size
                if (checkedLevel < this.afsGridSquareSelectionSize)
                {
                    e.NewValue = e.CurrentValue;

                    CustomMessageBox message = new CustomMessageBox("You cannnot selected an AFS Level bigger than the grid square selection size.", 
                        "AeroScenery", MessageBoxIcon.Information);

                    message.ShowDialog();
                }
                else
                {
                    if (settings.AFSLevelsToGenerate.Contains(checkedLevel))
                    {
                        settings.AFSLevelsToGenerate.Remove(checkedLevel);
                    }
                    else
                    {
                        settings.AFSLevelsToGenerate.Add(checkedLevel);
                    }
                }

                AeroSceneryManager.Instance.SaveSettings();
            }

        }

        private void downloadImageTileCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (downloadImageTileCheckBox.Checked)
            {
                AeroSceneryManager.Instance.Settings.DownloadImageTiles = true;
            }
            else
            {
                AeroSceneryManager.Instance.Settings.DownloadImageTiles = false;
            }

            AeroSceneryManager.Instance.SaveSettings();
        }


        private void stitchImageTilesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (stitchImageTilesCheckBox.Checked)
            {
                AeroSceneryManager.Instance.Settings.StitchImageTiles = true;
            }
            else
            {
                AeroSceneryManager.Instance.Settings.StitchImageTiles = false;
            }

            AeroSceneryManager.Instance.SaveSettings();
        }

        private void generateAFSFilesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (generateAFSFilesCheckBox.Checked)
            {
                AeroSceneryManager.Instance.Settings.GenerateAIDAndTMCFiles = true;
            }
            else
            {
                AeroSceneryManager.Instance.Settings.GenerateAIDAndTMCFiles = false;
            }

            AeroSceneryManager.Instance.SaveSettings();
        }

        private void runConverterCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (runConverterCheckBox.Checked)
            {
                AeroSceneryManager.Instance.Settings.RunConverter = true;
            }
            else
            {
                AeroSceneryManager.Instance.Settings.RunConverter = false;
            }

            AeroSceneryManager.Instance.SaveSettings();
        }

        private void deleteStitchedImagesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (deleteStitchedImagesCheckBox.Checked)
            {
                AeroSceneryManager.Instance.Settings.DeleteStitchedImageTiles = true;
            }
            else
            {
                AeroSceneryManager.Instance.Settings.DeleteStitchedImageTiles = false;
            }

            AeroSceneryManager.Instance.SaveSettings();
        }

        private void installSceneryIntoAFSCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (installSceneryIntoAFSCheckBox.Checked)
            {
                AeroSceneryManager.Instance.Settings.InstallScenery = true;
            }
            else
            {
                AeroSceneryManager.Instance.Settings.InstallScenery = false;
            }

            AeroSceneryManager.Instance.SaveSettings();
        }

        private void helpToolStripButton_Click(object sender, EventArgs e)
        {
            var url = "https://github.com/nickhod/aeroscenery";
            System.Diagnostics.Process.Start(url);
        }

        public void UpdateParentTaskLabel(string parentTask)
        {
            this.parentTaskLabel.Text = parentTask;
        }

        public void UpdateChildTaskLabel(string childTask)
        {
            this.childTaskLabel.Text = childTask;
            this.StartTimingStep(childTask);
        }

        /// <summary>
        /// Shows progress inside the current step. It changes only the label. It does not start
        /// a new step, so the log gets one timing line for the step, not one per update.
        /// </summary>
        public void UpdateChildTaskProgress(string progress)
        {
            this.childTaskLabel.Text = progress;
        }

        public void UpdateTaskLabels(string parentTask, string childTask)
        {
            this.parentTaskLabel.Text = parentTask;
            this.childTaskLabel.Text = childTask;
        }

        /// <summary>
        /// Starts the clocks for a new run. Both the run and its current step are timed, and the
        /// labels are refreshed once a second so that a long step still looks alive.
        /// </summary>
        private void StartElapsedClock()
        {
            this.currentStepName = null;
            this.runStopwatch.Restart();
            this.stepStopwatch.Restart();
            this.elapsedTimer.Start();

            this.UpdateElapsedLabels();
        }

        /// <summary>
        /// Stops the clocks and leaves the totals on screen. The log keeps them too, since that is
        /// the only record left once the window is closed.
        /// </summary>
        private void StopElapsedClock(bool finished)
        {
            if (!this.runStopwatch.IsRunning)
            {
                return;
            }

            this.elapsedTimer.Stop();
            this.runStopwatch.Stop();
            this.stepStopwatch.Stop();

            if (this.currentStepName != null)
            {
                log.Info(String.Format("{0} took {1}", this.currentStepName, FormatElapsed(this.stepStopwatch.Elapsed)));
                this.currentStepName = null;
            }

            log.Info(String.Format("Run {0} after {1}", finished ? "finished" : "stopped", FormatElapsed(this.runStopwatch.Elapsed)));

            this.UpdateElapsedLabels();

            this.statusStripElapsedLabel.Text = String.Format("{0} {1}",
                finished ? "Finished in" : "Stopped after",
                FormatElapsed(this.runStopwatch.Elapsed));
        }

        /// <summary>
        /// Times the step that is starting, and logs how long the one it replaces took. Every step
        /// change goes through the child task label, which is why the timing hangs off it.
        /// </summary>
        private void StartTimingStep(string stepName)
        {
            // Label updates outside a run - "Finished", "Stopped" - are not steps and must not
            // restart a clock that has already been stopped
            if (!this.runStopwatch.IsRunning || stepName == this.currentStepName)
            {
                return;
            }

            if (this.currentStepName != null)
            {
                log.Info(String.Format("{0} took {1}", this.currentStepName, FormatElapsed(this.stepStopwatch.Elapsed)));
            }

            this.currentStepName = stepName;
            this.stepStopwatch.Restart();

            this.UpdateElapsedLabels();
        }

        private void ElapsedTimer_Tick(object sender, EventArgs e)
        {
            this.UpdateElapsedLabels();
        }

        private void UpdateElapsedLabels()
        {
            this.runElapsedLabel.Text = String.Format("Elapsed {0}", FormatElapsed(this.runStopwatch.Elapsed));

            this.stepElapsedLabel.Text = this.currentStepName != null
                ? String.Format("this step {0}", FormatElapsed(this.stepStopwatch.Elapsed))
                : "";

            // Once the run is over the status strip says how it ended, so only touch it while running
            if (this.runStopwatch.IsRunning)
            {
                this.statusStripElapsedLabel.Text = String.Format("Running {0}", FormatElapsed(this.runStopwatch.Elapsed));
            }
        }

        /// <summary>
        /// Hours, minutes and seconds. TimeSpan's own "hh" wraps at a day and these runs can pass it.
        /// </summary>
        private static string FormatElapsed(TimeSpan elapsed)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                (int)elapsed.TotalHours,
                elapsed.Minutes,
                elapsed.Seconds);
        }

        public bool ActionsRunning
        {
            get
            {
                return this.actionsRunning;
            }
            set
            {
                this.actionsRunning = value;

                if (this.actionsRunning)
                {
                    this.startStopButton.Text = "Stop";
                }
                else
                {
                    this.startStopButton.Text = "Start";
                }
            }
        }

        public int CurrentActionProgressPercentage
        {
            get
            {
                return this.currentActionProgressBar.Value;
            }
            set
            {
                this.currentActionProgressBar.Value = value;
            }
        }

        public void ActionsComplete()
        {
            this.mainTabControl.SelectedIndex = 0;

            // Say how the run ended rather than leaving the last step on screen, where it reads as
            // still running. The stop button clears actionsRunning itself, so it tells the two apart
            var finished = this.actionsRunning;

            // The clock runs until the work really ends, not until Stop is pressed - a step already
            // under way, the conversion above all, carries on regardless
            this.StopElapsedClock(finished);

            this.UpdateTaskLabels("", finished ? "Finished" : "Stopped");

            this.ActionsRunning = false;
            this.ResetProgress();
            this.UnlockUI();

            this.stopping = false;
            this.UpdateStatusStrip();
        }

        private void gridSquareSelectionSizeToolstripCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;

            // If any grid squares are selected and the message hasn't been show before,
            // show a message to say that the selection will be lost when changing size
            if (this.SelectedAFS2GridSquares.Count() > 0 && this.shownSelectionSizeChangeInfo)
            {
                var messageBox = new CustomMessageBox("Changing the grid square selection size removes any current selections.\nAeroScenery can only process one size of grid square per run.",
                    "AeroScenery", MessageBoxIcon.Information);

                messageBox.ShowDialog();

                this.shownSelectionSizeChangeInfo = false;
            }

            int? minAFSLevel = null;

            switch (this.gridSquareSelectionSizeToolstripCombo.SelectedIndex)
            {
                // 9
                case 0:
                    this.afsGridSquareSelectionSize = 9;
                    this.ClearAllSelectedAFSGridSquares();
                    //#MOD_i
                    minAFSLevel = 9;
                    break;

                // 10
                case 1:
                    this.afsGridSquareSelectionSize = 10;
                    this.ClearAllSelectedAFSGridSquares();
                    minAFSLevel = 10;
                    break;

                // 11
                case 2:
                    this.afsGridSquareSelectionSize = 11;
                    this.ClearAllSelectedAFSGridSquares();
                    minAFSLevel = 11;
                    break;

                // 12
                case 3:
                    this.afsGridSquareSelectionSize = 12;
                    this.ClearAllSelectedAFSGridSquares();
                    minAFSLevel = 12;
                    break;

                // 13
                case 4:
                    this.afsGridSquareSelectionSize = 13;
                    this.ClearAllSelectedAFSGridSquares();
                    minAFSLevel = 13;
                    break;

                // 14
                case 5:
                    this.afsGridSquareSelectionSize = 14;
                    this.ClearAllSelectedAFSGridSquares();
                    minAFSLevel = 14;
                    break;

                //#MOD_i
                // 7

                // 8
            }

            if (minAFSLevel.HasValue)
            {
                this.processCheckBoxListEvents = false;

                for (int index = 0; index < this.afsLevelsCheckBoxList.Items.Count; ++index)
                {
                    var afsLevel = (AFSLevel)this.afsLevelsCheckBoxList.Items[index];

                    if (afsLevel.Level < minAFSLevel.Value)
                    {
                        this.afsLevelsCheckBoxList.SetItemChecked(index, false);
                        afsLevel.IsChecked = false;
                        settings.AFSLevelsToGenerate.Remove(afsLevel.Level);
                    }
                }

                AeroSceneryManager.Instance.SaveSettings();

                this.processCheckBoxListEvents = true;
            }
        }

        
        /*
        private async void button2_Click(object sender, EventArgs e)
        {
        }
        */

        private void CultivationEditorForm_CultivationEditorFormClosed(object sender, EventArgs e)
        {
            this.mainMap.DisableFocusOnMouseEnter = false;
        }

        private void AutoSelectAFSLevelsButton_Click(object sender, EventArgs e)
        {
            var zoomLevel = AeroSceneryManager.Instance.Settings.ZoomLevel;

            List<int> afsLevels = new List<int>();

            switch (this.afsGridSquareSelectionSize)
            {
                //#MOD_i
                case 7:
                    afsLevels.Add(7);

                    break;

                //#MOD_i
                case 8:
                    afsLevels.Add(8);

                    break;

                case 9:

                    afsLevels.Add(9);
                    //#MOD_e
                    afsLevels.Add(10);
                    afsLevels.Add(11);
                    afsLevels.Add(12);

                    if (zoomLevel > 15)
                    {
                        afsLevels.Add(13);
                    }

                    if (zoomLevel > 16)
                    {
                        afsLevels.Add(14);                    
                    }

                    if (zoomLevel > 17)
                    {
                        afsLevels.Add(15);
                    }

                    break;


                case 10:

                    afsLevels.Add(10);
                    afsLevels.Add(11);
                    afsLevels.Add(12);

                    if (zoomLevel > 15)
                    {
                        afsLevels.Add(13);
                    }

                    if (zoomLevel > 16)
                    {
                        afsLevels.Add(14);
                    }

                    if (zoomLevel > 17)
                    {
                        afsLevels.Add(15);
                    }

                    break;

                case 11:

                    afsLevels.Add(11);
                    afsLevels.Add(12);
                    afsLevels.Add(13);

                    if (zoomLevel > 16)
                    {
                        afsLevels.Add(14);
                    }

                    if (zoomLevel > 17)
                    {
                        afsLevels.Add(15);
                    }

                    break;

                case 12:

                    afsLevels.Add(12);
                    afsLevels.Add(13);

                    if (zoomLevel > 16)
                    {
                        afsLevels.Add(14);
                    }

                    if (zoomLevel > 17)
                    {
                        afsLevels.Add(15);
                    }

                    break;
                case 13:

                    afsLevels.Add(13);
                    afsLevels.Add(14);

                    if (zoomLevel > 17)
                    {
                        afsLevels.Add(15);
                    }

                    break;
                case 14:
                    afsLevels.Add(14);

                    if (zoomLevel > 17)
                    {
                        afsLevels.Add(15);
                    }

                    break;
            }

            this.SetAFSLevels(afsLevels);
        }

        private void SetAFSLevels(List<int> afsLevels)
        {
            // Uncheck everything first
            for (int i = 0; i < afsLevelsCheckBoxList.Items.Count; i++)
            {
                AFSLevel level = (AFSLevel)afsLevelsCheckBoxList.Items[i];
                level.IsChecked = false;
                afsLevelsCheckBoxList.SetItemChecked(i, false);
            }

            // Check what needs to be checked
            for (int i = 0; i < afsLevelsCheckBoxList.Items.Count; i++)
            {
                AFSLevel level = (AFSLevel)afsLevelsCheckBoxList.Items[i];

                if (afsLevels.Contains(level.Level))
                {
                    level.IsChecked = true;
                    afsLevelsCheckBoxList.SetItemChecked(i, level.IsChecked);
                }

            }

            AeroSceneryManager.Instance.Settings.AFSLevelsToGenerate = afsLevels;
        }


        private void MainMap_OnMapZoomChanged()
        {
            AeroSceneryManager.Instance.Settings.MapControlLastZoomLevel = Convert.ToInt32(this.mainMap.Zoom);
            UpdateCoastlineZoomLabel();
        }

        private void MainMap_OnMapDrag()
        {
            AeroSceneryManager.Instance.Settings.MapControlLastX = this.mainMap.Position.Lat;
            AeroSceneryManager.Instance.Settings.MapControlLastY = this.mainMap.Position.Lng;
            UpdateCoastlineZoomLabel();
        }

        /// <summary>
        /// What one screen pixel is worth on the ground here, and whether that is fine enough to
        /// draw a coast on.
        ///
        /// The map has a zoom control and no zoom READOUT, so "draw at level 12" was advice with no
        /// way to follow it - the user could only go in and out and guess. Metres per pixel is the
        /// number that actually decides anything anyway, and it is the same unit the Image Detail
        /// slider talks in.
        ///
        /// Asked of the provider's own projection rather than worked out from a Mercator formula,
        /// because the map has eight providers and only some of them are Mercator.
        /// </summary>
        private void UpdateCoastlineZoomLabel()
        {
            if (this.coastlineZoomLabel == null || this.mainMap == null)
            {
                return;
            }

            double metresPerPixel;
            try
            {
                metresPerPixel = this.mainMap.MapProvider.Projection.GetGroundResolution(
                    (int)this.mainMap.Zoom, this.mainMap.Position.Lat);
            }
            catch (Exception)
            {
                this.coastlineZoomLabel.Text = "";
                return;
            }

            // No decimal once the number is big: zoomed right out this reads tens of thousands of
            // metres, and "32822,3" only makes the warning that follows it fall off the end.
            this.coastlineZoomLabel.Text = String.Format(
                metresPerPixel >= 100.0 ? "zoom {0}  -  {1:0} m/px{2}" : "zoom {0}  -  {1:0.#} m/px{2}",
                (int)this.mainMap.Zoom, metresPerPixel,
                metresPerPixel > CoastlineEditor.CoarsestMetresPerPixel ? "  too coarse" : "");
        }

        //#MOD_l
        // The map provider behind each Map Type menu item, keyed by the item's Tag. Null for anything
        // this menu does not offer - the map is left alone rather than guessed at.
        private static GMapProvider MapProviderForMapType(string mapType)
        {
            switch (mapType)
            {
                case "GoogleHybrid":
                    return GMapProviders.GoogleHybridMap;
                case "GoogleSatellite":
                    return GMapProviders.GoogleSatelliteMap;
                case "GoogleStandard":
                    return GMapProviders.GoogleMap;
                case "GoogleTerrain":
                    return GMapProviders.GoogleTerrainMap;
                case "BingHybrid":
                    return GMapProviders.BingHybridMap;
                case "BingSatellite":
                    return GMapProviders.BingSatelliteMap;
                case "BingStandard":
                    return GMapProviders.BingMap;
                case "OpenStreetMap":
                    return GMapProviders.OpenStreetMap;
                default:
                    return null;
            }
        }

        //#MOD_l
        // Which side of the satellite <-> drawn toggle a map type sits on. Hybrids count as imagery:
        // they are photos with labels on top, and it is the photos that hide the borders.
        private static bool IsImageryMapType(string mapType)
        {
            return mapType == "GoogleHybrid" || mapType == "GoogleSatellite" ||
                   mapType == "BingHybrid" || mapType == "BingSatellite";
        }

        //#MOD_l
        // The tag of the map type currently on the control, or null if it is showing something the
        // menu does not list (an old setting, say). The menu is the source of truth for what is on
        // offer, so this walks it rather than keeping a third list in step.
        private string ActiveMapType()
        {
            foreach (ToolStripItem item in this.mapTypeToolStripDropDown.DropDownItems)
            {
                var menuItem = item as ToolStripMenuItem;

                if (menuItem != null && MapProviderForMapType(menuItem.Tag as string) == this.mainMap.MapProvider)
                {
                    return menuItem.Tag as string;
                }
            }

            return null;
        }

        //#MOD_l
        private void ApplyMapType(string mapType)
        {
            var provider = MapProviderForMapType(mapType);

            if (provider == null)
            {
                return;
            }

            this.mainMap.MapProvider = provider;

            var settings = AeroSceneryManager.Instance.Settings;
            settings.MapControlLastMapType = provider.GetType().Name.Replace("Provider", "");

            // Remember it as this side's choice, so the toggle comes back to it rather than to a default
            if (IsImageryMapType(mapType))
            {
                settings.MapControlLastImageryMapType = mapType;
            }
            else
            {
                settings.MapControlLastDrawnMapType = mapType;
            }

            this.ShowActiveMapType();
        }

        //#MOD_l
        // Put the active map's name on the button and a tick against it in the menu. With eight
        // entries and a button that always read "Map Type", there was no way to tell where you were.
        private void ShowActiveMapType()
        {
            var activeMapType = this.ActiveMapType();
            this.mapTypeToolStripDropDown.Text = "Map Type";

            foreach (ToolStripItem item in this.mapTypeToolStripDropDown.DropDownItems)
            {
                var menuItem = item as ToolStripMenuItem;

                if (menuItem == null)
                {
                    continue;
                }

                menuItem.Checked = activeMapType != null && (menuItem.Tag as string) == activeMapType;

                if (menuItem.Checked)
                {
                    // "Google Hybrid Map" -> "Google Hybrid". The button is narrow and "Map" adds nothing
                    var name = menuItem.Text;
                    this.mapTypeToolStripDropDown.Text = name.EndsWith(" Map")
                        ? name.Substring(0, name.Length - " Map".Length)
                        : name;
                }
            }
        }

        //#MOD_l
        // Clicking the button body flips between satellite and drawn; the arrow still opens the full
        // list. Borders are hard to read on imagery, and going back and forth through a menu for that
        // is two clicks each way.
        private void mapTypeToolStripDropDown_ButtonClick(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;
            var activeMapType = this.ActiveMapType();

            // Anything unrecognised is treated as imagery, so the click lands on a drawn map
            this.ApplyMapType(activeMapType == null || IsImageryMapType(activeMapType)
                ? settings.MapControlLastDrawnMapType
                : settings.MapControlLastImageryMapType);
        }

        private void mapTypeToolStripDropDown_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            this.ApplyMapType(e.ClickedItem.Tag as string);
        }

        private void MainTabControl_Selecting(object sender, TabControlCancelEventArgs e)
        {
            // Prevent users returning to the map page if actions are running
            if (actionsRunning && e.TabPageIndex == 0)
            {
                e.Cancel = true;
            }
        }

        private void afsLevelsCheckBoxList_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void afsLevelsCheckBoxList_Leave(object sender, EventArgs e)
        {
            this.afsLevelsCheckBoxList.ClearSelected();
        }

        private void sideTabControl_Selecting(object sender, TabControlCancelEventArgs e)
        {
            // Prevent users returning to the map page if actions are running
            if (actionsRunning)
            {
                e.Cancel = true;
            }
        }

        private async void InstallSceneryToolStripButton_ClickAsync(object sender, EventArgs e)
        {
            if (this.SelectedAFS2GridSquare != null)
            {
                await this.InstallSceneryForGridSquareAsync(this.SelectedAFS2GridSquare, true);
            }

        }

        /// <summary>
        /// Installs the ttc files of a grid square into the Aerofly scenery folder.
        /// Shared by the toolbar button and the Install Scenery action, which differ only in whether
        /// the user is asked to confirm first.
        /// </summary>
        public async Task InstallSceneryForGridSquareAsync(AFS2GridSquare afs2GridSquare, bool confirmWithUser)
        {
            var gridSquareDirectory = AeroSceneryManager.Instance.Settings.WorkingDirectory + afs2GridSquare.Name;

            if (Directory.Exists(gridSquareDirectory))
            {
                var result = this.sceneryInstaller.ConfirmSceneryInstallation(afs2GridSquare, confirmWithUser);

                if (result == DialogResult.Yes)
                {
                    var ttcFiles = new List<string>();

                    var duplicateResult = this.sceneryInstaller.CheckForDuplicateTTCFiles(afs2GridSquare, out ttcFiles);

                    if (duplicateResult == null || duplicateResult == DialogResult.OK)
                    {
                        var installTask = this.sceneryInstaller.InstallSceneryAsync(afs2GridSquare, ttcFiles);

                        var fileOperationProgressForm = new FileOperationProgressForm();
                        fileOperationProgressForm.MessageText = "Installing Scenery";
                        fileOperationProgressForm.Title = "Installing Scenery";

                        fileOperationProgressForm.FileOperationTask = installTask;
                        await fileOperationProgressForm.DoTaskAsync();
                        fileOperationProgressForm = null;
                    }
                }

            }
            else
            {
                var messageBox = new CustomMessageBox(String.Format("There is no image folder yet for grid square {0}", afs2GridSquare.Name),
                    "AeroScenery",
                    MessageBoxIcon.Information);

                messageBox.ShowDialog();
            }

        }

        private void openMapToolStripDropDownButton_Click(object sender, EventArgs e)
        {

        }
        private void copyToClipboardToolStripButton_Click(object sender, EventArgs e)
        {
            //#MOD_f
            Clipboard.SetData(DataFormats.Text, (Object)gridSquareBoundaryBox.Text);

        }

        private void openUserFolderToolstripButton_Click(object sender, EventArgs e)
        {
            // Falls back to detecting the folder when none is configured, same as the installer does
            var afsUserDirectory = DirectoryHelper.FindAFSUserDirectory(AeroSceneryManager.Instance.Settings);

            if (afsUserDirectory != null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = afsUserDirectory,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                var messageBox = new CustomMessageBox("No Aerofly user folder found.\n" +
                    "Looked for 'Aerofly FS 4' and 'Aerofly FS 2' in your Documents folder.\n" +
                    "If yours is elsewhere, set it as the AFS User Folder in Settings.",
                    "AeroScenery",
                    MessageBoxIcon.Information);

                messageBox.ShowDialog();
            }
        }

        private void openSceneryEditorToolStripButton_Click(object sender, EventArgs e)
        {
            var afs2EditorUrl = "https://afs2-editor.nabeelamjad.co.uk/";

            System.Diagnostics.Process.Start(afs2EditorUrl);

        }

        private void toolStripSearchTileButton_Click(object sender, EventArgs e)
        {
            //#MOD_g
            string inputBoxText = "";
            if (CustomeInputBox.InputBox("Tile/ Location Search", "Tile or Location (e.g. '8500_a500' or 'Paris, France'):", ref inputBoxText) == DialogResult.OK)
            {
                AFS2GridSquare aFS2GridSquareSearch = new AFS2GridSquare();
                AFS2Grid aFS2Grid = new AFS2Grid();
                string squareName = inputBoxText;
                //
                if (squareName.Length > 9) 
                {
                    squareName = inputBoxText.Substring(inputBoxText.Length - 9, 9);
                }
                aFS2GridSquareSearch = aFS2Grid.GetGridSquareName(squareName, this.afsGridSquareSelectionSize);

                if (aFS2GridSquareSearch != null)
                {
                    this.ClearAllSelectedAFSGridSquares();

                    this.mainMap.Position = new PointLatLng((aFS2GridSquareSearch.NorthLatitude + aFS2GridSquareSearch.SouthLatitude) / 2, (aFS2GridSquareSearch.WestLongitude + aFS2GridSquareSearch.EastLongitude) / 2);
                    this.mainMap.Zoom = 10;
                    this.activeGridSquareOverlay = this.gMapControlManager.DrawGridSquare(aFS2GridSquareSearch, GridSquareDisplayType.Show);
                }
                else 
                {
                    //#MOD_j
                    // Perform geocoding for location search using OpenSreeet Map Data 
                    var geoCoder = GMapProviders.OpenStreetMap;

                    // Receive list of points found and status code
                    List<PointLatLng> geocodingPointList;
                    var locations = geoCoder.GetPoints(inputBoxText, out geocodingPointList);

                    // Check whether the search was successful
                    if (geocodingPointList != null && geocodingPointList.Count > 0)
                    {
                        // Use the first item from the list (if several were found)
                        var location = geocodingPointList.First();

                        // Show the coordinates on the map
                        this.mainMap.Position = new PointLatLng(location.Lat, location.Lng);
                        this.mainMap.Zoom = 12;
                    }
                    else
                    {
                        var messageBox = new CustomMessageBox(String.Format("Map Tile/ Location '{0}' not found", inputBoxText),
                        "AeroScenery",
                        MessageBoxIcon.Information);

                        messageBox.ShowDialog();
                    }
                }
            }
        }

        private void mainMap_Load(object sender, EventArgs e)
        {
            //#MOD_k
            // Here rather than in the constructor: the settings the path comes from are not
            // populated until Initialize has run, and a coastline that quietly failed to come
            // back would look exactly like one that was never saved.
            try
            {
                string path = CoastlinePath();
                if (File.Exists(path))
                {
                    this.coastlineEditor.Load(path);
                    this.coastlineMarginToolStripTextBox.Text =
                        (this.coastlineEditor.MarginKm / 1.852).ToString("0.#");
                    this.ShowCoastlineLand();
                    log.Info(String.Format("Loaded a coastline of {0} points from {1}",
                                           this.coastlineEditor.Line.Points.Count, path));
                }
            }
            catch (Exception ex)
            {
                log.Error("Could not load the coastline", ex);
            }

            UpdateCoastlineZoomLabel();
        }
    }
}
