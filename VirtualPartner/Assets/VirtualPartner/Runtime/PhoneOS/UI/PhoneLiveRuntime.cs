using UnityEngine;
namespace VirtualPartner.Runtime.PhoneOS
{
    // Composition boundary for the daily phone. Service ownership stays on the bootstrap.
    public sealed class PhoneLiveRuntime : MonoBehaviour
    {
        public MomotalkConversationController Conversation { get; private set; }
        public LlmRelay Relay { get; private set; }
        public AsrManager Asr { get; private set; }
        public PhonePresentationShell Shell { get; private set; }
        public bool Ready => Conversation != null && Conversation.RuntimeReady;
        public bool legacyDebugUi;
        private string debugAsrSession;
        private CharacterRuntimeContext debugAsrContext;
        private bool startingDebugAsr;
        private VirtualPartnerRuntimeDebugPanel legacyPanel;
        private bool originalBackground;
        private void Awake() { Shell=GetComponent<PhonePresentationShell>();originalBackground=Application.runInBackground;Application.runInBackground=true; }
        public void Configure(MomotalkConversationController conversation,LlmRelay relay,AsrManager asr)
        {
            Conversation=conversation;Relay=relay;Asr=asr;
            legacyPanel=FindFirstObjectByType<VirtualPartnerRuntimeDebugPanel>();
            Conversation.ContactsChanged+=RefreshUnread;
            Asr.RecognitionFinished+=DebugRecognitionFinished;
            foreach(var driver in FindObjectsByType<VirtualSceneCameraInputDriver>(FindObjectsSortMode.None))driver.enabled=false;
            var camera=FindFirstObjectByType<VirtualSceneCameraController>();camera?.SetDebugInputEnabled(false);
            RefreshUnread();
        }
        public bool StartDebugRecognition(bool mock,out string error)
        {
            if(Asr.Active){error="Microphone is busy.";return false;}
            var contexts=new System.Collections.Generic.List<CharacterRuntimeContext>();CharacterRegistry.GetRegisteredContexts(contexts);
            debugAsrContext=contexts.Find(c=>c.CharacterId==Conversation.CurrentCharacterId)??(contexts.Count>0?contexts[0]:null);
            startingDebugAsr=true;
            var started=mock?Asr.StartMockRecognition(out error):Asr.StartRealRecognition(out error);
            startingDebugAsr=false;if(started&&Asr.Active)debugAsrSession=Asr.CurrentSessionId;return started;
        }
        public void CancelDebugRecognition()
        {
            var owned=debugAsrSession;debugAsrSession=null;debugAsrContext=null;startingDebugAsr=false;
            if(Asr!=null&&owned!=null&&Asr.CurrentSessionId==owned&&Asr.Active)Asr.CancelRecognition();
        }
        private void DebugRecognitionFinished(AsrRecognitionResult result)
        {
            if(result==null||(!startingDebugAsr&&result.SessionId!=debugAsrSession))return;
            debugAsrSession=null;
            if(result.Status!=AsrRecognitionStatus.Done||debugAsrContext==null||string.IsNullOrWhiteSpace(result.Text))return;
            if(result.ResultMode==AsrResultMode.AutoSendToLlm)Conversation.SendMessage(debugAsrContext,result.Text);
            // Fill-only debug sessions expose their transcript in Debug diagnostics.
        }
        private void LateUpdate()
        {
            if(!Ready)return;
            if(legacyPanel!=null&&legacyPanel.Visible!=legacyDebugUi)legacyPanel.SetVisible(legacyDebugUi);
        }
        public bool IsConversationVisible(string id)
        {
            var current=Shell.host.CurrentApp as PhonePreviewApp;
            var chat=current!=null?current.GetComponent<PhoneLiveMomotalk>():null;
            return Shell.IsOpen && current!=null && !current.Suspended && current.CurrentPage==1 && chat!=null && chat.CharacterId==id;
        }
        private void RefreshUnread() { if(Shell.unreadDot!=null)Shell.unreadDot.SetActive(Conversation.TotalUnreadCount>0); }
        private void OnDestroy() { Application.runInBackground=originalBackground; if(Conversation!=null)Conversation.ContactsChanged-=RefreshUnread;if(Asr!=null)Asr.RecognitionFinished-=DebugRecognitionFinished; }
    }
}
