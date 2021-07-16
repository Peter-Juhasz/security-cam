using Windows.Media.MediaProperties;

namespace SecurityCamera.Console
{
    class EncodingOptions
    {
        public string Format { get; set; } = "HEVC";

        public VideoEncodingQuality Quality { get; set; } = VideoEncodingQuality.HD1080p;
    }
}
