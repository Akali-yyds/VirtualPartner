using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VirtualPartner.Runtime;
using VirtualPartner.Runtime.PhoneOS;
using Object=UnityEngine.Object;
namespace VirtualPartner.EditorTools
{
    // Explicit local fixture. Never saved into the user's API configuration.
    public static class PhoneLiveServiceChecks
    {
        public static bool Running {get;private set;}
        public static string Result {get;private set;}
        private static readonly List<string> results=new List<string>();
        private static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
        public static void Run()
        {
            if(Running)return;var runtime=Object.FindFirstObjectByType<PhoneLiveRuntime>();if(runtime==null||!runtime.Ready)throw new InvalidOperationException("Live phone not ready.");
            results.Clear();Running=true;runtime.StartCoroutine(Check(runtime));
        }
        private static void Verify(bool ok,string label)=>results.Add((ok?"PASS ":"FAIL ")+label);
        private static IEnumerator Check(PhoneLiveRuntime runtime)
        {
            var relay=runtime.Relay;var shell=runtime.Shell;var controller=runtime.Conversation;
            var configField=Field(typeof(LlmRelay),"config");var savedConfig=configField.GetValue(relay);
            var memoryField=Field(typeof(MomotalkConversationController),"memorySystem");var savedMemory=memoryField.GetValue(controller);
            var tts=Object.FindFirstObjectByType<TtsManager>();var played=false;Action onPlayback=()=>played=true;tts.SpeechPlaybackStarted+=onPlayback;
            PhoneLiveMomotalk chat=null;var originalDraft="";var oldAutoSend=shell.AutoSendVoice;
            try
            {
                var draft=new LlmRelayConfigDraft{apiKey="local-fixture",model="phone-test",baseUrl="http://127.0.0.1:18767",chatCompletionsUrl="http://127.0.0.1:18767/v1/chat/completions",interactionTimeoutSeconds=45,useJsonResponseFormat=true};
                var temporary=savedConfig.GetType().GetMethod("FromDraft",BindingFlags.Static|BindingFlags.Public).Invoke(null,new object[]{draft});
                configField.SetValue(relay,temporary);memoryField.SetValue(controller,null);
                shell.OpenApp("momotalk");yield return new WaitForSecondsRealtime(.4f);
                var app=(PhonePreviewApp)shell.host.CurrentApp;chat=app.GetComponent<PhoneLiveMomotalk>();var contexts=new List<CharacterRuntimeContext>();CharacterRegistry.GetRegisteredContexts(contexts);chat.Select(contexts[0]);yield return null;
                originalDraft=chat.input.text;
                var existing=new HashSet<string>(controller.ReadMessages(contexts[0].CharacterId).Select(m=>m.messageId));
                chat.input.text="[Controlled integration test 1] replacement target";chat.Send();var first=relay.LatestRequestId;
                chat.input.text="[Controlled integration test 2] background reply and TTS";chat.Send();var second=relay.LatestRequestId;
                shell.Recent();yield return new WaitForSecondsRealtime(.4f);
                shell.host.DismissTask("momotalk");shell.Collapse();
                var deadline=Time.realtimeSinceStartup+55;
                while(Time.realtimeSinceStartup<deadline&&(relay.RequestPending||controller.ReadPending(contexts[0].CharacterId).Count>0||!played))yield return null;
                var messages=controller.ReadMessages(contexts[0].CharacterId).Where(m=>!existing.Contains(m.messageId)).ToList();
                Verify(messages.Any(m=>m.requestId==first&&m.status=="replaced"),"Superseded request has an explicit replaced record");
                Verify(messages.Count(m=>m.requestId==second&&m.sender=="character")==1,"One controlled response reaches the original conversation through StagePlan");
                Verify(!messages.Any(m=>m.requestId==first&&m.sender=="character"),"Superseded response is ignored");
                Verify(controller.TotalUnreadCount>0&&shell.unreadDot.activeSelf,"Background reply increments unread and launcher dot");
                Verify(played&&tts.CurrentProvider=="GptSoVits","Real GptSoVits playback survives task dismissal and phone collapse");
                shell.OpenApp("momotalk");yield return new WaitForSecondsRealtime(.4f);
                chat.Select(contexts[0]);yield return new WaitForSecondsRealtime(.3f);
                Verify(controller.TotalUnreadCount==0,"Returning to conversation marks reply read");
                // Real microphone session cancellation plus a deliberately late result.
                shell.SetAutoSend(true);chat.input.text="Keep this draft";chat.StartVoice();yield return new WaitForSecondsRealtime(.3f);
                var session=runtime.Asr.CurrentSessionId;
                Verify(runtime.Asr.Active,"Real ASR session starts from Momotalk microphone");
                shell.Recent();yield return new WaitForSecondsRealtime(.4f);
                Verify(!runtime.Asr.Active,"Entering overview cancels the owned microphone session");
                var count=relay.LatestRequestId;
                typeof(PhoneLiveMomotalk).GetMethod("Recognized",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chat,new object[]{new AsrRecognitionResult(session,AsrRecognitionStatus.Done,"late transcript","",AsrResultMode.AutoSendToLlm)});
                Verify(chat.input.text=="Keep this draft"&&relay.LatestRequestId==count,"Late ASR cannot overwrite draft or auto-send after navigation");
                shell.OpenApp("momotalk");yield return new WaitForSecondsRealtime(.3f);shell.SetAutoSend(false);chat.StartVoice();yield return new WaitForSecondsRealtime(.2f);
                var fillSession=runtime.Asr.CurrentSessionId;
                var recognition=typeof(PhoneLiveMomotalk).GetMethod("Recognized",BindingFlags.Instance|BindingFlags.NonPublic);
                recognition.Invoke(chat,new object[]{new AsrRecognitionResult(fillSession,AsrRecognitionStatus.Done,"受控转写填入测试","",AsrResultMode.FillInputOnly)});
                Verify(chat.input.text=="受控转写填入测试"&&relay.LatestRequestId==count,"Owned transcript fills draft without submitting by default");runtime.Asr.CancelRecognition();
                shell.SetAutoSend(true);chat.StartVoice();yield return new WaitForSecondsRealtime(.2f);
                var sendSession=runtime.Asr.CurrentSessionId;
                recognition.Invoke(chat,new object[]{new AsrRecognitionResult(sendSession,AsrRecognitionStatus.Done,"受控自动发送测试","",AsrResultMode.FillInputOnly)});
                Verify(relay.LatestRequestId==count+1&&chat.input.text=="","Auto-send option submits an owned transcript exactly once");runtime.Asr.CancelRecognition();
                var voiceDeadline=Time.realtimeSinceStartup+45;
                while(Time.realtimeSinceStartup<voiceDeadline&&(relay.RequestPending||controller.ReadPending(contexts[0].CharacterId).Count>0))yield return null;
            }
            finally
            {
                configField.SetValue(relay,savedConfig);memoryField.SetValue(controller,savedMemory);tts.SpeechPlaybackStarted-=onPlayback;
                if(chat!=null){chat.CancelVoice();chat.input.text=originalDraft;}shell.SetAutoSend(oldAutoSend);
                Running=false;Result=string.Join("\n",results);Directory.CreateDirectory(PhoneLiveReviewChecks.Output);File.WriteAllText(PhoneLiveReviewChecks.Output+"/service-checks.txt",Result);
            }
        }
    }
}
