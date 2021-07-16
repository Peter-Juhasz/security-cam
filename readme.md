# Security Camera
Records and encodes video to HEVC/MP4 using hardware acceleration, uploads to Azure Blob Storage, detects faces and sends notifications.

## Usage
Run `SecurityCamera.Console.exe`.

### Configuration
Configuration is stored in `appsettings.json` or can be added as CLI arguments.

- `Encoding`
  - `Format`: `"HEVC"` or `"MP4"` (default `"HEVC"`)
  - `Quality`: `"HD720p"`, `"HD1080p"`, `"Uhd2160p"` or any member of [VideoEncodingQuality](https://docs.microsoft.com/en-us/uwp/api/windows.media.mediaproperties.videoencodingquality) enum (default `"HD1080p"`)
- `FaceDetection`
  - `Enabled`: whether to enable face detection (default `true`)
  - `DetectionMode`: any member of [FaceDetectionMode](https://docs.microsoft.com/en-us/uwp/api/windows.media.core.facedetectionmode) enum (default `"HighPerformance"`)
  - `DesiredDetectionInterval`: the time span for which face detection should be performed (default `"00:00:00.500"`)
- `Recording`
  - `MaximumRecordTime`: maximum time to record (default `null`)
- `Blobs`
  - **`ConnectionString`**: the connection string for the Storage Account
  - `ContainerName`: the name of the container to upload blobs (default `"recordings"`)
  - `BufferSize`: the size of the buffer to write the blob (default `null`, defaults to library default which is `4 MB`)
  - `BlobNameFormat`: format used for naming blobs, with argument `DateTimeOffset` at index `0` (default `"{0:yyyy/MM/dd/HH-mm-ss}.mp4"`)

## Tutorial
### How to record video in Windows
First, we need to create a [MediaEncodingProfile](https://docs.microsoft.com/en-us/uwp/api/windows.media.mediaproperties.mediaencodingprofile):
```cs
var profile = MediaEncodingProfile.CreateHevc(VideoEncodingQuality.HD1080p);
```

Then, we have to initialize a [MediaCapture](https://docs.microsoft.com/en-us/uwp/api/Windows.Media.Capture.MediaCapture).
```cs
using var capture = new MediaCapture();
await capture.InitializeAsync();
```

This is where we could select the camera or microphone we would like to use. If we don't specify any, it is going to use the default settings.

The next step is to start and stop recording:
```cs
await capture.StartRecordToStreamAsync(profile, stream);

// capture video ...

var result = await capture.StopRecordWithResultAsync();
```

### How to detect faces
For photos, there the `Windows.Media.FaceAnalysis` namespace contains a [FaceDetector](https://docs.microsoft.com/en-us/uwp/api/windows.media.faceanalysis.facedetector) class:
```cs
var faces = await FaceDetector.DetectFacesAsync(bitmap);
```

For videos, the [FaceTracker](https://docs.microsoft.com/en-us/uwp/api/Windows.Media.FaceAnalysis.FaceTracker) is the right choice, as it was optimized to work with [VideoFrame](https://docs.microsoft.com/en-us/uwp/api/windows.media.videoframe)s:
```cs
var faces = await FaceTracker.ProcessNextFrameAsync(videoFrame);
```

To process a video in real-time and pass `VideoFrame`s to the `FaceTracker`, we have multiple choices:
 - Grab a preview using `GetPreviewFrameAsync` periodically, and pass the `VideoFrame` for processing.
 - Implement a custom sink and process video using `StartRecordToCustomSinkAsync`.
 - Implement an [IBasicVideoEffect](https://docs.microsoft.com/en-us/uwp/api/Windows.Media.Effects.IBasicVideoEffect) and listen to `VideoFrame`s in its `ProcessFrame` method.

Video processing and face detection is not easy at all, especially if we want to be efficient as well, even offload some of the work to the GPU. Fortunately, Microsoft has already implemented this functionaly as [FaceDetectionEffect](https://docs.microsoft.com/en-us/uwp/api/Windows.Media.Core.FaceDetectionEffect), which we can easily add to the `MediaCapture`:
```cs
var definition = new FaceDetectionEffectDefinition
{
	DetectionMode = FaceDetectionMode.HighPerformance,
};
var effect = await capture.AddVideoEffectAsync(definition, MediaStreamType.VideoRecord);
effect.FaceDetected += OnFaceDetected; // subscribe to event
effect.Enabled = true;
```

### Stream to Azure Blob Storage
We can open a Block Blob to write as a `Stream`, so this way we can transfer the video seamlessly to Azure without buffering to disk or memory:
```cs
var container = BlobServiceClient.GetBlobContainerClient(blobsOptions.ContainerName);
var blob = container.GetBlockBlobClient($"{now:yyyy/MM/dd/HH-mm-ss}.mp4");
await using var stream = await blob.OpenWriteAsync(overwrite: false);
```

In order to use a `Stream` in Windows Runtime, we have to it wrap into a [IRandomAccessStream ](https://docs.microsoft.com/en-us/uwp/api/windows.storage.streams.irandomaccessstream):
```cs
using var randomAccessStream = stream.AsRandomAccessStream();
```