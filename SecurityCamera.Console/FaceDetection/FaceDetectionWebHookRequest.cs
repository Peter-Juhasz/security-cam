using System.Collections.Generic;

using Windows.Media.FaceAnalysis;

namespace SecurityCamera.Console
{
    record FaceDetectionWebHookRequest(
        IReadOnlyCollection<DetectedFace> DetectedFaces
    )
    { }
}
