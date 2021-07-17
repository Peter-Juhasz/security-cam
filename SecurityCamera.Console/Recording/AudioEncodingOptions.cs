namespace SecurityCamera.Console
{
    class AudioEncodingOptions
    {
        public bool Enabled { get; set; } = true;

        public uint? Bitrate { get; set; }

        public uint? BitsPerSample { get; set; }
    }
}
