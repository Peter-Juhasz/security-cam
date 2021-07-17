﻿using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
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
            IOptions<VideoEncodingOptions> videoEncodingOptions,
            IOptions<AudioEncodingOptions> audioEncodingOptions,
            IOptions<WakeOptions> wakeOptions,
            IEnumerable<IFaceDetectionSink> faceDetectionSinks,
            ILogger<RecordingWorker> logger
        )
        {
            BlobServiceClient = blobServiceClient;
            RecordingOptions = recordingOptions;
            FaceDetectionOptions = faceDetectionOptions;
            BlobsOptions = blobsOptions;
            VideoEncodingOptions = videoEncodingOptions;
            AudioEncodingOptions = audioEncodingOptions;
            WakeOptions = wakeOptions;
            FaceDetectionSinks = faceDetectionSinks;
            Logger = logger;
        }

        private BlobServiceClient BlobServiceClient { get; }
        private IOptions<RecordingOptions> RecordingOptions { get; }
        private IOptions<FaceDetectionOptions> FaceDetectionOptions { get; }
        private IOptions<BlobsOptions> BlobsOptions { get; }
        private IOptions<VideoEncodingOptions> VideoEncodingOptions { get; }
        public IOptions<AudioEncodingOptions> AudioEncodingOptions { get; }
        public IOptions<WakeOptions> WakeOptions { get; }
        public IEnumerable<IFaceDetectionSink> FaceDetectionSinks { get; }
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
                var videoEncodingOptions = VideoEncodingOptions.Value;
                var profile = videoEncodingOptions.Format switch
                {
                    "HEVC" => MediaEncodingProfile.CreateHevc(videoEncodingOptions.Quality),
                    "MP4" => MediaEncodingProfile.CreateMp4(videoEncodingOptions.Quality),
                    "WMV" => MediaEncodingProfile.CreateWmv(videoEncodingOptions.Quality),
                    _ => throw new NotSupportedException($"Encoding format '{videoEncodingOptions.Format}' is not supported.")
                };
                if (videoEncodingOptions.Bitrate is uint videoBitrate)
                {
                    profile.Video.Bitrate = videoBitrate;
                }
                if (videoEncodingOptions.FrameRate is uint frameRate)
                {
                    profile.Video.FrameRate.Numerator = frameRate;
                    profile.Video.FrameRate.Denominator = 1;
                }

                var audioEncodingOptions = AudioEncodingOptions.Value;
                if (audioEncodingOptions.Enabled == false)
                {
                    profile.SetAudioTracks(Array.Empty<AudioStreamDescriptor>());
                }
                else
                {
                    if (audioEncodingOptions.Bitrate is uint audioBitrate)
                    {
                        profile.Audio.Bitrate = audioBitrate;
                    }
                    if (audioEncodingOptions.BitsPerSample is uint bitsPerSample)
                    {
                        profile.Audio.BitsPerSample = bitsPerSample;
                    }
                }

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

        private async void OnFaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            var frame = args.ResultFrame;
            if (frame.DetectedFaces.Count != _numberOfFacesDetected)
            {
                foreach (var sink in FaceDetectionSinks)
                {
                    try
                    {
                        await sink.OnFaceDetectionChangedAsync(frame, null);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, $"Face detection sink of type '{sink.GetType()}' has failed.");
                    }
                }
            }
        }
    }
}
