using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using AeroScenery.AFS2;

/// <summary>
/// Checks WicScanlineSource against GDI+ on a real stitched PNG, for both bytes and memory.
///
///     probe.ps1 "path\to\g_15_stitch_1_1.png"
///
/// Run one mode per process, because peak memory is a process-lifetime number and the two would
/// contaminate each other. The point is that both modes print the same SHA-256 of the same RGB
/// pixels while their peaks differ by an order of magnitude - correctness and the memory claim in
/// one run, on a real file rather than a synthetic one.
/// </summary>
internal static class ProbeReader
{
    private const int BandRows = 2048;

    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: ProbeReader <wic|gdi> <image.png>");
            return 2;
        }

        string mode = args[0].ToLowerInvariant();
        string path = args[1];
        var sw = Stopwatch.StartNew();
        string hash = mode == "gdi" ? HashWithGdiPlus(path) : HashWithWic(path);
        sw.Stop();

        var p = Process.GetCurrentProcess();
        Console.WriteLine("  mode        {0}", mode);
        Console.WriteLine("  sha256      {0}", hash);
        Console.WriteLine("  elapsed     {0:f2} s", sw.Elapsed.TotalSeconds);
        Console.WriteLine("  peak wset   {0:n0} MB", p.PeakWorkingSet64 / (1024 * 1024));
        Console.WriteLine("  peak paged  {0:n0} MB", p.PeakPagedMemorySize64 / (1024 * 1024));
        return 0;
    }

    private static string HashWithWic(string path)
    {
        using (var src = new WicScanlineSource(path))
        using (var sha = SHA256.Create())
        {
            Console.WriteLine("  size        {0} x {1}", src.Width, src.Height);

            var band = new byte[(long)BandRows * src.Width * 3];
            int row = 0;
            while (row < src.Height)
            {
                int rows = Math.Min(BandRows, src.Height - row);
                src.ReadRows(band, 0, rows);
                int n = rows * src.Width * 3;
                sha.TransformBlock(band, 0, n, null, 0);
                row += rows;
            }
            sha.TransformFinalBlock(new byte[0], 0, 0);
            return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>The pattern the app uses today, for comparison. Loads the whole image.</summary>
    private static string HashWithGdiPlus(string path)
    {
        using (var img = Image.FromFile(path))
        using (var bmp = new Bitmap(img))
        using (var sha = SHA256.Create())
        {
            Console.WriteLine("  size        {0} x {1}", bmp.Width, bmp.Height);

            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var srcRow = new byte[Math.Abs(data.Stride)];
                var dstRow = new byte[bmp.Width * 3];
                for (int y = 0; y < bmp.Height; y++)
                {
                    IntPtr p = new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride);
                    System.Runtime.InteropServices.Marshal.Copy(p, srcRow, 0, srcRow.Length);
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        // GDI+ 32bppArgb is B,G,R,A in memory - same order WIC calls Bgra32
                        dstRow[x * 3] = srcRow[x * 4 + 2];
                        dstRow[x * 3 + 1] = srcRow[x * 4 + 1];
                        dstRow[x * 3 + 2] = srcRow[x * 4];
                    }
                    sha.TransformBlock(dstRow, 0, dstRow.Length, null, 0);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            sha.TransformFinalBlock(new byte[0], 0, 0);
            return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
