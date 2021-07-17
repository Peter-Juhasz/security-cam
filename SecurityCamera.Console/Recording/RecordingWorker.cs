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
using Windows.System.Display;

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
            IOptions<WakeOptions> wakeOptions,
            ILogger<RecordingWorker> logger
        )
        {
            BlobServiceClient = blobServiceClient;
            RecordingOptions = recordingOptions;
            FaceDetectionOptions = faceDetectionOptions;
            BlobsOptions = blobsOptions;
            EncodingOptions = encodingOptions;
            WakeOptions = wakeOptions;
            Logger = logger;
        }

        private BlobServiceClient BlobServiceClient { get; }
        private IOptions<RecordingOptions> RecordingOptions { get; }
        private IOptions<FaceDetectionOptions> FaceDetectionOptions { get; }
        private IOptions<BlobsOptions> BlobsOptions { get; }
        private IOptions<EncodingOptions> EncodingOptions { get; }
        public IOptions<WakeOptions> WakeOptions { get; }
        private ILogger<RecordingWorker> Logger { get; }

        private static readonly TimeSpan Infinity = TimeSpan.FromMilliseconds(-1);

        public TimeSpan TotalRecordingTime { get; private set; } = TimeSpan.Zero;

        private int _numberOfFacesDetected = 0;

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var wakeOptions = WakeOptions.Value;
            DisplayRequest? displayRequest = null;

            try
            {
                // prevent computer from sleep
                if (wakeOptions.Enabled)
                {
                    displayRequest = new();
                    displayRequest.RequestActive();
                }

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

                // record
                Logger.LogInformation($"Initializing media capture...");
                using var capture = new MediaCapture();
                await capture.InitializeAsync();

                // face detection
                var faceDetectionOptions = FaceDetectionOptions.Value;
                FaceDetectionEffect? faceDetectionEffect = null;
                if (faceDetectionOptions.Enabled)
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

                // timestamp
                var now = DateTimeOffset.Now;
                var started = now;
                Logger.LogInformation($"Current timestamp: {now}");

                // create blob
                Logger.LogInformation($"Initializing blob container...");
                var blobsOptions = BlobsOptions.Value;
                var container = BlobServiceClient.GetBlobContainerClient(blobsOptions.ContainerName);
                await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // timestamp
                        now = DateTimeOffset.Now;
                        Logger.LogInformation($"Current timestamp: {now}");

                        // create blob
                        Logger.LogInformation($"Initializing blob...");
                        var blob = container.GetPageBlobClient(String.Format(blobsOptions.BlobNameFormat, now));
                        Logger.LogInformation($"Blob: {blob.Uri}");
                        await using var randomAccessStream = new PageBlobRandomAccessStream(blob, blobsOptions);

                        // start
                        Logger.LogInformation($"Starting recording...");
                        await capture.StartRecordToStreamAsync(profile, randomAccessStream);

                        // record
                        Logger.LogInformation($"Recording started.");
                        var recordingOptions = RecordingOptions.Value;
                        var remainingTime = Infinity;
                        if (recordingOptions.ChunkSize is TimeSpan chunkSize)
                        {
                            remainingTime = chunkSize;
                        }
                        if (recordingOptions.MaximumRecordTime is TimeSpan maximumTime)
                        {
                            var maximumRemainingTime = started + maximumTime - now;
                            if (maximumRemainingTime < remainingTime)
                            {
                                remainingTime = maximumRemainingTime;
                            }
                        }
                        await Task.Delay(remainingTime, cancellationToken);

                        // stop
                        Logger.LogInformation($"Stopping recording...");
                        var result = await capture.StopRecordWithResultAsync();
                        Logger.LogInformation($"Recording stopped, duration: '{result.RecordDuration}'.");
                        TotalRecordingTime += result.RecordDuration;

                        // flush
                        await randomAccessStream.FlushAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "An error occurred while recording. Retrying...");
                    }
                }

                // cleanup
                Logger.LogInformation($"Cleaning up...");
                if (faceDetectionOptions.Enabled)
                {
                    faceDetectionEffect!.Enabled = false;
                    faceDetectionEffect.FaceDetected -= OnFaceDetected;
                    await capture.ClearEffectsAsync(MediaStreamType.VideoRecord);
                }

                Logger.LogInformation($"Total recording time: {TotalRecordingTime}");
                Logger.LogInformation($"Total run time: {DateTimeOffset.Now - started}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unrecoverable error occurred.");
            }
            finally
            {
                // release display request
                if (wakeOptions.Enabled)
                {
                    displayRequest.RequestRelease();
                }
            }
        }

        private void OnFaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            var frame = args.ResultFrame;
            if (frame.DetectedFaces.Count != _numberOfFacesDetected)
            {
                Logger.LogInformation($"Faces detected: {frame.DetectedFaces.Count} at {DateTimeOffset.Now} (relative: {frame.SystemRelativeTime})");
            }
        }
    }
}
