using gluck.camera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace CenterRegistration
{
    public class GigE_FLIRCameraMangement : CameraManagementBase
    {
        public GigE_FLIRCameraMangement() : base(Cam.FILR)
        {

        }
        public override void DeleteGrabImageEvent(CamTriggerMode mode)
        {
            throw new NotImplementedException();
        }

        public override object GetCamera()
        {
            throw new NotImplementedException();
        }

        public override void InitialCam(int id, CamTriggerMode mode)
        {
            this.CamIndex = id;
            mIsInitial = mCamera.Initial(id);  
        }

        public override void InitialCam(int id, string port, int baudRate, CamTriggerMode mode)
        {
            throw new NotImplementedException();
        }

        public override void LinkingGrabImageEvent(CamTriggerMode mode)
        {
            throw new NotImplementedException();
        }
        public override void ContinueFrame()
        {
            ((GigEVision<HImage>)mCamera).ContinueFrame();
        }

        public override void StopContinueFrame()
        {
            ((GigEVision<HImage>)mCamera).IsContinueFrame = false;
            System.Threading.Thread.Sleep(200);
            ((GigEVision<HImage>)mCamera).Stop();
            //((USB3Vision<HImage>)mCamera).IsContinueFrame = isContinue;
        }
        /// <summary>
        /// 取得相機名稱
        /// </summary>
        /// <returns></returns>
        public static string[] GetDeviceName()
        {
            var deviceInfo = GigEVision<HImage>.GetDeviceInfo();

            List<string> deviceNameList = new List<string>();
            if (deviceInfo!= null)
            {
                for (int i = 0; i < deviceInfo.Length; i++)
                {
                    var tmpSplit = deviceInfo.TupleSelect(i).S.Split(new string[] { "|", " " }, StringSplitOptions.RemoveEmptyEntries);
                    var deviceName = tmpSplit[0].Split(new string[] { "device", ":" }, StringSplitOptions.RemoveEmptyEntries);

                    deviceNameList.Add(deviceName[0]);
                }
            }

            return deviceNameList.ToArray();
        }
        public override string GetDeviceSerialNumber()
        {
            HTuple tmpSerialNumber = null;

            string serialNumber = string.Empty;
            try
            {
                tmpSerialNumber = ((GigEVision<HImage>)mCamera).GetDeviceSerialNumber();
                serialNumber = tmpSerialNumber.S;
            }
            catch (Exception)
            {
                serialNumber = string.Empty;
            }

            return serialNumber;
        }
        /// <summary>
        /// 關閉所有連接
        /// </summary>
        public static void CloseAllFramegrabbers()
        {
            GigEVision<HImage>.CloseAllFramegrabbers();
        }
        /// <summary>
        /// 設定相機曝光時間
        /// </summary>
        public override void SetExposureTime(double value = 1000)
        {
            try
            {
                ((GigEVision<HImage>)mCamera).SetExposureTime(value);
            }
            catch (Exception)
            {

            }
        }
        /// <summary>
        /// 設定相機Frame rate
        /// FILR相機 FrameRate需要先把旗標打開才能設定
        /// </summary>
        public override void SetFrameRate(double value = 10)
        {
            try
            {
                ((GigEVision<HImage>)mCamera).SetFrameRate(value);
            }
            catch (Exception)
            {

            }
        }
    }
}
