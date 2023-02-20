using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;
using gluck.camera;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using Authority;
using System.Collections.Concurrent;
using System.Diagnostics;
using Recipe;
using System.Drawing;
using LanguageChanged;
using IO;
using LightModule;
using HWindowDisplay;
using HslCommunication;
using Dongle;
using HslCommunication.Profinet.Melsec;

namespace CenterRegistration
{
    public class CenterRegistrationPresent
    {
        //主UI
        private CenterRegistrationForm mCenterRegistrationForm;
        private AuthorityControl mAuthorityControl = null;
        //Authority 
        private AuthorityLogInForm mLogIn;

        public MainControls MainControlUI = MainControls.None;
        public bool IsLive { get; set; }
        //public CameraManagementBase CamManagement { get; set; }
        public RecipeManagement CurrentRecipe { get; set; } = new RecipeManagement();

        #region 存圖
        /// <summary>判斷是否儲存影像(全存)</summary>
        public bool IsSaveImage { get; set; } = true;
        public bool IsCheckDateChange { get; set; } = true;
        public string SaveImageDirectory { get; set; } = $"{Application.StartupPath}\\SaveImage";
        /// <summary>影像儲存路徑(年/日期/)</summary>
        public string SaveImagePath { get; set; } = string.Empty;//$"{Application.StartupPath}\\SaveImage";
        public ConcurrentQueue<SaveImageData> SaveImageDataQueue { get; set; } = new ConcurrentQueue<SaveImageData>();

        public string TxtFileName { get; set; } = string.Empty;
        #endregion
        /// <summary>計算時間使用的旗標</summary>
        public bool IsTimer { get; set; } = false;
        /// <summary>系統參數</summary>
        public SystemParams ParamsSystem { get; set; } = new SystemParams();
        //新耀光光源
        public LightHighBrightTech HightBrightTechLight { get; set; } = new LightHighBrightTech();
        //<summary> 顯示字串於SmartWindow </summary>
        HWindowDisplayFont mWindowDisplayFont = new HWindowDisplayFont();
        /// <summary> 開始檢測 </summary>
        public bool IsStart { get; set; } = false;
        /// <summary> 存放檢測影像 </summary>
        public ConcurrentQueue<HImage>[] ImageQueue { get; set; } = null;//new ConcurrentQueue<HImage>();
        //<summary>收到IO取像的手動執行續</summary>
        public ManualResetEvent GetInspectImageEvent { get; set; } = new ManualResetEvent(false);
        //<summary> 相機物件</summary>
        public CameraManagementBase[] CamManagement { get; set; }
        //<summary> 相機最多的數量</summary>
        public const int CAMERA_MAX_COUNT = 4;
        //<summary> 實際相機數量</summary>
        public int CameraCount { get; set; } = 1;
        //<summary> 演算法執行物件</summary>
        public Alg Alg_ { get; set; }
        /// <summary>
        /// 站別
        /// </summary>
        public Station CurrentStation = Station.DIFFUSION_RADIUS;
        /// <summary>
        /// diffusion ridus 第一片當作基準片旗標
        /// </summary>
        public bool IsDiffusionRadius_Standard { get; set; } = false;
        /// <summary>
        /// 保護單一執行續執行影像Queue
        /// </summary>
        public object[] LockQueue;
        /// <summary>
        /// 是否顯示ROI
        /// </summary>
        public bool IsDisplayRoi { get; set; } = true;
        /// <summary>
        /// 第一次按下檢測,計算完N張影像區間計算
        /// </summary>
        private int FirstTimeIntervalCount { get; set; } = 30;
        /// <summary>
        /// 第一次更新完後,後面開始改成N張更新區間
        /// </summary>
        private int UpdateIntervalCount { get; set; } = 1;
        /// <summary>
        /// 存放檢測是邊界資訊的容器
        /// </summary>
        public List<BorderInfo> RadiusBorderInfoList { get; set; } = new List<BorderInfo>();
        /// <summary>
        /// MsChart更新最近的N張影像
        /// </summary>
        public const int RECENT_IMAGE_COUNT = 500;

        public double[] DistanceArrayRadius { get; set; } = new double[RECENT_IMAGE_COUNT];

        public CenterRegistrationPresent(CenterRegistrationForm form)
        {
            this.mCenterRegistrationForm = form;
            //演算法
            this.Alg_ = new Alg(this);
            //this.CamManagement = new DalsaCameraManagement();
            this.CreateSaveImageFolder();
            new Thread(new ThreadStart(SaveImage)) { IsBackground = true }.Start();

            this.LockQueue = new object[CenterRegistrationPresent.CAMERA_MAX_COUNT];
            for (int i = 0; i < CAMERA_MAX_COUNT; i++)
            {
                this.LockQueue[i] = new object();
            }

            this.AddProductionLinking();
        }

        /// <summary>檢測事件連結</summary>
        public void AddProductionLinking()
        {
        }
        public void SelectTool(Tool selectedIndex)
        {
            switch (selectedIndex)
            {
                case Tool.Authority:
                    if (mLogIn == null)
                    {
                        mLogIn = new AuthorityLogInForm();
                        mLogIn.StartPosition = FormStartPosition.CenterScreen;
                        mLogIn.TopMost = true;
                        mLogIn.ShowDialog();

                        string msg = string.Empty;
                        if (mLogIn.IsLogInPass)
                        {
                            mCenterRegistrationForm.IsLogInPass = true;

                            msg = Messenger.GetString("M1002", this.mCenterRegistrationForm.LanguageMode);
                            this.ShowAndLogMessage(msg);

                            this.mCenterRegistrationForm.CurrentAuthority.Level = mLogIn.LogInAuthority.Level;
                            this.mCenterRegistrationForm.CurrentAuthority.ID = mLogIn.LogInAuthority.ID;

                            this.mCenterRegistrationForm.DoUpdateAuthorityName(mLogIn.LogInAuthority.Level);
                            this.mCenterRegistrationForm.DoUpdateUserName(mLogIn.LogInAuthority.ID);
                        }
                        else
                        {
                            mCenterRegistrationForm.IsLogInPass = false;

                            msg = Messenger.GetString("M1001", this.mCenterRegistrationForm.LanguageMode);
                            this.ShowAndLogMessage(msg);
                        }
                        mLogIn = null;
                    }
                    break;
                case Tool.EditMember:
                    if (mAuthorityControl == null)
                    {
                        if (this.mCenterRegistrationForm.CurrentAuthority.Level == AuthorityLevel.Admin)
                        {
                            mAuthorityControl = new AuthorityControl();
                            InitialSubForm(mAuthorityControl);
                        }
                        else
                        {
                            var msg = Messenger.GetString("M1009", this.mCenterRegistrationForm.LanguageMode);
                            this.ShowAndLogMessage(msg);
                        }
                    }
                    else
                        this.ChangeWindowState(mAuthorityControl);
                    break;
            }
        }
        public void InitializeCameras()
        {
            string msg = Messenger.GetString("M1013", this.mCenterRegistrationForm.LanguageMode);
            this.mCenterRegistrationForm.UpdateListBoxAndShowAndLogMessage(msg);
            //關閉所有相機連接
            //U3_IDSCameraMangement.CloseAllFramegrabbers();
            GigE_FLIRCameraMangement.CloseAllFramegrabbers();

            //判斷實際工作站所需要的相機數量
            if (this.CurrentStation == Station.DIFFUSION_RADIUS)
            {
                this.CameraCount = 1;
            }
            else if (this.CurrentStation == Station.CVD)
            {
                this.CameraCount = 3;
            }
            else if (this.CurrentStation == Station.ECTHING)
            {
                this.CameraCount = 4;
            }
            //建立相機物件(建立相機最大的數量,多建立因為用不到所以沒差)
            this.CamManagement = new CameraManagementBase[CenterRegistrationPresent.CAMERA_MAX_COUNT];
            this.ImageQueue = new ConcurrentQueue<HImage>[CenterRegistrationPresent.CAMERA_MAX_COUNT];
            for (int i = 0; i < CenterRegistrationPresent.CAMERA_MAX_COUNT; i++)
            {
                this.ImageQueue[i] = new ConcurrentQueue<HImage>();
            }

            //根據實際機種搭配的相機數量建立實體物件
            for (int i = 0; i < this.CameraCount; i++)
            {
                //this.CamManagement[i] = new U3_IDSCameraMangement();
                this.CamManagement[i] = new GigE_FLIRCameraMangement();
            }
            ///如果相機數量為2支時就用for迴圈initial因為內部infoGrabber的hv_BoardsInfoValues資訊為2,所以hv_BoardsInfoValues[0],hv_BoardsInfoValues[1]不會有任何問題
            ///但是今天如果只有1支相機hv_BoardsInfoValues資訊為1,hv_BoardsInfoValues[0]就需要判斷是哪一支相機,因為hv_BoardsInfoValues只會拋出一支相機的資訊
            //var devicesName = U3_IDSCameraMangement.GetDeviceName();
            var devicesName = GigE_FLIRCameraMangement.GetDeviceName();
            CameraManagementBase[] tmpCamManagement = new CameraManagementBase[devicesName.Length];

            for (int i = 0; i < devicesName.Length; i++)
            {
                //tmpCamManagement[i] = new U3_IDSCameraMangement();
                tmpCamManagement[i] = new GigE_FLIRCameraMangement();
                tmpCamManagement[i].InitialCam(i, CamTriggerMode.Off);
                Thread.Sleep(100);
            }

            var recipeValue = this.CurrentRecipe.XmlParams.ParamsCamera;

            //int fps = 8;
            int exposureTime = 30000;
            for (int i = 0; i < devicesName.Length; i++)
            {
                var serialNumber = tmpCamManagement[i].GetDeviceSerialNumber();

                if (serialNumber == this.ParamsSystem.Device1_SerialNumber)
                {
                    this.CamManagement[(int)CameraIndex.First] = tmpCamManagement[i];
                    this.CamManagement[(int)CameraIndex.First].CamIndex = 0;
                    //this.CamManagement[(int)CameraIndex.First].SetFrameRate(fps);
                    this.CamManagement[(int)CameraIndex.First].SetExposureTime(exposureTime);
                }
                else if (serialNumber == this.ParamsSystem.Device2_SerialNumber)
                {
                    this.CamManagement[(int)CameraIndex.Second] = tmpCamManagement[i];
                    this.CamManagement[(int)CameraIndex.Second].CamIndex = 1;
                    //this.CamManagement[(int)CameraIndex.Second].SetFrameRate(fps);
                    this.CamManagement[(int)CameraIndex.Second].SetExposureTime(exposureTime);
                }
                else if (serialNumber == this.ParamsSystem.Device3_SerialNumber)
                {
                    this.CamManagement[(int)CameraIndex.Third] = tmpCamManagement[i];
                    this.CamManagement[(int)CameraIndex.Third].CamIndex = 2;
                    //this.CamManagement[(int)CameraIndex.Third].SetFrameRate(fps);
                    this.CamManagement[(int)CameraIndex.Third].SetExposureTime(exposureTime);
                }
                else if (serialNumber == this.ParamsSystem.Device4_SerialNumber)
                {
                    this.CamManagement[(int)CameraIndex.Fourth] = tmpCamManagement[i];
                    this.CamManagement[(int)CameraIndex.Fourth].CamIndex = 3;
                    //this.CamManagement[(int)CameraIndex.Fourth].SetFrameRate(fps);
                    this.CamManagement[(int)CameraIndex.Fourth].SetExposureTime(exposureTime);
                }
            }
            string msg2 = string.Empty;

            for (int i = 0; i < this.CameraCount; i++)
            {
                if (i == (int)CameraIndex.First)
                    msg2 = this.CamManagement[i].IsInitial ? "M1110" : "M1111"; //M1110 相機1 初始化成功/失敗
                else if (i == (int)CameraIndex.Second)
                    msg2 = this.CamManagement[i].IsInitial ? "M1112" : "M1113"; //M1112 相機2 初始化成功/失敗
                else if (i == (int)CameraIndex.Third)
                    msg2 = this.CamManagement[i].IsInitial ? "M1114" : "M1115"; //M1114 相機3 初始化成功/失敗
                else if (i == (int)CameraIndex.Fourth)
                    msg2 = this.CamManagement[i].IsInitial ? "M1116" : "M1117"; //M1114 相機3 初始化成功/失敗

                this.mCenterRegistrationForm.UpdateListBoxAndShowAndLogMessage(Messenger.GetString(msg2, this.mCenterRegistrationForm.LanguageMode));
            }
        }
        public bool InitializeSystemParams()
        {
            return this.ParamsSystem.Import();
        }

        public void InitializeRecipe()
        {
            var msg = Messenger.GetString("M1024", this.mCenterRegistrationForm.LanguageMode);
            this.mCenterRegistrationForm.UpdateListBoxAndShowAndLogMessage(msg);

            var lastName = this.GetLastRecipeName();

            var isImportSuccessed = false;

            if (lastName != string.Empty)
            {
                isImportSuccessed = this.GetCurrentRecipe(lastName);

                //var p = mPresent.CurrentRecipe;

                if (!isImportSuccessed)
                    lastName = "";
            }

            if (!isImportSuccessed)
                msg = lastName + " " + Messenger.GetString("M1026", this.mCenterRegistrationForm.LanguageMode);//"Load " + lastName + " Recipe is failed.";
            else
            {
                msg = lastName + " " + Messenger.GetString("M1025", this.mCenterRegistrationForm.LanguageMode);//"Load " + lastName + " Recipe is successed.";

                this.CurrentRecipe.RecipeName = lastName;
                this.mCenterRegistrationForm.DoUpdateRecipeName(lastName);
            }
            this.mCenterRegistrationForm.UpdateListBoxAndShowAndLogMessage(msg);
        }
        /// <summary>顯示LOG於UI及寫入LOG訊息</summary>
        public void ShowAndLogMessage(string msg2)
        {
            this.ShowMessage(msg2);
            this.LogMessage(msg2);
        }

        public void ShowMessage(string msg)
        {
            if (msg != string.Empty)
                this.mCenterRegistrationForm.ShowMessage(msg);
        }
        public void LogMessage(string msg)
        {
            if (msg != string.Empty)
                this.mCenterRegistrationForm.LogMessage(msg);
        }
        private void InitialSubForm(Form subForm)
        {
            subForm.Show();
            subForm.TopMost = true;
            subForm.FormClosed += Sub_FormClosed;
        }
        public void ChangeWindowState(Form subForm)
        {
            if (subForm == null) return;

            if (subForm.WindowState == FormWindowState.Minimized)
                subForm.WindowState = FormWindowState.Normal;
        }
        private void Sub_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (sender == mAuthorityControl) { mAuthorityControl = null; }
        }
        //取得上一次recipe name
        public string GetLastRecipeName()
        {
            LastRecipe lp = new LastRecipe();
            lp.Import();

            return lp.LastRecipeName;
        }
        public bool GetCurrentRecipe(string lastName)
        {
            RecipeFiles f = new RecipeFiles();

            var isImportSuccess = f.Import(lastName);

            this.CurrentRecipe = isImportSuccess ? f.CurrentRecipe : new RecipeManagement();
            this.CurrentRecipe.RecipeName = lastName;

            return isImportSuccess;
        }

        public bool InitializeLight()
        {
            //return true;
            //光源連結 連結成功 光源數值給0
            this.HightBrightTechLight.Port = "COM3";//this.ParamsSystem.Port;
            this.HightBrightTechLight.BaudRate = 9600;//this.ParamsSystem.BaudRate;

            var isHightBrightTechConnected = this.HightBrightTechLight.Connect();

            if (isHightBrightTechConnected)
            {
                var msg = this.FormatHightBrightTechLightValue(0);

                this.HightBrightTechLight.Write(msg);

                return true;
            }
            else
            {
                return false;
            }


            //this.ImacLight.IP = this.ParamsSystem.IP;

            //var isImacConneted = this.ImacLight.Connect();

            //if (isHightBrightTechConnected || isImacConneted)
            //{
            //    var msg = this.FormatHightBrightTechLightValue(0);

            //    this.HightBrightTechLight.Write(msg);
            //    this.ImacLight.SetLightValue(0);

            //    return true;
            //}
            //else
            //{
            //    return false;
            //}
        }
        public string FormatHightBrightTechLightValue(int value)
        {
            return string.Format("{0},{1}\r\n", 1, value);
        }
        public void ContinueGrabImage()
        {
            this.DoContinueGrabImage();
            //new Thread(new ThreadStart(DoContinueGrabImage)) { IsBackground = true }.Start();
        }
        /// <summary>
        /// 連續取相
        /// </summary>
        public void DoContinueGrabImage()
        {
            if (IsLive)
            {
                for (int i = 0; i < this.CameraCount; i++)
                {
                    if (this.CamManagement[i].IsInitial)
                    {
                        this.CamManagement[i].ContinueFrame();
                    }
                }
            }
            else
            {
                for (int i = 0; i < this.CameraCount; i++)
                {
                    if (this.CamManagement[i].IsInitial)
                    {
                        this.CamManagement[i].StopContinueFrame();
                    }
                }
            }
            //while (IsLive)
            //{
            //    for (int i = 0; i < this.CameraCount; i++)
            //    {
            //        if (this.CamManagement[i].IsInitial)
            //        {
            //            this.CamManagement[i].Camera.Freeze();
            //        }
            //    }
            //    SpinWait.SpinUntil(() => false, 10);
            //}

        }
        /// <summary>
        /// 單張取像功能
        /// </summary>
        public async void GrabSingleImage(int cameraIndex)
        {
            await Task.Run(() =>
            {
                if (this.CamManagement[cameraIndex].IsInitial)
                {
                    this.CamManagement[cameraIndex].Camera.Freeze();
                }
            });
        }
        public void UpdateMainControl(MainControls mainControl)
        {
            this.mCenterRegistrationForm.UpdateMainControl(mainControl);
        }
        public void UpdateSubControl(SubControls subControl)
        {
            this.mCenterRegistrationForm.UpdateSubControl(subControl);
        }
        public void LinkingCameraGrabing()
        {
            for (int i = 0; i < this.CameraCount; i++)
            {
                if (this.CamManagement[i].IsInitial)
                {
                    this.CamManagement[i].GrabImage -= this.mCenterRegistrationForm.DoActionWhenReceivedImage;
                    Thread.Sleep(50);
                    this.CamManagement[i].GrabImage += this.mCenterRegistrationForm.DoActionWhenReceivedImage;
                }
            }
            //var devicesName = U3_FILRCameraMangement.GetDeviceName();

            //for (int i = 0; i < devicesName.Length; i++)
            //{
            //    if (this.CamManagement[i].IsInitial)
            //    {
            //        //this.CamManagement[i].Camera.Stop();
            //        this.CamManagement[i].GrabImage -= this.mCenterRegistrationForm.DoActionWhenReceivedImage;
            //        Thread.Sleep(50);
            //        this.CamManagement[i].GrabImage += this.mCenterRegistrationForm.DoActionWhenReceivedImage;
            //    }
            //}
        }

        /// <summary>
        /// 將粗定位和細定位的數值寫入物件方便後面存取
        /// </summary>
        public void ImportStandardMatchInfo()
        {
            //this.RoughStandardMatchInfo.MatchRow = this.ParamsSystem.Rough_Standard_Row;
            //this.RoughStandardMatchInfo.MatchColumn = this.ParamsSystem.Rough_Standard_Column;
            //this.RoughStandardMatchInfo.MatchColumn = this.ParamsSystem.Rough_Standard_Angle;
            ////細定位標準片定位數值
            //this.PreciseStandardMatchInfo.MatchRow = this.ParamsSystem.Precise_Standard_Row;
            //this.PreciseStandardMatchInfo.MatchColumn = this.ParamsSystem.Precise_Standard_Column;
            //this.PreciseStandardMatchInfo.MatchColumn = this.ParamsSystem.Precise_Standard_Angle;
        }
        /// <summary>
        /// 開始檢測
        /// </summary>
        public void StartInspection()
        {
            new Thread(new ThreadStart(Inspect)) { IsBackground = true }.Start();
        }
        private void Inspect()
        {
            DistanceArrayRadius = new double[RECENT_IMAGE_COUNT];

            int inspectedCount = 0;
            int firstIntervalCount = this.FirstTimeIntervalCount;

            Stopwatch time = new Stopwatch();

            if (this.CurrentStation == Station.DIFFUSION_RADIUS)
            {
                this.RadiusBorderInfoList.Clear();
                //更新區間設定 如果FirstTimeIntervalCount = 30 ,UpdateIntervalCount =1 (0~29)(1~30)(2~31)
                //如果FirstTimeIntervalCount = 20 ,UpdateIntervalCount = 2 (0~19)(1~20)(2~21)
                int rangeCount = this.FirstTimeIntervalCount;

                bool isJugeGood = false;
                var roi = this.CurrentRecipe.RegistrateParams.Cam1_ROI;
                var winSt = this.mCenterRegistrationForm.GetSmartWindow((int)Station.DIFFUSION_RADIUS);

                BorderInfo borderMin = new BorderInfo();
                BorderInfo borderMax = new BorderInfo();

                //int startIndex = 0; int endIndex = 0;

                while (!this.ImageQueue[(int)Station.DIFFUSION_RADIUS].IsEmpty || this.IsStart)
                {
                    this.ImageQueue[(int)Station.DIFFUSION_RADIUS].TryDequeue(out HImage img);
                    if (img == null)
                        continue;
                    time.Restart();

                    if (this.ImageQueue[(int)Station.DIFFUSION_RADIUS].Count > 5)
                    {
                        Console.WriteLine("> 5 ");
                        this.ClearImageQueue((int)Station.DIFFUSION_RADIUS);
                    }

                    //SaveImageData saveData = new SaveImageData();
                    //saveData.Image.Dispose();
                    //saveData.Image = img.CopyObj(1, -1);

                    //saveData.Name = this.GetInspectionFinishedTime();
                    //this.SaveImageDataQueue.Enqueue(saveData);

                    //執行演算法
                    this.Alg_.Diffusion_radius_Alg(img, roi, out BorderInfo borderInfo);

                    //time.Stop();
                    //Console.WriteLine("alg " + time.ElapsedMilliseconds.ToString());

                    //判斷是否為空
                    HObject empty;
                    HOperatorSet.GenEmptyObj(out empty);
                    HOperatorSet.TestEqualObj(borderInfo.TargetLineRegion, empty, out HTuple isEqual);

                    if (isEqual == 0 && borderInfo.Distance > 1)
                    {
                        this.RadiusBorderInfoList.Add(borderInfo);
                        inspectedCount++;
                    }
                    empty.Dispose();
                    isEqual.Dispose();

                    //計算n張影像最大最小和區間值
                    if (inspectedCount == firstIntervalCount)
                    {
                        inspectedCount = 0;
                        firstIntervalCount = this.UpdateIntervalCount;

                        borderMin.Dispose(); borderMax.Dispose();
                        this.GetMaxMinBorder(this.RadiusBorderInfoList, this.UpdateIntervalCount, out borderMin, out borderMax);

                        var offset = this.Alg_.GetDistanceBetweenLine1AndLine2(borderMin.TargetLineRegion, borderMax.TargetLineRegion);
                        var realOffset = Math.Round(offset * this.ParamsSystem.CameraResolution, 2);

                        //chart
                        this.DistanceArrayRadius[this.DistanceArrayRadius.Length - 1] = realOffset / 1000.0;
                        Array.Copy(this.DistanceArrayRadius, 1, this.DistanceArrayRadius, 0, this.DistanceArrayRadius.Length - 1);

                        this.UpdateDiffusionRadiusChart(this.DistanceArrayRadius);
                        //this.BorderInfoList.Clear();
                        this.UpdateDiffusionRadiusInterval(realOffset);
                        isJugeGood = (realOffset > this.CurrentRecipe.XmlParams.ParamsRule.Radius_JudgeValue) ? false : true;
                    }
                    //顯示影像
                    this.mCenterRegistrationForm.ShowImageOnHalconWindow((int)Station.DIFFUSION_RADIUS, img);

                    //顯示Range範圍                  
                    if (borderMin.TargetLineRegion.IsInitialized() && borderMax.TargetLineRegion.IsInitialized())
                    {
                        var c = (isJugeGood) ? "spring green" : "red";

                        this.ShowObjOnWindow(borderMin.TargetLineRegion, winSt, c, 5);
                        this.ShowObjOnWindow(borderMax.TargetLineRegion, winSt, c, 5);
                    }
                    //顯示框選區域
                    this.ShowObjOnWindow(roi, winSt, "violet", 2);
                    //顯示當下結果
                    this.ShowObjOnWindow(borderInfo.TargetLineRegion, winSt, "blue", 2);
                    time.Stop();
                    // Console.WriteLine("total Time " + time.ElapsedMilliseconds.ToString());

                    SpinWait.SpinUntil(() => false, 10);
                }
            }
        }
        /// <summary>
        /// 取得最大和最小的線段
        /// </summary>
        private void GetMaxMinBorder(List<BorderInfo> infoList, int updateCount, out BorderInfo borderMin, out BorderInfo borderMax)
        {
            if (infoList.Count > 0)
            {

                var borders = infoList.OrderBy(o => o.Distance.D);
                borderMin = borders.First().Clone();
                borderMax = borders.Last().Clone();

                for (int i = 0; i < updateCount; i++)
                {
                    var deleteObj = infoList[i];
                    infoList.Remove(deleteObj);
                }
            }
            else
            {
                borderMin = new BorderInfo();
                borderMax = new BorderInfo();
            }
        }
        /// <summary>
        /// 更新最大最小和區間數值(Radius)
        /// </summary>
        private void UpdateDiffusionRadiusInterval(double realOffset)
        {
            this.mCenterRegistrationForm.UpdateDiffusionRadiusInterval(realOffset);
        }
        /// <summary>
        /// 更新畫布(Radius)
        /// </summary>
        /// <param name="distanceArray"></param>
        private void UpdateDiffusionRadiusChart(double[] distanceArray)
        {
            this.mCenterRegistrationForm.UpdateDiffusionRadiusChart(distanceArray);
        }
        /// <summary>
        /// 更新最大最小和區間數值(Mozart)
        /// </summary>
        private void UpdateDiffusionMozartInterval(int camIndex, double realOffset)
        {
            this.mCenterRegistrationForm.UpdateDiffusionMozartInterval(camIndex, realOffset);
        }
        /// <summary>
        /// 更新畫布(Mozart)
        /// </summary>
        /// <param name="distanceArray"></param>
        private void UpdateDiffusionMozartChart(int camIndex, double[] distanceArray)
        {
            this.mCenterRegistrationForm.UpdateDiffusionMozartChart(camIndex,distanceArray);
        }
        /// <summary>
        /// 在SmartWindow顯示Hobject
        /// </summary>
        public void ShowObjOnWindow(HObject obj, HWindow winSt, HTuple color, HTuple width)
        {
            try
            {
                HOperatorSet.SetDraw(winSt, "margin");
                HOperatorSet.SetLineWidth(winSt, width);
                HOperatorSet.SetColor(winSt, color);
                HOperatorSet.DispObj(obj, winSt);
            }
            catch (Exception ex)
            {

            }
        }
        /// <summary>
        /// 取得檢測完畢的時間
        /// </summary>
        public string GetInspectionFinishedTime()
        {
            var currentDay = $"{ DateTime.Now.ToString("MMdd")}";
            var currentHour = $"{ DateTime.Now.ToString("HHmmss")}";

            var currentTime = currentDay + "-" + currentHour;

            return currentTime;
        }
        public string GetMessangerByIndex(string indexNumber)
        {
            return Messenger.GetString(indexNumber, this.mCenterRegistrationForm.LanguageMode);
        }
        public void ShowAndLogMessageByMsgIndex(string msgIndex)
        {
            this.ShowAndLogMessage(Messenger.GetString(msgIndex, this.mCenterRegistrationForm.LanguageMode));
        }

        public void ClearSubUserControl()
        {
            this.mCenterRegistrationForm.ClearSubUserControl();
        }
        public void UpdateInspectionCount(int total, int passCount, int ngCount)
        {
            this.mCenterRegistrationForm.UpdateInspectionCount(total, passCount, ngCount);
        }
        public string GetMessageByMsgIndex(string msgIndex)
        {
            return Messenger.GetString(msgIndex, this.mCenterRegistrationForm.LanguageMode);
        }
        /// <summary>取得當前主Form語言</summary>
        public ChangeLanguage.Language GetLanguageMode()
        {
            return this.mCenterRegistrationForm.LanguageMode;
        }
        public void set_display_font(int size)
        {
            //var winSt = this.mCCLRegistrationForm.GetImageControlID();
            //mWindowDisplayFont.set_display_font(winSt, size, "sans", "true", "false");
        }
        public void DisplayFont(int size, string color, string msg, int hv_Row = 0, int hv_Column = 0, string hv_CoordSystem = "window", string hv_Box = "false")
        {
            //var winSt = this.mCCLRegistrationForm.GetImageControlID();

            //this.set_display_font(size);
            //mWindowDisplayFont.disp_message(winSt, msg, hv_CoordSystem, hv_Row, hv_Column, color, hv_Box);
        }
        public HTuple GetImageControlID(int cameraIndex)
        {
            return this.mCenterRegistrationForm.GetImageControlID(cameraIndex);
        }
        public HWindow GetSmartWindow(int cameraIndex)
        {
            return this.mCenterRegistrationForm.GetSmartWindow(cameraIndex);
        }
        public HObject GetImageControlImage(int cameraIndex)
        {
            return this.mCenterRegistrationForm.GetImageControlImage(cameraIndex);
        }
        public void ClearImageQueue(int camIndex)
        {
            lock (this.LockQueue[camIndex])
            {
                while (!this.ImageQueue[camIndex].IsEmpty)
                {
                    this.ImageQueue[camIndex].TryDequeue(out HImage image);
                    SpinWait.SpinUntil(() => false, 1);
                }
            }
        }
        public void ClearAllImageQueue()
        {
            for (int i = 0; i < CenterRegistrationPresent.CAMERA_MAX_COUNT; i++)
            {
                while (!this.ImageQueue[i].IsEmpty)
                {
                    this.ImageQueue[i].TryDequeue(out HImage image);
                    SpinWait.SpinUntil(() => false, 1);
                }
            }
        }
        public void TurnOnLight(LIGHT_INDEX lightIndex)
        {
            this.mCenterRegistrationForm.TurnOnLight(lightIndex);
        }
        public void TurnOffLight(LIGHT_INDEX lightIndex)
        {
            this.mCenterRegistrationForm.TurnOffLight(lightIndex);
        }

        #region SaveImage setting
        private void CreateSaveImageFolder()
        {
            var directoryPath = this.SaveImageDirectory; //$"{Application.StartupPath}\\SaveImage";

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            //取得日期(年)資訊
            var yearDate = $"{ DateTime.Now.ToString("yyyy")}";
            var yearDirectory = directoryPath + "\\" + yearDate;
            //年度目錄
            if (!Directory.Exists(yearDirectory))
                Directory.CreateDirectory(yearDirectory);

            //取得日期(日)資訊
            var dayDate = $"{ DateTime.Now.ToString("MMdd")}";
            var pathDirectory = yearDirectory + "\\" + dayDate;
            //日期目錄
            if (!Directory.Exists(pathDirectory))
                Directory.CreateDirectory(pathDirectory);

            this.SaveImagePath = pathDirectory;

            this.DeleteSaveImageFolderDate();
        }
        public void SaveImage()
        {
            while (this.IsSaveImage)
            {
                try
                {
                    if (!this.SaveImageDataQueue.IsEmpty)
                    {
                        this.CheckSaveImageFolderDate();

                        SaveImageData data;
                        this.SaveImageDataQueue.TryDequeue(out data);

                        HObject ho_ImageZoom;
                        HOperatorSet.GenEmptyObj(out ho_ImageZoom);

                        HTuple hv_Height = null, hv_Width = null, hv_Ratio = null;
                        HTuple hv_newHeight = null, hv_newWidth = null;

                        HOperatorSet.HeightWidthRatio(data.Image, out hv_Height, out hv_Width, out hv_Ratio);
                        //縮小影像
                        var ratio = 1;//this.ParamsSystem.SaveImageRatio;

                        HOperatorSet.TupleInt(hv_Height / ratio, out hv_newHeight);//縮小1/8
                        HOperatorSet.TupleInt(hv_Width / ratio, out hv_newWidth);

                        ho_ImageZoom.Dispose();
                        HOperatorSet.ZoomImageSize(data.Image, out ho_ImageZoom, hv_newWidth, hv_newHeight, "constant");

                        HObject ho_RegionZoom;
                        HOperatorSet.GenEmptyObj(out ho_RegionZoom);

                        string format = "bmp";//this.ParamsSystem.SaveImageFormat;
                        string filename = Path.Combine($"{this.SaveImagePath}", data.Name + "." + format);

                        HOperatorSet.WriteImage(ho_ImageZoom, format, 0, filename);

                        ////將NG區域畫上紅色區域
                        //if (data.IsNG)
                        //{
                        //    HOperatorSet.OpenWindow(0, 0, hv_newWidth, hv_newHeight, 0, "invisible", "", out HTuple hv_WindowHandle);
                        //    HOperatorSet.SetDraw(hv_WindowHandle, "margin");
                        //    HOperatorSet.SetColor(hv_WindowHandle, "red");
                        //    HOperatorSet.SetLineWidth(hv_WindowHandle, 2);
                        //    HOperatorSet.SetPart(hv_WindowHandle, 0, 0, hv_newHeight, hv_newWidth);

                        //    HOperatorSet.DispObj(ho_ImageZoom, hv_WindowHandle);

                        //    //縮小region
                        //    HTuple r = 1.0 / ratio;
                        //    HTuple s_ratio = ((r.TupleString(".2f"))).TupleNumber();

                        //    HObject unionResult;
                        //    HOperatorSet.GenEmptyObj(out unionResult);
                        //    if (data.DefectRegions.IsInitialized())
                        //    {
                        //        HOperatorSet.Union1(data.DefectRegions, out unionResult);
                        //        HOperatorSet.ZoomRegion(unionResult, out ho_RegionZoom, s_ratio, s_ratio);
                        //    }

                        //    HOperatorSet.DispObj(ho_RegionZoom, hv_WindowHandle);
                        //    HOperatorSet.DumpWindowImage(out HObject drawImage, hv_WindowHandle);

                        //    string filename2 = Path.Combine($"{this.SaveImagePath}", data.NG_Mark_Name + "." + format);
                        //    HOperatorSet.WriteImage(drawImage, format, 0, filename2);

                        //    HOperatorSet.CloseWindow(hv_WindowHandle);
                        //    drawImage.Dispose();
                        //    unionResult.Dispose();
                        //}

                        data.Image.Dispose();
                        data.DefectRegions?.Dispose();
                        ho_ImageZoom.Dispose();
                        ho_RegionZoom.Dispose();

                    }
                }
                catch (Exception ex)
                {

                }
                SpinWait.SpinUntil(() => false, 50);
            }
        }

        /// <summary>
        /// 檢查存圖的日期是否隔天,隔天or隔年時新增資料夾
        /// </summary>
        private void CheckSaveImageFolderDate()
        {
            this.CreateSaveImageFolder();
        }
        /// <summary>
        /// 避免檔案過多導致硬碟爆掉,故只保留最近的30天資料夾
        /// </summary>
        private void DeleteSaveImageFolderDate()
        {
            //取得SaveImage路徑
            var directoryPath = this.SaveImageDirectory;

            var topDirectory = Directory.GetDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly);
            var allDirectories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories);

            var subFolders = allDirectories.Except(topDirectory).ToList();

            var retain_days = this.ParamsSystem.Retain_days;

            if (retain_days <= 1)
                retain_days = 1;

            var dirUnderDelete = subFolders.OrderByDescending(s => s).Skip(retain_days);

            foreach (var item in dirUnderDelete)
            {
                Directory.Delete(item, true);
            }
        }
        #endregion 存圖設定
    }
}