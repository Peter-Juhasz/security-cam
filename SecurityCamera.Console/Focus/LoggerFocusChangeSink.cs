using Microsoft.Extensions.Logging;

using System.Threading.Tasks;

using Windows.Media.Devices;

namespace SecurityCamera.Console
{
    record LoggerFocusChangeSink(ILogger<LoggerFaceDetectionSink> Logger) : IFocusChangeSink
    {
        public ValueTask OnFocusStateChangedAsync(MediaCaptureFocusState state)
        {
            Logger.LogInformation($"Focus state changed to '{state}'.");
            return default;
        }
    }
}
