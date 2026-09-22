using AeroScenery.UI;

namespace AeroScenery
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.mainMap = new GMap.NET.WindowsForms.GMapControl();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusStripLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStripElapsedLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.settingsButton = new System.Windows.Forms.ToolStripButton();
            this.helpToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.openUserFolderToolstripButton = new System.Windows.Forms.ToolStripButton();
            this.openSceneryEditorToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.mapTypeToolStripDropDown = new System.Windows.Forms.ToolStripSplitButton();
            this.hybridToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.satelliteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.googleTerrainMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bingHybridMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bingSatelliteMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.binStandardMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openStreetMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripLabel2 = new System.Windows.Forms.ToolStripLabel();
            this.gridSquareSelectionSizeToolstripCombo = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.mainTabControl = new System.Windows.Forms.TabControl();
            this.mapTabPage = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.coastlineSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.coastlineDrawToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.coastlineUndoToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.coastlineSaveToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.coastlineMarginLabel = new System.Windows.Forms.ToolStripLabel();
            this.coastlineMarginToolStripTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.coastlineLandToolStripComboBox = new System.Windows.Forms.ToolStripComboBox();
            this.coastlineZoomLabel = new System.Windows.Forms.ToolStripLabel();
            this.coastlineLabel = new System.Windows.Forms.ToolStripLabel();
            this.toolStrip2 = new System.Windows.Forms.ToolStrip();
            this.coastlineToolStrip = new System.Windows.Forms.ToolStrip();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.toolStripSearchTileButton = new System.Windows.Forms.ToolStripButton();
            this.gridSquareLabel = new System.Windows.Forms.ToolStripLabel();
            this.toolStripDownloadedLabel = new System.Windows.Forms.ToolStripLabel();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.openImageFolderToolstripButton = new System.Windows.Forms.ToolStripButton();
            this.installSceneryToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.deleteImagesToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.openMapToolStripDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.openInGoogleMapsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openInBingMApsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openInGoogleEarthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyToClipboardToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.gridSquareBoundaryBox = new System.Windows.Forms.ToolStripLabel();
            this.progressTabPage = new System.Windows.Forms.TabPage();
            this.childTaskLabel = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.downloadThreadProgress8 = new AeroScenery.UI.DownloadThreadProgressControl();
            this.downloadThreadProgress7 = new AeroScenery.UI.DownloadThreadProgressControl();
            this.downloadThreadProgress6 = new AeroScenery.UI.DownloadThreadProgressControl();
            this.downloadThreadProgress5 = new AeroScenery.UI.DownloadThreadProgressControl();
            this.downloadThreadProgress4 = new AeroScenery.UI.DownloadThreadProgressControl();
            this.downloadThreadProgress3 = new AeroScenery.UI.DownloadThreadProgressControl();
            this.downloadThreadProgress2 = new AeroScenery.UI.DownloadThreadProgressControl();
            this.downloadThreadProgress1 = new AeroScenery.UI.DownloadThreadProgressControl();
            this.label6 = new System.Windows.Forms.Label();
            this.currentActionProgressBar = new System.Windows.Forms.ProgressBar();
            this.parentTaskLabel = new System.Windows.Forms.Label();
            this.runElapsedLabel = new System.Windows.Forms.Label();
            this.stepElapsedLabel = new System.Windows.Forms.Label();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.logTextBox = new System.Windows.Forms.TextBox();
            this.sideTabControl = new System.Windows.Forms.TabControl();
            this.imagesTabPage = new System.Windows.Forms.TabPage();
            this.autoSelectAFSLevelsButton = new System.Windows.Forms.Button();
            this.generateAFS2LevelsHelpImage = new System.Windows.Forms.Label();
            this.zoomLevelLabel = new System.Windows.Forms.Label();
            this.zoomLevelTrackBar = new System.Windows.Forms.TrackBar();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.chooseActionsToRunHelpImage = new System.Windows.Forms.Label();
            this.actionSetComboBox = new System.Windows.Forms.ComboBox();
            this.installSceneryIntoAFSCheckBox = new System.Windows.Forms.CheckBox();
            this.deleteStitchedImagesCheckBox = new System.Windows.Forms.CheckBox();
            this.runConverterCheckBox = new System.Windows.Forms.CheckBox();
            this.generateAFSFilesCheckBox = new System.Windows.Forms.CheckBox();
            this.stitchImageTilesCheckBox = new System.Windows.Forms.CheckBox();
            this.downloadImageTileCheckBox = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.afsLevelsCheckBoxList = new System.Windows.Forms.CheckedListBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.imageSourceComboBox = new AeroScenery.UI.ImageComboBox();
            this.startStopButton = new System.Windows.Forms.Button();
            this.shutdownCheckbox = new System.Windows.Forms.CheckBox();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.statusStrip.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.mainTabControl.SuspendLayout();
            this.mapTabPage.SuspendLayout();
            this.panel1.SuspendLayout();
            this.toolStrip2.SuspendLayout();
            this.coastlineToolStrip.SuspendLayout();
            this.progressTabPage.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabPage5.SuspendLayout();
            this.sideTabControl.SuspendLayout();
            this.imagesTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.zoomLevelTrackBar)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainMap
            // 
            this.mainMap.BackColor = System.Drawing.SystemColors.Control;
            this.mainMap.Bearing = 0F;
            this.mainMap.CanDragMap = true;
            this.mainMap.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainMap.EmptyTileColor = System.Drawing.Color.Navy;
            this.mainMap.GrayScaleMode = false;
            this.mainMap.HelperLineOption = GMap.NET.WindowsForms.HelperLineOptions.DontShow;
            this.mainMap.LevelsKeepInMemmory = 5;
            this.mainMap.Location = new System.Drawing.Point(3, 3);
            this.mainMap.MarkersEnabled = true;
            this.mainMap.MaxZoom = 2;
            this.mainMap.MinZoom = 2;
            this.mainMap.MouseWheelZoomEnabled = true;
            this.mainMap.MouseWheelZoomType = GMap.NET.MouseWheelZoomType.MousePositionWithoutCenter;
            this.mainMap.Name = "mainMap";
            this.mainMap.NegativeMode = false;
            this.mainMap.PolygonsEnabled = true;
            this.mainMap.RetryLoadTile = 0;
            this.mainMap.RoutesEnabled = true;
            this.mainMap.ScaleMode = GMap.NET.WindowsForms.ScaleModes.Integer;
            this.mainMap.SelectedAreaFillColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(65)))), ((int)(((byte)(105)))), ((int)(((byte)(225)))));
            this.mainMap.ShowTileGridLines = false;
            this.mainMap.Size = new System.Drawing.Size(1064, 741);
            this.mainMap.TabIndex = 0;
            this.mainMap.Zoom = 0D;
            this.mainMap.OnMapDrag += new GMap.NET.MapDrag(this.MainMap_OnMapDrag);
            this.mainMap.OnMapZoomChanged += new GMap.NET.MapZoomChanged(this.MainMap_OnMapZoomChanged);
            this.mainMap.Load += new System.EventHandler(this.mainMap_Load);
            this.mainMap.DoubleClick += new System.EventHandler(this.mainMap_DoubleClick);
            this.mainMap.MouseDown += new System.Windows.Forms.MouseEventHandler(this.MainMap_MouseDown);
            this.mainMap.MouseUp += new System.Windows.Forms.MouseEventHandler(this.MainMap_MouseUp);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusStripLabel1,
            this.statusStripElapsedLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 838);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1491, 22);
            this.statusStrip.TabIndex = 2;
            this.statusStrip.Text = "statusStrip";
            // 
            // statusStripLabel1
            // 
            this.statusStripLabel1.Name = "statusStripLabel1";
            this.statusStripLabel1.Size = new System.Drawing.Size(0, 17);
            //
            // statusStripElapsedLabel
            //
            this.statusStripElapsedLabel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.statusStripElapsedLabel.Name = "statusStripElapsedLabel";
            this.statusStripElapsedLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // toolStrip1
            // 
            this.toolStrip1.AutoSize = false;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.settingsButton,
            this.helpToolStripButton,
            this.openUserFolderToolstripButton,
            this.openSceneryEditorToolStripButton,
            this.toolStripSeparator4,
            this.mapTypeToolStripDropDown,
            this.toolStripSeparator7,
            this.toolStripLabel2,
            this.gridSquareSelectionSizeToolstripCombo,
            this.toolStripSeparator5});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(12, 5, 0, 5);
            this.toolStrip1.Size = new System.Drawing.Size(1491, 42);
            this.toolStrip1.TabIndex = 4;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // settingsButton
            // 
            this.settingsButton.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.settingsButton.Image = ((System.Drawing.Image)(resources.GetObject("settingsButton.Image")));
            this.settingsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.settingsButton.Name = "settingsButton";
            this.settingsButton.Size = new System.Drawing.Size(74, 29);
            this.settingsButton.Text = "Settings";
            this.settingsButton.Click += new System.EventHandler(this.settingsButton_Click);
            // 
            // helpToolStripButton
            // 
            this.helpToolStripButton.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.helpToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("helpToolStripButton.Image")));
            this.helpToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.helpToolStripButton.Name = "helpToolStripButton";
            this.helpToolStripButton.Size = new System.Drawing.Size(55, 29);
            this.helpToolStripButton.Text = "Help";
            this.helpToolStripButton.ToolTipText = "Link to Nick Hoddinott\'s original AeroScenery on GitHub";
            this.helpToolStripButton.Click += new System.EventHandler(this.helpToolStripButton_Click);
            // 
            // openUserFolderToolstripButton
            // 
            this.openUserFolderToolstripButton.Image = ((System.Drawing.Image)(resources.GetObject("openUserFolderToolstripButton.Image")));
            this.openUserFolderToolstripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.openUserFolderToolstripButton.Name = "openUserFolderToolstripButton";
            this.openUserFolderToolstripButton.Size = new System.Drawing.Size(118, 29);
            this.openUserFolderToolstripButton.Text = "Open User Folder";
            this.openUserFolderToolstripButton.ToolTipText = "Open the AFS User scenery folder";
            this.openUserFolderToolstripButton.Click += new System.EventHandler(this.openUserFolderToolstripButton_Click);
            // 
            // openSceneryEditorToolStripButton
            // 
            this.openSceneryEditorToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("openSceneryEditorToolStripButton.Image")));
            this.openSceneryEditorToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.openSceneryEditorToolStripButton.Name = "openSceneryEditorToolStripButton";
            this.openSceneryEditorToolStripButton.Size = new System.Drawing.Size(134, 29);
            this.openSceneryEditorToolStripButton.Text = "Open Scenery Editor";
            this.openSceneryEditorToolStripButton.ToolTipText = "Open the FS2 Cultivation Editor by Nabeel";
            this.openSceneryEditorToolStripButton.Click += new System.EventHandler(this.openSceneryEditorToolStripButton_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(6, 32);
            //
            // mapTypeToolStripDropDown
            // 
            this.mapTypeToolStripDropDown.BackColor = System.Drawing.SystemColors.Control;
            this.mapTypeToolStripDropDown.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.hybridToolStripMenuItem,
            this.satelliteToolStripMenuItem,
            this.sToolStripMenuItem,
            this.googleTerrainMapToolStripMenuItem,
            this.bingHybridMapToolStripMenuItem,
            this.bingSatelliteMapToolStripMenuItem,
            this.binStandardMapToolStripMenuItem,
            this.openStreetMapToolStripMenuItem});
            this.mapTypeToolStripDropDown.Image = ((System.Drawing.Image)(resources.GetObject("mapTypeToolStripDropDown.Image")));
            this.mapTypeToolStripDropDown.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.mapTypeToolStripDropDown.Name = "mapTypeToolStripDropDown";
            //#MOD_l
            // Fixed width: the text is now the active map's name, and letting it autosize would shove
            // the grid square size selector left and right on every toggle.
            this.mapTypeToolStripDropDown.AutoSize = false;
            this.mapTypeToolStripDropDown.Size = new System.Drawing.Size(170, 29);
            this.mapTypeToolStripDropDown.Text = "Map Type";
            this.mapTypeToolStripDropDown.ToolTipText = "Click to switch between satellite and drawn map. Use the arrow for the full list.";
            this.mapTypeToolStripDropDown.ButtonClick += new System.EventHandler(this.mapTypeToolStripDropDown_ButtonClick);
            this.mapTypeToolStripDropDown.DropDownItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.mapTypeToolStripDropDown_DropDownItemClicked);
            // 
            // hybridToolStripMenuItem
            // 
            this.hybridToolStripMenuItem.Name = "hybridToolStripMenuItem";
            this.hybridToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.hybridToolStripMenuItem.Tag = "GoogleHybrid";
            this.hybridToolStripMenuItem.Text = "Google Hybrid Map";
            // 
            // satelliteToolStripMenuItem
            // 
            this.satelliteToolStripMenuItem.Name = "satelliteToolStripMenuItem";
            this.satelliteToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.satelliteToolStripMenuItem.Tag = "GoogleSatellite";
            this.satelliteToolStripMenuItem.Text = "Google Satellite Map";
            // 
            // sToolStripMenuItem
            // 
            this.sToolStripMenuItem.Name = "sToolStripMenuItem";
            this.sToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.sToolStripMenuItem.Tag = "GoogleStandard";
            this.sToolStripMenuItem.Text = "Google Standard Map";
            // 
            // googleTerrainMapToolStripMenuItem
            // 
            this.googleTerrainMapToolStripMenuItem.Name = "googleTerrainMapToolStripMenuItem";
            this.googleTerrainMapToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.googleTerrainMapToolStripMenuItem.Tag = "GoogleTerrain";
            this.googleTerrainMapToolStripMenuItem.Text = "Google Terrain Map";
            // 
            // bingHybridMapToolStripMenuItem
            // 
            this.bingHybridMapToolStripMenuItem.Name = "bingHybridMapToolStripMenuItem";
            this.bingHybridMapToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.bingHybridMapToolStripMenuItem.Tag = "BingHybrid";
            this.bingHybridMapToolStripMenuItem.Text = "Bing Hybrid Map";
            // 
            // bingSatelliteMapToolStripMenuItem
            // 
            this.bingSatelliteMapToolStripMenuItem.Name = "bingSatelliteMapToolStripMenuItem";
            this.bingSatelliteMapToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.bingSatelliteMapToolStripMenuItem.Tag = "BingSatellite";
            this.bingSatelliteMapToolStripMenuItem.Text = "Bing Satellite Map";
            // 
            // binStandardMapToolStripMenuItem
            // 
            this.binStandardMapToolStripMenuItem.Name = "binStandardMapToolStripMenuItem";
            this.binStandardMapToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.binStandardMapToolStripMenuItem.Tag = "BingStandard";
            this.binStandardMapToolStripMenuItem.Text = "Bing Standard Map";
            // 
            // openStreetMapToolStripMenuItem
            // 
            this.openStreetMapToolStripMenuItem.Name = "openStreetMapToolStripMenuItem";
            this.openStreetMapToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.openStreetMapToolStripMenuItem.Tag = "OpenStreetMap";
            //#MOD_l
            // Back to plain Open Street Map. It was swapped for Open Cycle Map under #MOD_f because
            // OSM "doesn't work anymore"; it does - b.tile.openstreetmap.org serves fine, and GMap
            // ships a seeded world cache for it up to zoom 8 - and a cycling map is a poor way to
            // read borders, which is the whole point of having a drawn map here.
            //
            // One word, unlike its neighbours, because that is the project's actual name - and it
            // keeps ShowActiveMapType from putting "Open Street" on the button, which reads truncated.
            this.openStreetMapToolStripMenuItem.Text = "OpenStreetMap";
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            this.toolStripSeparator7.Size = new System.Drawing.Size(6, 32);
            // 
            // toolStripLabel2
            // 
            this.toolStripLabel2.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.toolStripLabel2.Name = "toolStripLabel2";
            this.toolStripLabel2.Size = new System.Drawing.Size(161, 29);
            this.toolStripLabel2.Text = "Grid Square Selection Size";
            // 
            // gridSquareSelectionSizeToolstripCombo
            // 
            this.gridSquareSelectionSizeToolstripCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.gridSquareSelectionSizeToolstripCombo.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.gridSquareSelectionSizeToolstripCombo.Items.AddRange(new object[] {
            "Size 9 (Default)",
            "Size 10",
            "Size 11",
            "Size 12 ",
            "Size 13",
            "Size 14 (Smallest)"});
            this.gridSquareSelectionSizeToolstripCombo.Name = "gridSquareSelectionSizeToolstripCombo";
            this.gridSquareSelectionSizeToolstripCombo.Size = new System.Drawing.Size(121, 32);
            this.gridSquareSelectionSizeToolstripCombo.ToolTipText = "Select Size 9 as default --> You may do smaller tiles with higher resolution late" +
    "r";
            this.gridSquareSelectionSizeToolstripCombo.SelectedIndexChanged += new System.EventHandler(this.gridSquareSelectionSizeToolstripCombo_SelectedIndexChanged);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(6, 32);
            // 
            // mainTabControl
            // 
            this.mainTabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mainTabControl.Controls.Add(this.mapTabPage);
            this.mainTabControl.Controls.Add(this.progressTabPage);
            this.mainTabControl.Controls.Add(this.tabPage5);
            this.mainTabControl.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.mainTabControl.Location = new System.Drawing.Point(401, 45);
            this.mainTabControl.Name = "mainTabControl";
            this.mainTabControl.SelectedIndex = 0;
            this.mainTabControl.Size = new System.Drawing.Size(1078, 777);
            this.mainTabControl.TabIndex = 6;
            this.mainTabControl.Selecting += new System.Windows.Forms.TabControlCancelEventHandler(this.MainTabControl_Selecting);
            // 
            // mapTabPage
            // 
            this.mapTabPage.Controls.Add(this.panel1);
            this.mapTabPage.Controls.Add(this.mainMap);
            this.mapTabPage.Location = new System.Drawing.Point(4, 26);
            this.mapTabPage.Name = "mapTabPage";
            this.mapTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.mapTabPage.Size = new System.Drawing.Size(1070, 747);
            this.mapTabPage.TabIndex = 0;
            this.mapTabPage.Text = "Map";
            this.mapTabPage.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.coastlineToolStrip);
            this.panel1.Controls.Add(this.toolStrip2);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1064, 56);
            this.panel1.TabIndex = 1;
            // 
            // toolStrip2
            // 
            this.toolStrip2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.toolStrip2.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.toolStripSearchTileButton,
            this.gridSquareLabel,
            this.toolStripDownloadedLabel,
            this.toolStripSeparator3,
            this.openImageFolderToolstripButton,
            this.installSceneryToolStripButton,
            this.deleteImagesToolStripButton,
            this.toolStripSeparator2,
            this.openMapToolStripDropDownButton,
            this.copyToClipboardToolStripButton,
            this.gridSquareBoundaryBox});
            //
            // coastline controls - a row of their own, because the map toolbar was already full
            // and anything pushed into its overflow chevron may as well not exist
            //
            this.coastlineToolStrip.Dock = System.Windows.Forms.DockStyle.None;
            this.coastlineToolStrip.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.coastlineToolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.coastlineToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.coastlineDrawToolStripButton,
            this.coastlineUndoToolStripButton,
            this.coastlineSeparator,
            this.coastlineMarginLabel,
            this.coastlineMarginToolStripTextBox,
            this.coastlineLandToolStripComboBox,
            this.coastlineSaveToolStripButton,
            this.coastlineZoomLabel,
            this.coastlineLabel});
            this.coastlineToolStrip.Location = new System.Drawing.Point(0, 28);
            this.coastlineToolStrip.Name = "coastlineToolStrip";
            this.coastlineToolStrip.Size = new System.Drawing.Size(1064, 25);
            this.coastlineToolStrip.TabIndex = 2;
            this.coastlineSeparator.Name = "coastlineSeparator";
            this.coastlineSeparator.Size = new System.Drawing.Size(6, 25);
            this.coastlineDrawToolStripButton.CheckOnClick = true;
            this.coastlineDrawToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.coastlineDrawToolStripButton.Name = "coastlineDrawToolStripButton";
            this.coastlineDrawToolStripButton.Size = new System.Drawing.Size(85, 22);
            this.coastlineDrawToolStripButton.Text = "Draw Coast";
            this.coastlineDrawToolStripButton.ToolTipText = "Drag to trace the waterline. Arrow keys pan, the screen edge pans while you draw, Ctrl+Z undoes a stroke.";
            this.coastlineDrawToolStripButton.Click += new System.EventHandler(this.coastlineDrawToolStripButton_Click);
            this.coastlineUndoToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.coastlineUndoToolStripButton.Name = "coastlineUndoToolStripButton";
            this.coastlineUndoToolStripButton.Size = new System.Drawing.Size(45, 22);
            this.coastlineUndoToolStripButton.Text = "Undo";
            this.coastlineUndoToolStripButton.Click += new System.EventHandler(this.coastlineUndoToolStripButton_Click);
            this.coastlineMarginLabel.Name = "coastlineMarginLabel";
            this.coastlineMarginLabel.Size = new System.Drawing.Size(60, 22);
            this.coastlineMarginLabel.Text = "Cut NM";
            this.coastlineMarginToolStripTextBox.Name = "coastlineMarginToolStripTextBox";
            this.coastlineMarginToolStripTextBox.Size = new System.Drawing.Size(40, 25);
            this.coastlineMarginToolStripTextBox.Leave += new System.EventHandler(this.coastlineMarginToolStripTextBox_Leave);
            this.coastlineMarginToolStripTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.coastlineMarginToolStripTextBox_KeyDown);
            this.coastlineLandToolStripComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.coastlineLandToolStripComboBox.Items.AddRange(new object[] {
            "Land east",
            "Land west",
            "Land north",
            "Land south"});
            this.coastlineLandToolStripComboBox.Name = "coastlineLandToolStripComboBox";
            this.coastlineLandToolStripComboBox.Size = new System.Drawing.Size(90, 25);
            this.coastlineLandToolStripComboBox.ToolTipText = "Which side of the line is land. Land east for a coast that faces west, land north for a coast that faces south.";
            this.coastlineLandToolStripComboBox.SelectedIndexChanged += new System.EventHandler(this.coastlineLandToolStripComboBox_SelectedIndexChanged);
            this.coastlineSaveToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.coastlineSaveToolStripButton.Name = "coastlineSaveToolStripButton";
            this.coastlineSaveToolStripButton.Size = new System.Drawing.Size(40, 22);
            this.coastlineSaveToolStripButton.Text = "Save";
            this.coastlineSaveToolStripButton.Click += new System.EventHandler(this.coastlineSaveToolStripButton_Click);
            this.coastlineZoomLabel.AutoSize = false;
            this.coastlineZoomLabel.Name = "coastlineZoomLabel";
            this.coastlineZoomLabel.Size = new System.Drawing.Size(250, 22);
            this.coastlineZoomLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.coastlineZoomLabel.Text = "";
            this.coastlineZoomLabel.ToolTipText = "How much ground one screen pixel covers here. Draw at 50 m/px or finer, or the mouse lays points down further apart than the line keeps.";
            this.coastlineLabel.Name = "coastlineLabel";
            this.coastlineLabel.Size = new System.Drawing.Size(120, 22);
            this.coastlineLabel.Text = "no coastline";
            this.toolStrip2.Location = new System.Drawing.Point(0, 0);
            this.toolStrip2.Name = "toolStrip2";
            this.toolStrip2.Size = new System.Drawing.Size(1064, 25);
            this.toolStrip2.TabIndex = 1;
            this.toolStrip2.Text = "toolStrip2";
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.toolStripLabel1.Margin = new System.Windows.Forms.Padding(0, 1, 5, 2);
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Size = new System.Drawing.Size(82, 22);
            this.toolStripLabel1.Text = "Grid Square:";
            // 
            // toolStripSearchTileButton
            // 
            this.toolStripSearchTileButton.Image = ((System.Drawing.Image)(resources.GetObject("toolStripSearchTileButton.Image")));
            this.toolStripSearchTileButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripSearchTileButton.Name = "toolStripSearchTileButton";
            this.toolStripSearchTileButton.Size = new System.Drawing.Size(23, 22);
            this.toolStripSearchTileButton.ToolTipText = "Serach Tile/ Location";
            this.toolStripSearchTileButton.Click += new System.EventHandler(this.toolStripSearchTileButton_Click);
            // 
            // gridSquareLabel
            // 
            this.gridSquareLabel.AutoSize = false;
            this.gridSquareLabel.Margin = new System.Windows.Forms.Padding(0, 1, 8, 2);
            this.gridSquareLabel.Name = "gridSquareLabel";
            this.gridSquareLabel.Size = new System.Drawing.Size(125, 22);
            this.gridSquareLabel.Text = "map_09_xxxx_xxxx";
            // 
            // toolStripDownloadedLabel
            // 
            this.toolStripDownloadedLabel.Margin = new System.Windows.Forms.Padding(0, 1, 10, 2);
            this.toolStripDownloadedLabel.Name = "toolStripDownloadedLabel";
            this.toolStripDownloadedLabel.Size = new System.Drawing.Size(108, 22);
            this.toolStripDownloadedLabel.Text = "Not Downloaded";
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 25);
            // 
            // openImageFolderToolstripButton
            // 
            this.openImageFolderToolstripButton.Enabled = false;
            this.openImageFolderToolstripButton.Image = ((System.Drawing.Image)(resources.GetObject("openImageFolderToolstripButton.Image")));
            this.openImageFolderToolstripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.openImageFolderToolstripButton.Name = "openImageFolderToolstripButton";
            this.openImageFolderToolstripButton.Size = new System.Drawing.Size(101, 22);
            this.openImageFolderToolstripButton.Text = "Open Folder";
            this.openImageFolderToolstripButton.ToolTipText = "Open the folder for this grid square";
            this.openImageFolderToolstripButton.Click += new System.EventHandler(this.openImageFolderToolstripButton_Click);
            // 
            // installSceneryToolStripButton
            // 
            this.installSceneryToolStripButton.Enabled = false;
            this.installSceneryToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("installSceneryToolStripButton.Image")));
            this.installSceneryToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.installSceneryToolStripButton.Name = "installSceneryToolStripButton";
            this.installSceneryToolStripButton.Size = new System.Drawing.Size(110, 22);
            this.installSceneryToolStripButton.Text = "Install Scenery";
            this.installSceneryToolStripButton.ToolTipText = "Install the converted scenery into the Aerofly user scenery folder";
            this.installSceneryToolStripButton.Click += new System.EventHandler(this.InstallSceneryToolStripButton_ClickAsync);
            // 
            // deleteImagesToolStripButton
            // 
            this.deleteImagesToolStripButton.Enabled = false;
            this.deleteImagesToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("deleteImagesToolStripButton.Image")));
            this.deleteImagesToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.deleteImagesToolStripButton.Name = "deleteImagesToolStripButton";
            this.deleteImagesToolStripButton.Size = new System.Drawing.Size(94, 22);
            this.deleteImagesToolStripButton.Text = "Delete Files";
            this.deleteImagesToolStripButton.ToolTipText = "Delete files related to this grid square";
            this.deleteImagesToolStripButton.Click += new System.EventHandler(this.deleteImagesToolStripButton_ClickAsync);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // openMapToolStripDropDownButton
            // 
            this.openMapToolStripDropDownButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openInGoogleMapsToolStripMenuItem,
            this.openInBingMApsToolStripMenuItem,
            this.openInGoogleEarthToolStripMenuItem});
            this.openMapToolStripDropDownButton.Enabled = false;
            this.openMapToolStripDropDownButton.Image = ((System.Drawing.Image)(resources.GetObject("openMapToolStripDropDownButton.Image")));
            this.openMapToolStripDropDownButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.openMapToolStripDropDownButton.Name = "openMapToolStripDropDownButton";
            this.openMapToolStripDropDownButton.Size = new System.Drawing.Size(114, 22);
            this.openMapToolStripDropDownButton.Text = "Open In Map";
            this.openMapToolStripDropDownButton.ToolTipText = "Open Square In Map";
            this.openMapToolStripDropDownButton.Click += new System.EventHandler(this.openMapToolStripDropDownButton_Click);
            // 
            // openInGoogleMapsToolStripMenuItem
            // 
            this.openInGoogleMapsToolStripMenuItem.Name = "openInGoogleMapsToolStripMenuItem";
            this.openInGoogleMapsToolStripMenuItem.Size = new System.Drawing.Size(206, 22);
            this.openInGoogleMapsToolStripMenuItem.Text = "Open In Google Maps";
            this.openInGoogleMapsToolStripMenuItem.Click += new System.EventHandler(this.openInGoogleMapsToolStripMenuItem_Click);
            // 
            // openInBingMApsToolStripMenuItem
            // 
            this.openInBingMApsToolStripMenuItem.Name = "openInBingMApsToolStripMenuItem";
            this.openInBingMApsToolStripMenuItem.Size = new System.Drawing.Size(206, 22);
            this.openInBingMApsToolStripMenuItem.Text = "Open In Bing Maps";
            this.openInBingMApsToolStripMenuItem.Click += new System.EventHandler(this.openInBingMApsToolStripMenuItem_Click);
            // 
            // openInGoogleEarthToolStripMenuItem
            // 
            this.openInGoogleEarthToolStripMenuItem.Name = "openInGoogleEarthToolStripMenuItem";
            this.openInGoogleEarthToolStripMenuItem.Size = new System.Drawing.Size(206, 22);
            this.openInGoogleEarthToolStripMenuItem.Text = "Open in Google Earth";
            this.openInGoogleEarthToolStripMenuItem.Click += new System.EventHandler(this.openInGoogleEarthToolStripMenuItem_Click);
            // 
            // copyToClipboardToolStripButton
            // 
            this.copyToClipboardToolStripButton.Enabled = false;
            this.copyToClipboardToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("copyToClipboardToolStripButton.Image")));
            this.copyToClipboardToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.copyToClipboardToolStripButton.Name = "copyToClipboardToolStripButton";
            this.copyToClipboardToolStripButton.Size = new System.Drawing.Size(104, 22);
            this.copyToClipboardToolStripButton.Text = "To Clipboard";
            this.copyToClipboardToolStripButton.ToolTipText = "Copy boundary box coodinates to clipboard";
            this.copyToClipboardToolStripButton.Click += new System.EventHandler(this.copyToClipboardToolStripButton_Click);
            // 
            // gridSquareBoundaryBox
            // 
            this.gridSquareBoundaryBox.AutoSize = false;
            this.gridSquareBoundaryBox.Margin = new System.Windows.Forms.Padding(0, 1, 8, 2);
            this.gridSquareBoundaryBox.Name = "gridSquareBoundaryBox";
            this.gridSquareBoundaryBox.Size = new System.Drawing.Size(90, 22);
            this.gridSquareBoundaryBox.Text = "BoundaryBox";
            this.gridSquareBoundaryBox.Visible = false;
            // 
            // progressTabPage
            // 
            this.progressTabPage.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.progressTabPage.Controls.Add(this.childTaskLabel);
            this.progressTabPage.Controls.Add(this.label7);
            this.progressTabPage.Controls.Add(this.groupBox1);
            this.progressTabPage.Controls.Add(this.label6);
            this.progressTabPage.Controls.Add(this.currentActionProgressBar);
            this.progressTabPage.Controls.Add(this.parentTaskLabel);
            this.progressTabPage.Controls.Add(this.runElapsedLabel);
            this.progressTabPage.Controls.Add(this.stepElapsedLabel);
            this.progressTabPage.Location = new System.Drawing.Point(4, 26);
            this.progressTabPage.Name = "progressTabPage";
            this.progressTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.progressTabPage.Size = new System.Drawing.Size(1070, 747);
            this.progressTabPage.TabIndex = 1;
            this.progressTabPage.Text = "Progress";
            // 
            // childTaskLabel
            // 
            this.childTaskLabel.AutoSize = true;
            this.childTaskLabel.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.childTaskLabel.Location = new System.Drawing.Point(88, 55);
            this.childTaskLabel.Name = "childTaskLabel";
            this.childTaskLabel.Size = new System.Drawing.Size(0, 17);
            this.childTaskLabel.TabIndex = 6;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(13, 55);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(67, 17);
            this.label7.TabIndex = 5;
            this.label7.Text = "Currently:";
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.downloadThreadProgress8);
            this.groupBox1.Controls.Add(this.downloadThreadProgress7);
            this.groupBox1.Controls.Add(this.downloadThreadProgress6);
            this.groupBox1.Controls.Add(this.downloadThreadProgress5);
            this.groupBox1.Controls.Add(this.downloadThreadProgress4);
            this.groupBox1.Controls.Add(this.downloadThreadProgress3);
            this.groupBox1.Controls.Add(this.downloadThreadProgress2);
            this.groupBox1.Controls.Add(this.downloadThreadProgress1);
            this.groupBox1.Location = new System.Drawing.Point(9, 154);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(1055, 571);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Downloaders";
            // 
            // downloadThreadProgress8
            // 
            this.downloadThreadProgress8.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadThreadProgress8.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.downloadThreadProgress8.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.downloadThreadProgress8.Location = new System.Drawing.Point(6, 477);
            this.downloadThreadProgress8.Margin = new System.Windows.Forms.Padding(3, 21, 3, 21);
            this.downloadThreadProgress8.Name = "downloadThreadProgress8";
            this.downloadThreadProgress8.Size = new System.Drawing.Size(995, 58);
            this.downloadThreadProgress8.TabIndex = 7;
            // 
            // downloadThreadProgress7
            // 
            this.downloadThreadProgress7.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadThreadProgress7.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.downloadThreadProgress7.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.downloadThreadProgress7.Location = new System.Drawing.Point(6, 413);
            this.downloadThreadProgress7.Margin = new System.Windows.Forms.Padding(3, 16, 3, 16);
            this.downloadThreadProgress7.Name = "downloadThreadProgress7";
            this.downloadThreadProgress7.Size = new System.Drawing.Size(995, 58);
            this.downloadThreadProgress7.TabIndex = 6;
            // 
            // downloadThreadProgress6
            // 
            this.downloadThreadProgress6.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadThreadProgress6.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.downloadThreadProgress6.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.downloadThreadProgress6.Location = new System.Drawing.Point(6, 350);
            this.downloadThreadProgress6.Margin = new System.Windows.Forms.Padding(3, 12, 3, 12);
            this.downloadThreadProgress6.Name = "downloadThreadProgress6";
            this.downloadThreadProgress6.Size = new System.Drawing.Size(995, 58);
            this.downloadThreadProgress6.TabIndex = 5;
            // 
            // downloadThreadProgress5
            // 
            this.downloadThreadProgress5.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadThreadProgress5.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.downloadThreadProgress5.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.downloadThreadProgress5.Location = new System.Drawing.Point(6, 286);
            this.downloadThreadProgress5.Margin = new System.Windows.Forms.Padding(3, 9, 3, 9);
            this.downloadThreadProgress5.Name = "downloadThreadProgress5";
            this.downloadThreadProgress5.Size = new System.Drawing.Size(995, 58);
            this.downloadThreadProgress5.TabIndex = 4;
            // 
            // downloadThreadProgress4
            // 
            this.downloadThreadProgress4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadThreadProgress4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.downloadThreadProgress4.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.downloadThreadProgress4.Location = new System.Drawing.Point(7, 222);
            this.downloadThreadProgress4.Margin = new System.Windows.Forms.Padding(3, 7, 3, 7);
            this.downloadThreadProgress4.Name = "downloadThreadProgress4";
            this.downloadThreadProgress4.Size = new System.Drawing.Size(995, 58);
            this.downloadThreadProgress4.TabIndex = 3;
            // 
            // downloadThreadProgress3
            // 
            this.downloadThreadProgress3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadThreadProgress3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.downloadThreadProgress3.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.downloadThreadProgress3.Location = new System.Drawing.Point(7, 156);
            this.downloadThreadProgress3.Margin = new System.Windows.Forms.Padding(3, 7, 3, 7);
            this.downloadThreadProgress3.Name = "downloadThreadProgress3";
            this.downloadThreadProgress3.Size = new System.Drawing.Size(995, 59);
            this.downloadThreadProgress3.TabIndex = 2;
            // 
            // downloadThreadProgress2
            // 
            this.downloadThreadProgress2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadThreadProgress2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.downloadThreadProgress2.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.downloadThreadProgress2.Location = new System.Drawing.Point(7, 93);
            this.downloadThreadProgress2.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.downloadThreadProgress2.Name = "downloadThreadProgress2";
            this.downloadThreadProgress2.Size = new System.Drawing.Size(995, 54);
            this.downloadThreadProgress2.TabIndex = 1;
            // 
            // downloadThreadProgress1
            // 
            this.downloadThreadProgress1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadThreadProgress1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.downloadThreadProgress1.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.downloadThreadProgress1.Location = new System.Drawing.Point(7, 31);
            this.downloadThreadProgress1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.downloadThreadProgress1.Name = "downloadThreadProgress1";
            this.downloadThreadProgress1.Size = new System.Drawing.Size(995, 48);
            this.downloadThreadProgress1.TabIndex = 0;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(13, 94);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(147, 17);
            this.label6.TabIndex = 3;
            this.label6.Text = "Current Action Progress";
            // 
            // currentActionProgressBar
            // 
            this.currentActionProgressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.currentActionProgressBar.Location = new System.Drawing.Point(16, 120);
            this.currentActionProgressBar.Name = "currentActionProgressBar";
            this.currentActionProgressBar.Size = new System.Drawing.Size(994, 18);
            this.currentActionProgressBar.TabIndex = 2;
            // 
            // parentTaskLabel
            // 
            this.parentTaskLabel.AutoSize = true;
            this.parentTaskLabel.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.parentTaskLabel.Location = new System.Drawing.Point(12, 17);
            this.parentTaskLabel.Name = "parentTaskLabel";
            this.parentTaskLabel.Size = new System.Drawing.Size(276, 21);
            this.parentTaskLabel.TabIndex = 1;
            this.parentTaskLabel.Text = "Working On AFS2 Grid Square - of -";
            //
            // runElapsedLabel
            //
            this.runElapsedLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.runElapsedLabel.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.runElapsedLabel.Location = new System.Drawing.Point(710, 17);
            this.runElapsedLabel.Name = "runElapsedLabel";
            this.runElapsedLabel.Size = new System.Drawing.Size(300, 21);
            this.runElapsedLabel.TabIndex = 7;
            this.runElapsedLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // stepElapsedLabel
            //
            this.stepElapsedLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.stepElapsedLabel.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.stepElapsedLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.stepElapsedLabel.Location = new System.Drawing.Point(710, 55);
            this.stepElapsedLabel.Name = "stepElapsedLabel";
            this.stepElapsedLabel.Size = new System.Drawing.Size(300, 17);
            this.stepElapsedLabel.TabIndex = 8;
            this.stepElapsedLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.logTextBox);
            this.tabPage5.Location = new System.Drawing.Point(4, 26);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage5.Size = new System.Drawing.Size(1070, 747);
            this.tabPage5.TabIndex = 2;
            this.tabPage5.Text = "Log";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // logTextBox
            // 
            this.logTextBox.BackColor = System.Drawing.SystemColors.Window;
            this.logTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.logTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logTextBox.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.logTextBox.Location = new System.Drawing.Point(3, 3);
            this.logTextBox.Multiline = true;
            this.logTextBox.Name = "logTextBox";
            this.logTextBox.ReadOnly = true;
            this.logTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.logTextBox.Size = new System.Drawing.Size(1064, 741);
            this.logTextBox.TabIndex = 0;
            // 
            // sideTabControl
            // 
            this.sideTabControl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.sideTabControl.Controls.Add(this.imagesTabPage);
            this.sideTabControl.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.sideTabControl.Location = new System.Drawing.Point(12, 45);
            this.sideTabControl.Name = "sideTabControl";
            this.sideTabControl.SelectedIndex = 0;
            this.sideTabControl.Size = new System.Drawing.Size(379, 679);
            this.sideTabControl.TabIndex = 7;
            this.sideTabControl.Selecting += new System.Windows.Forms.TabControlCancelEventHandler(this.sideTabControl_Selecting);
            // 
            // imagesTabPage
            // 
            this.imagesTabPage.Controls.Add(this.autoSelectAFSLevelsButton);
            this.imagesTabPage.Controls.Add(this.generateAFS2LevelsHelpImage);
            this.imagesTabPage.Controls.Add(this.zoomLevelLabel);
            this.imagesTabPage.Controls.Add(this.zoomLevelTrackBar);
            this.imagesTabPage.Controls.Add(this.groupBox2);
            this.imagesTabPage.Controls.Add(this.label4);
            this.imagesTabPage.Controls.Add(this.afsLevelsCheckBoxList);
            this.imagesTabPage.Controls.Add(this.label3);
            this.imagesTabPage.Controls.Add(this.label2);
            this.imagesTabPage.Controls.Add(this.imageSourceComboBox);
            this.imagesTabPage.Location = new System.Drawing.Point(4, 26);
            this.imagesTabPage.Name = "imagesTabPage";
            this.imagesTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.imagesTabPage.Size = new System.Drawing.Size(371, 649);
            this.imagesTabPage.TabIndex = 0;
            this.imagesTabPage.Text = "Images";
            this.imagesTabPage.UseVisualStyleBackColor = true;
            // 
            // autoSelectAFSLevelsButton
            // 
            this.autoSelectAFSLevelsButton.Location = new System.Drawing.Point(217, 132);
            this.autoSelectAFSLevelsButton.Name = "autoSelectAFSLevelsButton";
            this.autoSelectAFSLevelsButton.Size = new System.Drawing.Size(138, 32);
            this.autoSelectAFSLevelsButton.TabIndex = 11;
            this.autoSelectAFSLevelsButton.Text = "Choose For Me";
            this.autoSelectAFSLevelsButton.UseVisualStyleBackColor = true;
            this.autoSelectAFSLevelsButton.Click += new System.EventHandler(this.AutoSelectAFSLevelsButton_Click);
            // 
            // generateAFS2LevelsHelpImage
            // 
            this.generateAFS2LevelsHelpImage.AutoSize = true;
            this.generateAFS2LevelsHelpImage.Image = ((System.Drawing.Image)(resources.GetObject("generateAFS2LevelsHelpImage.Image")));
            this.generateAFS2LevelsHelpImage.Location = new System.Drawing.Point(144, 140);
            this.generateAFS2LevelsHelpImage.Name = "generateAFS2LevelsHelpImage";
            this.generateAFS2LevelsHelpImage.Size = new System.Drawing.Size(16, 17);
            this.generateAFS2LevelsHelpImage.TabIndex = 10;
            this.generateAFS2LevelsHelpImage.Text = "  ";
            // 
            // zoomLevelLabel
            // 
            this.zoomLevelLabel.AutoSize = true;
            this.zoomLevelLabel.Location = new System.Drawing.Point(182, 65);
            this.zoomLevelLabel.Name = "zoomLevelLabel";
            this.zoomLevelLabel.Size = new System.Drawing.Size(22, 17);
            this.zoomLevelLabel.TabIndex = 9;
            this.zoomLevelLabel.Text = "16";
            // 
            // zoomLevelTrackBar
            // 
            this.zoomLevelTrackBar.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.zoomLevelTrackBar.LargeChange = 1;
            this.zoomLevelTrackBar.Location = new System.Drawing.Point(16, 88);
            this.zoomLevelTrackBar.Maximum = 20;
            this.zoomLevelTrackBar.Minimum = 12;
            this.zoomLevelTrackBar.Name = "zoomLevelTrackBar";
            this.zoomLevelTrackBar.Size = new System.Drawing.Size(337, 45);
            this.zoomLevelTrackBar.TabIndex = 8;
            this.zoomLevelTrackBar.TickStyle = System.Windows.Forms.TickStyle.TopLeft;
            this.zoomLevelTrackBar.Value = 12;
            this.zoomLevelTrackBar.Scroll += new System.EventHandler(this.zoomLevelTrackBar_Scroll);
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.chooseActionsToRunHelpImage);
            this.groupBox2.Controls.Add(this.actionSetComboBox);
            this.groupBox2.Controls.Add(this.installSceneryIntoAFSCheckBox);
            this.groupBox2.Controls.Add(this.deleteStitchedImagesCheckBox);
            this.groupBox2.Controls.Add(this.runConverterCheckBox);
            this.groupBox2.Controls.Add(this.generateAFSFilesCheckBox);
            this.groupBox2.Controls.Add(this.stitchImageTilesCheckBox);
            this.groupBox2.Controls.Add(this.downloadImageTileCheckBox);
            this.groupBox2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(16, 336);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(339, 298);
            this.groupBox2.TabIndex = 7;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Actions";
            // 
            // chooseActionsToRunHelpImage
            // 
            this.chooseActionsToRunHelpImage.AutoSize = true;
            this.chooseActionsToRunHelpImage.Image = ((System.Drawing.Image)(resources.GetObject("chooseActionsToRunHelpImage.Image")));
            this.chooseActionsToRunHelpImage.Location = new System.Drawing.Point(289, 33);
            this.chooseActionsToRunHelpImage.Name = "chooseActionsToRunHelpImage";
            this.chooseActionsToRunHelpImage.Size = new System.Drawing.Size(16, 17);
            this.chooseActionsToRunHelpImage.TabIndex = 12;
            this.chooseActionsToRunHelpImage.Text = "  ";
            // 
            // actionSetComboBox
            // 
            this.actionSetComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.actionSetComboBox.FormattingEnabled = true;
            this.actionSetComboBox.Items.AddRange(new object[] {
            "Run Default Actions",
            "Choose Actions To Run"});
            this.actionSetComboBox.Location = new System.Drawing.Point(19, 30);
            this.actionSetComboBox.Name = "actionSetComboBox";
            this.actionSetComboBox.Size = new System.Drawing.Size(256, 25);
            this.actionSetComboBox.TabIndex = 6;
            this.actionSetComboBox.SelectedIndexChanged += new System.EventHandler(this.actionSetComboBox_SelectedIndexChanged);
            // 
            // installSceneryIntoAFSCheckBox
            // 
            this.installSceneryIntoAFSCheckBox.AutoSize = true;
            this.installSceneryIntoAFSCheckBox.Enabled = false;
            this.installSceneryIntoAFSCheckBox.Location = new System.Drawing.Point(19, 176);
            this.installSceneryIntoAFSCheckBox.Name = "installSceneryIntoAFSCheckBox";
            this.installSceneryIntoAFSCheckBox.Size = new System.Drawing.Size(109, 21);
            this.installSceneryIntoAFSCheckBox.TabIndex = 5;
            this.installSceneryIntoAFSCheckBox.Text = "Install Scenery";
            this.installSceneryIntoAFSCheckBox.UseVisualStyleBackColor = true;
            this.installSceneryIntoAFSCheckBox.CheckedChanged += new System.EventHandler(this.installSceneryIntoAFSCheckBox_CheckedChanged);
            // 
            // deleteStitchedImagesCheckBox
            // 
            this.deleteStitchedImagesCheckBox.AutoSize = true;
            this.deleteStitchedImagesCheckBox.Enabled = false;
            this.deleteStitchedImagesCheckBox.Location = new System.Drawing.Point(166, 167);
            this.deleteStitchedImagesCheckBox.Name = "deleteStitchedImagesCheckBox";
            this.deleteStitchedImagesCheckBox.Size = new System.Drawing.Size(160, 21);
            this.deleteStitchedImagesCheckBox.TabIndex = 4;
            this.deleteStitchedImagesCheckBox.Text = "Delete Stitched Images";
            this.deleteStitchedImagesCheckBox.UseVisualStyleBackColor = true;
            this.deleteStitchedImagesCheckBox.Visible = false;
            this.deleteStitchedImagesCheckBox.CheckedChanged += new System.EventHandler(this.deleteStitchedImagesCheckBox_CheckedChanged);
            // 
            // runConverterCheckBox
            // 
            this.runConverterCheckBox.AutoSize = true;
            this.runConverterCheckBox.Enabled = false;
            this.runConverterCheckBox.Location = new System.Drawing.Point(19, 149);
            this.runConverterCheckBox.Name = "runConverterCheckBox";
            this.runConverterCheckBox.Size = new System.Drawing.Size(122, 21);
            this.runConverterCheckBox.TabIndex = 3;
            this.runConverterCheckBox.Text = "Run Converter";
            this.runConverterCheckBox.UseVisualStyleBackColor = true;
            this.runConverterCheckBox.CheckedChanged += new System.EventHandler(this.runConverterCheckBox_CheckedChanged);
            // 
            // generateAFSFilesCheckBox
            // 
            this.generateAFSFilesCheckBox.AutoSize = true;
            this.generateAFSFilesCheckBox.Enabled = false;
            this.generateAFSFilesCheckBox.Location = new System.Drawing.Point(19, 122);
            this.generateAFSFilesCheckBox.Name = "generateAFSFilesCheckBox";
            this.generateAFSFilesCheckBox.Size = new System.Drawing.Size(173, 21);
            this.generateAFSFilesCheckBox.TabIndex = 2;
            this.generateAFSFilesCheckBox.Text = "Generate AID / TMC Files";
            this.generateAFSFilesCheckBox.UseVisualStyleBackColor = true;
            this.generateAFSFilesCheckBox.CheckedChanged += new System.EventHandler(this.generateAFSFilesCheckBox_CheckedChanged);
            // 
            // stitchImageTilesCheckBox
            // 
            this.stitchImageTilesCheckBox.AutoSize = true;
            this.stitchImageTilesCheckBox.Enabled = false;
            this.stitchImageTilesCheckBox.Location = new System.Drawing.Point(19, 95);
            this.stitchImageTilesCheckBox.Name = "stitchImageTilesCheckBox";
            this.stitchImageTilesCheckBox.Size = new System.Drawing.Size(128, 21);
            this.stitchImageTilesCheckBox.TabIndex = 1;
            this.stitchImageTilesCheckBox.Text = "Stitch Image Tiles";
            this.stitchImageTilesCheckBox.UseVisualStyleBackColor = true;
            this.stitchImageTilesCheckBox.CheckedChanged += new System.EventHandler(this.stitchImageTilesCheckBox_CheckedChanged);
            // 
            // downloadImageTileCheckBox
            // 
            this.downloadImageTileCheckBox.AutoSize = true;
            this.downloadImageTileCheckBox.Enabled = false;
            this.downloadImageTileCheckBox.Location = new System.Drawing.Point(19, 68);
            this.downloadImageTileCheckBox.Name = "downloadImageTileCheckBox";
            this.downloadImageTileCheckBox.Size = new System.Drawing.Size(165, 21);
            this.downloadImageTileCheckBox.TabIndex = 0;
            this.downloadImageTileCheckBox.Text = "Download Image Tiles";
            this.downloadImageTileCheckBox.UseVisualStyleBackColor = true;
            this.downloadImageTileCheckBox.CheckedChanged += new System.EventHandler(this.downloadImageTileCheckBox_CheckedChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 139);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(125, 17);
            this.label4.TabIndex = 6;
            this.label4.Text = "Generate AFS Levels";
            // 
            // afsLevelsCheckBoxList
            // 
            this.afsLevelsCheckBoxList.CheckOnClick = true;
            this.afsLevelsCheckBoxList.FormattingEnabled = true;
            this.afsLevelsCheckBoxList.Location = new System.Drawing.Point(16, 177);
            this.afsLevelsCheckBoxList.Name = "afsLevelsCheckBoxList";
            this.afsLevelsCheckBoxList.Size = new System.Drawing.Size(339, 144);
            this.afsLevelsCheckBoxList.TabIndex = 7;
            this.afsLevelsCheckBoxList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.gridSquareLevelsCheckBoxList_ItemCheck);
            this.afsLevelsCheckBoxList.SelectedIndexChanged += new System.EventHandler(this.afsLevelsCheckBoxList_SelectedIndexChanged);
            this.afsLevelsCheckBoxList.Leave += new System.EventHandler(this.afsLevelsCheckBoxList_Leave);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 65);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(160, 17);
            this.label3.TabIndex = 3;
            this.label3.Text = "Image Detail (Zoom Level)";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 22);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(88, 17);
            this.label2.TabIndex = 2;
            this.label2.Text = "Image Source";
            // 
            // imageSourceComboBox
            // 
            this.imageSourceComboBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.imageSourceComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.imageSourceComboBox.FormattingEnabled = true;
            this.imageSourceComboBox.ImageList = null;
            this.imageSourceComboBox.Location = new System.Drawing.Point(112, 19);
            this.imageSourceComboBox.Name = "imageSourceComboBox";
            this.imageSourceComboBox.Size = new System.Drawing.Size(243, 26);
            this.imageSourceComboBox.TabIndex = 1;
            this.imageSourceComboBox.SelectedIndexChanged += new System.EventHandler(this.imageSourceComboBox_SelectedIndexChanged);
            // 
            // startStopButton
            // 
            this.startStopButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.startStopButton.Enabled = false;
            this.startStopButton.Font = new System.Drawing.Font("Segoe UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.startStopButton.Location = new System.Drawing.Point(12, 759);
            this.startStopButton.Name = "startStopButton";
            this.startStopButton.Size = new System.Drawing.Size(379, 63);
            this.startStopButton.TabIndex = 3;
            this.startStopButton.Text = "Start";
            this.startStopButton.UseVisualStyleBackColor = true;
            this.startStopButton.Click += new System.EventHandler(this.ButtonStart_Click);
            // 
            // shutdownCheckbox
            // 
            this.shutdownCheckbox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.shutdownCheckbox.AutoSize = true;
            this.shutdownCheckbox.Enabled = false;
            this.shutdownCheckbox.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.shutdownCheckbox.Location = new System.Drawing.Point(12, 730);
            this.shutdownCheckbox.Name = "shutdownCheckbox";
            this.shutdownCheckbox.Size = new System.Drawing.Size(223, 21);
            this.shutdownCheckbox.TabIndex = 8;
            this.shutdownCheckbox.Text = "Shut Down Computer When Done";
            this.shutdownCheckbox.UseVisualStyleBackColor = true;
            this.shutdownCheckbox.Visible = false;
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "arrow_down.png");
            this.imageList1.Images.SetKeyName(1, "arrow_down_active.png");
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(23, 23);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1491, 860);
            this.Controls.Add(this.shutdownCheckbox);
            this.Controls.Add(this.sideTabControl);
            this.Controls.Add(this.startStopButton);
            this.Controls.Add(this.mainTabControl);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.statusStrip);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "AeroScenery";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Shown += new System.EventHandler(this.MainForm_Shown);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.mainTabControl.ResumeLayout(false);
            this.mapTabPage.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.toolStrip2.ResumeLayout(false);
            this.coastlineToolStrip.ResumeLayout(false);
            this.coastlineToolStrip.PerformLayout();
            this.toolStrip2.PerformLayout();
            this.progressTabPage.ResumeLayout(false);
            this.progressTabPage.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.tabPage5.ResumeLayout(false);
            this.tabPage5.PerformLayout();
            this.sideTabControl.ResumeLayout(false);
            this.imagesTabPage.ResumeLayout(false);
            this.imagesTabPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.zoomLevelTrackBar)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private GMap.NET.WindowsForms.GMapControl mainMap;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton settingsButton;
        private System.Windows.Forms.TabControl mainTabControl;
        private System.Windows.Forms.TabPage mapTabPage;
        private System.Windows.Forms.TabPage progressTabPage;
        private System.Windows.Forms.TabControl sideTabControl;
        private System.Windows.Forms.TabPage imagesTabPage;
        private System.Windows.Forms.Button startStopButton;
        private System.Windows.Forms.CheckBox downloadImageTileCheckBox;
        private System.Windows.Forms.ToolStripButton helpToolStripButton;
        private System.Windows.Forms.ProgressBar currentActionProgressBar;
        private System.Windows.Forms.Label parentTaskLabel;
        private System.Windows.Forms.Label runElapsedLabel;
        private System.Windows.Forms.Label stepElapsedLabel;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label childTaskLabel;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.TextBox logTextBox;
        private UI.DownloadThreadProgressControl downloadThreadProgress2;
        private UI.DownloadThreadProgressControl downloadThreadProgress1;
        private UI.DownloadThreadProgressControl downloadThreadProgress4;
        private UI.DownloadThreadProgressControl downloadThreadProgress3;
        private UI.DownloadThreadProgressControl downloadThreadProgress6;
        private UI.DownloadThreadProgressControl downloadThreadProgress5;
        private UI.DownloadThreadProgressControl downloadThreadProgress8;
        private UI.DownloadThreadProgressControl downloadThreadProgress7;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.CheckBox runConverterCheckBox;
        private System.Windows.Forms.CheckBox generateAFSFilesCheckBox;
        private System.Windows.Forms.CheckBox stitchImageTilesCheckBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckedListBox afsLevelsCheckBoxList;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private ImageComboBox imageSourceComboBox;
        private System.Windows.Forms.CheckBox installSceneryIntoAFSCheckBox;
        private System.Windows.Forms.CheckBox deleteStitchedImagesCheckBox;
        private System.Windows.Forms.Panel panel1;
        //#MOD_k
        private System.Windows.Forms.ToolStrip coastlineToolStrip;
        private System.Windows.Forms.ToolStripSeparator coastlineSeparator;
        private System.Windows.Forms.ToolStripButton coastlineDrawToolStripButton;
        private System.Windows.Forms.ToolStripButton coastlineUndoToolStripButton;
        private System.Windows.Forms.ToolStripButton coastlineSaveToolStripButton;
        private System.Windows.Forms.ToolStripLabel coastlineMarginLabel;
        private System.Windows.Forms.ToolStripTextBox coastlineMarginToolStripTextBox;
        private System.Windows.Forms.ToolStripComboBox coastlineLandToolStripComboBox;
        private System.Windows.Forms.ToolStripLabel coastlineZoomLabel;
        private System.Windows.Forms.ToolStripLabel coastlineLabel;
        private System.Windows.Forms.ToolStrip toolStrip2;
        private System.Windows.Forms.ToolStripButton deleteImagesToolStripButton;
        private System.Windows.Forms.ToolStripButton openImageFolderToolstripButton;
        private System.Windows.Forms.ToolStripDropDownButton openMapToolStripDropDownButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripLabel gridSquareLabel;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripLabel toolStripDownloadedLabel;
        private System.Windows.Forms.ToolStripMenuItem openInGoogleMapsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openInBingMApsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ComboBox actionSetComboBox;
        private System.Windows.Forms.TrackBar zoomLevelTrackBar;
        private System.Windows.Forms.Label zoomLevelLabel;
        private System.Windows.Forms.Label generateAFS2LevelsHelpImage;
        private System.Windows.Forms.CheckBox shutdownCheckbox;
        private System.Windows.Forms.ToolStripStatusLabel statusStripLabel1;
        private System.Windows.Forms.ToolStripStatusLabel statusStripElapsedLabel;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripLabel toolStripLabel2;
        private System.Windows.Forms.ToolStripComboBox gridSquareSelectionSizeToolstripCombo;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.Button autoSelectAFSLevelsButton;
        private System.Windows.Forms.ToolStripSplitButton mapTypeToolStripDropDown;
        private System.Windows.Forms.ToolStripMenuItem hybridToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem satelliteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripMenuItem bingHybridMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bingSatelliteMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem binStandardMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openStreetMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton installSceneryToolStripButton;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
        private System.Windows.Forms.ToolStripButton copyToClipboardToolStripButton;
        private System.Windows.Forms.ToolStripLabel gridSquareBoundaryBox;
        private System.Windows.Forms.ToolStripMenuItem openInGoogleEarthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem googleTerrainMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton openUserFolderToolstripButton;
        private System.Windows.Forms.ToolStripButton openSceneryEditorToolStripButton;
        private System.Windows.Forms.ToolStripButton toolStripSearchTileButton;
        private System.Windows.Forms.Label chooseActionsToRunHelpImage;
    }
}

