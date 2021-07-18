using Windows.Media.Devices;

namespace SecurityCamera.Console
{
    record FocusChangeWebHookRequest(
        MediaCaptureFocusState State
    )
    { }
}
