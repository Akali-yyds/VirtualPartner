using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VirtualPartner.Runtime.PhoneOS;
using static VirtualPartner.EditorTools.PhoneVisualUI;
namespace VirtualPartner.EditorTools
{
    public static partial class PhoneVisualStageBuilder
    {
        [MenuItem("VirtualPartner/Phone OS/Build Live Phone")]
        public static void BuildLive(){live=true;try{Build();}finally{live=false;}}
        private static T Find<T>(Transform root,string path) where T:Component
        {var item=root.Find(path);if(item==null)throw new InvalidOperationException("Missing UI binding: "+path);return item.GetComponent<T>();}
        private static void Bind(Button button,UnityAction action){button.onClick=new Button.ButtonClickedEvent();Click(button,action);}
        private static void BindLiveApp(PhonePreviewApp app)
        {
            if(app.appId=="momotalk")
            {
                var old=app.GetComponentInChildren<PhoneChatPreview>(true);var ui=app.gameObject.AddComponent<PhoneLiveMomotalk>();
                ui.input=old.input;ui.scroll=old.scroll;ui.newMessages=old.newMessagesButton;ui.outgoingTemplate=old.outgoingTemplate;
                ui.incomingTemplate=Bubble(old.outgoingTemplate.transform.parent,"",false);
                foreach(Transform child in ui.scroll.content.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
                var contact=Find<Button>(app.transform,"Contacts/TokiConversation");var list=Scroll(app.pages[0].transform,"ContactRows",164,0,out _,12);ui.contacts=list;
                contact.transform.SetParent(app.transform.Find("Templates"),false);ui.contactTemplate=contact;
                var element=contact.gameObject.AddComponent<LayoutElement>();element.preferredHeight=72;
                ui.search=Find<TMP_InputField>(app.transform,"Contacts/SearchContacts");
                app.pages[0].transform.Find("EmptyHint").gameObject.SetActive(false);app.pages[0].transform.Find("Chat").gameObject.SetActive(false);
                ui.nameLabel=Find<TMP_Text>(app.transform,"Conversation/ChatHeader/Name");ui.statusLabel=Find<TMP_Text>(app.transform,"Conversation/ChatHeader/Status");
                ui.voiceTitle=Find<TMP_Text>(app.transform,"VoiceInput/Title");ui.voiceInfo=Find<TMP_Text>(app.transform,"VoiceInput/Info");
                Find<TMP_Text>(app.transform,"VoiceInput/Header/Subtitle").text="Speech to text";
                ui.historyFeedback=Find<TMP_Text>(app.transform,"ClearHistory/Feedback");ui.memoryFeedback=Find<TMP_Text>(app.transform,"ClearMemory/Feedback");
                ui.historyFeedback.text=ui.memoryFeedback.text="This action changes saved data.";
                Find<TMP_Text>(app.transform,"ClearHistory/Confirm/Label").text="Clear chat history";
                Find<TMP_Text>(app.transform,"ClearMemory/Confirm/Label").text="Clear long-term memory";
                Bind(Find<Button>(app.transform,"ClearHistory/Confirm"),ui.ClearHistory);Bind(Find<Button>(app.transform,"ClearMemory/Confirm"),ui.ClearMemory);
                Bind(Find<Button>(app.transform,"Conversation/Composer/SendPreview"),ui.Send);
                Bind(Find<Button>(app.transform,"Conversation/Composer/Microphone"),ui.StartVoice);
                Bind(ui.newMessages.GetComponent<Button>(),ui.JumpToLatest);
                UnityEngine.Object.DestroyImmediate(old);
            }
            else if(app.appId=="camera")
            {
                var ui=app.gameObject.AddComponent<PhoneLiveCamera>();var root=app.transform.Find("Controls/ControlsScroll/Viewport/Content");
                ui.pan=Find<PhoneJoystick>(root,"Joysticks/Pan");ui.orbit=Find<PhoneJoystick>(root,"Joysticks/Orbit");
                ui.zoom=Find<Slider>(root,"Distance/Zoom");ui.value=Find<TMP_Text>(root,"Distance/Value");ui.zoom.onValueChanged=new Slider.SliderEvent();
                Bind(Find<Button>(root,"Reset/ResetButton"),ui.ResetView);Find<TMP_Text>(root,"Mode/Hint").text="Ground pan · orbit · zoom";
            }
            else if(app.appId=="settings")
            {
                var ui=app.gameObject.AddComponent<PhoneLiveSettings>();var root=app.transform.Find("ApiConfiguration/ApiFields/Viewport/Content");
                ui.model=Find<TMP_InputField>(root,"Model/Input");ui.baseUrl=Find<TMP_InputField>(root,"Base URL/Input");ui.endpoint=Find<TMP_InputField>(root,"Chat completions URL/Input");ui.key=Find<TMP_InputField>(root,"API key/Input");ui.timeout=Find<TMP_InputField>(root,"Interaction timeout (seconds)/Input");
                ui.json=Find<Toggle>(root,"RequestOptions/JSON response format");ui.developer=Find<Toggle>(root,"RequestOptions/Developer role");
                var notes=root.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Text"&&t.transform.parent.name=="Note").ToArray();notes[0].text="Uses the existing LLM configuration file. Save applies your edits.";ui.feedback=notes.Last();ui.feedback.text="Loading configuration…";
                foreach(var button in root.GetComponentsInChildren<Button>())switch(button.name){case "Reload file":Bind(button,ui.Reload);break;case "Load current":Bind(button,ui.LoadCurrent);break;case "Test":Bind(button,ui.Test);break;case "Save":Bind(button,ui.Save);break;}
                foreach(var text in app.pages[0].GetComponentsInChildren<TMP_Text>())if(text.text.Contains("Visual preview"))text.text="Display changes save immediately. API changes require Save.";
            }
            // Clipboard and password visibility remain shared, functional helpers.
        }
    }
}
