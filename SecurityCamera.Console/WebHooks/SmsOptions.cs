using System;

namespace SecurityCamera.Console
{
    class WebHookOptions
    {
        public Uri? FaceDetectionUrl { get; set; }

        public Uri? FocusChangeUrl { get; set; }
    }
}
