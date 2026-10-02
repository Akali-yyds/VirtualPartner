using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneLiveCamera : MonoBehaviour, IPhoneTaskReset
    {
        public PhoneJoystick pan,orbit;
        public Slider zoom;
        public TMP_Text value;
        private VirtualSceneCameraController controller;
        private PhonePreviewApp app;
        private PhonePresentationShell shell;
        private bool panning;
        private void OnEnable(){Sync();}
        public void ResetTaskView(){Stop();Sync();}
        private void Start()
        {
            controller=FindFirstObjectByType<VirtualSceneCameraController>();app=GetComponent<PhonePreviewApp>();shell=GetComponentInParent<PhonePresentationShell>();
            zoom.onValueChanged.AddListener(SetZoom);Sync();
        }
        private void Update()
        {
            if(controller==null||!shell.IsOpen||app.Suspended){Stop();return;}
            if(pan.Value!=Vector2.zero && !panning){controller.BeginPan();panning=true;}
            if(pan.Value==Vector2.zero && panning){controller.EndPan();panning=false;}
            controller.Pan(-pan.Value*Time.unscaledDeltaTime*180f);
            controller.Orbit(orbit.Value*Time.unscaledDeltaTime*180f);
        }
        private void SetZoom(float distance){controller?.SetRadius(distance);Sync();}
        private void Sync(){if(controller==null)return;zoom.minValue=controller.MinRadius;zoom.maxValue=controller.MaxRadius;zoom.SetValueWithoutNotify(controller.Radius);value.text=controller.Radius.ToString("0.0");}
        public void ResetView(){Stop();controller?.ResetView();Sync();}
        private void Stop(){pan.Release();orbit.Release();if(panning)controller?.EndPan();panning=false;}
        private void OnDisable()=>Stop();
        private void OnApplicationFocus(bool focus){if(!focus)Stop();}
    }
}
