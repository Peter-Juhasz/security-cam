using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Media.Core;

namespace SecurityCamera.Console
{
    record WebHookFaceDetectionSink(IHttpClientFactory Factory, IOptions<WebHookOptions> Options, ILogger<WebHookFaceDetectionSink> Logger) : IFaceDetectionSink
    {
        public async ValueTask OnFaceDetectionChangedAsync(FaceDetectionEffectFrame frame, SoftwareBitmap snapshot)
        {
            var options = Options.Value;
            Logger.LogInformation($"Calling web hook at '{options.FaceDetectionUrl}'...");
            using var client = Factory.CreateClient(nameof(WebHookFaceDetectionSink));
            var request = new FaceDetectionWebHookRequest(frame.DetectedFaces);
            using var response = await client.PostAsJsonAsync(options.FaceDetectionUrl, request);
            response.EnsureSuccessStatusCode();
        }
    }
}
