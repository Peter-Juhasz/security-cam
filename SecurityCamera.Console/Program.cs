﻿using Azure.Communication.Sms;
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

                    // sms
                    if (context.Configuration.GetSection("Sms").Exists())
                    {
                        services.Configure<SmsOptions>(context.Configuration.GetSection("Sms"));
                        services.AddSingleton<SmsClient>(sp => new SmsClient(sp.GetRequiredService<IOptions<SmsOptions>>().Value.ConnectionString));
                    }

                    // webhook
                    if (context.Configuration.GetSection("WebHook").Exists())
                    {
                        services.Configure<WebHookOptions>(context.Configuration.GetSection("WebHook"));
                    }

                    // face detection
                    services.Configure<FaceDetectionOptions>(context.Configuration.GetSection("FaceDetection"));
                    services.AddSingleton<IFaceDetectionSink, LoggerFaceDetectionSink>();
                    services.AddSingleton<IFaceDetectionSink, ApplicationInsightsFaceDetectionSink>();
                    if (context.Configuration.GetSection("Sms").Exists())
                    {
                        services.AddSingleton<IFaceDetectionSink, SmsFaceDetectionSink>();
                    }
                    if (context.Configuration.GetSection("WebHook").Exists() && context.Configuration.GetSection("WebHook")[nameof(WebHookOptions.FaceDetectionUrl)] != null)
                    {
                        services.AddHttpClient(nameof(WebHookFaceDetectionSink));
                        services.AddSingleton<IFaceDetectionSink, WebHookFaceDetectionSink>();
                    }

                    // focus
                    services.AddSingleton<IFocusChangeSink, LoggerFocusChangeSink>();
                    services.AddSingleton<IFocusChangeSink, ApplicationInsightsFocusChangeSink>();
                    if (context.Configuration.GetSection("Sms").Exists())
                    {
                        services.AddSingleton<IFocusChangeSink, SmsFocusChangeSink>();
                    }
                    if (context.Configuration.GetSection("WebHook").Exists() && context.Configuration.GetSection("WebHook")[nameof(WebHookOptions.FocusChangeUrl)] != null)
                    {
                        services.AddHttpClient(nameof(WebHookFocusChangeSink));
                        services.AddSingleton<IFocusChangeSink, WebHookFocusChangeSink>();
                    }

                    // wake
                    services.Configure<WakeOptions>(context.Configuration.GetSection("Wake"));

                    // recording
                    services.Configure<VideoOptions>(context.Configuration.GetSection("Video"));
                    services.Configure<AudioOptions>(context.Configuration.GetSection("Audio"));
                    services.Configure<RecordingOptions>(context.Configuration.GetSection("Recording"));
                    services.AddHostedService<RecordingWorker>();
                })
                .UseConsoleLifetime()
            ;
    }
}
