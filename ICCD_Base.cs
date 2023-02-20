using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gluck.camera
{
    public delegate void CameraRawData<T>(T img);

    //public delegate void IOTriggered(bool isHigh);
    public interface ICCD_Base<T>
    {
        bool Initial(int id);

        bool LoadParameter(string path);
        void SetTriggerMode(CamTriggerMode mode);

        void LinkingGrabImageEvent(CamTriggerMode mode);
        void DeleteGrabImageEvent(CamTriggerMode mode);

        void Start();
  
        void Stop();

        void Freeze();

        void Live();

        bool IsSensorColor { get; set; }

        void Release();

        object GetCamera();
        
        void GetImageSize(out int width,out int height);

        event CameraRawData<T> ImageObtained;

        //event IOTriggered ReceivedSignal;

        //Dasla
        void SetAcqResourceIndex(int value);
    }

    public enum CamTriggerMode
    {
        //IDS
        Off,
        Hi_Lo_Sync,
        Lo_Hi_Sync,
        Continuous,
        Hi_Lo,
        Lo_Hi,
        Software,
        Hi_Lo_Pre,
        Lo_Hi_Pre,
        //DALSA
        External,
        ExternalFrame,
        ExternalLine 
    }
    public enum CamStrobeMode
    {
        Off,
        TriggerLowActive,
        TriggerHighActive,
        ConstantHigh,
        ConstantLow,
        FreerunLowActive,
        FreerunHighActive,
    }
}
