using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneLiveSettings : MonoBehaviour, IPhoneTaskReset
    {
        public TMP_InputField model,baseUrl,endpoint,key,timeout;
        public Toggle json,developer;
        public TMP_Text feedback;
        private PhoneLiveRuntime runtime;
        private bool loaded,testing;
        public bool Testing => testing && runtime!=null && runtime.Relay!=null && runtime.Relay.ConfigTestPending;
        public string TestState { get; private set; } = "Not tested";
        private void AcceptBaseline(){var presentation=GetComponent<PhoneSettingsPresentation>();if(presentation!=null)presentation.AcceptBaseline();}
        private PhonePreviewApp app;
        private void OnEnable()
        {
            if(key==null)return;
            key.contentType=TMP_InputField.ContentType.Password;key.ForceLabelUpdate();
            foreach(var toggle in GetComponentsInChildren<Toggle>(true))if(toggle.transform.parent.name=="KeyVisibility"){toggle.SetIsOnWithoutNotify(false);var visual=toggle.GetComponent<PhoneSwitch>();if(visual!=null)visual.Apply(false);}
        }
        public void ResetTaskView(){OnEnable();}
        private void Start(){runtime=GetComponentInParent<PhoneLiveRuntime>();app=GetComponent<PhonePreviewApp>();app.StateChanged+=OnEnable;}
        private void OnDestroy(){if(app!=null)app.StateChanged-=OnEnable;}
        private void Update()
        {
            if(runtime==null||!runtime.Ready)return;
            if(!loaded){LoadCurrent();loaded=true;}
            if(testing){feedback.text=runtime.Relay.ConfigTestStatus;testing=runtime.Relay.ConfigTestPending;if(!testing)TestState=feedback.text.StartsWith("Test succeeded:",System.StringComparison.Ordinal)?"Connection successful":"Connection failed · See details";}
        }
        private bool Draft(out LlmRelayConfigDraft draft)
        {
            draft=null;
            if(!float.TryParse(timeout.text,NumberStyles.Float,CultureInfo.InvariantCulture,out var seconds)||seconds<=0||float.IsInfinity(seconds)||float.IsNaN(seconds))
            {feedback.text="Enter a positive timeout in seconds.";TestState="Invalid timeout · See details";return false;}
            draft=new LlmRelayConfigDraft{model=model.text,baseUrl=baseUrl.text,chatCompletionsUrl=endpoint.text,apiKey=key.text,interactionTimeoutSeconds=seconds,useJsonResponseFormat=json.isOn,supportsDeveloperRole=developer.isOn};return true;
        }
        public void LoadCurrent()
        {
            if(runtime==null||!runtime.Ready){feedback.text="Runtime is starting.";return;}
            var d=runtime.Relay.CreateConfigDraft();model.text=d.model;baseUrl.text=d.baseUrl;endpoint.text=d.chatCompletionsUrl;key.text=d.apiKey;timeout.text=d.interactionTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            json.SetIsOnWithoutNotify(d.useJsonResponseFormat);developer.SetIsOnWithoutNotify(d.supportsDeveloperRole);
            foreach(var visual in GetComponentsInChildren<PhoneSwitch>(true))visual.Apply(visual.toggle.isOn);
            feedback.text="Current configuration loaded. Edits require Save.";TestState="Current configuration loaded";AcceptBaseline();
        }
        public void Reload(){if(runtime==null||!runtime.Ready)return;runtime.Relay.ReloadConfig();LoadCurrent();feedback.text=runtime.Relay.ConfigStatus;}
        public void Test(){if(runtime==null||!runtime.Ready||!Draft(out var d))return;testing=runtime.Relay.StartConfigTest(d);feedback.text=runtime.Relay.ConfigTestStatus;TestState=testing?"Testing connection…":"Test could not start · See details";}
        public void Save(){if(runtime==null||!runtime.Ready||!Draft(out var d))return;var saved=runtime.Relay.SaveConfig(d,out var result);feedback.text=result;TestState=saved?"Configuration saved":"Save failed · See details";if(saved)AcceptBaseline();}
    }
}
