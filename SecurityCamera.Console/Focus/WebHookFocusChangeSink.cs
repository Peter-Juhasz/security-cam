using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Windows.Media.Devices;

namespace SecurityCamera.Console
{
    record WebHookFocusChangeSink(IHttpClientFactory Factory, IOptions<WebHookOptions> Options, ILogger<WebHookFocusChangeSink> Logger) : IFocusChangeSink
    {
        public async ValueTask OnFocusStateChangedAsync(MediaCaptureFocusState state)
        {
            var options = Options.Value;
            Logger.LogInformation($"Calling web hook at '{options.FocusChangeUrl}'...");
            using var client = Factory.CreateClient(nameof(WebHookFocusChangeSink));
            var request = new FocusChangeWebHookRequest(state);
            using var response = await client.PostAsJsonAsync(options.FocusChangeUrl, request);
            response.EnsureSuccessStatusCode();
        }
    }
}
