using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Text;
using System.Threading.Tasks;

namespace AeroScenery.Common
{
    public static class DirectoryHelper
    {
        /// <summary>
        /// My Documents folder names for the Aerofly versions we install into, newest first.
        /// </summary>
        private static readonly string[] SupportedAFSUserFolderNames =
        {
            "Aerofly FS 4",
            "Aerofly FS 2"
        };

        private const string DefaultSceneryPackageName = "aeroscenery";


        /// <summary>
        /// The Aerofly user directory to install into. Uses the configured path when there is one,
        /// otherwise looks in My Documents for a supported Aerofly version, newest first.
        /// Returns null when nothing is found.
        /// </summary>
        public static string FindAFSUserDirectory(Settings settings)
        {
            if (!String.IsNullOrEmpty(settings.AFS2UserDirectory))
            {
                return Directory.Exists(settings.AFS2UserDirectory) ? settings.AFS2UserDirectory : null;
            }

            string myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            foreach (var folderName in SupportedAFSUserFolderNames)
            {
                string candidate = Path.Combine(myDocumentsPath, folderName);

                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets, creating it if needed, the directory to install ttc files into.
        ///
        /// Scenery goes under addons\scenery\&lt;package&gt;\images\, which is the layout Aerofly
        /// add-on scenery actually ships in. Aerofly scans that tree recursively, so the folder a
        /// grid square ends up in is purely organisational, and keeping each package separate means
        /// installing one area cannot overwrite the tiles of another.
        ///
        /// Returns null when no Aerofly user directory could be found.
        /// </summary>
        public static string FindAFSSceneryInstallDirectory(Settings settings)
        {
            string afsUserDirectory = FindAFSUserDirectory(settings);

            if (afsUserDirectory == null)
            {
                return null;
            }

            string packageName = String.IsNullOrEmpty(settings.AFSSceneryFolder)
                ? DefaultSceneryPackageName
                : settings.AFSSceneryFolder;

            string imagesPath = Path.Combine(afsUserDirectory, @"addons\scenery", packageName, "images");

            if (!Directory.Exists(imagesPath))
            {
                Directory.CreateDirectory(imagesPath);
            }

            return imagesPath;
        }
    }
}
