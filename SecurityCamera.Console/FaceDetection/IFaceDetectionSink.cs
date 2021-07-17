using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Media.Core;

namespace SecurityCamera.Console
{
    public interface IFaceDetectionSink
    {
        ValueTask OnFaceDetectionChangedAsync(FaceDetectionEffectFrame frame, SoftwareBitmap snapshot);
    }
}
