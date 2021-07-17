using Microsoft.ApplicationInsights;

using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Media.Core;

namespace SecurityCamera.Console
{
    record ApplicationInsightsFaceDetectionSink(TelemetryClient TelemetryClient) : IFaceDetectionSink
    {
        public ValueTask OnFaceDetectionChangedAsync(FaceDetectionEffectFrame frame, SoftwareBitmap snapshot)
        {
            var count = frame.DetectedFaces.Count;
            if (count > 0)
            {
                TelemetryClient.TrackEvent("FaceDetected");
            }

            TelemetryClient.TrackMetric("NumberOfFacesDetected", count);
            return default;
        }
    }
}
