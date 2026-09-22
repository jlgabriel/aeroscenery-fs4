using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using AeroScenery.ImageProcessing;
using AeroScenery.Controls;

namespace AeroScenery.UI
{
    public partial class SettingsForm : Form
    {
        private readonly ILog log = LogManager.GetLogger("AeroScenery");

        private ImageProcessingPreviewForm imageProcessingPreviewForm;

        private bool updateImagePreview;
        //#MOD_g
        private bool showMessageStartAppAgain = false;

        public SettingsForm()
        {
            InitializeComponent();
            this.updateImagePreview = true;

            //#MOD_i
            ToolTip toolTip1 = new ToolTip();
            toolTip1.IsBalloon = true;
            toolTip1.InitialDelay = 500;

            ToolTip toolTip2 = new ToolTip();
            toolTip2.IsBalloon = true;
            toolTip2.InitialDelay = 500;

            ToolTip toolTip3 = new ToolTip();
            toolTip3.IsBalloon = true;
            toolTip3.InitialDelay = 500;

            ToolTip toolTip4 = new ToolTip();
            toolTip4.IsBalloon = true;
            toolTip4.InitialDelay = 500;

            ToolTip toolTip5 = new ToolTip();
            toolTip5.IsBalloon = true;
            toolTip5.InitialDelay = 500;

            ToolTip toolTip6 = new ToolTip();
            toolTip6.IsBalloon = true;
            toolTip6.InitialDelay = 500;
            toolTip6.SetToolTip(this.imageProcessingHelpImage, "This option allows you to adjust images before conversion.\nAfter changing the parameters just run the single step 'Stitch Image Tiles' again to aply the changes.\nThe option 'Remove alpha chanel' replaces the alpha chanel of the sea with a default dark blue color (only works with masked Google images).");
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;

            settings.WorkingDirectory = pathWithTrailingDirectorySeparatorChar(this.workingFolderTextBox.Text);
            settings.AFS2UserDirectory = pathWithTrailingDirectorySeparatorChar(this.afs2UserFolderTextBox.Text);
            //#MOD_i 
            //settings.AFS4UserDirectory = pathWithTrailingDirectorySeparatorChar(this.afs4UserFolderTextBox.Text);

            //#MOD_i
            settings.AFSSceneryFolder = pathWithTrailingDirectorySeparatorChar(this.afsSceneryFolderTextBox.Text);
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace(" ", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace("#", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace("%", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace("*", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace("&", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace("?", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace("/", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace("+", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace(".", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace(",", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace(":", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace(";", "");
            settings.AFSSceneryFolder = settings.AFSSceneryFolder.Replace("__", "_");


            settings.UserAgent = this.userAgentTextBox.Text;

            if (!String.IsNullOrEmpty(this.downloadWaitTextBox.Text))
            {
                settings.DownloadWaitMs = int.Parse(this.downloadWaitTextBox.Text);
            }

            if (!String.IsNullOrEmpty(this.downloadWaitRandomTextBox.Text))
            {
                settings.DownloadWaitRandomMs = int.Parse(this.downloadWaitRandomTextBox.Text);
            }

            //#MOD_g
            if (Convert.ToInt32(this.simultaneousDownloadsComboBox.Text) != settings.SimultaneousDownloads)
            {
                showMessageStartAppAgain = true;
                settings.SimultaneousDownloads = Convert.ToInt32(this.simultaneousDownloadsComboBox.Text);
            }
            /*
            switch (this.simultaneousDownloadsComboBox.SelectedIndex)
            {
                case 0:
                    settings.SimultaneousDownloads = 4;
                    break;
                case 1:
                    settings.SimultaneousDownloads = 6;
                    break;
                case 2:
                    settings.SimultaneousDownloads = 8;
                    break;
            }
            */

            string maxTilesPerStitchedImageMessage = null;

            if (!String.IsNullOrEmpty(this.maxTilesPerStitchedImageTextBox.Text))
            {
                // Parsed as a long so that a number too big for an int is clamped below like any
                // other oversized value, rather than rejected as if it were not a number at all
                long maxTilesPerStitchedImage;

                if (!long.TryParse(this.maxTilesPerStitchedImageTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxTilesPerStitchedImage))
                {
                    maxTilesPerStitchedImageMessage = String.Format(
                        "'{0}' could not be read as a whole number, so the maximum\n" +
                        "stitched image size has been left at {1}.",
                        this.maxTilesPerStitchedImageTextBox.Text,
                        settings.MaximumStitchedImageSize);
                }
                else
                {
                    var largestUsable = TileStitcher.MaxTilesPerStitchedImage();

                    if (maxTilesPerStitchedImage > largestUsable)
                    {
                        maxTilesPerStitchedImageMessage = String.Format(
                            "A maximum stitched image size of {0} has been reduced to {1}.\n\n" +
                            "Each stitched image is held as a single Windows bitmap, and\n" +
                            "Windows cannot create one of 2 GB. At {1} tiles per side a\n" +
                            "stitched image is already {2}px x {2}px.",
                            maxTilesPerStitchedImage,
                            largestUsable,
                            largestUsable * TileStitcher.SourceTileSize);

                        maxTilesPerStitchedImage = largestUsable;
                    }
                    else if (maxTilesPerStitchedImage < 1)
                    {
                        maxTilesPerStitchedImageMessage = String.Format(
                            "A maximum stitched image size of {0} has been raised to 1.\n\n" +
                            "It is a number of image tiles per side, so it cannot be zero or negative.",
                            maxTilesPerStitchedImage);

                        maxTilesPerStitchedImage = 1;
                    }

                    settings.MaximumStitchedImageSize = (int)maxTilesPerStitchedImage;
                }
            }

            // Index 0 = yes, index 1 = no
            settings.WriteImagesWithMask = this.gcWriteImagesWithMaskCombo.SelectedIndex == 0;

            int converterThreads;
            settings.ConverterThreads =
                int.TryParse(this.converterThreadsComboBox.Text, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out converterThreads)
                ? converterThreads
                : 0;      // "Automatic", or anything unparseable

            settings.CutAtCoastline = this.cutAtCoastlineCheckBox.Checked;

            settings.LinzApiKey = this.linzKeyTextBox.Text.Trim();
            //#MOD_e
            settings.MapboxApiKey = this.mapboxKeyTextBox.Text.Trim();

            //#MOD_h
            //#DOD_h
            settings.HereWeGoApiKey = this.herewegoKeyTextBox.Text.Trim();

            //#MOD_g

            //#DEVL_h

            //#MOD_i



            settings.ShrinkTMCGridSquareCoords = double.Parse(this.shrinkTMCGridSquaresTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture);

            // Image processing
            settings.EnableImageProcessing = this.imageProcessingEnabledCheckBox.Checked;
            settings.BrightnessAdjustment = this.imgProcBrightnessSlider.Value;
            settings.ContrastAdjustment = this.imgProcContrastSlider.Value;
            settings.SaturationAdjustment = this.imgProcSaturationSlider.Value;
            settings.SharpnessAdjustment = this.imgProcSharpnessSlider.Value;
            settings.RedAdjustment = this.imgProcRedSlider.Value;
            settings.GreenAdjustment = this.imgProcGreenSlider.Value;
            settings.BlueAdjustment = this.imgProcBlueSlider.Value;
            //#MOD_i
            settings.RemoveAlphaChannelAdjustment = this.imageRemoveAlphaChannelCheckBox.Checked;

            AeroSceneryManager.Instance.SaveSettings();
            this.Hide();
            log.Info("Settings saved");

            if (maxTilesPerStitchedImageMessage != null)
            {
                var messageBox = new CustomMessageBox(maxTilesPerStitchedImageMessage,
                    "AeroScenery",
                    MessageBoxIcon.Warning);

                messageBox.ShowDialog();
            }

            //#MOD_g
            if (showMessageStartAppAgain)
            {
                var messageBox = new CustomMessageBox("Please restart the App to make the changes effective.",
                    "AeroScenery",
                    MessageBoxIcon.Warning);

                messageBox.ShowDialog();
                
            }
        }

        private void SettingsForm_Shown(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;

            this.workingFolderTextBox.Text = settings.WorkingDirectory;
            this.afs2UserFolderTextBox.Text = settings.AFS2UserDirectory;
            //#MOD_h
            //this.afs4UserFolderTextBox.Text = settings.AFS4UserDirectory;

            //#MOD_i
            this.afsSceneryFolderTextBox.Text = settings.AFSSceneryFolder;

            this.userAgentTextBox.Text = settings.UserAgent;
            this.downloadWaitTextBox.Text = settings.DownloadWaitMs.ToString();
            this.downloadWaitRandomTextBox.Text = settings.DownloadWaitRandomMs.ToString();

            //#MOD_g
            this.simultaneousDownloadsComboBox.Text = Convert.ToString(settings.SimultaneousDownloads);
            /*
            switch (settings.SimultaneousDownloads)
            {
                case 4:
                    this.simultaneousDownloadsComboBox.SelectedIndex = 0;
                    break;
                case 6:
                    this.simultaneousDownloadsComboBox.SelectedIndex = 1;
                    break;
                case 8:
                    this.simultaneousDownloadsComboBox.SelectedIndex = 2;
                    break;
            }
            */

            this.maxTilesPerStitchedImageTextBox.Text = settings.MaximumStitchedImageSize.ToString();

            this.gcWriteImagesWithMaskCombo.SelectedIndex = settings.WriteImagesWithMask.Value ? 0 : 1;

            int threads = settings.ConverterThreads ?? 0;
            this.converterThreadsComboBox.SelectedItem = threads > 0
                ? threads.ToString(CultureInfo.InvariantCulture)
                : "Automatic";
            if (this.converterThreadsComboBox.SelectedIndex < 0)
            {
                // A saved value that is not one of the offered ones, e.g. hand edited
                this.converterThreadsComboBox.SelectedIndex = 0;
            }

            this.cutAtCoastlineCheckBox.Checked = settings.CutAtCoastline.Value;

            this.linzKeyTextBox.Text = settings.LinzApiKey;
            //#MOD_e
            this.mapboxKeyTextBox.Text = settings.MapboxApiKey;

            //#MOD_h
            //#MOD_h
            this.herewegoKeyTextBox.Text = settings.HereWeGoApiKey;

            //#MOD_g

            //DEVL_h

            //#MOD_i


            this.shrinkTMCGridSquaresTextBox.Text = Convert.ToString(settings.ShrinkTMCGridSquareCoords, CultureInfo.InvariantCulture);

            // Image processing
            this.imageProcessingEnabledCheckBox.Checked = settings.EnableImageProcessing.Value;

            this.imgProcBrightnessSlider.Value = settings.BrightnessAdjustment.Value;
            this.imgProcBrightnessTextBox.Text = settings.BrightnessAdjustment.Value.ToString();

            this.imgProcContrastSlider.Value = settings.ContrastAdjustment.Value;
            this.imgProcContrastTextBox.Text = settings.ContrastAdjustment.Value.ToString();

            this.imgProcSaturationSlider.Value = settings.SaturationAdjustment.Value;
            this.imgProcSaturationTextBox.Text = settings.SaturationAdjustment.Value.ToString();

            this.imgProcSharpnessSlider.Value = settings.SharpnessAdjustment.Value;
            this.imgProcSharpnessTextBox.Text = settings.SharpnessAdjustment.Value.ToString();

            this.imgProcRedSlider.Value = settings.RedAdjustment.Value;
            this.imgProcRedTextBox.Text = settings.RedAdjustment.Value.ToString();
            this.imgProcGreenSlider.Value = settings.GreenAdjustment.Value;
            this.imgProcGreenTextBox.Text = settings.GreenAdjustment.Value.ToString();
            this.imgProcBlueSlider.Value = settings.BlueAdjustment.Value;
            this.imgProcBlueTextBox.Text = settings.BlueAdjustment.Value.ToString();

            //#MOD_i
            this.imageRemoveAlphaChannelCheckBox.Checked = settings.RemoveAlphaChannelAdjustment.Value;

            // Enable or disable sliders depending on whether image processing is enabled
            if (this.imageProcessingEnabledCheckBox.Checked)
            {
                this.ToggleImageProcessingControlsEnabled(true);
            }
            else
            {
                this.ToggleImageProcessingControlsEnabled(false);
            }

        }

        private void ToggleImageProcessingControlsEnabled(bool enabled)
        {
            this.imgProcBrightnessSlider.Enabled = enabled;
            this.imgProcBrightnessTextBox.Enabled = enabled;

            this.imgProcContrastSlider.Enabled = enabled;
            this.imgProcContrastTextBox.Enabled = enabled;

            this.imgProcSaturationSlider.Enabled = enabled;
            this.imgProcSaturationTextBox.Enabled = enabled;

            this.imgProcSharpnessSlider.Enabled = enabled;
            this.imgProcSharpnessTextBox.Enabled = enabled;

            this.imgProcRedSlider.Enabled = enabled;
            this.imgProcRedTextBox.Enabled = enabled;
            this.imgProcGreenSlider.Enabled = enabled;
            this.imgProcGreenTextBox.Enabled = enabled;
            this.imgProcBlueSlider.Enabled = enabled;
            this.imgProcBlueTextBox.Enabled = enabled;
        }

        private void folderBrowserDialog1_HelpRequest(object sender, EventArgs e)
        {

        }

        private void workingFolderButton_Click(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;
            //MOD_i
            this.folderBrowserDialog1.SelectedPath = this.workingFolderTextBox.Text;

            DialogResult result = this.folderBrowserDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                this.workingFolderTextBox.Text = folderBrowserDialog1.SelectedPath;
                //#FIX_f (else Cancel would not work)
                //settings.WorkingDirectory = this.workingFolderTextBox.Text;
            }
        }

        private void afsUserFolderButton_Click(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;
            //MOD_i
            this.folderBrowserDialog1.SelectedPath = this.afs2UserFolderTextBox.Text;

            DialogResult result = this.folderBrowserDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                this.afs2UserFolderTextBox.Text = folderBrowserDialog1.SelectedPath;
                //#FIX_f (else Cancel would not work)
                //settings.AFS2UserDirectory = this.afsUserFolderTextBox.Text;
            }
        }

        //#MOD_h

        private string pathWithTrailingDirectorySeparatorChar(string path)
        {
            if (!String.IsNullOrEmpty(path))
            {
                // They're always one character but EndsWith is shorter than
                // array style access to last path character. Change this
                // if performance are a (measured) issue.
                string separator1 = Path.DirectorySeparatorChar.ToString();
                string separator2 = Path.AltDirectorySeparatorChar.ToString();

                // Trailing white spaces are always ignored but folders may have
                // leading spaces. It's unusual but it may happen. If it's an issue
                // then just replace TrimEnd() with Trim(). Tnx Paul Groke to point this out.
                path = path.TrimEnd();

                // Argument is always a directory name then if there is one
                // of allowed separators then I have nothing to do.
                if (path.EndsWith(separator1) || path.EndsWith(separator2))
                    return path;

                // If there is the "alt" separator then I add a trailing one.
                // Note that URI format (file://drive:\path\filename.ext) is
                // not supported in most .NET I/O functions then we don't support it
                // here too. If you have to then simply revert this check:
                // if (path.Contains(separator1))
                //     return path + separator1;
                //
                // return path + separator2;
                if (path.Contains(separator2))
                    return path + separator2;

                // If there is not an "alt" separator I add a "normal" one.
                // It means path may be with normal one or it has not any separator
                // (for example if it's just a directory name). In this case I
                // default to normal as users expect.
                return path + separator1;
            }

            return path;

        }

        private void maxTilesPerStitchedImageTextBox_TextChanged(object sender, EventArgs e)
        {
            var numbersOnly = this.GetInteger(this.maxTilesPerStitchedImageTextBox.Text);

            if (this.maxTilesPerStitchedImageTextBox.Text != numbersOnly)
            {
                this.maxTilesPerStitchedImageTextBox.Text = numbersOnly;
            }

            int maxTiles = 0;
            if (int.TryParse(this.maxTilesPerStitchedImageTextBox.Text, out maxTiles))
            {
                int resolution = 256 * maxTiles;
                this.maxTilesPerStitchedImageInfoLabel.Text = string.Format("tiles x {0} tiles. ({1}px x {2}px)", maxTiles, resolution, resolution);
            }

        }

        private string GetInteger(string input)
        {
            return new string(input.Where(c => char.IsDigit(c)).ToArray());
        }

        private string GetDecimal(string input)
        {
            return new string(input.Where(c => char.IsDigit(c) || c == '.').ToArray());
        }

        private string GetSignedInteger(string input)
        {
            return new string(input.Where(c => char.IsDigit(c) || c == '-').ToArray());
        }

        private void downloadWaitTextBox_TextChanged(object sender, EventArgs e)
        {
            var numbersOnly = this.GetInteger(this.downloadWaitTextBox.Text);

            if (this.downloadWaitTextBox.Text != numbersOnly)
            {
                this.downloadWaitTextBox.Text = numbersOnly;
            }
        }

        private void downloadWaitRandomTextBox_TextChanged(object sender, EventArgs e)
        {
            var numbersOnly = this.GetInteger(this.downloadWaitRandomTextBox.Text);

            if (this.downloadWaitRandomTextBox.Text != numbersOnly)
            {
                this.downloadWaitRandomTextBox.Text = numbersOnly;
            }
        }

        private void ShrinkTMCGridSquaresTextBox_TextChanged(object sender, EventArgs e)
        {
            var numbersOnly = this.GetDecimal(this.shrinkTMCGridSquaresTextBox.Text);

            if (this.shrinkTMCGridSquaresTextBox.Text != numbersOnly)
            {
                this.shrinkTMCGridSquaresTextBox.Text = numbersOnly;
            }
        }

        private void AddUserFolderToConfigButton_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }


        private async void UpdateImagePreview()
        {
            if (this.updateImagePreview)
            {
                if (this.imageProcessingPreviewForm != null)
                {
                    if (this.imageProcessingPreviewForm.Visible)
                    {
                        var imageProcessingSettings = new ImageProcessingSettings();
                        imageProcessingSettings.BrightnessAdjustment = this.imgProcBrightnessSlider.Value;
                        imageProcessingSettings.ContrastAdjustment = this.imgProcContrastSlider.Value;
                        imageProcessingSettings.SaturationAdjustment = this.imgProcSaturationSlider.Value;
                        imageProcessingSettings.SharpnessAdjustment = this.imgProcSharpnessSlider.Value;
                        imageProcessingSettings.RedAdjustment = this.imgProcRedSlider.Value;
                        imageProcessingSettings.GreenAdjustment = this.imgProcGreenSlider.Value;
                        imageProcessingSettings.BlueAdjustment = this.imgProcBlueSlider.Value;

                        await this.imageProcessingPreviewForm.UpdateImage(imageProcessingSettings);
                    }
                }
            }

        }


        private void imgProcBrightnessSlider_ValueChanged(object sender, EventArgs e)
        {
            if (imgProcBrightnessTextBox.Text != imgProcBrightnessSlider.Value.ToString())
            {
                imgProcBrightnessTextBox.Text = imgProcBrightnessSlider.Value.ToString();
            }

            this.UpdateImagePreview();
        }


        private void imgProcContrastSlider_ValueChanged(object sender, EventArgs e)
        {
            if (imgProcContrastTextBox.Text != imgProcContrastSlider.Value.ToString())
            {
                imgProcContrastTextBox.Text = imgProcContrastSlider.Value.ToString();
            }

            this.UpdateImagePreview();
        }

        private void imgProcSaturationSlider_ValueChanged(object sender, EventArgs e)
        {
            if (imgProcSaturationTextBox.Text != imgProcSaturationSlider.Value.ToString())
            {
                imgProcSaturationTextBox.Text = imgProcSaturationSlider.Value.ToString();
            }

            this.UpdateImagePreview();
        }

        private void imgProcSharpnessSlider_ValueChanged(object sender, EventArgs e)
        {
            if (imgProcSharpnessTextBox.Text != imgProcSharpnessSlider.Value.ToString())
            {
                imgProcSharpnessTextBox.Text = imgProcSharpnessSlider.Value.ToString();
            }

            this.UpdateImagePreview();
        }

        private void imgProcRedSlider_ValueChanged(object sender, EventArgs e)
        {
            if (imgProcRedTextBox.Text != imgProcRedSlider.Value.ToString())
            {
                imgProcRedTextBox.Text = imgProcRedSlider.Value.ToString();
            }

            this.UpdateImagePreview();
        }

        private void imgProcGreenSlider_ValueChanged(object sender, EventArgs e)
        {
            if (imgProcGreenTextBox.Text != imgProcGreenSlider.Value.ToString())
            {
                imgProcGreenTextBox.Text = imgProcGreenSlider.Value.ToString();
            }

            this.UpdateImagePreview();
        }

        private void imgProcBlueSlider_ValueChanged(object sender, EventArgs e)
        {
            if (imgProcBlueTextBox.Text != imgProcBlueSlider.Value.ToString())
            {
                imgProcBlueTextBox.Text = imgProcBlueSlider.Value.ToString();
            }

            this.UpdateImagePreview();
        }

        private void imgProcBrightnessTextBox_Leave(object sender, EventArgs e)
        {
            this.fixLoneNegativeSign(imgProcBrightnessTextBox);
        }

        private void imgProcContrastTextBox_Leave(object sender, EventArgs e)
        {
            this.fixLoneNegativeSign(imgProcBlueTextBox);
        }

        private void imgProcSaturationTextBox_Leave(object sender, EventArgs e)
        {
            this.fixLoneNegativeSign(imgProcSaturationTextBox);        
        }

        private void imgProcSharpessTextBox_Leave(object sender, EventArgs e)
        {
            this.fixLoneNegativeSign(imgProcSharpnessTextBox);
        }

        private void imgProcRedTextBox_Leave(object sender, EventArgs e)
        {
            this.fixLoneNegativeSign(imgProcRedTextBox);
        }

        private void imgProcGreenTextBox_Leave(object sender, EventArgs e)
        {
            this.fixLoneNegativeSign(imgProcGreenTextBox);
        }

        private void imgProcBlueTextBox_Leave(object sender, EventArgs e)
        {
            this.fixLoneNegativeSign(imgProcBlueTextBox);
        }

        private void fixLoneNegativeSign(TextBox textBox)
        {
            if (textBox.Text == "-")
            {
                textBox.Text = "0";
            }
        }

        private void imgProcTextBoxTextChanged(TextBox textBox, TrackBar slider, int minValue, int maxValue)
        {
            var validatedText = this.GetSignedInteger(textBox.Text);

            if (validatedText != "-")
            {
                int intVal;

                if (int.TryParse(validatedText, out intVal))
                {
                    if (intVal < minValue)
                        intVal = minValue;

                    if (intVal > maxValue)
                        intVal = maxValue;

                    slider.Value = intVal;
                }

                if (textBox.Text != intVal.ToString())
                {
                    textBox.Text = intVal.ToString();
                }
            }
        }

        private void imgProcBrightnessTextBox_TextChanged(object sender, EventArgs e)
        {
            this.imgProcTextBoxTextChanged(this.imgProcBrightnessTextBox, this.imgProcBrightnessSlider, -100, 100);
        }

        private void imgProcContrastTextBox_TextChanged(object sender, EventArgs e)
        {
            this.imgProcTextBoxTextChanged(this.imgProcContrastTextBox, this.imgProcContrastSlider, -100, 100);
        }

        private void imgProcSaturationTextBox_TextChanged(object sender, EventArgs e)
        {
            this.imgProcTextBoxTextChanged(this.imgProcSaturationTextBox, this.imgProcSaturationSlider, -100, 100);
        }

        private void imgProcSharpessTextBox_TextChanged(object sender, EventArgs e)
        {
            this.imgProcTextBoxTextChanged(this.imgProcSharpnessTextBox, this.imgProcSharpnessSlider, 0, 10);
        }

        private void imgProcRedTextBox_TextChanged(object sender, EventArgs e)
        {
            this.imgProcTextBoxTextChanged(this.imgProcRedTextBox, this.imgProcRedSlider, -100, 100);
        }

        private void imgProcGreenTextBox_TextChanged(object sender, EventArgs e)
        {
            this.imgProcTextBoxTextChanged(this.imgProcGreenTextBox, this.imgProcGreenSlider, -100, 100);
        }

        private void imgProcBlueTextBox_TextChanged(object sender, EventArgs e)
        {
            this.imgProcTextBoxTextChanged(this.imgProcBlueTextBox, this.imgProcBlueSlider, -100, 100);
        }

        private void imageProcessingEnabledCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (this.imageProcessingEnabledCheckBox.Checked)
            {
                this.ToggleImageProcessingControlsEnabled(true);
            }
            else
            {
                this.ToggleImageProcessingControlsEnabled(false);
                //#MOD_i
                this.imageRemoveAlphaChannelCheckBox.Checked = false;
            }
        }

        private void showPreviewWindowButton_Click(object sender, EventArgs e)
        {
            if (this.imageProcessingPreviewForm != null)
            {
                if (this.imageProcessingPreviewForm.IsDisposed)
                {
                    this.imageProcessingPreviewForm = null;
                }
                else
                {
                    this.imageProcessingPreviewForm.Show();
                }
            }

            if (this.imageProcessingPreviewForm == null)
            {
                this.imageProcessingPreviewForm = new ImageProcessingPreviewForm();
            }


            this.imageProcessingPreviewForm.StartPosition = FormStartPosition.Manual;
            this.imageProcessingPreviewForm.Height = this.Height;
            this.imageProcessingPreviewForm.Left = this.Right;
            this.imageProcessingPreviewForm.Top = this.Top;

            this.imageProcessingPreviewForm.Show();
            this.UpdateImagePreview();
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            this.updateImagePreview = false;
            this.imgProcBrightnessSlider.Value = 0;
            this.imgProcContrastSlider.Value = 0;
            this.imgProcSaturationSlider.Value = 0;
            this.imgProcSharpnessSlider.Value = 0;
            this.imgProcRedSlider.Value = 0;
            this.imgProcGreenSlider.Value = 0;
            this.imgProcBlueSlider.Value = 0;
            this.updateImagePreview = true;

            this.UpdateImagePreview();
        }
 
        private void linkLabel1_Click(object sender, EventArgs e)
        {
            //#MOD_h
            //System.Diagnostics.Process.Start("https://www.linz.govt.nz/data/linz-data-service/guides-and-documentation/creating-an-api-key");
            System.Diagnostics.Process.Start("https://basemaps.linz.govt.nz/?i=nz-satellite-2021-2022-10m#@-41.3768088,172.9687500,z5.2493");
        }
 



        private void tabPage5_Click(object sender, EventArgs e)
        {

        }

        private void groupBox8_Enter(object sender, EventArgs e)
        {

        }

        private void groupBox6_Enter(object sender, EventArgs e)
        {

        }

        private void linzKeyTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://account.mapbox.com/auth/signup/");
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void afsSDKFolderTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void groupBox7_Enter(object sender, EventArgs e)
        {

        }

        private void label29_Click(object sender, EventArgs e)
        {

        }
        //#MOD_g
        //#MOD_g

        //#MOD_g

        private void tabPage4_Click(object sender, EventArgs e)
        {

        }

        private void imgProcSharpnessSlider_Scroll(object sender, EventArgs e)
        {

        }
        //#MOD_g

        private void label32_Click(object sender, EventArgs e)
        {

        }

        private void label31_Click(object sender, EventArgs e)
        {

        }

        private void workingFolderTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {

        }

        private void groupBox9_Enter(object sender, EventArgs e)
        {

        }

        private void simultaneousDownloadsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void groupBox10_Enter(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void label36_Click(object sender, EventArgs e)
        {

        }


        private void label38_Click(object sender, EventArgs e)
        {

        }

        private void linkLabel4_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://platform.here.com/");
        }

        private void tabPage3_Click(object sender, EventArgs e)
        {

        }

        private void herewegoKeyTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void afs2UserFolderTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void label40_Click(object sender, EventArgs e)
        {

        }
        //#MOD_h
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {

        }

        private void groupBox12_Enter(object sender, EventArgs e)
        {

        }

        private void label45_Click(object sender, EventArgs e)
        {

        }

        private void label49_Click(object sender, EventArgs e)
        {

        }

        private void groupBox4_Enter(object sender, EventArgs e)
        {

        }

        private void label42_Click(object sender, EventArgs e)
        {

        }

        private void afs4UserFolderTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void afsSceneryFolderTextBox_TextChanged(object sender, EventArgs e)
        {
            //#MOD_i
            this.afsSceneryFolderTextBox.Text = pathWithTrailingDirectorySeparatorChar(this.afsSceneryFolderTextBox.Text);
            this.afsSceneryFolderTextBox.Text = this.afsSceneryFolderTextBox.Text.ToLower();
            this.afsSceneryFolderTextBox.Text = this.afsSceneryFolderTextBox.Text.Replace("\\\\", "\\");
        }
    }
}
