using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AeroScenery.AFS2
{
    public class AIDFile
    {
        public string ImageFile { get; set; }
        public bool FlipVertical { get; set; }

        public double StepsPerPixelX { get; set; }
        public double StepsPerPixelY { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>
        /// Reads an .aid back. The app only ever wrote these, because GeoConvert was the only
        /// thing that read them; the in-app converter needs the georeferencing out of them, and
        /// out of files written by earlier runs as well as this one.
        ///
        /// X and Y are the top left corner in lon/lat, and StepsPerPixelY is normally negative
        /// because the image runs north to south while latitude runs the other way.
        /// </summary>
        public static AIDFile Parse(string path)
        {
            return ParseText(File.ReadAllText(path, System.Text.Encoding.UTF8),
                Path.GetFileName(path));
        }

        public static AIDFile ParseText(string text, string nameForErrors = "aid")
        {
            var aid = new AIDFile();
            bool haveSteps = false;
            bool haveTopLeft = false;

            foreach (var f in TmFields.Parse(text))
            {
                switch (f.Name)
                {
                    case "image":
                        aid.ImageFile = f.Value;
                        break;
                    case "flip_vertical":
                        aid.FlipVertical = TmFields.Bool(f.Value);
                        break;
                    case "steps_per_pixel":
                    {
                        double sx, sy;
                        if (TmFields.TryPair(f.Value, out sx, out sy))
                        {
                            aid.StepsPerPixelX = sx;
                            aid.StepsPerPixelY = sy;
                            haveSteps = true;
                        }
                        break;
                    }
                    case "top_left":
                    {
                        double x, y;
                        if (TmFields.TryPair(f.Value, out x, out y))
                        {
                            aid.X = x;
                            aid.Y = y;
                            haveTopLeft = true;
                        }
                        break;
                    }
                }
            }

            if (String.IsNullOrEmpty(aid.ImageFile) || !haveSteps || !haveTopLeft)
            {
                throw new InvalidDataException(String.Format(
                    "{0} is missing image, steps_per_pixel or top_left", nameForErrors));
            }
            if (aid.StepsPerPixelX == 0 || aid.StepsPerPixelY == 0)
            {
                throw new InvalidDataException(String.Format(
                    "{0} has a zero steps_per_pixel, so it maps every pixel to one coordinate",
                    nameForErrors));
            }

            return aid;
        }


        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<[file][][]");
            sb.AppendLine("\t<[tm_aerial_image_definition][][]");
            sb.AppendLine(String.Format("\t\t<[string8][image][{0}]>", ImageFile));
            sb.AppendLine("\t\t<[string8][mask][]>");
            sb.AppendLine(String.Format("\t\t<[vector2_float64][steps_per_pixel][{0} {1}]>", 
                StepsPerPixelX.ToString("0.###################################################################################################################################################################################################################################################################################################################################################e-00", CultureInfo.InvariantCulture), 
                StepsPerPixelY.ToString("0.###################################################################################################################################################################################################################################################################################################################################################e-00", CultureInfo.InvariantCulture)));
            sb.AppendLine(String.Format("\t\t<[vector2_float64][top_left][{0} {1}]>", X.ToString(CultureInfo.InvariantCulture), Y.ToString(CultureInfo.InvariantCulture)));
            sb.AppendLine("\t\t<[string8][coordinate_system][lonlat]>");
            sb.AppendLine(String.Format("\t\t<[bool][flip_vertical][{0}]>", FlipVertical.ToString().ToLower()));
            sb.AppendLine("\t>");
            sb.AppendLine(">");

            return sb.ToString();
        }

    }
}
