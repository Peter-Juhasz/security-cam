using Windows.Media.MediaProperties;

namespace SecurityCamera.Console
{
    class VideoOptions
    {
        public string Format { get; set; } = "HEVC";

        public VideoEncodingQuality Quality { get; set; } = VideoEncodingQuality.HD1080p;

        public uint? Bitrate { get; set; }

        public uint? FrameRate { get; set; }
    }
}
