namespace SecurityCamera.Console
{
    class AudioOptions
    {
        public bool Enabled { get; set; } = true;

        public uint? Bitrate { get; set; }

        public uint? BitsPerSample { get; set; }
    }
}
