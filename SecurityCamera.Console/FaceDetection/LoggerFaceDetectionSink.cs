using Microsoft.Extensions.Logging;

using System;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Media.Core;

namespace SecurityCamera.Console
{
    record LoggerFaceDetectionSink(ILogger<LoggerFaceDetectionSink> Logger) : IFaceDetectionSink
    {
        public ValueTask OnFaceDetectionChangedAsync(FaceDetectionEffectFrame frame, SoftwareBitmap snapshot)
        {
            Logger.LogInformation($"Faces detected: {frame.DetectedFaces.Count} at {DateTimeOffset.Now} (relative: {frame.SystemRelativeTime})");
            return default;
        }
    }
}
