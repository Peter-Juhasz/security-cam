using Azure.Communication.Sms;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Media.Core;

namespace SecurityCamera.Console
{
    record SmsFaceDetectionSink(SmsClient Client, IOptions<SmsOptions> Options, ILogger<SmsFaceDetectionSink> Logger) : IFaceDetectionSink
    {
        public async ValueTask OnFaceDetectionChangedAsync(FaceDetectionEffectFrame frame, SoftwareBitmap snapshot)
        {
            var options = Options.Value;
            Logger.LogInformation($"Sending SMS to '{String.Join(", ", options.To)}'...");
            var response = await Client.SendAsync(options.From, options.To, "Face detected!");
            foreach (var item in response.Value)
            {
                Logger.LogInformation($"Sending SMS to '{item.To}' was {(item.Successful ? "successful" : "failed")} with message ID '{item.MessageId}'.");
            }
        }
    }
}
