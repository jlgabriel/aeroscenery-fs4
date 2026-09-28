using AeroScenery.AFS2;
using AeroScenery.Common;
using AeroScenery.OrthophotoSources;
using AeroScenery.OrthophotoSources.Switzerland;
using AeroScenery.OrthoPhotoSources;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AeroScenery.Download
{

    public class DownloadManager
    {
        private int downloadThreads = 4; // = Default Value (without override from Settings)

        //#MOD_e
        private int maxDownloadRetryAttempts = 10;  // 10 attempts instead of 5 for a better reliability
        private readonly ILog log = LogManager.GetLogger("AeroScenery");

        private CancellationTokenSource cancellationTokenSource;

        // Outcome of every tile in the current run. Written from all download threads, so always
        // through Interlocked.
        private int tilesSaved;
        private int tilesUnavailable;
        private int tilesFailed;
        private int tilesNotAnImage;

        // The tiles the source says it has no imagery for, in this run. The fill after the
        // download asks a lower zoom for them. A tile that failed for another reason is not here:
        // the next run asks for it again.
        private ConcurrentBag<ImageTile> tilesNoImagery = new ConcurrentBag<ImageTile>();

        /// <summary>
        /// How many zoom levels below the requested one the fill looks. Five levels down, one
        /// pixel of the lower tile covers 32 x 32 pixels of the tile it fills. That is blurred, but
        /// at sea, where the fill is used, it looks the same as the sharp imagery.
        /// </summary>
        private const int MaxFillLevels = 5;

        public DownloadManager()
        {
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void StopDownloads()
        {
            cancellationTokenSource.Cancel();
        }

        //#MOD_g
//        public async Task DownloadImageTiles(OrthophotoSource orthophotoSource, List<ImageTile> imageTiles, IProgress<DownloadThreadProgress> threadProgress, 
//            string downloadDirectory, GenericOrthophotoSource orthophotoSourceInstance)
        /// <param name="fillWanted">
        /// Which tiles with no imagery are worth filling from a lower zoom, or null for all of them.
        /// With a coastline cut, the caller passes the tiles inside the cut.
        /// </param>
        public async Task DownloadImageTiles(OrthophotoSource orthophotoSource, List<ImageTile> imageTiles, IProgress<DownloadThreadProgress> threadProgress,
            string downloadDirectory, GenericOrthophotoSource orthophotoSourceInstance, int downloadThreads,
            Func<ImageTile, bool> fillWanted = null)
        {
            //#MOD_g
            //Override numbers of Simultaneous Downloads from Settings
            this.downloadThreads = downloadThreads;

            // Each worker below blocks a thread-pool thread for the whole of its request, because
            // DownloadFile waits on GetAsync(...).Result rather than awaiting it. Past its minimum
            // the pool only injects roughly one thread per second, so a SimultaneousDownloads above
            // the core count does not just fail to help - it collapses. Measured against Bing on a
            // 28-logical-core machine: 17.6 tiles/s at 4 workers, but 6.9 at 48 with the default
            // floor and 89.3 at 48 with the floor raised. Give the pool room for every worker.
            int minWorkerThreads;
            int minCompletionPortThreads;
            ThreadPool.GetMinThreads(out minWorkerThreads, out minCompletionPortThreads);

            if (minWorkerThreads < this.downloadThreads + 8)
            {
                ThreadPool.SetMinThreads(this.downloadThreads + 8, minCompletionPortThreads);
            }

            // Reset cancellation token status
            cancellationTokenSource = new CancellationTokenSource();

            // Each run keeps its own token. The workers must not read the field: the next run
            // replaces it, and a stopped worker would then see a token that is not cancelled.
            var token = cancellationTokenSource.Token;

            // Stop clears the caller's list to free memory while the workers still index it. The
            // workers use this copy, so a stop cannot move an index out of range.
            imageTiles = new List<ImageTile>(imageTiles);

            this.tilesSaved = 0;
            this.tilesUnavailable = 0;
            this.tilesFailed = 0;
            this.tilesNotAnImage = 0;
            this.tilesNoImagery = new ConcurrentBag<ImageTile>();

            if (imageTiles.Count > 0)
            {
                log.InfoFormat("Beginning download of {0} image tiles from {1}", imageTiles.Count, orthophotoSource.ToString());

                int downloadsPerThread = imageTiles.Count / this.downloadThreads;
                int downloadsPerThreadMod = imageTiles.Count % this.downloadThreads;

                var tasks = new List<Task>();

                    // Spawn the required number of threads
                    for (int i = 0; i < this.downloadThreads; i++)
                    {
                        var threadNumber = i;

                        tasks.Add(Task.Run(async () =>
                        {
                            var xmlSerializer = new XmlSerializer(typeof(ImageTile));


                            var maxWait = AeroSceneryManager.Instance.Settings.DownloadWaitMs.Value + AeroSceneryManager.Instance.Settings.DownloadWaitRandomMs.Value;
                            var minWait = AeroSceneryManager.Instance.Settings.DownloadWaitMs.Value - AeroSceneryManager.Instance.Settings.DownloadWaitRandomMs.Value;
                            Random random = new Random(Guid.NewGuid().GetHashCode());


                            var downloadThreadProgress = new DownloadThreadProgress();
                            downloadThreadProgress.TotalFiles = downloadsPerThread;
                            downloadThreadProgress.DownloadThreadNumber = threadNumber;

                            if (threadNumber == this.downloadThreads - 1)
                            {
                                downloadThreadProgress.TotalFiles += downloadsPerThreadMod;
                            }

                        //Debug.WriteLine("Thread " + threadNumber.ToString());
                        var cookieContainer = new CookieContainer();

                            using (var handler = new HttpClientHandler()
                            {
                                CookieContainer = cookieContainer
                            })
                            {
                                using (HttpClient httpClient = new HttpClient(handler))
                                {
                                    long lastDownloadTimestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

                                // Work through this threads share of downloads
                                for (int j = 0 + (threadNumber * downloadsPerThread); j < (threadNumber + 1) * downloadsPerThread; j++)
                                    {
                                        if (token.IsCancellationRequested)
                                        {
                                            break;
                                        }

                                        int waitTime = random.Next(minWait, maxWait);
                                        var waitTimeSpan = new TimeSpan(waitTime * TimeSpan.TicksPerMillisecond);
                                        await Task.Delay(waitTimeSpan);

                                        await this.DownloadTileIfMissing(httpClient, cookieContainer, xmlSerializer, imageTiles[j], downloadDirectory,
                                            orthophotoSource, orthophotoSourceInstance, waitTimeSpan, token);

                                        downloadThreadProgress.FilesDownloaded++;
                                        threadProgress.Report(downloadThreadProgress);

                                        //Debug.WriteLine("Thread " + threadNumber.ToString() + " Index " + j.ToString());

                                    }

                                // If this is the 'last' thread, also work through the remainder 
                                if (threadNumber == this.downloadThreads - 1)
                                    {
                                        for (int k = 0; k < downloadsPerThreadMod; k++)
                                        {
                                            if (token.IsCancellationRequested)
                                            {
                                                break;
                                            }


                                            int waitTime = random.Next(minWait, maxWait);
                                            var waitTimeSpan = new TimeSpan(waitTime * TimeSpan.TicksPerMillisecond);
                                            await Task.Delay(waitTimeSpan);

                                            var index = k + (downloadsPerThread * this.downloadThreads);

                                            await this.DownloadTileIfMissing(httpClient, cookieContainer, xmlSerializer, imageTiles[index], downloadDirectory,
                                                orthophotoSource, orthophotoSourceInstance, waitTimeSpan, token);

                                            downloadThreadProgress.FilesDownloaded++;
                                            threadProgress.Report(downloadThreadProgress);

                                        //Debug.WriteLine("Thread " + threadNumber.ToString() + "Index " + k.ToString());

                                        }
                                    }

                                }

                            }

                            xmlSerializer = null;

                        }));
                    }


                await Task.WhenAll(tasks);

                if (token.IsCancellationRequested)
                {
                    log.InfoFormat("Stopped the download of {0} image tiles from {1}. Start again to resume it.", imageTiles.Count, orthophotoSource.ToString());
                    return;
                }

                log.InfoFormat("Finished download of {0} image tiles from {1}", imageTiles.Count, orthophotoSource.ToString());

                // Say plainly how much of the area we actually got. Without this a square that is
                // mostly holes looks exactly like a square that downloaded perfectly.
                log.InfoFormat("Image tile results: {0} saved, {1} with no imagery available, {2} failed, {3} discarded as not an image",
                    this.tilesSaved, this.tilesUnavailable, this.tilesFailed, this.tilesNotAnImage);

                int tilesFilled = 0;
                int tilesNotWanted = 0;

                bool fillOn = AeroSceneryManager.Instance.Settings.FillMissingTiles ?? true;

                if (orthophotoSource == OrthophotoSource.Bing && fillOn && !this.tilesNoImagery.IsEmpty)
                {
                    var noImagery = this.tilesNoImagery.ToList();
                    var wanted = fillWanted == null ? noImagery : noImagery.Where(fillWanted).ToList();
                    tilesNotWanted = noImagery.Count - wanted.Count;

                    if (tilesNotWanted > 0)
                    {
                        log.InfoFormat("{0} of the {1} tiles with no imagery are past the coastline cut, so they are not filled",
                            tilesNotWanted, noImagery.Count);
                    }

                    tilesFilled = await this.FillFromLowerZoom(wanted, downloadDirectory, token);

                    if (token.IsCancellationRequested)
                    {
                        log.Info("Stopped filling the tiles with no imagery. Start again to resume it.");
                        return;
                    }
                }

                // Tiles past the cut are not in the scenery, so they are not reported as missing.
                var tilesMissing = this.tilesUnavailable + this.tilesFailed + this.tilesNotAnImage - tilesFilled - tilesNotWanted;

                if (tilesMissing > 0)
                {
                    log.WarnFormat("{0} of {1} image tiles ({2:0.#}%) are missing from this grid square. Those areas will be black in the finished scenery.",
                        tilesMissing, imageTiles.Count, (tilesMissing * 100.0) / imageTiles.Count);
                }

            }
        }

        /// <summary>
        /// Downloads one tile unless an earlier run already has it, and writes its .aero file.
        ///
        /// A tile is complete only when both files are there. The stitcher places a tile by its
        /// .aero file, so an image without one is left out and shows black. A run that stops
        /// between the two writes leaves such an image. The next run writes the missing .aero
        /// file and does not download the image again.
        /// </summary>
        private async Task DownloadTileIfMissing(HttpClient httpClient, CookieContainer cookieContainer, XmlSerializer xmlSerializer,
            ImageTile imageTile, string downloadDirectory, OrthophotoSource orthophotoSource, GenericOrthophotoSource orthophotoSourceInstance,
            TimeSpan waitTimeSpan, CancellationToken token)
        {
            var imagePath = downloadDirectory + imageTile.FileName + "." + imageTile.ImageExtension;
            var aeroPath = downloadDirectory + imageTile.FileName + ".aero";

            bool haveImage = File.Exists(imagePath) && new FileInfo(imagePath).Length > 0;

            if (haveImage && File.Exists(aeroPath))
            {
                return;
            }

            if (!haveImage)
            {
                await this.DownloadFile(httpClient, cookieContainer, imageTile, downloadDirectory, orthophotoSource, orthophotoSourceInstance, waitTimeSpan, token);
            }

            this.SaveImageTileAeroFile(xmlSerializer, imageTile, downloadDirectory);
        }

        private async Task DownloadFile(HttpClient httpClient, CookieContainer cookieContainer, ImageTile imageTile, string path,
            OrthophotoSource orthophotoSource, GenericOrthophotoSource orthophotoSourceInstance, TimeSpan retryWaitTimeSpan,
            CancellationToken token)
        {
            string fullFilePath = path + imageTile.FileName + "." + imageTile.ImageExtension;



            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AeroSceneryManager.Instance.Settings.UserAgent);
            httpClient.DefaultRequestHeaders.Referrer = new Uri("http://google.com/");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");

            if (orthophotoSourceInstance.AdditionalHttpHeaders != null)
            {
                foreach (var key in orthophotoSourceInstance.AdditionalHttpHeaders.Keys)
                {
                    if (key.ToLower() == "referrer" || key.ToLower() == "referer")
                    {
                        httpClient.DefaultRequestHeaders.Referrer = new Uri(orthophotoSourceInstance.AdditionalHttpHeaders[key]);
                    }
                    else
                    {
                        httpClient.DefaultRequestHeaders.Add(key, orthophotoSourceInstance.AdditionalHttpHeaders[key]);
                    }
                }
            }

            try
            {
                var responseResult = httpClient.GetAsync(imageTile.URL, token);

                bool saveFile = true;


                switch (orthophotoSource)
                {
                    case OrthophotoSource.Bing:
                        // If we are Bing we might be served a valid image but there is really no tile available
                        // Check the Bing tile info header
                        if (responseResult.Result.Headers.Contains("X-VE-Tile-Info"))
                        {
                            var tileInfoHeaderValue = responseResult.Result.Headers.GetValues("X-VE-Tile-Info").FirstOrDefault();

                            // If there is really no file, the header value will be no-tile
                            // In this case we shouldn't save the image
                            if (tileInfoHeaderValue == "no-tile")
                            {
                                saveFile = false;
                                Interlocked.Increment(ref this.tilesUnavailable);
                                this.tilesNoImagery.Add(imageTile);
                                log.DebugFormat("No imagery available for tile {0}. Bing answered no-tile", imageTile.FileName);
                            }
                        }

                        break;
                    case OrthophotoSource.US_USGS:

                        // Added to fix this error
                        // https://stackoverflow.com/questions/2859790/the-request-was-aborted-could-not-create-ssl-tls-secure-channel
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                        // If we don't get a success status code or not modified, try again
                        if (!(responseResult.Result.IsSuccessStatusCode || responseResult.Result.StatusCode == HttpStatusCode.NotModified))
                        {
                            log.DebugFormat("Invalid USGS tile {0}. Status is {1}", imageTile.FileName, responseResult.Result.StatusCode);

                            for (int i = 0; i < maxDownloadRetryAttempts; i++)
                            {
                                await Task.Delay(retryWaitTimeSpan);

                                responseResult = httpClient.GetAsync(imageTile.URL, token);

                                if (responseResult.Result.IsSuccessStatusCode || responseResult.Result.StatusCode == HttpStatusCode.NotModified)
                                {
                                    break;
                                }
                                else
                                {
                                    log.DebugFormat("Invalid USGS tile {0}. Retry {1} Status is {2}", imageTile.FileName, i, responseResult.Result.StatusCode);
                                }
                            }
                        }

                        break;
                    //#MOD_d
                    case OrthophotoSource.ArcGIS:

                        // Added to fix this error
                        // https://stackoverflow.com/questions/2859790/the-request-was-aborted-could-not-create-ssl-tls-secure-channel
                        ServicePointManager.Expect100Continue = true;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                        // If we don't get a success status code or not modified, try again
                        if (!(responseResult.Result.IsSuccessStatusCode || responseResult.Result.StatusCode == HttpStatusCode.NotModified))
                        {
                            log.DebugFormat("Invalid ArcGIS tile {0}. Status is {1}", imageTile.FileName, responseResult.Result.StatusCode);

                            for (int i = 0; i < maxDownloadRetryAttempts * 3; i++) //#MOD_e: triples the number of attempts for ArcGIS to avoid missing tiles / Add. recommendation for settings:  increase the "Waiting time between Downloads" at least to 15 (instead of 10) and the "randomize" at least to 5 (instead of 3)
                            {
                                await Task.Delay(retryWaitTimeSpan);

                                responseResult = httpClient.GetAsync(imageTile.URL, token);

                                if (responseResult.Result.IsSuccessStatusCode || responseResult.Result.StatusCode == HttpStatusCode.NotModified)
                                {
                                    break;
                                }
                                else
                                {
                                    log.DebugFormat("Invalid ArcGIS tile {0}. Retry {1} Status is {2}", imageTile.FileName, i, responseResult.Result.StatusCode);
                                }
                            }
                        }

                        break;
                    //#MOD_H
                    case OrthophotoSource.CH_Geoportal:

                        // Added to fix this error
                        // https://stackoverflow.com/questions/2859790/the-request-was-aborted-could-not-create-ssl-tls-secure-channel
                        ServicePointManager.Expect100Continue = true;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                        // If we don't get a success status code or not modified, try again
                        if (!(responseResult.Result.IsSuccessStatusCode || responseResult.Result.StatusCode == HttpStatusCode.NotModified))
                        {
                            log.DebugFormat("Invalid CH_Geoportal tile {0}. Status is {1}", imageTile.FileName, responseResult.Result.StatusCode);

                            for (int i = 0; i < maxDownloadRetryAttempts; i++)
                            {
                                await Task.Delay(retryWaitTimeSpan);

                                responseResult = httpClient.GetAsync(imageTile.URL, token);

                                if (responseResult.Result.IsSuccessStatusCode || responseResult.Result.StatusCode == HttpStatusCode.NotModified)
                                {
                                    break;
                                }
                                else
                                {
                                    log.DebugFormat("Invalid CH_Geoportal tile {0}. Retry {1} Status is {2}", imageTile.FileName, i, responseResult.Result.StatusCode);
                                }
                            }
                        }

                        break;

                    //#MOD_h
                    case OrthophotoSource.HereWeGo:

                        // Added to fix this error
                        // https://stackoverflow.com/questions/2859790/the-request-was-aborted-could-not-create-ssl-tls-secure-channel
                        ServicePointManager.Expect100Continue = true;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                        // If we don't get a success status code or not modified, try again
                        if (!(responseResult.Result.IsSuccessStatusCode || responseResult.Result.StatusCode == HttpStatusCode.NotModified))
                        {
                            log.DebugFormat("Invalid HereWeGo tile {0}. Status is {1}", imageTile.FileName, responseResult.Result.StatusCode);

                            for (int i = 0; i < maxDownloadRetryAttempts * 3; i++) //#MOD_h: triples the number of attempts for HereWeGo to avoid missing tiles / Add. recommendation for settings:  increase the "Waiting time between Downloads" at least to 15 (instead of 10) and the "randomize" at least to 5 (instead of 3)
                            {
                                await Task.Delay(retryWaitTimeSpan);

                                responseResult = httpClient.GetAsync(imageTile.URL, token);

                                if (responseResult.Result.IsSuccessStatusCode || responseResult.Result.StatusCode == HttpStatusCode.NotModified)
                                {
                                    break;
                                }
                                else
                                {
                                    log.DebugFormat("Invalid HereWeGo tile {0}. Retry {1} Status is {2}", imageTile.FileName, i, responseResult.Result.StatusCode);
                                }
                            }
                        }

                        break;

                }


                var response = responseResult.Result;

                // A non success status means there is no image to save. This used to fall straight through
                // to the write below, so a 404 error page was saved as tile.jpg and was indistinguishable
                // from a real tile from then on.
                if (saveFile && !(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified))
                {
                    saveFile = false;

                    if (IsPermanentFailure(response.StatusCode))
                    {
                        Interlocked.Increment(ref this.tilesUnavailable);
                        this.tilesNoImagery.Add(imageTile);
                        log.DebugFormat("No imagery available for tile {0}. Status {1} from {2}",
                            imageTile.FileName, (int)response.StatusCode, imageTile.URL);
                    }
                    else
                    {
                        Interlocked.Increment(ref this.tilesFailed);
                        log.WarnFormat("Failed to download tile {0}. Status {1} from {2}",
                            imageTile.FileName, (int)response.StatusCode, imageTile.URL);
                    }
                }

                bool gzipped = false;

                if (response.Content.Headers.Contains("Content-Encoding"))
                {
                    var contentEncodingValue = response.Content.Headers.GetValues("Content-Encoding").FirstOrDefault();

                    if (contentEncodingValue == "gzip")
                    {
                        gzipped = true;
                    }

                }


                if (saveFile)
                {
                    var content = ReadResponseContent(response, gzipped);

                    // A success status is not a promise of an image. Read the magic bytes rather than
                    // trusting the status code and the file extension we are about to give this.
                    if (IsSupportedImage(content))
                    {
                        File.WriteAllBytes(fullFilePath, content);
                        Interlocked.Increment(ref this.tilesSaved);
                    }
                    else
                    {
                        Interlocked.Increment(ref this.tilesNotAnImage);
                        log.WarnFormat("Discarded tile {0}. Status {1} but the {2} byte response is not an image, from {3}",
                            imageTile.FileName, (int)response.StatusCode, content.Length, imageTile.URL);
                    }
                }


            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                // Stop cancelled the request. Nothing was saved, so the next run asks again.
            }
            catch (Exception ex)
            {
                log.Error("There was an error downloading " + imageTile.URL, ex);
            }
            finally
            {

            }
        }

        /// <summary>
        /// True when the source is telling us this tile does not exist. Asking again will not change
        /// the answer, so these are reported separately from failures that are worth a retry.
        ///
        /// Only unambiguous answers belong here. 403 in particular is left out on purpose: tile servers
        /// use it both for restricted imagery and for throttling us, and calling a throttle "no imagery
        /// available" would quietly under-report a problem the user needs to know about. Anything not
        /// listed is treated as a failure and logged as a warning, which is the safer mistake.
        /// </summary>
        private static bool IsPermanentFailure(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.NotFound
                || statusCode == HttpStatusCode.Gone;
        }

        private static byte[] ReadResponseContent(HttpResponseMessage response, bool gzipped)
        {
            using (var responseStream = response.Content.ReadAsStreamAsync().Result)
            {
                using (var buffer = new MemoryStream())
                {
                    if (gzipped)
                    {
                        using (var compressionStream = new GZipStream(responseStream, CompressionMode.Decompress))
                        {
                            compressionStream.CopyTo(buffer);
                        }
                    }
                    else
                    {
                        responseStream.CopyTo(buffer);
                    }

                    return buffer.ToArray();
                }
            }
        }

        /// <summary>
        /// Checks the leading magic bytes against the image formats orthophoto sources serve.
        /// Deliberately permissive about the format, strict about it being an image at all.
        /// </summary>
        private static bool IsSupportedImage(byte[] content)
        {
            if (content == null || content.Length < 12)
            {
                return false;
            }

            // JPEG
            if (content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            {
                return true;
            }

            // PNG
            if (content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
            {
                return true;
            }

            // GIF
            if (content[0] == 0x47 && content[1] == 0x49 && content[2] == 0x46)
            {
                return true;
            }

            // BMP
            if (content[0] == 0x42 && content[1] == 0x4D)
            {
                return true;
            }

            // TIFF, little and big endian
            if ((content[0] == 0x49 && content[1] == 0x49 && content[2] == 0x2A && content[3] == 0x00)
                || (content[0] == 0x4D && content[1] == 0x4D && content[2] == 0x00 && content[3] == 0x2A))
            {
                return true;
            }

            // WEBP, which is RIFF with a WEBP marker at offset 8
            if (content[0] == 0x52 && content[1] == 0x49 && content[2] == 0x46 && content[3] == 0x46
                && content[8] == 0x57 && content[9] == 0x45 && content[10] == 0x42 && content[11] == 0x50)
            {
                return true;
            }

            return false;
        }

        // -----------------------------------------------------------------------------------
        // the fill from a lower zoom
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// Share of the tiles to fill that one zoom level must have imagery for before the whole
        /// square is filled from it. The few tiles it does not have come from the next level down.
        /// </summary>
        private const double FillLevelCoverage = 0.98;

        /// <summary>
        /// A pixel whose channels are all at or below this is taken as no imagery. Bing fills the
        /// part of a lower tile that is outside its imagery with black. Deep water is dark but not
        /// this dark: off Cabrera it averaged (2, 11, 16).
        /// </summary>
        private const int NoImageryMax = 4;

        /// <summary>A tile to fill, with its quadkey and where the quadkey sits in its URL.</summary>
        private sealed class FillTile
        {
            public ImageTile Tile;
            public string QuadKey;
            public int UrlAt;

            /// <summary>The URL of the tile that covers this one, the given number of levels down.</summary>
            public string ParentUrl(int up)
            {
                string own = "a" + QuadKey + ".";
                return Tile.URL.Substring(0, UrlAt) + "a" + QuadKey.Substring(0, QuadKey.Length - up) + "."
                     + Tile.URL.Substring(UrlAt + own.Length);
            }

            /// <summary>Where this tile sits inside the tile the given number of levels down.</summary>
            public int ChildX(int up) { return Tile.TileX & ((1 << up) - 1); }

            public int ChildY(int up) { return Tile.TileY & ((1 << up) - 1); }
        }

        /// <summary>
        /// Gives each tile Bing has no imagery for the matching part of a tile at a lower zoom,
        /// enlarged. Returns how many tiles it filled.
        ///
        /// Bing's imagery at a high zoom often stops a short way out to sea, while its lower zooms
        /// go on. Off the Balearic Islands, zoom 17 stopped within the 3 NM of the coastline cut:
        /// round Menorca, 46% of the covered area had no tile, and the stitcher leaves a missing
        /// tile black.
        ///
        /// A lower tile covers its children exactly: for Bing, a tile's quadkey with its last digit
        /// removed is the quadkey of its parent. So the fill needs no geometry, only the URL with a
        /// shorter quadkey and a crop of the parent. Each parent is downloaded once and serves
        /// every child it covers.
        ///
        /// Three rules, each learnt on the first Balearic build:
        ///
        /// Only the tiles the caller asks for are filled. With a coastline cut, that is the tiles
        /// inside it; the rest are cut away anyway. Filling every missing tile of seven squares took
        /// 1.5 hours, and 91% of it was sea outside the cut.
        ///
        /// All tiles of a square come from one zoom level: the finest that has imagery for nearly
        /// all of them. Taking the finest level for each tile on its own mixed levels from tile to
        /// tile, and the levels are different acquisitions, so the sea came out as a patchwork of
        /// blues.
        ///
        /// A lower tile can exist and still be black where it has no imagery. Those pixels, and a
        /// few round them that JPEG has smeared, are taken from the next level down.
        ///
        /// The filled tile is written as the tile itself, so the stitcher, the converter and the
        /// coastline cut treat it like any other, and the next run does not download it again.
        /// </summary>
        private async Task<int> FillFromLowerZoom(List<ImageTile> tiles, string downloadDirectory, CancellationToken token)
        {
            var toFill = new List<FillTile>();
            foreach (var t in tiles)
            {
                string quadKey = BingOrthophotoTileHelper.TileXYToQuadKey(t.TileX, t.TileY, t.ZoomLevel);
                int at = t.URL.IndexOf("a" + quadKey + ".", StringComparison.Ordinal);
                if (at >= 0)
                {
                    toFill.Add(new FillTile { Tile = t, QuadKey = quadKey, UrlAt = at });
                }
            }

            if (toFill.Count == 0)
            {
                return 0;
            }

            // In quadkey order, so tiles that share a parent are filled close together.
            toFill.Sort((a, b) => String.CompareOrdinal(a.QuadKey, b.QuadKey));

            int zoom = toFill[0].Tile.ZoomLevel;
            int maxUp = Math.Min(MaxFillLevels, zoom - 1);
            int limit = Math.Max(1, this.downloadThreads);
            int filled = 0;
            var perLevel = new int[MaxFillLevels + 1];
            var parents = new ConcurrentDictionary<string, Lazy<Task<byte[]>>>();

            using (var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            using (var httpClient = new HttpClient(handler))
            {
                // Set once, before any request. The download above sets them per request, but there
                // each thread has a client of its own. Here all the tasks share one.
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AeroSceneryManager.Instance.Settings.UserAgent);
                httpClient.DefaultRequestHeaders.Referrer = new Uri("http://google.com/");
                httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");

                Func<string, Task<byte[]>> parent = url => parents.GetOrAdd(url,
                    u => new Lazy<Task<byte[]>>(() => this.FetchImage(httpClient, u, token))).Value;

                // The finest level with imagery for nearly every tile. Each level tried costs one
                // request per parent, and there are four times fewer parents at each level down.
                int chosen = 0, bestUp = 1, bestCount = -1;
                for (int up = 1; up <= maxUp; up++)
                {
                    int level = up;
                    await ForEachLimited(toFill.Select(f => f.ParentUrl(level)).Distinct().ToList(), limit,
                        u => parent(u), token);
                    if (token.IsCancellationRequested)
                    {
                        return 0;
                    }

                    int count = 0;
                    Parallel.ForEach(toFill, f =>
                    {
                        byte[] p = parent(f.ParentUrl(level)).Result;
                        if (p != null && NoImageryShare(p, f.ChildX(level), f.ChildY(level), level) < 0.01)
                        {
                            Interlocked.Increment(ref count);
                        }
                    });

                    log.InfoFormat("Zoom {0} has imagery for {1} of {2} tiles to fill", zoom - up, count, toFill.Count);

                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestUp = up;
                    }
                    if (count >= FillLevelCoverage * toFill.Count)
                    {
                        chosen = up;
                        break;
                    }
                }

                if (chosen == 0)
                {
                    chosen = bestUp;
                }

                log.InfoFormat("Filling {0} tiles from zoom {1}. What it lacks comes from lower levels.",
                    toFill.Count, zoom - chosen);

                await ForEachLimited(toFill, limit, async f =>
                {
                    try
                    {
                        int level = await this.FillOne(parent, f, chosen, maxUp, downloadDirectory);
                        if (level > 0)
                        {
                            Interlocked.Increment(ref filled);
                            Interlocked.Increment(ref perLevel[level]);
                        }
                    }
                    catch (Exception) when (token.IsCancellationRequested)
                    {
                    }
                    catch (Exception ex)
                    {
                        log.Error("Could not fill tile " + f.Tile.FileName, ex);
                    }
                }, token);
            }

            var levels = new List<string>();
            for (int up = 1; up <= MaxFillLevels; up++)
            {
                if (perLevel[up] > 0)
                {
                    levels.Add(String.Format("{0} from zoom {1}", perLevel[up], zoom - up));
                }
            }

            log.InfoFormat("Filled {0} of {1} tiles from lower zoom levels{2}{3}",
                filled, toFill.Count, levels.Count > 0 ? ": " : "", String.Join(", ", levels));

            return filled;
        }

        /// <summary>
        /// Fills one tile from the chosen level, and whatever that level lacks from the levels
        /// below it. Returns the first level it used, or 0 if none had any imagery.
        /// </summary>
        private async Task<int> FillOne(Func<string, Task<byte[]>> parent, FillTile f, int chosen, int maxUp,
            string downloadDirectory)
        {
            int width = f.Tile.Width, height = f.Tile.Height;
            byte[] pixels = null;
            bool[] holes = null;
            int first = 0;

            for (int up = chosen; up <= maxUp; up++)
            {
                byte[] p = await parent(f.ParentUrl(up));
                if (p == null)
                {
                    continue;
                }

                byte[] layer = Enlarge(p, f.ChildX(up), f.ChildY(up), up, width, height);
                if (layer == null)
                {
                    continue;
                }

                // A band round each black area goes too: JPEG and the enlargement smear the black
                // into its neighbours. Two pixels of the lower tile, in pixels of this one.
                bool[] layerHoles = NoImagery(layer, width, height, 2 << up);

                if (pixels == null)
                {
                    pixels = layer;
                    holes = layerHoles;
                    first = up;
                }
                else
                {
                    for (int i = 0; i < holes.Length; i++)
                    {
                        if (holes[i] && !layerHoles[i])
                        {
                            pixels[3 * i] = layer[3 * i];
                            pixels[3 * i + 1] = layer[3 * i + 1];
                            pixels[3 * i + 2] = layer[3 * i + 2];
                            holes[i] = false;
                        }
                    }
                }

                if (Array.IndexOf(holes, true) < 0)
                {
                    break;
                }
            }

            if (pixels == null)
            {
                return 0;
            }

            File.WriteAllBytes(downloadDirectory + f.Tile.FileName + "." + f.Tile.ImageExtension,
                ToJpeg(pixels, width, height));
            return first;
        }

        /// <summary>Runs a task for each item, no more than limit at a time.</summary>
        private static async Task ForEachLimited<T>(IEnumerable<T> items, int limit, Func<T, Task> body,
            CancellationToken token)
        {
            using (var slots = new SemaphoreSlim(limit))
            {
                var tasks = items.Select(async item =>
                {
                    try
                    {
                        await slots.WaitAsync(token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    try
                    {
                        await body(item);
                    }
                    finally
                    {
                        slots.Release();
                    }
                }).ToList();

                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// One tile's image, or null when the source has no imagery there or the request failed.
        /// A failed parent only means the fill tries the next level down.
        /// </summary>
        private async Task<byte[]> FetchImage(HttpClient httpClient, string url, CancellationToken token)
        {
            try
            {
                using (var response = await httpClient.GetAsync(url, token))
                {
                    IEnumerable<string> info;
                    if (response.Headers.TryGetValues("X-VE-Tile-Info", out info) && info.FirstOrDefault() == "no-tile")
                    {
                        return null;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    byte[] content = await response.Content.ReadAsByteArrayAsync();
                    return IsSupportedImage(content) ? content : null;
                }
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                log.DebugFormat("Could not fetch {0} for the fill: {1}", url, ex.Message);
                return null;
            }
        }

        private static readonly ImageCodecInfo JpegCodec =
            ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

        /// <summary>
        /// Share of the part of a parent tile that covers one child which has no imagery. Read from
        /// the parent's own pixels, without enlarging them, because it is asked for every tile at
        /// every level tried. A parent that cannot be decoded counts as all missing.
        /// </summary>
        private static double NoImageryShare(byte[] parent, int childX, int childY, int levelsUp)
        {
            try
            {
                using (var input = new MemoryStream(parent))
                using (var source = new Bitmap(input))
                {
                    int sizeX = Math.Max(1, source.Width >> levelsUp);
                    int sizeY = Math.Max(1, source.Height >> levelsUp);
                    var rect = new Rectangle(Math.Min(childX * sizeX, source.Width - sizeX),
                                             Math.Min(childY * sizeY, source.Height - sizeY), sizeX, sizeY);

                    BitmapData data = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        var row = new byte[sizeX * 3];
                        int none = 0;
                        for (int y = 0; y < sizeY; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                            for (int x = 0; x < sizeX; x++)
                            {
                                if (row[3 * x] <= NoImageryMax && row[3 * x + 1] <= NoImageryMax && row[3 * x + 2] <= NoImageryMax)
                                {
                                    none++;
                                }
                            }
                        }
                        return (double)none / (sizeX * sizeY);
                    }
                    finally
                    {
                        source.UnlockBits(data);
                    }
                }
            }
            catch (Exception)
            {
                return 1.0;
            }
        }

        /// <summary>
        /// The part of a parent tile that covers one child, enlarged to the child's size, as
        /// packed 24-bit pixels, or null if the parent cannot be decoded.
        ///
        /// Bilinear, and allowed to read past the crop into the rest of the parent, so neighbouring
        /// filled tiles join without a seam. At the parent's own edges the image is mirrored rather
        /// than faded to black.
        /// </summary>
        private static byte[] Enlarge(byte[] parent, int childX, int childY, int levelsUp, int width, int height)
        {
            try
            {
                using (var input = new MemoryStream(parent))
                using (var source = new Bitmap(input))
                using (var target = new Bitmap(width, height, PixelFormat.Format24bppRgb))
                {
                    float sizeX = (float)source.Width / (1 << levelsUp);
                    float sizeY = (float)source.Height / (1 << levelsUp);

                    using (var g = Graphics.FromImage(target))
                    using (var attributes = new ImageAttributes())
                    {
                        attributes.SetWrapMode(WrapMode.TileFlipXY);
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(source, new Rectangle(0, 0, width, height),
                            childX * sizeX, childY * sizeY, sizeX, sizeY, GraphicsUnit.Pixel, attributes);
                    }

                    var pixels = new byte[width * height * 3];
                    BitmapData data = target.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        for (int y = 0; y < height; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * width * 3, width * 3);
                        }
                    }
                    finally
                    {
                        target.UnlockBits(data);
                    }
                    return pixels;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Which pixels have no imagery, grown by a radius so the smeared edge of a black area goes
        /// with it. The growth is a square, done as two passes that each find the nearest hole.
        /// </summary>
        private static bool[] NoImagery(byte[] pixels, int width, int height, int radius)
        {
            var hole = new bool[width * height];
            bool any = false;
            for (int i = 0; i < hole.Length; i++)
            {
                if (pixels[3 * i] <= NoImageryMax && pixels[3 * i + 1] <= NoImageryMax && pixels[3 * i + 2] <= NoImageryMax)
                {
                    hole[i] = true;
                    any = true;
                }
            }

            if (!any || radius <= 0)
            {
                return hole;
            }

            var across = new bool[hole.Length];
            for (int y = 0; y < height; y++)
            {
                int last = -radius - 1;
                for (int x = 0; x < width; x++)
                {
                    if (hole[y * width + x]) last = x;
                    if (x - last <= radius) across[y * width + x] = true;
                }
                last = width + radius + 1;
                for (int x = width - 1; x >= 0; x--)
                {
                    if (hole[y * width + x]) last = x;
                    if (last - x <= radius) across[y * width + x] = true;
                }
            }

            var grown = new bool[hole.Length];
            for (int x = 0; x < width; x++)
            {
                int last = -radius - 1;
                for (int y = 0; y < height; y++)
                {
                    if (across[y * width + x]) last = y;
                    if (y - last <= radius) grown[y * width + x] = true;
                }
                last = height + radius + 1;
                for (int y = height - 1; y >= 0; y--)
                {
                    if (across[y * width + x]) last = y;
                    if (last - y <= radius) grown[y * width + x] = true;
                }
            }
            return grown;
        }

        /// <summary>Packed 24-bit pixels as a JPEG, at quality 90.</summary>
        private static byte[] ToJpeg(byte[] pixels, int width, int height)
        {
            using (var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                try
                {
                    for (int y = 0; y < height; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(pixels, y * width * 3, data.Scan0 + y * data.Stride, width * 3);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                using (var output = new MemoryStream())
                using (var parameters = new EncoderParameters(1))
                {
                    parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                    bitmap.Save(output, JpegCodec, parameters);
                    return output.ToArray();
                }
            }
        }

        private void SaveImageTileAeroFile(XmlSerializer xmlSerializer, ImageTile imageTile, string path)
        {
            using (TextWriter tw = new StreamWriter(path + imageTile.FileName + ".aero"))
            {
                xmlSerializer.Serialize(tw, imageTile);
            }
        }

    }
}
