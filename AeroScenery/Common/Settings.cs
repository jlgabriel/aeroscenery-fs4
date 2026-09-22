using AeroScenery.OrthoPhotoSources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AeroScenery.Common
{
    public enum ActionSet
    {
        Default,
        Custom
    }

    public class Settings
    {
        public Settings()
        {
            this.OrthophotoSourceSettings = new OrthophotoSourceSettings();
        }

        public string AFS2Directory { get; set; }

        public string WorkingDirectory { get; set; }

        public OrthophotoSource? OrthophotoSource { get; set; }

        public int? ZoomLevel { get; set; }

        public bool? DownloadImageTiles { get; set; }

        public bool? StitchImageTiles { get; set; }

        public bool? GenerateAIDAndTMCFiles { get; set; }

        /// <summary>
        /// Whether the Convert action runs. The XML name is the one the setting had when the
        /// action ran IPACS GeoConvert, so that an existing settings.xml keeps its value.
        /// </summary>
        [XmlElement("RunGeoConvert")]
        public bool? RunConverter { get; set; }

        //#MOD_i

        //#MOD_h


        //#MOD_g

        public bool? DeleteStitchedImageTiles { get; set; }

        public bool? InstallScenery { get; set; }

        public ActionSet? ActionSet { get; set; }

        public List<int> AFSLevelsToGenerate { get; set; }

        public string UserAgent { get; set; }


        public int? DownloadWaitMs { get; set; }

        public int? DownloadWaitRandomMs { get; set; }

        public int? SimultaneousDownloads { get; set; }

        public int? MaximumStitchedImageSize { get; set; }

        /// <summary>
        /// Whether the converter writes a _mask.ttc beside each tile that is only partly covered,
        /// so that Aerofly's own imagery shows through where there is no photo. The XML name is
        /// the one the setting had in the GeoConvert era, kept so an existing settings.xml still
        /// reads.
        /// </summary>
        [XmlElement("GeoConvertWriteImagesWithMask")]
        public bool? WriteImagesWithMask { get; set; }

        /// <summary>
        /// Threads the built-in converter may use for BC1 encoding. 0 means automatic, which is
        /// half the logical processors.
        ///
        /// A setting rather than a constant because saturating the machine is not always what is
        /// wanted: being able to keep working while a square builds can matter more than finishing
        /// it soonest, and only the person at the keyboard knows which.
        /// </summary>
        public int? ConverterThreads { get; set; }

        /// <summary>
        /// Whether the converter stops the photoscenery at the coastline drawn with Draw Coast on
        /// the Map tab. It does nothing until a line exists, and it never cuts a grid square that
        /// is not wholly inside the stretch of coast the line covers. See AeroSceneryManager.CoastFor.
        /// </summary>
        public bool? CutAtCoastline { get; set; }


        public string LinzApiKey { get; set; }

        //#MOD_e
        public string MapboxApiKey { get; set; }
        //#MOD_h
        public string HereWeGoApiKey { get; set; }

        public int? MapControlLastZoomLevel { get; set;}
        public double? MapControlLastX { get; set; }
        public double? MapControlLastY { get; set; }
        public string MapControlLastMapType { get; set; }
        //#MOD_l
        // The two sides of the Map Type button's satellite <-> drawn toggle, remembered separately so
        // the one you are not looking at is still your choice when you flip back. Menu item tags, not
        // provider names - MapControlLastMapType above stays the provider name it always was.
        public string MapControlLastImageryMapType { get; set; }
        public string MapControlLastDrawnMapType { get; set; }
        public double? ShrinkTMCGridSquareCoords { get; set; }
        public string AFS2UserDirectory { get; set; }


        //#MOD_i
        public string AFSSceneryFolder { get; set; }

        // Image procesing
        public bool? EnableImageProcessing { get; set; }
        public int? BrightnessAdjustment { get; set; }
        public int? ContrastAdjustment { get; set; }
        public int? SaturationAdjustment { get; set; }
        public int? SharpnessAdjustment { get; set; }
        public int? RedAdjustment { get; set; }
        public int? GreenAdjustment { get; set; }
        public int? BlueAdjustment { get; set; }

        //#MOD_i
        public bool? RemoveAlphaChannelAdjustment { get; set; }

        public OrthophotoSourceSettings OrthophotoSourceSettings { get; set; }


        //#MOD_g

        //#MOD_h

        //#MOD_i



    }
}
