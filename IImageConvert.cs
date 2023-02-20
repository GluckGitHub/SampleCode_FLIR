using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gluck.camera
{
    public interface IImageConvert<TImage>
    {
        void SetImageData(int width, int height, bool isColor);
        TImage ConvertImage(object sender, IntPtr imgPtr);
    }
}
