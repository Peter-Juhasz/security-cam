using Microsoft.ApplicationInsights;

using System.Collections.Generic;
using System.Threading.Tasks;

using Windows.Media.Devices;

namespace SecurityCamera.Console
{
    record ApplicationInsightsFocusChangeSink(TelemetryClient TelemetryClient) : IFocusChangeSink
    {
        public ValueTask OnFocusStateChangedAsync(MediaCaptureFocusState state)
        {
            TelemetryClient.TrackEvent("FocusChanged", new Dictionary<string, string>
            {
                { "State", state.ToString() }
            });
            return default;
        }
    }
}
