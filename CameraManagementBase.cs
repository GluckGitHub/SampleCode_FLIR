using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;
using gluck.camera;
using System.Windows.Forms;

namespace CenterRegistration
{
    public enum Cam
    { DALSA = 0, VieworksBayerWithDalsa = 1, IDS, FILR }
    public abstract class CameraManagementBase
    {
        protected ICCD_Base<HImage> mCamera;// = new IDS_2<HImage>(new HImageConverter());

        protected string mFileName = Application.StartupPath + @"\CamFiles\";
        protected HImage mImage;// = new HImage();
        protected bool mIsInitial = false;
        protected int mCamIndex = -1;

        //Action DisplayImage();
        public event Action<int> DisplayImage;
        //Action Grab image to ImageBuffer
        public event Action<int, HImage> GrabImage = null;
        //
        public event Action<bool> IOTriggered = null;

        private bool mIsBayerImage { get; set; } = false;
        protected CameraManagementBase(Cam type)
        {
            switch (type)
            {
                case Cam.DALSA:
                    //mCamera = new Dalsa_2<HImage>(new HImageConverter());
                    break;
                case Cam.IDS:
                    //mCamera = new IDS_2<HImage>(new HImageConverter());
                    break;
                case Cam.VieworksBayerWithDalsa:
                   // mCamera = new VieworksBayerWithDalsa<HImage>(new HImageConverter());
                    mIsBayerImage = true;
                    break;
                case Cam.FILR:
                    mCamera = new GigEVision<HImage>();
                    break;
                default:
                    break;
            }

            mImage = new HImage();

            this.mCamera.ImageObtained += (o) => this.UpdateImage(o);
        }

        public abstract void InitialCam(int id, CamTriggerMode mode);
        public abstract void InitialCam(int id, string port, int baudRate, CamTriggerMode mode);
        public abstract object GetCamera();
        public abstract string GetDeviceSerialNumber();
        public abstract void SetExposureTime(double value);
        public abstract void SetFrameRate(double value);

        public bool IsInitial
        {
            get { return mIsInitial; }
            set { mIsInitial = value; }
        }
        //camera
        public ICCD_Base<HImage> Camera
        {
            get { return mCamera; }
            set { mCamera = value; }
        }
        //image
        public HImage Image
        {
            get { return mImage; }
            set { mImage = value; }
        }
        public int CamIndex
        {
            get { return mCamIndex; }
            set { mCamIndex = value; }
        }


        public abstract void DeleteGrabImageEvent(CamTriggerMode mode);

        public abstract void LinkingGrabImageEvent(CamTriggerMode mode);
        public abstract void ContinueFrame();

        public abstract void StopContinueFrame();

        private void UpdateImage(HImage img)
        {
            mImage = img.CopyObj(1, -1);
            img.Dispose();

            this.DisplayImage?.Invoke(CamIndex);
            this.GrabImage?.Invoke(CamIndex, mImage);
        }
        public void ReceivedIOSignal(bool isHigh)
        {
            this.IOTriggered?.Invoke(isHigh);
        }
        /// <summary>
        /// 是否開始檢測
        /// </summary>
        public bool IsStart { get; set; } = false;
    }
}
