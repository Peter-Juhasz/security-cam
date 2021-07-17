namespace SecurityCamera.Console
{
    class SmsOptions
    {
        public string ConnectionString { get; set; } = null!;

        public string From { get; set; } = null!;

        public string[] To { get; set; } = null!;
    }
}
