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
using Windows.Media.Core;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;

namespace SecurityCamera.Console
{
    class RecordingWorker : BackgroundService
    {
        public RecordingWorker(
            BlobServiceClient blobServiceClient,
            IOptions<RecordingOptions> recordingOptions,
            IOptions<FaceDetectionOptions> faceDetectionOptions,
            IOptions<BlobsOptions> blobsOptions,
            IOptions<EncodingOptions> encodingOptions,
            ILogger<RecordingWorker> logger
        )
        {
            BlobServiceClient = blobServiceClient;
            RecordingOptions = recordingOptions;
            FaceDetectionOptions = faceDetectionOptions;
            BlobsOptions = blobsOptions;
            EncodingOptions = encodingOptions;
            Logger = logger;
        }

        private BlobServiceClient BlobServiceClient { get; }
        private IOptions<RecordingOptions> RecordingOptions { get; }
        private IOptions<FaceDetectionOptions> FaceDetectionOptions { get; }
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
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            var blob = container.GetBlockBlobClient(String.Format(blobsOptions.BlobNameFormat, now));
            Logger.LogInformation($"Blob: {blob.Uri}");
            var httpHeaders = new BlobHttpHeaders
            {
                ContentType = "video/mp4",
            };
            await using var stream = await blob.OpenWriteAsync(overwrite: false, new()
            {
                HttpHeaders = httpHeaders,
                BufferSize = blobsOptions.BufferSize,
            }, cancellationToken);
            using var randomAccessStream = stream.AsRandomAccessStream();

            // record
            Logger.LogInformation($"Initializing media capture...");
            using var capture = new MediaCapture();
            await capture.InitializeAsync();

            // face detection
            var faceDetectionOptions = FaceDetectionOptions.Value;
            FaceDetectionEffect? faceDetectionEffect = null;
            if (faceDetectionOptions.IsEnabled)
            {
                Logger.LogInformation($"Initializing face detection...");
                var faceDetectionEffectDefinition = new FaceDetectionEffectDefinition
                {
                    DetectionMode = faceDetectionOptions.DetectionMode,
                    SynchronousDetectionEnabled = false,
                };
                faceDetectionEffect = (FaceDetectionEffect)await capture.AddVideoEffectAsync(faceDetectionEffectDefinition, MediaStreamType.VideoRecord);
                faceDetectionEffect.DesiredDetectionInterval = faceDetectionOptions.DesiredDetectionInterval;
                faceDetectionEffect.FaceDetected += OnFaceDetected;
                faceDetectionEffect.Enabled = true;
            }

            // start
            Logger.LogInformation($"Starting recording...");
            await capture.StartRecordToStreamAsync(profile, randomAccessStream);

            // record
            Logger.LogInformation($"Recording started.");
            await Task.Delay(RecordingOptions.Value.MaximumRecordTime ?? TimeSpan.FromMilliseconds(-1), cancellationToken);

            // stop
            Logger.LogInformation($"Stopping recording...");
            var result = await capture.StopRecordWithResultAsync();

            // cleanup
            if (faceDetectionOptions.IsEnabled)
            {
                faceDetectionEffect!.Enabled = false;
                faceDetectionEffect.FaceDetected -= OnFaceDetected;
                await capture.ClearEffectsAsync(MediaStreamType.VideoRecord);
            }
            await randomAccessStream.FlushAsync();
            await stream.FlushAsync();
            Logger.LogInformation($"Recording stopped. Recorded '{result.RecordDuration}'.");
        }

        private void OnFaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            var frame = args.ResultFrame;
            Logger.LogInformation($"Faces detected: {frame.DetectedFaces.Count} at {frame.SystemRelativeTime}");
        }
    }
}
