using Azure.Communication.Sms;
using Azure.Storage.Blobs;

using Microsoft.Extensions.Configuration;
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
                    // application insights
                    services.AddApplicationInsightsTelemetryWorkerService(options =>
                    {
                        options.EnableAdaptiveSampling = false;
                        options.EnableQuickPulseMetricStream = true;
                    });

                    // azure
                    services.Configure<BlobsOptions>(context.Configuration.GetSection("Blobs"));
                    services.AddSingleton<BlobServiceClient>(sp => new(
                        sp.GetRequiredService<IOptions<BlobsOptions>>().Value.ConnectionString
                    ));

                    // face detection
                    services.Configure<FaceDetectionOptions>(context.Configuration.GetSection("FaceDetection"));
                    services.AddSingleton<IFaceDetectionSink, LoggerFaceDetectionSink>();
                    services.AddSingleton<IFaceDetectionSink, ApplicationInsightsFaceDetectionSink>();
                    if (context.Configuration.GetSection("Sms").Exists())
                    {
                        services.Configure<SmsOptions>(context.Configuration.GetSection("Sms"));
                        services.AddSingleton<SmsClient>(sp => new SmsClient(sp.GetRequiredService<IOptions<SmsOptions>>().Value.ConnectionString));
                        services.AddSingleton<IFaceDetectionSink, SmsFaceDetectionSink>();
                    }

                    // wake
                    services.Configure<WakeOptions>(context.Configuration.GetSection("Wake"));

                    // recording
                    services.Configure<VideoEncodingOptions>(context.Configuration.GetSection("Video"));
                    services.Configure<AudioEncodingOptions>(context.Configuration.GetSection("Audio"));
                    services.Configure<RecordingOptions>(context.Configuration.GetSection("Recording"));
                    services.AddHostedService<RecordingWorker>();
                })
                .UseConsoleLifetime()
            ;
    }
}
