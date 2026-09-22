using AeroScenery.AFS2;
using AeroScenery.Common;
using AeroScenery.OrthophotoSources;
using AeroScenery.OrthophotoSources.Switzerland;
using AeroScenery.OrthoPhotoSources;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public async Task DownloadImageTiles(OrthophotoSource orthophotoSource, List<ImageTile> imageTiles, IProgress<DownloadThreadProgress> threadProgress,
            string downloadDirectory, GenericOrthophotoSource orthophotoSourceInstance, int downloadThreads)
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

                var tilesMissing = this.tilesUnavailable + this.tilesFailed + this.tilesNotAnImage;

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

        private void SaveImageTileAeroFile(XmlSerializer xmlSerializer, ImageTile imageTile, string path)
        {
            using (TextWriter tw = new StreamWriter(path + imageTile.FileName + ".aero"))
            {
                xmlSerializer.Serialize(tw, imageTile);
            }
        }

    }
}
