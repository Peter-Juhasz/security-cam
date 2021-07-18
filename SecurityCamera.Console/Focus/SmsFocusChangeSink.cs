using Azure.Communication.Sms;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Threading.Tasks;

using Windows.Media.Devices;

namespace SecurityCamera.Console
{
    record SmsFocusChangeSink(SmsClient Client, IOptions<SmsOptions> Options, ILogger<SmsFaceDetectionSink> Logger) : IFocusChangeSink
    {
        public async ValueTask OnFocusStateChangedAsync(MediaCaptureFocusState state)
        {
            var options = Options.Value;
            Logger.LogInformation($"Sending SMS to '{String.Join(", ", options.To)}'...");
            var response = await Client.SendAsync(options.From, options.To, $"Focus state changed to '{state}'");
            foreach (var item in response.Value)
            {
                Logger.LogInformation($"Sending SMS to '{item.To}' was {(item.Successful ? "successful" : "failed")} with message ID '{item.MessageId}'.");
            }
        }
    }
}
