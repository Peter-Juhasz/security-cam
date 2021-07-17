namespace SecurityCamera.Console
{
    class BlobsOptions
    {
        public string ConnectionString { get; set; } = null!;

        public string ContainerName { get; set; } = "recordings";

        public long? BufferSize { get; set; }

        public string BlobNameFormat { get; set; } = "{0:yyyy/MM/dd/HH-mm-ss}.mp4";

        public long InitialSizeHint { get; set; } = 32 * 1024 * 1024;

        public double ResizeFactor { get; set; } = 2;
    }
}
