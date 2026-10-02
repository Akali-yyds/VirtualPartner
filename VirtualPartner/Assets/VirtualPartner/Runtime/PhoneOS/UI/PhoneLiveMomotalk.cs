using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneLiveMomotalk : MonoBehaviour, IPhoneTaskReset
    {
        public TMP_InputField input,search;
        public ScrollRect scroll;
        public PhoneTextBubble outgoingTemplate,incomingTemplate;
        public Button contactTemplate;
        public Transform contacts;
        public GameObject newMessages;
        public TMP_Text nameLabel,statusLabel,voiceTitle,voiceInfo,historyFeedback,memoryFeedback;
        private PhoneLiveRuntime runtime;
        private PhonePreviewApp app;
        private CharacterRuntimeContext selected;
        private readonly List<CharacterRuntimeContext> contexts=new List<CharacterRuntimeContext>();
        private readonly Dictionary<string,Button> contactRows=new Dictionary<string,Button>();
        private readonly Dictionary<string,PhoneTextBubble> rows=new Dictionary<string,PhoneTextBubble>();
        private readonly Dictionary<string,float> positions=new Dictionary<string,float>();
        private bool subscribed,dirty=true,submitPending,firstLoad=true,startingVoice;
        private string voiceSession,voiceCharacter;
        public string CharacterId => selected?.CharacterId ?? string.Empty;
        private string DraftKey => "VirtualPartner.PhoneOS.Draft."+CharacterId.ToLowerInvariant();
        private void Awake(){app=GetComponent<PhonePreviewApp>();runtime=GetComponentInParent<PhoneLiveRuntime>();app.StateChanged+=PageChanged;}
        private void Start()
        {
            input.onValueChanged.AddListener(StoreDraft);
            input.onSubmit.AddListener(_=>{if(input is PhoneMessageInputField field && field.CanSubmit)submitPending=true;});
            search.onValueChanged.AddListener(_=>RefreshContacts());
        }
        private void Update()
        {
            if(runtime==null||!runtime.Ready)return;
            if(!subscribed){runtime.Conversation.ContactsChanged+=Changed;runtime.Asr.RecognitionFinished+=Recognized;subscribed=true;dirty=true;}
            if(contexts.Count!=CharacterRegistry.RegisteredCount){CharacterRegistry.GetRegisteredContexts(contexts);RefreshContacts();}
            if(dirty){if(selected!=null && app.CurrentPage==1)RefreshMessages();RefreshContacts();dirty=false;}
            if(voiceSession!=null && runtime.Asr.Active){voiceTitle.text=runtime.Asr.Status.ToString();voiceInfo.text=runtime.Asr.LastMessage;}
            if(submitPending){submitPending=false;Send();}
        }
        private void Changed(){dirty=true;}
        private void OnEnable(){dirty=true;}
        private void StoreDraft(string text){if(selected!=null)PlayerPrefs.SetString(DraftKey,text);}
        private void RefreshContacts()
        {
            if(runtime==null||!runtime.Ready)return;
            foreach(var context in contexts)
            {
                if(!contactRows.TryGetValue(context.CharacterId,out var row))
                {
                    row=Instantiate(contactTemplate,contacts);row.gameObject.SetActive(true);contactRows.Add(context.CharacterId,row);
                    var target=context;row.onClick=new Button.ButtonClickedEvent();row.onClick.AddListener(()=>Select(target));
                }
                row.transform.Find("Name").GetComponent<TMP_Text>().text=context.Profile.DisplayName;
                row.transform.Find("Message").GetComponent<TMP_Text>().text=runtime.Conversation.GetContactSummary(context);
                var unread=runtime.Conversation.GetUnreadCount(context);row.transform.Find("Time").GetComponent<TMP_Text>().text=unread>0?$"{unread} new":"";
                row.transform.Find("TokiAvatar/Portrait").GetComponent<Image>().sprite=context.Profile.AvatarIcon;
                row.gameObject.SetActive(string.IsNullOrWhiteSpace(search.text)||context.Profile.DisplayName.IndexOf(search.text,StringComparison.OrdinalIgnoreCase)>=0);
            }
        }
        public void Select(CharacterRuntimeContext context)
        {
            CancelVoice();
            if(selected!=null){StoreDraft(input.text);positions[CharacterId]=scroll.content.anchoredPosition.y;}
            selected=context;runtime.Conversation.SelectConversation(context);
            input.SetTextWithoutNotify(PlayerPrefs.GetString(DraftKey,""));nameLabel.text=context.Profile.DisplayName;statusLabel.text=context.Profile.MomotalkStatus;
            foreach(var portrait in app.pages[1].GetComponentsInChildren<Image>(true))if(portrait.name=="Portrait")portrait.sprite=context.Profile.AvatarIcon;
            app.pages[2].transform.Find("Name").GetComponent<TMP_Text>().text=context.Profile.DisplayName;
            app.pages[2].transform.Find("Identity").GetComponent<TMP_Text>().text=context.Profile.MomotalkStatus;
            app.pages[2].transform.Find("TokiAvatar/Portrait").GetComponent<Image>().sprite=context.Profile.AvatarIcon;
            foreach(var row in rows.Values)Destroy(row.gameObject);rows.Clear();firstLoad=true;
            app.ShowPage(1);dirty=true;
        }
        private void PageChanged()
        {
            if(app.Suspended||app.CurrentPage!=3)CancelVoice();
            if(runtime!=null&&runtime.Ready&&runtime.IsConversationVisible(CharacterId))runtime.Conversation.MarkRead(CharacterId);
            dirty=true;
        }
        public void Send()
        {
            if(selected==null||!runtime.Ready||string.IsNullOrWhiteSpace(input.text))return;
            // Even rejected submissions are recorded with their real error by the controller.
            runtime.Conversation.SendMessage(selected,input.text);input.text="";dirty=true;
        }
        private void RefreshMessages()
        {
            var atBottom=scroll.content.rect.height<=scroll.viewport.rect.height||scroll.verticalNormalizedPosition<.04f;
            var oldPosition=scroll.content.anchoredPosition;
            var keep=new HashSet<string>();var added=false;
            foreach(var message in runtime.Conversation.ReadMessages(CharacterId))
            {
                var id=message.messageId;keep.Add(id);
                if(!rows.TryGetValue(id,out var row)){row=Instantiate(message.sender=="user"?outgoingTemplate:incomingTemplate,scroll.content);rows[id]=row;row.gameObject.SetActive(true);added=true;}
                var isError=message.sender=="system"&&(message.status=="error"||message.status=="failed");
                row.body.text=isError?"Reply unavailable\nTap for details":message.sender=="system"?$"{message.status}: {message.text}":message.text;
                row.body.color=isError?PhoneVisualTheme.Current.pink:PhoneVisualTheme.Current.ink;
                var details=row.bubble.GetComponent<PhoneMessageDetails>();
                if(isError&&details==null)details=row.bubble.gameObject.AddComponent<PhoneMessageDetails>();
                if(details!=null)details.Details=isError?message.text:null;
                row.bubble.GetComponent<Image>().raycastTarget=isError;
                row.bubble.Find("Time").GetComponent<TMP_Text>().text=DateTime.TryParse(message.timestampUtc,out var time)?time.ToLocalTime().ToString("HH:mm"):"";
                row.transform.SetAsLastSibling();
            }
            foreach(var id in runtime.Conversation.ReadPending(CharacterId))
            {
                var key="pending:"+id;keep.Add(key);
                if(!rows.TryGetValue(key,out var row)){row=Instantiate(incomingTemplate,scroll.content);rows[key]=row;row.gameObject.SetActive(true);}
                row.body.text="Waiting for reply…";row.bubble.Find("Time").GetComponent<TMP_Text>().text="";row.transform.SetAsLastSibling();
            }
            foreach(var key in new List<string>(rows.Keys))if(!keep.Contains(key)){Destroy(rows[key].gameObject);rows.Remove(key);}
            if(firstLoad){firstLoad=false;StartCoroutine(RestorePosition(positions.TryGetValue(CharacterId,out var y)?y:-1));}
            else if(added && atBottom)JumpToLatest();
            else{scroll.content.anchoredPosition=oldPosition;if(added)newMessages.SetActive(true);}
            if(runtime.IsConversationVisible(CharacterId))runtime.Conversation.MarkRead(CharacterId);
        }
        private IEnumerator RestorePosition(float y){yield return null;yield return null;Canvas.ForceUpdateCanvases();if(y<0)scroll.verticalNormalizedPosition=0;else scroll.content.anchoredPosition=new Vector2(0,y);}
        public void JumpToLatest(){newMessages.SetActive(false);StartCoroutine(RestorePosition(-1));}
        public void ClearHistory(){if(selected==null)return;runtime.Conversation.ClearConversation(selected);historyFeedback.text="Chat history cleared. Long-term memory kept.";dirty=true;}
        public void ClearMemory(){if(selected==null)return;runtime.Conversation.ClearMemory(selected);memoryFeedback.text="Long-term memory cleared. Chat history kept.";}
        public void StartVoice()
        {
            if(selected==null||!runtime.Ready)return;
            app.ShowPage(3);voiceTitle.text="Listening";voiceInfo.text="Starting microphone…";
            if(runtime.Asr.Active){voiceTitle.text="Microphone busy";voiceInfo.text="Another recognition session is active.";return;}
            runtime.Asr.SetResultMode(AsrResultMode.FillInputOnly);voiceCharacter=CharacterId;startingVoice=true;
            var started=runtime.Asr.StartRealRecognition(out var error);startingVoice=false;
            if(!started){voiceTitle.text="ASR unavailable";voiceInfo.text=error;voiceCharacter=null;return;}
            if(runtime.Asr.Active)voiceSession=runtime.Asr.CurrentSessionId;
        }
        private void Recognized(AsrRecognitionResult result)
        {
            if(result==null||(!startingVoice&&result.SessionId!=voiceSession)||voiceCharacter!=CharacterId||app.Suspended||!runtime.Shell.IsOpen||app.CurrentPage!=3)return;
            voiceSession=null;voiceCharacter=null;
            if(result.Status!=AsrRecognitionStatus.Done){voiceTitle.text=result.Status.ToString();voiceInfo.text=result.Error;return;}
            if(string.IsNullOrWhiteSpace(result.Text)){voiceTitle.text="No speech recognized";return;}
            input.text=result.Text;app.ShowPage(1);if(runtime.Shell.AutoSendVoice)Send();
        }
        public void CancelVoice()
        {
            var owned=voiceSession;voiceSession=null;voiceCharacter=null;startingVoice=false;
            if(runtime!=null&&runtime.Asr!=null&&owned!=null&&runtime.Asr.CurrentSessionId==owned&&runtime.Asr.Active)runtime.Asr.CancelRecognition();
        }
        public void ResetTaskView()
        {CancelVoice();submitPending=false;if(selected!=null)StoreDraft(input.text);search.SetTextWithoutNotify("");positions.Clear();firstLoad=true;newMessages.SetActive(false);dirty=true;}
        private void OnDisable(){submitPending=false;CancelVoice();}
        private void OnDestroy(){app.StateChanged-=PageChanged;if(subscribed){runtime.Conversation.ContactsChanged-=Changed;runtime.Asr.RecognitionFinished-=Recognized;}}
    }
}
