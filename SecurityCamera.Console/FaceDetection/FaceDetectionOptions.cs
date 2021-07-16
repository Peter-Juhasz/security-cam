using System;

using Windows.Media.Core;

namespace SecurityCamera.Console
{
    class FaceDetectionOptions
    {
        public bool IsEnabled { get; set; } = true;

        public FaceDetectionMode DetectionMode { get; set; } = FaceDetectionMode.HighPerformance;

        public TimeSpan DesiredDetectionInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    }
}
