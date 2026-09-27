using System;
using System.Collections.Generic;
using System.IO;

namespace AeroScenery.AFS2
{
    /// <summary>
    /// One stitched image plus the lon/lat mapping from its .aid, held as a moving band of rows
    /// rather than as a picture.
    ///
    /// The .aid declares a constant degrees-per-pixel in both axes, so by the .aid a source is a
    /// plain linear lon/lat raster. The output grid is not linear in latitude - see AFS2World - and
    /// that mismatch is the whole reason resampling is needed rather than a copy.
    ///
    /// A stitched image of map tiles is not linear in latitude either. Its rows are Web Mercator
    /// rows. With mercatorRows, the rows are placed as Mercator places them, between the same
    /// north and south edges that the .aid gives. Longitude is linear in both cases.
    ///
    /// The band is what makes this affordable. A tile row needs one contiguous run of source rows,
    /// and tile rows are processed north to south, so the run only ever moves one way. Consecutive
    /// tile rows share their boundary row, so the overlap is carried over instead of being re-read;
    /// without that every tile row would ask for one row it had just passed, and each of those
    /// would cost a full restart of the decoder.
    /// </summary>
    public class SourceImage : IDisposable
    {
        private readonly string imagePath;
        private IScanlineSource reader;

        private byte[] band;
        private int bandFirstRow;
        private int bandRows;

        public AIDFile Aid { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public double LonWest { get; private set; }
        public double LonEast { get; private set; }
        public double LatNorth { get; private set; }
        public double LatSouth { get; private set; }

        /// <summary>
        /// How many times the decoder had to start over. Should stay 0 for a north-to-south pass;
        /// anything else means something asked for rows it had already gone past, and paid a full
        /// re-inflate for them.
        /// </summary>
        public int Restarts { get; private set; }

        /// <summary>
        /// True when rows are read as Web Mercator rows, false when they are read as the .aid
        /// declares them, linear in latitude. See MercatorRows on TtcConverter.
        /// </summary>
        public bool MercatorRows { get; private set; }

        // Mercator y of the north edge, and source rows per unit of Mercator y
        private double mercatorNorth;
        private double mercatorRowsPerUnit;

        public SourceImage(string aidPath, bool mercatorRows = false)
        {
            Aid = AIDFile.Parse(aidPath);
            imagePath = Path.Combine(Path.GetDirectoryName(aidPath), Aid.ImageFile);

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException(String.Format(
                    "{0} names an image that is not beside it: {1}",
                    Path.GetFileName(aidPath), Aid.ImageFile), imagePath);
            }

            reader = new WicScanlineSource(imagePath);
            Width = reader.Width;
            Height = reader.Height;

            LonWest = Aid.X;
            LonEast = Aid.X + Width * Aid.StepsPerPixelX;
            LatNorth = Aid.Y;
            LatSouth = Aid.Y + Height * Aid.StepsPerPixelY;

            if (LonEast < LonWest)
            {
                double t = LonWest; LonWest = LonEast; LonEast = t;
            }
            if (LatNorth < LatSouth)
            {
                double t = LatNorth; LatNorth = LatSouth; LatSouth = t;
            }

            MercatorRows = mercatorRows;
            if (mercatorRows)
            {
                // The same edges as the linear case, written in the same order as in
                // tools/ttc/convert_tmc.py, so that the two give the same doubles.
                mercatorNorth = MercatorY(Aid.Y);
                mercatorRowsPerUnit = Height / (MercatorY(Aid.Y + Height * Aid.StepsPerPixelY) - mercatorNorth);
            }
        }

        /// <summary>
        /// Web Mercator y of a latitude in degrees, in radians of the projection. Only differences
        /// of it are used, so the scale and the sign do not matter.
        /// </summary>
        public static double MercatorY(double lat)
        {
            return Math.Log(Math.Tan(Math.PI / 4.0 + lat * Math.PI / 360.0));
        }

        /// <summary>Fractional source column for a longitude. Floor it to sample.</summary>
        public double ColOfLon(double lon)
        {
            return (lon - Aid.X) / Aid.StepsPerPixelX;
        }

        /// <summary>Fractional source row for a latitude. Floor it to sample.</summary>
        public double RowOfLat(double lat)
        {
            if (MercatorRows)
            {
                return (MercatorY(lat) - mercatorNorth) * mercatorRowsPerUnit;
            }
            return (lat - Aid.Y) / Aid.StepsPerPixelY;
        }

        public bool Covers(double west, double east, double south, double north)
        {
            return !(east <= LonWest || west >= LonEast || north <= LatSouth || south >= LatNorth);
        }

        /// <summary>
        /// Whether rows [first, endExclusive) are already in the band, so a caller can be sure
        /// sampling will not need to touch the decoder. Clamped the same way EnsureBand clamps, so
        /// asking for rows past an edge answers about what actually exists.
        /// </summary>
        public bool BandCovers(int first, int endExclusive)
        {
            if (first < 0)
            {
                first = 0;
            }
            if (endExclusive > Height)
            {
                endExclusive = Height;
            }
            if (endExclusive <= first)
            {
                return true;        // nothing to cover, so nothing is missing
            }
            return bandRows > 0 && first >= bandFirstRow && endExclusive <= bandFirstRow + bandRows;
        }

        public byte[] Band { get { return band; } }

        public int BandFirstRow { get { return bandFirstRow; } }

        public int BandRows { get { return bandRows; } }

        /// <summary>
        /// Makes rows [first, endExclusive) available in Band. Clamped to the image, so a caller
        /// may ask for rows past either edge and simply gets fewer.
        /// </summary>
        public void EnsureBand(int first, int endExclusive)
        {
            if (first < 0)
            {
                first = 0;
            }
            if (endExclusive > Height)
            {
                endExclusive = Height;
            }
            if (endExclusive <= first)
            {
                bandRows = 0;
                return;
            }

            if (bandRows > 0 && first >= bandFirstRow && endExclusive <= bandFirstRow + bandRows)
            {
                return;
            }

            int stride = Width * 3;
            int rows = endExclusive - first;

            long need = (long)rows * stride;
            if (band == null || band.Length < need)
            {
                band = new byte[need];
            }

            // Carry over whatever of the new band we already hold. This is the overlap between one
            // tile row and the next, and re-reading it would mean seeking backwards.
            int keep = 0;
            if (bandRows > 0 && first >= bandFirstRow && first < bandFirstRow + bandRows)
            {
                keep = Math.Min(rows, bandFirstRow + bandRows - first);
                // Array.Copy is defined for overlapping ranges; Buffer.BlockCopy is not.
                Array.Copy(band, (long)(first - bandFirstRow) * stride, band, 0, (long)keep * stride);
            }

            int readFrom = first + keep;
            int toRead = rows - keep;

            if (toRead > 0)
            {
                if (readFrom < reader.NextRow)
                {
                    // Backwards. PNG cannot seek, so this re-inflates from the top - counted
                    // rather than hidden, because it is the difference between one pass and many.
                    reader.Reset();
                    Restarts++;
                }
                if (readFrom > reader.NextRow)
                {
                    reader.SkipRows(readFrom - reader.NextRow);
                }

                reader.ReadRows(band, (long)keep * stride, toRead);
            }

            bandFirstRow = first;
            bandRows = rows;
        }

        /// <summary>
        /// Opens every .aid in a folder. Order is the folder's, which decides which source wins
        /// where two overlap - the first one to cover a pixel keeps it.
        /// </summary>
        public static List<SourceImage> OpenFolder(string folder, bool mercatorRows = false)
        {
            var list = new List<SourceImage>();
            var aids = Directory.GetFiles(folder, "*.aid");
            Array.Sort(aids, StringComparer.OrdinalIgnoreCase);

            foreach (var aid in aids)
            {
                list.Add(new SourceImage(aid, mercatorRows));
            }
            return list;
        }

        public void Dispose()
        {
            if (reader != null)
            {
                reader.Dispose();
                reader = null;
            }
            band = null;
            bandRows = 0;
        }
    }
}
