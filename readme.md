# SecurityCamera
Records and encodes video to HEVC/MP4 using hardware acceleration, uploads to Azure Blob Storage, detects faces and sends notifications.

## How to record video in Windows
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

```cs
await capture.StartRecordToStreamAsync(profile, stream);

// capture video ...

var result = await capture.StopRecordWithResultAsync();
```

## How to detect faces
TODO

## Stream to Azure Blob Storage
We can open a Block Blob to write as a `Stream`, so this way we can transfer the video directly to Azure:
```cs
var container = BlobServiceClient.GetBlobContainerClient(blobsOptions.ContainerName);
var blob = container.GetBlockBlobClient($"{now:yyyy/MM/dd/HH-mm-ss}.mp4");
await using var stream = await blob.OpenWriteAsync(overwrite: false);
```

In order to use a `Stream` in Windows Runtime, we have to wrap into a [IRandomAccessStream ](https://docs.microsoft.com/en-us/uwp/api/windows.storage.streams.irandomaccessstream):
```cs
using var ras = stream.AsRandomAccessStream();
```