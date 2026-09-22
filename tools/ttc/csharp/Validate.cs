using System;
using System.IO;
using System.Security.Cryptography;
using AeroScenery.AFS2;

/// <summary>
/// Validation suite for the C# .ttc writer, mirroring tools/ttc/validate_ttc.py.
///
/// Two jobs. First, the same checks the Python suite makes, against the same committed
/// GeoConvert fixtures - including rebuilding both reference files byte for byte, which is what
/// proves the header construction is right rather than merely plausible.
///
/// Second, and the reason this exists at all: it prints SHA-256 hashes of BC1 output over a
/// fixed test pattern. tools/ttc/cross_check.py prints the same hashes from the Python
/// reference. If they match, the two encoders agree bit for bit and the port is faithful - a
/// stronger statement than "both look reasonable", and the one thing a port can quietly get
/// wrong in a way no PSNR check would reveal.
/// </summary>
internal static class Validate
{
    private static int _passed;
    private static int _failed;

    private static void Check(string label, bool ok, string detail = "")
    {
        if (ok) { _passed++; Console.WriteLine("  PASS  " + label + (detail.Length > 0 ? "   " + detail : "")); }
        else { _failed++; Console.WriteLine("  FAIL  " + label + (detail.Length > 0 ? "   " + detail : "")); }
    }

    private static string Sha(byte[] data)
    {
        using (var sha = SHA256.Create())
        {
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// The shared cross-language test pattern. Pure integer arithmetic so C# and Python produce
    /// identical bytes - a numpy RNG could not be reproduced here, and a single content type
    /// would leave encoder paths untested. Four quadrants exercise the four cases that matter:
    /// a smooth gradient, a flat non-lattice colour, incompressible noise, and hard block edges.
    /// </summary>
    public static byte[] TestPattern(int n)
    {
        var img = new byte[n * n * 3];
        int half = n / 2;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                int i = (y * n + x) * 3;
                int r, g, b;
                if (y < half && x < half)
                {
                    r = x * 255 / Math.Max(1, half - 1);
                    g = y * 255 / Math.Max(1, half - 1);
                    b = (r + g) / 2;
                }
                else if (y < half)
                {
                    r = 37; g = 121; b = 200;      // not on the RGB565 lattice, on purpose
                }
                else if (x < half)
                {
                    uint h = (uint)(x * 374761393 + y * 668265263);
                    h = (h ^ (h >> 13)) * 1274126177u;
                    h ^= h >> 16;
                    r = (int)(h & 255); g = (int)((h >> 8) & 255); b = (int)((h >> 16) & 255);
                }
                else
                {
                    bool odd = (((x / 8) + (y / 8)) & 1) != 0;
                    r = odd ? 255 : 255; g = odd ? 0 : 255; b = odd ? 255 : 0;
                }
                img[i] = (byte)r; img[i + 1] = (byte)g; img[i + 2] = (byte)b;
            }
        }
        return img;
    }

    private static double Psnr(byte[] a, byte[] b)
    {
        double mse = 0;
        for (int i = 0; i < a.Length; i++)
        {
            double d = a[i] - b[i];
            mse += d * d;
        }
        mse /= a.Length;
        return mse == 0 ? Double.PositiveInfinity : 10 * Math.Log10(255.0 * 255.0 / mse);
    }

    public static int Main(string[] args)
    {
        string data = args.Length > 0
            ? args[0]
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(".")) ?? ".", "testdata");

        Console.WriteLine(new String('=', 74));
        Console.WriteLine("1. ROUND TRIP  -  rebuild each reference file from its own parts");
        Console.WriteLine(new String('=', 74));

        var refs = new[]
        {
            new { Name = "map_09_4580_9180.ttc",      Fmt = TtcFile.FormatDxt1, W = 2048, H = 2048, Mips = 12 },
            new { Name = "map_09_4580_9180_mask.ttc", Fmt = TtcFile.FormatL8,   W =  512, H =  512, Mips = 10 }
        };

        foreach (var r in refs)
        {
            string path = Path.Combine(data, r.Name);
            if (!File.Exists(path))
            {
                Console.WriteLine("\n  SKIP  " + r.Name + " missing - run make_reference.py");
                continue;
            }

            byte[] file = File.ReadAllBytes(path);
            TtcHeader h = TtcFile.Read(file);
            Console.WriteLine(String.Format("\n{0}  ({1:n0} bytes)", r.Name, file.Length));

            Check("header fields parse",
                h.Width == r.W && h.Height == r.H && h.NumMips == r.Mips && h.Format == r.Fmt,
                String.Format("{0}x{1} x{2} fmt={3}", h.Width, h.Height, h.NumMips, h.Format));
            Check("chunk header len is 0x40", h.ChunkHeaderLength == 0x40);
            Check("chunk magic2 matches", h.ChunkMagic2 == TtcFile.ChunkMagic2);
            Check("chunk params (21, 20)", h.ChunkParamA == 21 && h.ChunkParamB == 20);
            Check("chunk size_total == size_compressed", h.ChunkSizeTotal == h.SizeCompressed);
            Check("size_compressed accounts for whole file",
                TtcFile.HeaderLength + h.SizeCompressed == file.Length,
                String.Format("0x100 + {0:n0} == {1:n0}", h.SizeCompressed, file.Length));

            int expect = TtcFile.MipChainSize(r.W, r.H, r.Mips, r.Fmt);
            Check("size_uncompressed == exact mip chain", h.SizeUncompressed == expect,
                String.Format("{0:n0} vs computed {1:n0}", h.SizeUncompressed, expect));

            // Rebuild the chunk from the codec bytes, then the file from the chunk.
            var codec = new byte[file.Length - h.CodecOffset];
            Buffer.BlockCopy(file, h.CodecOffset, codec, 0, codec.Length);
            byte[] rebuiltChunk = TtcFile.BuildChunk(codec, h.SizeUncompressed);

            var origChunk = new byte[h.SizeCompressed];
            Buffer.BlockCopy(file, TtcFile.HeaderLength, origChunk, 0, origChunk.Length);
            Check("chunk rebuilt byte-exact", Same(rebuiltChunk, origChunk),
                String.Format("{0:n0} bytes", rebuiltChunk.Length));

            byte[] rebuilt = TtcFile.Build(h.Level, r.W, r.H, r.Mips, r.Fmt, rebuiltChunk,
                h.SizeUncompressed, h.Unk24, h.Unk28);
            Check("FILE rebuilt byte-exact", Same(rebuilt, file),
                String.Format("{0:n0} bytes", rebuilt.Length));
        }

        Console.WriteLine("\n" + new String('=', 74));
        Console.WriteLine("2. NAMING  -  filename <-> tile index");
        Console.WriteLine(new String('=', 74));

        // Levels other than 9 matter: the multiplier is 2^(16-level), not a constant 128.
        var names = new[]
        {
            new { N = "map_09_4580_9180.ttc",      L =  9, X =  139, Y =  291, M = false },
            new { N = "map_09_4580_9200.ttc",      L =  9, X =  139, Y =  292, M = false },
            new { N = "map_09_4580_9180_mask.ttc", L =  9, X =  139, Y =  291, M = true  },
            new { N = "map_09_4d80_6680.ttc",      L =  9, X =  155, Y =  205, M = false },
            new { N = "map_10_4d80_6680.ttc",      L = 10, X =  310, Y =  410, M = false },
            new { N = "map_11_4d80_6680.ttc",      L = 11, X =  620, Y =  820, M = false },
            new { N = "map_12_4d80_6680.ttc",      L = 12, X = 1240, Y = 1640, M = false },
            new { N = "map_13_4d80_6680.ttc",      L = 13, X = 2480, Y = 3280, M = false },
            new { N = "map_14_4dc0_6680.ttc",      L = 14, X = 4976, Y = 6560, M = false },
            new { N = "map_12_3230_6bd0.ttc",      L = 12, X =  803, Y = 1725, M = false }
        };

        foreach (var t in names)
        {
            int lvl, tx, ty; bool mask;
            bool ok = TtcTileName.TryParse(t.N, out lvl, out tx, out ty, out mask);
            Check("parse " + t.N, ok && lvl == t.L && tx == t.X && ty == t.Y && mask == t.M,
                String.Format("({0}, {1}, {2}, {3})", lvl, tx, ty, mask));
            Check("emit  " + t.N, TtcTileName.ForTile(t.L, t.X, t.Y, t.M) == t.N,
                TtcTileName.ForTile(t.L, t.X, t.Y, t.M));
        }

        Check("rubbish name is rejected, not thrown on", RejectsRubbish());

        Console.WriteLine("\n" + new String('=', 74));
        Console.WriteLine("3. MIP CHAIN  -  sizes against real GeoConvert output");
        Console.WriteLine(new String('=', 74));

        Check("full mip count 2048 -> 12", TtcFile.FullMipCount(2048, 2048) == 12);
        Check("full mip count 512 -> 10", TtcFile.FullMipCount(512, 512) == 10);
        Check("DXT1 2048^2 x12 == 2,796,216",
            TtcFile.MipChainSize(2048, 2048, 12, TtcFile.FormatDxt1) == 2796216,
            String.Format("{0:n0}", TtcFile.MipChainSize(2048, 2048, 12, TtcFile.FormatDxt1)));
        Check("L8 512^2 x10 == 349,525",
            TtcFile.MipChainSize(512, 512, 10, TtcFile.FormatL8) == 349525,
            String.Format("{0:n0}", TtcFile.MipChainSize(512, 512, 10, TtcFile.FormatL8)));

        Console.WriteLine("\n" + new String('=', 74));
        Console.WriteLine("3b. ENCODER QUALITY  -  BC1 is content-dependent, so test three contents");
        Console.WriteLine(new String('=', 74));

        // (33,121,206) is the RGB565 lattice point nearest (37,121,200)
        var flat565 = Fill(64, 64, 33, 121, 206);
        var dec565 = Bc1Encoder.Decode(Bc1Encoder.Encode(flat565, 64, 64), 64, 64);
        Check("flat 565-exact colour is lossless", Same(dec565, flat565));

        var flatAny = Fill(64, 64, 37, 121, 200);
        var decAny = Bc1Encoder.Decode(Bc1Encoder.Encode(flatAny, 64, 64), 64, 64);
        int maxErr = 0;
        for (int i = 0; i < decAny.Length; i++) maxErr = Math.Max(maxErr, Math.Abs(decAny[i] - flatAny[i]));
        Check("arbitrary flat colour within 565 quantisation", maxErr <= 7,
            String.Format("max channel error {0} (565 step is 8 for R/B, 4 for G)", maxErr));

        var grad = new byte[256 * 256 * 3];
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                int i = (y * 256 + x) * 3;
                grad[i] = (byte)x; grad[i + 1] = (byte)y; grad[i + 2] = (byte)((x + y) / 2);
            }
        double pg = Psnr(Bc1Encoder.Decode(Bc1Encoder.Encode(grad, 256, 256), 256, 256), grad);
        Check("smooth gradient >= 40 dB", pg >= 40, String.Format("PSNR {0:f2} dB", pg));

        Console.WriteLine("\n" + new String('=', 74));
        Console.WriteLine("4. STORED PAYLOAD  -  a .ttc with no tmcompress chunk");
        Console.WriteLine(new String('=', 74));
        Console.WriteLine("  Aerofly FS 4 rendered a tile in this shape; see docs/ttc-format.md.");

        // 8-pixel squares, so every 4x4 block is one flat colour and both colours sit on the
        // RGB565 lattice. Only then is the round trip lossless.
        var small = new byte[256 * 256 * 3];
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                int i = (y * 256 + x) * 3;
                bool odd = (((x / 8) + (y / 8)) & 1) != 0;
                small[i] = 255; small[i + 1] = (byte)(odd ? 0 : 255); small[i + 2] = (byte)(odd ? 255 : 0);
            }

        int smips;
        byte[] schain = TtcMipChain.Build(small, 256, 256, 3, TtcFile.FormatDxt1, out smips);
        byte[] stored = TtcFile.BuildStored(9, 256, 256, smips, TtcFile.FormatDxt1, schain);
        TtcHeader sh = TtcFile.Read(stored);

        Check("Read reports it stored", sh.Stored);
        Check("both size fields carry the chain length",
            sh.SizeCompressed == sh.SizeUncompressed && sh.SizeCompressed == schain.Length,
            String.Format("{0:n0}", schain.Length));
        Check("0x100 + size_compressed accounts for whole file",
            TtcFile.HeaderLength + sh.SizeCompressed == stored.Length,
            String.Format("0x100 + {0:n0} == {1:n0}", sh.SizeCompressed, stored.Length));
        Check("codec offset is 0x100, no chunk header", sh.CodecOffset == TtcFile.HeaderLength);
        Check("header still parses as format 10",
            sh.Format == TtcFile.FormatDxt1 && sh.NumMips == smips && sh.Width == 256,
            String.Format("fmt={0} mips={1}", sh.Format, sh.NumMips));

        var mip0 = new byte[64 * 64 * 8];
        Buffer.BlockCopy(stored, 0x100, mip0, 0, mip0.Length);
        Check("mip 0 decodes straight out of the file",
            Same(Bc1Encoder.Decode(mip0, 256, 256), small));
        Check("stored costs exactly the mip chain",
            stored.Length == 0x100 + TtcFile.MipChainSize(256, 256, smips, TtcFile.FormatDxt1));

        string refPath = Path.Combine(data, "map_09_4580_9180.ttc");
        if (File.Exists(refPath))
        {
            TtcHeader ch = TtcFile.Read(File.ReadAllBytes(refPath));
            Check("a real GeoConvert tile is still read as compressed", !ch.Stored,
                String.Format("ratio {0:f3}x", (double)ch.SizeUncompressed / ch.SizeCompressed));
        }

        Console.WriteLine("\n" + new String('=', 74));
        Console.WriteLine("5. INPUT FILES  -  reading .tmc and .aid back");
        Console.WriteLine(new String('=', 74));
        CheckParsers();

        Console.WriteLine("\n" + new String('=', 74));
        Console.WriteLine("6. WORLD GRID  -  lon/lat to tile, against known squares");
        Console.WriteLine(new String('=', 74));
        CheckWorldGrid();

        Console.WriteLine("\n" + new String('=', 74));
        Console.WriteLine("7. ROW ORDER  -  the flip on the way into the container");
        Console.WriteLine(new String('=', 74));
        CheckFlip();

        Console.WriteLine("\n" + new String('=', 74));
        Console.WriteLine("8. WATER FIX  -  the field the sampler applies over water");
        Console.WriteLine(new String('=', 74));
        CheckWaterFix();

        Console.WriteLine("\n" + new String('=', 74));
        Console.WriteLine("9. CROSS-LANGUAGE  -  hashes to compare with cross_check.py");
        Console.WriteLine(new String('=', 74));
        Console.WriteLine("  Run:  python tools/ttc/cross_check.py");
        Console.WriteLine("  Every hash below must match the Python reference exactly.\n");

        byte[] pattern = TestPattern(256);
        Console.WriteLine("  pattern-256      " + Sha(pattern));
        Console.WriteLine("  bc1-256          " + Sha(Bc1Encoder.Encode(pattern, 256, 256)));

        int hw, hh;
        byte[] halved = TtcMipChain.Halve(pattern, 256, 256, 3, out hw, out hh);
        Console.WriteLine("  halve-256        " + Sha(halved));

        int pmips;
        byte[] pchain = TtcMipChain.Build(pattern, 256, 256, 3, TtcFile.FormatDxt1, out pmips);
        Console.WriteLine(String.Format("  mipchain-256     {0}   ({1} mips, {2:n0} bytes)",
            Sha(pchain), pmips, pchain.Length));

        byte[] pttc = TtcFile.BuildStored(9, 256, 256, pmips, TtcFile.FormatDxt1, pchain);
        Console.WriteLine(String.Format("  stored-ttc-256   {0}   ({1:n0} bytes)", Sha(pttc), pttc.Length));

        // An L8 chain too - the mask path shares Halve but skips the encoder entirely.
        var l8 = new byte[256 * 256];
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
                l8[y * 256 + x] = (byte)((x * 7 + y * 13) & 0xFF);
        int lmips;
        byte[] lchain = TtcMipChain.Build(l8, 256, 256, 1, TtcFile.FormatL8, out lmips);
        Console.WriteLine(String.Format("  l8-mipchain-256  {0}   ({1} mips, {2:n0} bytes)",
            Sha(lchain), lmips, lchain.Length));

        Console.WriteLine("\n" + new String('=', 74));
        Console.WriteLine(String.Format("RESULT:  {0} passed, {1} failed", _passed, _failed));
        Console.WriteLine(new String('=', 74));
        return _failed > 0 ? 1 : 0;
    }

    // Verbatim from a real Santiago build - Documents\AeroScenery\working\map_09_4d80_6680.
    // Copied rather than referenced because those files are not in the repo, and abbreviated to
    // two levels. What matters is that it is the real syntax, tabs and all.
    private const string SampleTmc = @"<[file][][]
	<[tmcolormap_regions][][]
		<[string8][folder_source_files][C:\src\15-stitched\]>
		<[bool][write_ttc_files][true]>
		<[string8][folder_destination_ttc][C:\src\15-geoconvert-ttc\]>
		<[bool][write_raw_files][true]>
		<[string8][folder_destination_raw][C:\src\15-geoconvert-raw\]>
		<[bool][do_heightmaps][false]>
		<[bool][always_overwrite][true]>
		<[bool][write_images_with_mask][true]>

		<[list][region_list][]

			<[tmcolormap_region][element][0]
				<[uint32] [level] [9]>
				<[vector2_float64] [lonlat_min] [-71.005625 -33.00058106]>
				<[vector2_float64] [lonlat_max] [-70.3225 -33.56097368]>
				<[bool] [write_images_with_mask] [true]>
			>

			<[tmcolormap_region][element][0]
				<[uint32] [level] [12]>
				<[vector2_float64] [lonlat_min] [-71.005625 -33.00058106]>
				<[vector2_float64] [lonlat_max] [-70.3225 -33.56097368]>
				<[bool] [write_images_with_mask] [false]>
			>
		>
	>
>";

    private const string SampleAid = @"<[file][][]
	<[tm_aerial_image_definition][][]
		<[string8][image][g_15_stitch_1_1.png]>
		<[string8][mask][]>
		<[vector2_float64][steps_per_pixel][4.29153442382813e-05 -3.58733040502097e-05]>
		<[vector2_float64][top_left][-71.015625 -32.9902355596511]>
		<[string8][coordinate_system][lonlat]>
		<[bool][flip_vertical][false]>
	>
>";

    /// <summary>
    /// Writes a water fix field the way tools/ttc/water_fix.py does, so the reader is checked
    /// against the layout rather than against itself. Four texels a side, one degree each, north
    /// at the top.
    /// </summary>
    private static string WriteField(float[] scale, float[] offset, uint magic = WaterFixField.Magic,
        uint version = WaterFixField.Version)
    {
        string path = Path.Combine(Path.GetTempPath(), "validate_" + Guid.NewGuid().ToString("N") + ".awfx");
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(magic);
            w.Write(version);
            w.Write((uint)4);
            w.Write(0.0); w.Write(4.0);        // west, east
            w.Write(4.0); w.Write(0.0);        // north, south
            foreach (float v in scale) w.Write(v);
            foreach (float v in offset) w.Write(v);
        }
        return path;
    }

    private static void CheckWaterFix()
    {
        var scale = new float[16];
        var offset = new float[48];
        for (int i = 0; i < 16; i++) scale[i] = 1.0f;

        // One corrected texel, at row 1 column 2 - centre lon 2.5, lat 2.5.
        int t = 1 * 4 + 2;
        scale[t] = 0.5f;
        offset[t * 3] = 10.0f;
        offset[t * 3 + 1] = 20.0f;
        offset[t * 3 + 2] = 30.0f;

        string path = WriteField(scale, offset);
        try
        {
            var f = WaterFixField.Load(path);
            Check("field header parses", f.Size == 4 && f.West == 0.0 && f.East == 4.0
                && f.North == 4.0 && f.South == 0.0,
                String.Format("{0} texels, lon {1}..{2}, lat {3}..{4}",
                    f.Size, f.West, f.East, f.South, f.North));

            // The skip test. A box far from the one corrected texel must answer false, or every
            // tile in a square pays for a correction that is not there. "Far" means more than the
            // one texel of margin TouchesBox adds, which is what keeps a tile that only just
            // reaches a corrected texel from being skipped.
            Check("a box over untouched texels needs nothing",
                !f.TouchesBox(0.1, 0.4, 0.1, 0.4));
            Check("a box over the corrected texel is seen",
                f.TouchesBox(2.4, 2.6, 2.4, 2.6));
            Check("a box one texel away is still seen, because of the interpolation",
                f.TouchesBox(1.4, 1.6, 2.4, 2.6));

            // Bilinear lookup. At the texel centre the value is the texel's own.
            var cols = f.PlanColumns(new[] { 2.5 });
            var s = new float[1];
            var o = new float[3];
            f.RowFactors(2.5, cols, s, o);
            Check("at a texel centre the value is that texel's",
                Math.Abs(s[0] - 0.5f) < 1e-6 && Math.Abs(o[0] - 10.0f) < 1e-5
                && Math.Abs(o[1] - 20.0f) < 1e-5 && Math.Abs(o[2] - 30.0f) < 1e-5,
                String.Format("scale {0:0.###}, offset {1:0.#} {2:0.#} {3:0.#}", s[0], o[0], o[1], o[2]));

            // Halfway to the neighbour on the left, which is untouched, is the mean of the two.
            cols = f.PlanColumns(new[] { 2.0 });
            f.RowFactors(2.5, cols, s, o);
            Check("halfway between two texels is their mean",
                Math.Abs(s[0] - 0.75f) < 1e-6 && Math.Abs(o[0] - 5.0f) < 1e-5,
                String.Format("scale {0:0.###}, offset r {1:0.##}", s[0], o[0]));

            // Outside the field the edge is held, not switched off. See RowFactors.
            cols = f.PlanColumns(new[] { -5.0, 99.0 });
            var s2 = new float[2];
            var o2 = new float[6];
            f.RowFactors(2.5, cols, s2, o2);
            Check("outside the field the edge value is held",
                Math.Abs(s2[0] - 1.0f) < 1e-6 && Math.Abs(s2[1] - 1.0f) < 1e-6,
                String.Format("west {0:0.###}, east {1:0.###}", s2[0], s2[1]));

            Check("latitude runs the right way: row 0 is the north edge",
                RowIsNorth(f), "lat 3.5 hits an untouched row, lat 2.5 the corrected one");
        }
        finally
        {
            File.Delete(path);
        }

        // Applying it: multiply, add, clamp, round.
        Check("apply leaves an identity field alone", WaterFixField.Apply(137, 1.0f, 0.0f) == 137);
        Check("apply scales and offsets", WaterFixField.Apply(100, 0.5f, 10.0f) == 60,
            WaterFixField.Apply(100, 0.5f, 10.0f).ToString());
        Check("apply clamps below zero", WaterFixField.Apply(10, 1.0f, -50.0f) == 0);
        Check("apply clamps above 255", WaterFixField.Apply(200, 1.0f, 100.0f) == 255);
        Check("apply rounds rather than truncates", WaterFixField.Apply(100, 1.0f, 0.6f) == 101,
            WaterFixField.Apply(100, 1.0f, 0.6f).ToString());

        // Rubbish is refused loudly. A field read as something else would silently wreck a square.
        var ones = new float[16];
        for (int i = 0; i < 16; i++) ones[i] = 1.0f;
        Check("a file with the wrong magic is refused",
            Refuses(WriteField(ones, new float[48], 0xDEADBEEF, WaterFixField.Version)));
        Check("a file with a later version is refused",
            Refuses(WriteField(ones, new float[48], WaterFixField.Magic, 99)));
    }

    private static bool RowIsNorth(WaterFixField f)
    {
        var cols = f.PlanColumns(new[] { 2.5 });
        var s = new float[1];
        var o = new float[3];
        f.RowFactors(3.5, cols, s, o);          // north row, untouched
        bool north = Math.Abs(s[0] - 1.0f) < 1e-6;
        f.RowFactors(2.5, cols, s, o);          // the corrected row
        return north && Math.Abs(s[0] - 0.5f) < 1e-6;
    }

    private static bool Refuses(string path)
    {
        try
        {
            WaterFixField.Load(path);
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void CheckParsers()
    {
        TmcDocument doc = TmcReader.ParseText(SampleTmc);

        Check("tmc: both regions found", doc.Regions.Count == 2,
            String.Format("{0} regions", doc.Regions.Count));
        Check("tmc: levels in order", doc.Regions[0].Level == 9 && doc.Regions[1].Level == 12);
        Check("tmc: source folder", doc.FolderSourceFiles == @"C:\src\15-stitched\",
            doc.FolderSourceFiles);
        Check("tmc: ttc destination", doc.FolderDestinationTtc == @"C:\src\15-geoconvert-ttc\");
        Check("tmc: write_raw_files read", doc.WriteRawFiles);

        // lonlat_min carries the NORTH latitude, so reading the names literally flips the region.
        // -33.0006 is north of -33.5610; the parser has to know that, and this is the check that
        // would fail if someone "fixed" it to the obvious reading.
        var r = doc.Regions[0];
        Check("tmc: north is the larger latitude", r.North > r.South,
            String.Format("N {0} > S {1}", r.North, r.South));
        Check("tmc: north/south values", Math.Abs(r.North - (-33.00058106)) < 1e-9
            && Math.Abs(r.South - (-33.56097368)) < 1e-9);
        Check("tmc: west is the smaller longitude", r.West < r.East,
            String.Format("W {0} < E {1}", r.West, r.East));
        Check("tmc: west/east values", Math.Abs(r.West - (-71.005625)) < 1e-9
            && Math.Abs(r.East - (-70.3225)) < 1e-9);

        // A per-region flag must not leak into its neighbours
        Check("tmc: per-region mask flag", doc.Regions[0].WriteImagesWithMask
            && !doc.Regions[1].WriteImagesWithMask);

        AIDFile aid = AIDFile.ParseText(SampleAid);
        Check("aid: image name", aid.ImageFile == "g_15_stitch_1_1.png", aid.ImageFile);
        Check("aid: scientific notation parses", Math.Abs(aid.StepsPerPixelX - 4.29153442382813e-05) < 1e-18,
            aid.StepsPerPixelX.ToString("e6"));
        // Negative, because the image runs north to south while latitude runs south to north
        Check("aid: latitude step is negative", aid.StepsPerPixelY < 0,
            aid.StepsPerPixelY.ToString("e6"));
        Check("aid: top left lon/lat", Math.Abs(aid.X - (-71.015625)) < 1e-9
            && Math.Abs(aid.Y - (-32.9902355596511)) < 1e-9);
        Check("aid: flip_vertical false", !aid.FlipVertical);

        // The .aid top_left must sit north and west of the region it feeds, or the source does not
        // actually cover it. This catches a lon/lat transposition, which otherwise parses happily.
        Check("aid: source starts north-west of the region",
            aid.X <= r.West + 1e-9 && aid.Y >= r.North - 1e-9,
            String.Format("aid ({0}, {1}) vs region NW ({2}, {3})", aid.X, aid.Y, r.West, r.North));

        // InvalidDataException derives from SystemException, not IOException - catching the latter
        // here silently let the exception through and killed the run.
        bool threw = false;
        try { TmcReader.ParseText("<[file][][]>"); } catch (InvalidDataException) { threw = true; }
        Check("tmc: a file with no regions is rejected", threw);

        threw = false;
        try { AIDFile.ParseText("<[file][][]>"); } catch (InvalidDataException) { threw = true; }
        Check("aid: a file with no georeferencing is rejected", threw);
    }

    private static void CheckFlip()
    {
        // Aerofly stores tile rows bottom-up: row 0 of the texture is the SOUTH edge, while
        // everything upstream works north-up. This flip is the whole of that conversion, and
        // getting it wrong renders every tile mirrored about the horizontal axis - which cost a
        // session, because each tile is internally correct and only the mosaic reads as wrong.
        //
        // Tested with an ASYMMETRIC pattern on purpose. A checkerboard mirrors to a checkerboard
        // and a centred cross is symmetric under every flip, so neither can catch this; that is
        // exactly why the simulator test needed a letter F. Here, distinct row values do it.
        const int w = 4, h = 5;
        var src = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 3;
                src[i] = (byte)(10 * y + 1);      // row identity, so a flip is visible
                src[i + 1] = (byte)x;
                src[i + 2] = 0;
            }
        }

        var flipped = TtcTileWriter.FlipRows(src, w, h, 3);

        bool rowsReversed = true;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int f = (y * w + x) * 3;
                int s = ((h - 1 - y) * w + x) * 3;
                if (flipped[f] != src[s] || flipped[f + 1] != src[s + 1])
                {
                    rowsReversed = false;
                }
            }
        }
        Check("rows come out reversed", rowsReversed);

        Check("the north row ends up last",
            flipped[((h - 1) * w) * 3] == 1 && flipped[0] == (byte)(10 * (h - 1) + 1),
            String.Format("first row now {0}, last row now {1}", flipped[0], flipped[((h - 1) * w) * 3]));

        // Columns must NOT move. A flip that also reversed x would look like a rotation, and on a
        // roughly symmetric aerial image that is far harder to spot than a mirror.
        bool colsIntact = true;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (flipped[(y * w + x) * 3 + 1] != x) colsIntact = false;
            }
        }
        Check("columns are left alone", colsIntact);

        var twice = TtcTileWriter.FlipRows(flipped, w, h, 3);
        Check("flipping twice is the identity", Same(twice, src));

        // Odd heights must not lose or duplicate the middle row
        Check("odd height keeps every row", h % 2 == 1 && twice.Length == src.Length);
    }

    private static void CheckWorldGrid()
    {
        // Two squares whose folder names on disk are known, from section 5 of the handoff.
        // SCTB is Santiago Tobalaba, and its level-9 square is the map_09_4d80_6680 that also
        // appears in the naming table above; Easter Island is the other hemisphere of longitude.
        int sctbX = (int)Math.Floor(AFS2World.GridX(-70.5478, 9));
        int sctbY = (int)Math.Floor(AFS2World.GridY(-33.4556, 9));
        Check("SCTB lands in level-9 tile (155, 205)", sctbX == 155 && sctbY == 205,
            String.Format("({0}, {1})", sctbX, sctbY));

        int eiX = (int)Math.Floor(AFS2World.GridX(-109.4219, 9));
        int eiY = (int)Math.Floor(AFS2World.GridY(-27.1648, 9));
        Check("Easter Island lands in level-9 tile (100, 215)", eiX == 100 && eiY == 215,
            String.Format("({0}, {1})", eiX, eiY));

        // y counts northward from the south pole, so a southern latitude sits below the middle.
        // Getting this backwards mirrors the world and still produces plausible-looking tiles.
        Check("southern latitude is below the equator row",
            AFS2World.GridY(-33.4556, 9) < 256 && AFS2World.GridY(33.4556, 9) > 256);
        Check("the equator is the middle row", Math.Abs(AFS2World.GridY(0, 9) - 256) < 1e-9);
        Check("the prime meridian is the middle column",
            Math.Abs(AFS2World.GridX(0, 9) - 256) < 1e-9);

        // Round trips. These would pass for a wrong-but-self-consistent projection, which is why
        // the fixed squares above matter more - but they catch an inverse that does not invert.
        foreach (double lon in new[] { -180.0, -109.4219, -70.5478, 0.0, 12.5, 179.9 })
        {
            double back = AFS2World.LonOfGridX(AFS2World.GridX(lon, 12), 12);
            Check("lon round trip " + lon, Math.Abs(back - lon) < 1e-9, back.ToString("f9"));
        }
        foreach (double lat in new[] { -70.0, -33.4556, 0.0, 27.1648, 70.0 })
        {
            double back = AFS2World.LatOfGridY(AFS2World.GridY(lat, 12), 12);
            Check("lat round trip " + lat, Math.Abs(back - lat) < 1e-9, back.ToString("f9"));
        }

        // Latitude is NOT linear in this grid. A tile at 60 degrees must not span the same
        // latitude as one at the equator - that assumption is what made a four-quadrant split
        // duplicate a whole row of tiles once already.
        double atEquator = AFS2World.LatOfGridY(2049, 12) - AFS2World.LatOfGridY(2048, 12);
        double atSixty = AFS2World.LatOfGridY(2500, 12) - AFS2World.LatOfGridY(2499, 12);
        Check("latitude is not linear in the grid", Math.Abs(atSixty - atEquator) > 1e-6,
            String.Format("{0:f6} deg at the equator vs {1:f6} further north", atEquator, atSixty));

        // Longitude, on the other hand, IS linear - which is why splitting a square by longitude
        // is safe and splitting it by latitude is not.
        double lonA = AFS2World.LonOfGridX(2049, 12) - AFS2World.LonOfGridX(2048, 12);
        double lonB = AFS2World.LonOfGridX(2500, 12) - AFS2World.LonOfGridX(2499, 12);
        Check("longitude is linear in the grid", Math.Abs(lonA - lonB) < 1e-12,
            String.Format("{0:f9} vs {1:f9}", lonA, lonB));

        // A tile range must cover the box it was asked for, edges included
        int x0, x1, y0, y1;
        AFS2World.TileRange(-71.005625, -70.3225, -33.56097368, -33.00058106, 12,
            out x0, out x1, out y0, out y1);
        Check("Santiago level 12 needs 8 x 8 tiles",
            x0 == 1240 && x1 == 1248 && y0 == 1640 && y1 == 1648,
            String.Format("x {0}..{1}  y {2}..{3}", x0, x1 - 1, y0, y1 - 1));
    }

    private static bool RejectsRubbish()
    {
        int l, x, y; bool m;
        return !TtcTileName.TryParse("stitched_image.png", out l, out x, out y, out m)
            && !TtcTileName.TryParse("map_09_zzzz_9180.ttc", out l, out x, out y, out m);
    }

    private static byte[] Fill(int w, int h, int r, int g, int b)
    {
        var img = new byte[w * h * 3];
        for (int i = 0; i < w * h; i++)
        {
            img[i * 3] = (byte)r; img[i * 3 + 1] = (byte)g; img[i * 3 + 2] = (byte)b;
        }
        return img;
    }

    private static bool Same(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
