using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneSettingsPresentation : MonoBehaviour
    {
        public Button load,reload,test,save,details;
        public TMP_Text status;
        private PhoneLiveSettings settings;
        private string baseline;
        private bool initialized;
        public bool Dirty=>initialized&&baseline!=Fingerprint();
        private string Fingerprint()=>JsonUtility.ToJson(new Values{model=settings.model.text,url=settings.baseUrl.text,endpoint=settings.endpoint.text,key=settings.key.text,timeout=settings.timeout.text,json=settings.json.isOn,developer=settings.developer.isOn});
        [System.Serializable]private sealed class Values{public string model,url,endpoint,key,timeout;public bool json,developer;}
        private void Start()
        {
            settings=GetComponent<PhoneLiveSettings>();if(settings==null){enabled=false;return;}
            load.onClick=new Button.ButtonClickedEvent();load.onClick.AddListener(()=>RequestLoad(false));reload.onClick=new Button.ButtonClickedEvent();reload.onClick.AddListener(()=>RequestLoad(true));
            details.onClick.AddListener(()=>PhoneModal.Show(GetComponent<PhonePreviewApp>(),"Connection details",settings.feedback.text));
        }
        public void AcceptBaseline(){if(settings==null)settings=GetComponent<PhoneLiveSettings>();if(settings==null)return;baseline=Fingerprint();initialized=true;}
        public void RequestLoad(bool fromDisk)
        {
            System.Action apply=()=>{if(fromDisk)settings.Reload();else settings.LoadCurrent();};
            if(Dirty)PhoneModal.Show(GetComponent<PhonePreviewApp>(),"Replace unsaved edits?",fromDisk?"Load the configuration file from disk and replace this draft and the current runtime configuration?":"Replace this draft with the current runtime configuration?",apply,"Load");else apply();
        }
        private void Update()
        {
            if(settings==null)return;test.interactable=!settings.Testing;save.interactable=!settings.Testing;
            status.text=settings.Testing?"Testing connection…":settings.TestState+(Dirty?" · Unsaved edits":"");
        }
    }
}
