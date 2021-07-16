namespace SecurityCamera.Console
{
    class BlobsOptions
    {
        public string ConnectionString { get;set;  }

        public string ContainerName {  get; set; }

        public long? BufferSize { get; set; }

        public string BlobNameFormat { get; set; } = "{0:yyyy/MM/dd/HH-mm-ss}.mp4";
    }
}
