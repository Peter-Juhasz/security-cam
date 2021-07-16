using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Windows.Media.Capture;
using Windows.Media.MediaProperties;

namespace SecurityCamera.Console
{
    class RecordingWorker : BackgroundService
    {
        public RecordingWorker(
            BlobServiceClient blobServiceClient,
            IOptions<BlobsOptions> blobsOptions,
            IOptions<EncodingOptions> encodingOptions,
            ILogger<RecordingWorker> logger
        )
        {
            BlobServiceClient = blobServiceClient;
            BlobsOptions = blobsOptions;
            EncodingOptions = encodingOptions;
            Logger = logger;
        }

        private BlobServiceClient BlobServiceClient { get; }
        private IOptions<BlobsOptions> BlobsOptions { get; }
        private IOptions<EncodingOptions> EncodingOptions { get; }
        private ILogger<RecordingWorker> Logger { get; }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // set up encoding
            Logger.LogInformation($"Initializing encoding...");
            var encodingOptions = EncodingOptions.Value;
            var profile = encodingOptions.Format switch
            {
                "HEVC" => MediaEncodingProfile.CreateHevc(encodingOptions.Quality),
                "MP4" => MediaEncodingProfile.CreateMp4(encodingOptions.Quality),
                "WMV" => MediaEncodingProfile.CreateWmv(encodingOptions.Quality),
                _ => throw new NotSupportedException($"Encoding format '{encodingOptions.Format}' is not supported.")
            };

            // timestamp
            var now = DateTimeOffset.Now;
            Logger.LogInformation($"Current timestamp: {now}");

            // create blob
            Logger.LogInformation($"Initializing blob...");
            var blobsOptions = BlobsOptions.Value;
            var container = BlobServiceClient.GetBlobContainerClient(blobsOptions.ContainerName);
            var blob = container.GetBlockBlobClient($"{now:yyyy/MM/dd/HH-mm-ss}.mp4");
            var httpHeaders = new BlobHttpHeaders
            {
                ContentType = "video/mp4",
            };
            await using var stream = await blob.OpenWriteAsync(overwrite: false, new()
            {
                HttpHeaders = httpHeaders,
            }, cancellationToken);
            using var randomAccessStream = stream.AsRandomAccessStream();

            // face detection
            // TODO

            // record
            Logger.LogInformation($"Initializing media capture...");
            using var capture = new MediaCapture();
            await capture.InitializeAsync();

            Logger.LogInformation($"Starting recording...");
            await capture.StartRecordToStreamAsync(profile, randomAccessStream);

            Logger.LogInformation($"Recording started.");
            await Task.Delay(TimeSpan.FromMilliseconds(-1), cancellationToken);

            Logger.LogInformation($"Stopping recording...");
            var result = await capture.StopRecordWithResultAsync();
            await randomAccessStream.FlushAsync();
            await stream.FlushAsync();
            Logger.LogInformation($"Recording stopped. Recorded '{result.RecordDuration}'.");
        }
    }
}
