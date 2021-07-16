using System;

namespace SecurityCamera.Console
{
    class RecordingOptions
    {
        public TimeSpan? ChunkSize { get; set; } = TimeSpan.FromMinutes(10);

        public TimeSpan? MaximumRecordTime { get; set; }
    }
}
