using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using gluck.camera;
using HalconDotNet;

namespace CenterRegistration
{
    public class HImageConverter : IImageConvert<HImage>
    {
        private int mImageWidth = 0;
        private int mImageHeight = 0;
        private bool mIsSensorColor = false;

        private byte[] mCameraBuffer = null;

        private HImage mLastImage = new HImage();

        public HImageConverter()
        {

        }

        public HImage ConvertImage(object sender, IntPtr imgPtr)
        {
            try
            {
                Marshal.Copy(imgPtr, mCameraBuffer, 0, mCameraBuffer.Length);

                IntPtr Reuslt = Marshal.AllocHGlobal(mCameraBuffer.Length);
                Marshal.Copy(mCameraBuffer, 0, Reuslt, mCameraBuffer.Length);

                HImage testImage;//= new HImage();

                if (mIsSensorColor)
                {
                    testImage = new HImage();
                    testImage.GenImageInterleaved((IntPtr)Reuslt, "bgr", mImageWidth, mImageHeight, 0, "byte", 0, 0, 0, 0, 8, 0);
                }
                else
                    testImage = new HImage("byte", mImageWidth, mImageHeight, (IntPtr)Reuslt);

                if(mLastImage != null)
                {
                    mLastImage.Dispose();
                    mLastImage = null;

                    mLastImage = testImage;
                }
                //mLastImage = testImage.CopyImage();

                Marshal.FreeHGlobal(Reuslt);

                return mLastImage;
            }
            catch (Exception)
            { return new HImage(); }
        }

        public void SetImageData(int width, int height, bool isColor)
        {
            mImageWidth = width;
            mImageHeight = height;
            mIsSensorColor = isColor;

            if (isColor)
                mCameraBuffer = new byte[mImageWidth * mImageHeight * 3];
            else
                mCameraBuffer = new byte[mImageWidth * mImageHeight];
        }
    }
}
