using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Devices;

namespace SecurityCamera.Console
{
    public interface IFocusChangeSink
    {
        ValueTask OnFocusStateChangedAsync(MediaCaptureFocusState state);
    }
}
