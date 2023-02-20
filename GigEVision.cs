using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;
using HalconDotNet;
using System.Threading.Tasks;

namespace gluck.camera
{
    public class GigEVision<T> : ICCD_Base<T>
    {
        /// <summary>
        /// 相機介面
        /// </summary>
        private const string CAMERA_INTERFACE = "GigEVision2";

        /// <summary>
        /// 接到相機回傳的影像後，將影像往外拋的事件
        /// </summary>
        public event CameraRawData<T> ImageObtained;

        /// <summary>
        /// Trigger模式
        /// </summary>
        private CamTriggerMode mTriggerMode;        

        /// <summary>
        /// 連續取像用的執行緒
        /// </summary>
        private Thread mThLive = null;

        private HTuple mCamera;
        /// <summary>
        /// 控制相機取像物件
        /// </summary>
        public HTuple Cam
        {
            get
            {
                return mCamera;
            }
            set
            {
                mCamera = value;
            }
        }

        private bool mIsInitial_CCD;
        /// <summary>
        /// 相機初始化是否成功
        /// </summary>
        public bool IsInitial_CCD
        {
            get
            {
                return mIsInitial_CCD;
            }
        }

        private bool mIsLoadParm_CCD;
        /// <summary>
        /// 讀取相機參數是否成功
        /// </summary>
        public bool IsLoadParm_CCD
        {
            get
            {
                return mIsLoadParm_CCD;
            }
        }

        private bool mIsContinueFrame;
        /// <summary>
        /// 是否為連續取像模式
        /// </summary>
        public bool IsContinueFrame
        {
            get
            {
                return mIsContinueFrame;
            }
            set
            {
                mIsContinueFrame = value;
            }
        }
        
        private Rectangle mImageSize;
        /// <summary>
        /// 影像大小
        /// </summary>
        public Rectangle ImageSize
        {
            get
            {
                return mImageSize;
            }
            set
            {
                mImageSize = value;
            }
        }
        
        private int mCamId = 0;
        /// <summary>
        /// 相機ID(從0開始)
        /// </summary>
        public int CamId
        {
            get
            {
                return mCamId;
            }
        }               

        private double mExposure;
        /// <summary>
        /// 曝光時間(單位：微秒)
        /// </summary>
        public double Exposure
        {
            get
            {
                return mExposure;
            }
            set
            {
                mExposure = value;
                this.SetExposureTime(mExposure);
            }
        }

        private bool mIsSensorColor = false;
        /// <summary>
        /// 是否為彩色
        /// </summary>
        public bool IsSensorColor
        {
            get
            {
                return mIsSensorColor;
            }
            set
            {
                mIsSensorColor = value;
            }
        }

        public GigEVision()
        {
            mImageSize = Rectangle.Empty;
            mIsInitial_CCD = false;
            mIsLoadParm_CCD = false;
            mIsContinueFrame = false;
            mTriggerMode = CamTriggerMode.Off;
        }

        public bool Initial(int cameraID)
        {
            try
            {
                //HOperatorSet.CloseAllFramegrabbers();

                mIsInitial_CCD = false;
                mCamId = cameraID;

                if (mCamera == null || mCamera.Type.ToString() == "EMPTY")
                {
                    mCamera = new HTuple();

                    //Again:
                    try
                    {
                        HTuple hv_RevInfo = null;
                        HTuple hv_RevInfoValues = null, hv_BoardsInfo = null, hv_BoardsInfoValues = null;
                        HTuple hv_NumberOfDevices = null;

                        //check interface revision:
                        HOperatorSet.InfoFramegrabber(CAMERA_INTERFACE, "revision", out hv_RevInfo, out hv_RevInfoValues);
                        //check your installed cameras:
                        HOperatorSet.InfoFramegrabber(CAMERA_INTERFACE, "info_boards", out hv_BoardsInfo, out hv_BoardsInfoValues);
                        //open first available camera with default settings and raw format:
                        HOperatorSet.TupleLength(hv_BoardsInfoValues, out hv_NumberOfDevices);

                        if ((int)(new HTuple(hv_NumberOfDevices.TupleEqual(0))) != 0)
                        {
                            //No devices are connected
                            return false;
                        }

                        HOperatorSet.OpenFramegrabber(new HTuple(CAMERA_INTERFACE),
                                                      new HTuple(0),
                                                      new HTuple(0),
                                                      new HTuple(0),
                                                      new HTuple(0),
                                                      new HTuple(0),
                                                      new HTuple(0),
                                                      new HTuple("default"),   //new HTuple("progressive"),
                                                      new HTuple(-1),
                                                      new HTuple("default"),   //new HTuple("raw"),
                                                      new HTuple(-1),          //"install_driver=1409/8000"
                                                      new HTuple("false"),
                                                      new HTuple("default"),
                                                      new HTuple(hv_BoardsInfoValues.TupleSelect(mCamId)),
                                                      new HTuple(0),
                                                      new HTuple(-1),
                                                      out mCamera);

                    }
                    catch(Exception ex)
                    {
                        //num++;
                        //if (num < 5)
                        //    goto Again;
                    }

                    if (mCamera == null || mCamera.Type.ToString() == "EMPTY") return false;

                    //設定相機參數
                    this.SetFramegrabberParam();

                    //預設軟體觸發模式
                    this.SetTriggerMode(CamTriggerMode.Software);

                    //設定光源點亮模式
                    //this.SetStrobeMode(CamStrobeMode.Off);

                    //開始取像
                    this.Start();
                }

                mIsInitial_CCD = true;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        /// <summary>
        /// 取得相機ID(deviceID)
        /// </summary>
        /// <returns></returns>
        public static HTuple GetDeviceInfo()
        {
            HTuple hv_RevInfo, hv_RevInfoValues = null;
            HOperatorSet.InfoFramegrabber(CAMERA_INTERFACE, "device", out hv_RevInfo, out hv_RevInfoValues);

            if (hv_RevInfoValues.Length == 0)
                hv_RevInfoValues = null;

            return hv_RevInfoValues;
        }
        public static void CloseAllFramegrabbers()
        {
            HOperatorSet.CloseAllFramegrabbers();
        }
        public HTuple GetDeviceSerialNumber()
        {
            HTuple value = null;
            try
            {
                HOperatorSet.GetFramegrabberParam(mCamera, "[Device]DeviceSerialNumber", out value);
            }
            catch (Exception ex)
            {

            }

            return value;
        }
        /// <summary>
        /// 設定相機參數
        /// </summary>
        private void SetFramegrabberParam()
        {
            //查詢指令
            //HOperatorSet.GetFramegrabberParam(mCamera, "PixelFormat_values", out HTuple PixelFormatValues);

            HOperatorSet.SetFramegrabberParam(mCamera, "grab_timeout", -1);                  //取像超時                    
            //HOperatorSet.SetFramegrabberParam(mCamera, "PixelFormat", "BayerRG8");           //影像格式 
            HOperatorSet.SetFramegrabberParam(mCamera, "ExposureAuto", "Off");               //關閉自動曝光功能
            //HOperatorSet.SetFramegrabberParam(mCamera, "BalanceWhiteAuto", "Off");           //關閉自動白平衡功能
            HOperatorSet.SetFramegrabberParam(mCamera, "GainAuto", "Off");
            //HOperatorSet.SetFramegrabberParam(mCamera, "AutoExposureTargetGreyValueAuto", "Off");
            //HOperatorSet.SetFramegrabberParam(mCamera, "GammaEnable", 0);                    //關閉Gamma強化功能


            ////設定FrameRate為最高速          
            //HOperatorSet.GetFramegrabberParam(mCamera, "AcquisitionFrameRate_range", out HTuple hv_AcquisitionFrameRateRange);
            //HOperatorSet.SetFramegrabberParam(mCamera, "AcquisitionFrameRate", hv_AcquisitionFrameRateRange.TupleSelect(1));

            ////設定在最高速下的最大曝光時間
            //HOperatorSet.GetFramegrabberParam(mCamera, "ExposureTime_range", out HTuple hv_ExposureRange);
            //HOperatorSet.SetFramegrabberParam(mCamera, "ExposureTime", hv_ExposureRange.TupleSelect(0));
            //mExposure = hv_ExposureRange.TupleSelect(0);

            //取得影像寬高資訊
            HOperatorSet.GetFramegrabberParam(mCamera, "image_width", out HTuple width);
            HOperatorSet.GetFramegrabberParam(mCamera, "image_height", out HTuple height);
            mImageSize = new Rectangle(0, 0, width.I, height.I);


            ////設定光源觸發
            //HOperatorSet.SetFramegrabberParam(mCamera, "FlashReference", "ExposureActive");
            ////Line0是一般的trigger
            ////Line1是flash ouput
            ////Line2、3是GPIO1、2，對應接線圖中的GPIO1、2的pin腳位置
            //HOperatorSet.SetFramegrabberParam(mCamera, "LineSelector", "Line1");
            ////選擇Line1就不能設定LineMode，否則會跳錯誤
            ////HOperatorSet.SetFramegrabberParam(mCamera, "LineMode", "Output");
            //HOperatorSet.SetFramegrabberParam(mCamera, "LineSource", "FlashActive");
        }

        public bool LoadParameter(string Path)
        {
            mIsLoadParm_CCD = true;
            this.CheckIsSensorColor();
            return mIsLoadParm_CCD;
        }

        /// <summary>
        /// 設定相機觸發模式
        /// </summary>
        /// <param name="mode"></param>
        public void SetTriggerMode(CamTriggerMode mode)
        {
            mTriggerMode = mode;

            //停止取像
            this.Stop();

            switch (mode)
            {
                case CamTriggerMode.Off:
                    {
                        HOperatorSet.SetFramegrabberParam(mCamera, "TriggerSelector", "ExposureStart");
                        HOperatorSet.SetFramegrabberParam(mCamera, "TriggerMode", "Off");
                     
                        break;
                    }
                case CamTriggerMode.Software:
                    {
                        HOperatorSet.SetFramegrabberParam(mCamera, "TriggerMode", "On");
                        HOperatorSet.SetFramegrabberParam(mCamera, "TriggerSource", "Software");

                        break;
                    }
                case CamTriggerMode.Hi_Lo:
                    {
                        //HOperatorSet.SetFramegrabberParam(mCamera, "TriggerSelector", "ExposureStart");
                        HOperatorSet.SetFramegrabberParam(mCamera, "TriggerMode", "On");
                        HOperatorSet.SetFramegrabberParam(mCamera, "TriggerSource", "Line0");             //TriggerSource(Line 0 / 1 / 2 / 3)
                        HOperatorSet.SetFramegrabberParam(mCamera, "TriggerActivation", "FallingEdge");   //TriggerActivation(Rising/ falling Edge)
                        HOperatorSet.SetFramegrabberParam(mCamera, "AcquisitionMode", "Continuous");
                        break;
                    }
                case CamTriggerMode.Lo_Hi:
                    {
                        //HOperatorSet.SetFramegrabberParam(mCamera, "TriggerSelector", "ExposureStart");
                        HOperatorSet.SetFramegrabberParam(mCamera, "TriggerMode", "On");
                        HOperatorSet.SetFramegrabberParam(mCamera, "TriggerSource", "Line0");             //TriggerSource(Line 0 / 1 / 2 / 3)
                        HOperatorSet.SetFramegrabberParam(mCamera, "TriggerActivation", "RisingEdge");    //TriggerActivation(Rising/ falling Edge)
                        HOperatorSet.SetFramegrabberParam(mCamera, "AcquisitionMode", "Continuous");
                        break;
                    }
            }

            //開始取像
            this.Start();
        }

        /// <summary>
        /// 開始取像
        /// </summary>
        public void Start()
        {
            if (mCamera == null || mCamera.Type.ToString() == "EMPTY") return;

            HOperatorSet.GrabImageStart(mCamera, -1);
        }

        /// <summary>
        /// 停止取像
        /// </summary>
        public void Stop()
        {
            if (mCamera == null || mCamera.Type.ToString() == "EMPTY") return;

            if (mIsContinueFrame)
            {
                //停止grab_image_async的非同步取像執行緒，否則執行"do_abort_grab"指令會跳出halcon error #5336
                mThLive.Abort();
                //mIsContinueFrame = false;
            }

            //停止取像後才能設定相機參數
            HOperatorSet.SetFramegrabberParam(mCamera, "do_abort_grab", 1);   
        }

        /// <summary>
        /// 單次取像
        /// </summary>
        private void OneShot()
        {
            if (mCamera == null || mCamera.Type.ToString() == "EMPTY") return;

            if (mTriggerMode == CamTriggerMode.Software)
            {
                HOperatorSet.SetFramegrabberParam(mCamera, "TriggerSoftware", 1);
            }

            HOperatorSet.GrabImageAsync(out HObject img, mCamera, -1);
            HImage himg = new HImage();
            himg.IntegerToObj(img.ObjToInteger(1, -1));
            this.ImageObtained((T)Convert.ChangeType(himg, typeof(T)));
        }

        public void Freeze()
        {
            this.Start();
            this.OneShot();
        }

        /// <summary>
        /// 連續取像(無論軟體或硬體觸發都是使用此方法)
        /// </summary>
        public void ContinueFrame()
        {
            if (mIsContinueFrame == true)
            {
                this.Stop();
                mIsContinueFrame = false;
            }
            else
            {
                this.Start();
                mIsContinueFrame = true;
                mThLive = new Thread(DoContinueFrame);
                mThLive.IsBackground = true;
                mThLive.Start();
            }            
        }

        /// <summary>
        /// 執行連續取像的動作
        /// </summary>
        private void DoContinueFrame()
        {
            while (mIsContinueFrame)
            {
                this.OneShot();

                if (mTriggerMode == CamTriggerMode.Off || mTriggerMode == CamTriggerMode.Software)
                {
                    Thread.Sleep(10);
                    System.Windows.Forms.Application.DoEvents();
                }                    
            }
        }

        /// <summary>
        /// 確認相機是否為彩色
        /// </summary>
        private void CheckIsSensorColor()
        {
            if (mCamera == null || mCamera.Type.ToString() == "EMPTY") return;

            HOperatorSet.GetFramegrabberParam(mCamera, "PixelFormat", out HTuple formate);
            if (formate.S == "Mono8")
                mIsSensorColor = false;
            else
                mIsSensorColor = true;
        }

        public void Release()
        {
            if (mCamera == null || mCamera.Type.ToString() == "EMPTY")
                return;

            this.Stop();  //停止取像
            HOperatorSet.CloseFramegrabber(mCamera);   //關閉取像物件             
            HOperatorSet.CloseAllFramegrabbers();
            mCamera = null;
            mIsInitial_CCD = false;

        }

        /// <summary>
        /// 取得相機控制物件
        /// </summary>
        /// <returns></returns>
        public object GetCamera()
        {
            return this;
        }

        /// <summary>
        /// 取得影像尺寸
        /// </summary>
        /// <param name="width">影像的寬</param>
        /// <param name="height">影像的高</param>
        public void GetImageSize(out int width, out int height)
        {
            width = 0; height = 0;
            width = this.ImageSize.Width;
            height = this.ImageSize.Height;
        }

        /// <summary>
        /// 設定相機曝光時間(單位：微秒)
        /// </summary>
        /// <param name="second"></param>
        public void SetExposureTime(double millisecond)
        {
            if (mCamera == null || mCamera.Type.ToString() == "EMPTY") return;

            //HOperatorSet.GetFramegrabberParam(mCamera, "ExposureTime", out HTuple value);
            HOperatorSet.SetFramegrabberParam(mCamera, "ExposureTime", millisecond);
            SpinWait.SpinUntil(() => false, 100);
        }
        /// <summary>
        /// 設定相機frame rate
        /// </summary>
        /// <param name="second"></param>
        public void SetFrameRate(double value)
        {
            if (mCamera == null || mCamera.Type.ToString() == "EMPTY") return;

            //HOperatorSet.GetFramegrabberParam(mCamera, "ExposureTime", out HTuple value);
            HOperatorSet.SetFramegrabberParam(mCamera, "AcquisitionFrameRate", value);
            SpinWait.SpinUntil(() => false, 100);
        }

        /// <summary>
        /// 設定光源Strobe起始與結束時間(單位：微秒)
        /// </summary>
        /// <param name="flashStartDelay">起始時間(正值表示延遲點亮，負值表示提早點亮)</param>
        /// <param name="flashEndDelay">結束時間(正值表示延關閉，負值表示提早關閉)</param>
        public void SetStrobeDuration(double flashStartDelay = 0, double flashEndDelay = 0)
        {
            if (mCamera == null || mCamera.Type.ToString() == "EMPTY") return;

            //HOperatorSet.GetFramegrabberParam(mCamera, "FlashDuration", out HTuple value);
            HOperatorSet.SetFramegrabberParam(mCamera, "FlashStartDelay", flashStartDelay);
            HOperatorSet.SetFramegrabberParam(mCamera, "FlashEndDelay", flashEndDelay);
        }

        public void LinkingGrabImageEvent(CamTriggerMode mode)
        {
            throw new NotImplementedException();
        }

        public void DeleteGrabImageEvent(CamTriggerMode mode)
        {
            throw new NotImplementedException();
        }

        public void SetAcqResourceIndex(int value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 設定相機Strobe模式
        /// U3相機無法像UI相機設定不同光源點亮模式，僅能設定光源點亮時間
        /// </summary>
        /// <param name="mode">模式選擇</param>
        public void SetStrobeMode(CamStrobeMode mode)
        {
            ////設定光源觸發
            //HOperatorSet.SetFramegrabberParam(mCamera, "FlashReference", "ExposureActive");
            ////Line0是一般的trigger
            ////Line1是flash ouput
            ////Line2、3是GPIO1、2，對應接線圖中的GPIO1、2的pin腳位置
            //HOperatorSet.SetFramegrabberParam(mCamera, "LineSelector", "Line1");
            ////選擇Line1就不能設定LineMode，否則會跳錯誤
            ////HOperatorSet.SetFramegrabberParam(mCamera, "LineMode", "Output");
            //HOperatorSet.SetFramegrabberParam(mCamera, "LineSource", "FlashActive");

            switch (mode)
            {
                case CamStrobeMode.Off:
                    //HOperatorSet.SetFramegrabberParam(mCamera, "FlashReference", "ExposureActive");
                    HOperatorSet.SetFramegrabberParam(mCamera, "LineSource", "Off");

                    break;

                case CamStrobeMode.TriggerHighActive:
                    HOperatorSet.SetFramegrabberParam(mCamera, "FlashReference", "ExposureActive");
                    HOperatorSet.SetFramegrabberParam(mCamera, "LineSelector", "Line1");
                    HOperatorSet.SetFramegrabberParam(mCamera, "LineSource", "FlashActive");

                    break;

                case CamStrobeMode.TriggerLowActive:
                    HOperatorSet.SetFramegrabberParam(mCamera, "FlashReference", "ExposureActive");
                    HOperatorSet.SetFramegrabberParam(mCamera, "LineSelector", "Line1");
                    HOperatorSet.SetFramegrabberParam(mCamera, "LineSource", "FlashActive");

                    break;

                case CamStrobeMode.ConstantHigh:
                    HOperatorSet.SetFramegrabberParam(mCamera, "UserOutputSelector", "UserOutput0");
                    HOperatorSet.SetFramegrabberParam(mCamera, "LineSource", "UserOutput0");
                    HOperatorSet.SetFramegrabberParam(mCamera, "UserOutputValue", 1);
                    break;

                case CamStrobeMode.ConstantLow:
                    HOperatorSet.SetFramegrabberParam(mCamera, "UserOutputSelector", "UserOutput0");
                    HOperatorSet.SetFramegrabberParam(mCamera, "LineSource", "UserOutput0");
                    HOperatorSet.SetFramegrabberParam(mCamera, "UserOutputValue", 0);
                    break;

                    //case CamStrobeMode.FreerunHighActive:                 
                    //    HOperatorSet.SetFramegrabberParam(mCamera, "FlashReference", "ExposureActive");
                    //    HOperatorSet.SetFramegrabberParam(mCamera, "LineSelector", "Line0");
                    //    HOperatorSet.SetFramegrabberParam(mCamera, "LineMode", "Output");
                    //    HOperatorSet.SetFramegrabberParam(mCamera, "LineSource", "FlashActive");

                    //    break;

                    //case CamStrobeMode.FreerunLowActive:
                    //    HOperatorSet.SetFramegrabberParam(mCamera, "FlashReference", "ExposureActive");
                    //    HOperatorSet.SetFramegrabberParam(mCamera, "LineSelector", "Line0");
                    //    HOperatorSet.SetFramegrabberParam(mCamera, "LineMode", "Output");
                    //    HOperatorSet.SetFramegrabberParam(mCamera, "LineSource", "FlashActive");

                    break;

                default:

                    break;
            }

        }

        public void Live()
        {
            throw new NotImplementedException();
        }
    }
}
