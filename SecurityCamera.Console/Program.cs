using Azure.Storage.Blobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using System;

namespace SecurityCamera.Console
{
    class Program
    {
        public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // azure
                    services.Configure<BlobsOptions>(context.Configuration.GetSection("Blobs"));
                    services.AddSingleton<BlobServiceClient>(sp => new(
                        sp.GetRequiredService<IOptions<BlobsOptions>>().Value.ConnectionString
                    ));

                    // face detection
                    services.Configure<FaceDetectionOptions>(context.Configuration.GetSection("FaceDetection"));

                    // recording
                    services.Configure<EncodingOptions>(context.Configuration.GetSection("Encoding"));
                    services.Configure<RecordingOptions>(context.Configuration.GetSection("Recording"));
                    services.AddHostedService<RecordingWorker>();
                })
                .UseConsoleLifetime()
            ;
    }
}
