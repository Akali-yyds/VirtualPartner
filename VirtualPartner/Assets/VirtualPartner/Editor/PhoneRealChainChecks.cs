using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VirtualPartner.Runtime;
using VirtualPartner.Runtime.PhoneOS;
using Object=UnityEngine.Object;

namespace VirtualPartner.EditorTools
{
    // Explicit opt-in integration run. Credentials arrive once over loopback and remain in memory.
    public static class PhoneRealChainChecks
    {
        public static bool Running {get;private set;}
        public static string Result {get;private set;}
        private static Action restore;
        private static HttpListener listener;
        private static readonly List<string> results=new List<string>();
        private static FieldInfo Field(Type t,string name)=>t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
        public static void ReceiveAndRun()
        {
            if(Running)throw new InvalidOperationException("Review already running.");
            var runtime=Object.FindFirstObjectByType<PhoneLiveRuntime>();
            if(!Application.isPlaying||runtime==null||!runtime.Ready||runtime.Relay.RequestPending||runtime.Conversation.ActiveRequestCount>0)throw new InvalidOperationException("An idle live runtime is required.");
            listener=new HttpListener();listener.Prefixes.Add("http://127.0.0.1:18769/");listener.Start();
            Running=true;results.Clear();Result="Waiting for temporary credential";
            AssemblyReloadEvents.beforeAssemblyReload+=Cleanup;EditorApplication.playModeStateChanged+=ModeChanged;
            var input=Task.Run(async()=>{var context=await listener.GetContextAsync();if(context.Request.HttpMethod!="POST"||context.Request.ContentLength64>256){context.Response.StatusCode=400;context.Response.Close();return "";}string secret;using(var reader=new StreamReader(context.Request.InputStream))secret=await reader.ReadToEndAsync();context.Response.StatusCode=204;context.Response.Close();return secret.Trim();});
            runtime.StartCoroutine(Run(runtime,input));
        }
        private static void ModeChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Cleanup();}
        private static void Cleanup(){restore?.Invoke();restore=null;listener?.Close();listener=null;Running=false;AssemblyReloadEvents.beforeAssemblyReload-=Cleanup;EditorApplication.playModeStateChanged-=ModeChanged;}
        private static IEnumerator Run(PhoneLiveRuntime runtime,Task<string> input)
        {
            float deadline=Time.realtimeSinceStartup+45;while(!input.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!input.IsCompleted||input.IsFaulted||string.IsNullOrWhiteSpace(input.Result)){Result="BLOCKED credential not received";Cleanup();yield break;}
            listener.Close();listener=null;var credential=input.Result;
            var relay=runtime.Relay;var controller=runtime.Conversation;var shell=runtime.Shell;
            var cfg=Field(typeof(LlmRelay),"config");var oldConfig=cfg.GetValue(relay);var configReady=Field(typeof(LlmRelay),"configReady");var oldReady=configReady.GetValue(relay);
            var history=Field(typeof(MomotalkConversationController),"historyStore");var oldHistory=history.GetValue(controller);
            var cm=Field(typeof(MomotalkConversationController),"memorySystem");var oldCm=cm.GetValue(controller);var rm=Field(typeof(LlmRelay),"memorySystem");var oldRm=rm.GetValue(relay);
            var unread=Field(typeof(MomotalkConversationController),"unreadCounts");var unreadMap=(Dictionary<string,int>)unread.GetValue(controller);var oldUnread=new Dictionary<string,int>(unreadMap);
            var characters=new List<CharacterRuntimeContext>();CharacterRegistry.GetRegisteredContexts(characters);var character=characters[0];var player=character.StagePlanPlayer;var tts=Object.FindFirstObjectByType<TtsManager>();
            var originalHistoryPath=new MomotalkHistoryStore().GetPath(character.CharacterId);var historyBytes=File.Exists(originalHistoryPath)?File.ReadAllBytes(originalHistoryPath):null;
            bool audio=false,finished=false;int activeRequest=0;Action playback=()=>audio=true;Action<StagePlanFinishedEvent> completed=e=>{if(e.RequestId==activeRequest)finished=true;};
            PhoneLiveMomotalk chat=null;string oldDraft=null;bool originalAuto=shell.AutoSendVoice;
            var root=character.RootOrientationController.Root;var position=root.position;var rotation=root.rotation;
            restore=()=>{
                relay.StopLlmStagePlan();runtime.Asr.CancelRecognition();tts.SpeechPlaybackStarted-=playback;player.StagePlanFinished-=completed;
                cfg.SetValue(relay,oldConfig);configReady.SetValue(relay,oldReady);history.SetValue(controller,oldHistory);cm.SetValue(controller,oldCm);rm.SetValue(relay,oldRm);
                unreadMap.Clear();foreach(var entry in oldUnread)unreadMap[entry.Key]=entry.Value;
                if(chat!=null&&oldDraft!=null)chat.input.text=oldDraft;shell.SetAutoSend(originalAuto);root.SetPositionAndRotation(position,rotation);
            };
            try
            {
                string directory=Path.GetFullPath(PhonePagesReviewChecks.Output+"/isolated-history-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                history.SetValue(controller,new MomotalkHistoryStore(directory));cm.SetValue(controller,null);rm.SetValue(relay,null);
                var draft=new LlmRelayConfigDraft{apiKey=credential,model="deepseek-chat",baseUrl="https://api.deepseek.com",chatCompletionsUrl="https://api.deepseek.com/chat/completions",interactionTimeoutSeconds=90,useJsonResponseFormat=true,supportsDeveloperRole=false};
                cfg.SetValue(relay,oldConfig.GetType().GetMethod("FromDraft",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{draft}));configReady.SetValue(relay,true);credential=null;
                tts.SpeechPlaybackStarted+=playback;player.StagePlanFinished+=completed;
                shell.OpenApp("momotalk");yield return new WaitForSecondsRealtime(.5f);chat=((PhonePreviewApp)shell.host.CurrentApp).GetComponent<PhoneLiveMomotalk>();chat.Select(character);oldDraft=chat.input.text;
                var prompts=new[]{"请用中文简短问候老师，只说一句早上好。", "请说一句简短中文，并做一个基础微笑表情。", "请说一句简短中文，并用 bonePose 轻轻抬起一只手臂，使用可用的参数骨骼。", "请说一句简短中文，向前短距离移动约0.2米，然后轻微改变朝向。"};
                for(int i=0;i<prompts.Length;i++)
                {
                    shell.OpenApp("momotalk");yield return new WaitForSecondsRealtime(.4f);chat.Select(character);audio=false;finished=false;bool pose=false,expression=false,movement=false;var origin=root.position;var startYaw=root.eulerAngles.y;float yawDelta=0,boneDelta=0;var types=new HashSet<string>();var observedExpressions=new HashSet<string>();var boneRotations=new Dictionary<Transform,Quaternion>();var actionResults=new HashSet<string>();
                    chat.input.text="[真实链路验收测试，不写入长期记忆] "+prompts[i];chat.Send();activeRequest=relay.LatestRequestId;
                    if(i==3){shell.OpenApp("camera");yield return new WaitForSecondsRealtime(.4f);shell.Recent();yield return new WaitForSecondsRealtime(.4f);shell.host.DismissTask("momotalk");shell.Collapse();}
                    deadline=Time.realtimeSinceStartup+120;bool captured=false;
                    while(Time.realtimeSinceStartup<deadline&&(!finished||relay.RequestPending||tts.Active))
                    {
                        if(player.IsOwnerPlaying(LlmRelay.LlmOwnerId))
                        {
                            movement|=Vector3.Distance(origin,root.position)>.02f;yawDelta=Mathf.Max(yawDelta,Mathf.Abs(Mathf.DeltaAngle(startYaw,root.eulerAngles.y)));
                            string currentExpression=character.ExpressionActionExecutor.CurrentExpression;if(!string.IsNullOrEmpty(currentExpression))observedExpressions.Add(currentExpression);
                            expression|=string.Equals(currentExpression,"smile",StringComparison.OrdinalIgnoreCase);
                            foreach(var action in (IEnumerable)Field(typeof(StagePlanPlayer),"runningActions").GetValue(player)){var p=action.GetType().GetProperty("ActionType");if(p!=null){string kind=(string)p.GetValue(action);types.Add(kind);
                                var result=action.GetType().GetProperty("Result").GetValue(action) as StageActionResult;if(result!=null)actionResults.Add(kind+":"+result.Status+":"+result.Message);
                                if(kind=="bonePose")foreach(var bone in character.RuntimeRoot.GetComponentsInChildren<Transform>()){if(!boneRotations.ContainsKey(bone))boneRotations[bone]=bone.localRotation;else boneDelta=Mathf.Max(boneDelta,Quaternion.Angle(boneRotations[bone],bone.localRotation));}
                            }}
                        }
                        pose=types.Contains("bonePose")&&boneDelta>.1f;
                        if(audio&&!captured&&(i!=2||pose)){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(PhonePagesReviewChecks.Output+"/real-"+i+".png");captured=true;}
                        if(!relay.RequestPending&&!relay.IsLlmStagePlanPlaying&&!tts.Active&&!string.IsNullOrEmpty(relay.LastError))break;
                        yield return null;
                    }
                    var validation=StagePlanValidator.Validate(relay.LastExtractedStagePlan,character.Profile);
                    bool requested=i==0?audio:i==1?expression&&types.Contains("expression"):i==2?pose:movement&&yawDelta>1;
                    bool passed=finished&&validation.IsValid&&player.FailedCount==0&&audio&&tts.CurrentProvider=="GptSoVits"&&requested;
                    results.Add($"{(passed?"PASS":"FAIL")} scenario {i}: request={activeRequest}; finished={finished}; valid={validation.IsValid}; completed={player.CompletedCount}; failed={player.FailedCount}; audio={audio}; provider={tts.CurrentProvider}; poseObserved={pose}; expressionObserved={expression}; movementObserved={movement}; types={string.Join(",",types)}; boneDelta={boneDelta}; yawDelta={yawDelta}; expressions={string.Join(",",observedExpressions)}; actions={string.Join("|",actionResults)}; locomotion={character.LocomotionActionExecutor.LastMessage}; error={relay.LastError}; ttsError={tts.LatestError}");
                    File.WriteAllText(PhonePagesReviewChecks.Output+"/real-plan-"+i+".json",relay.LastExtractedStagePlan??"");
                    if(i==3)results.Add((controller.TotalUnreadCount>0?"PASS":"FAIL")+" background reply unread after task removal");
                    if(relay.RequestPending||relay.IsLlmStagePlanPlaying){relay.StopLlmStagePlan();yield return null;}
                    File.WriteAllLines(PhonePagesReviewChecks.Output+"/real-chain.txt",results);
                }
                shell.OpenApp("momotalk");yield return new WaitForSecondsRealtime(.4f);chat.Select(character);shell.SetAutoSend(true);chat.input.text="Keep isolated voice draft";chat.StartVoice();yield return new WaitForSecondsRealtime(.5f);
                bool started=runtime.Asr.Active;string session=runtime.Asr.CurrentSessionId;int before=relay.LatestRequestId;shell.Recent();yield return new WaitForSecondsRealtime(.4f);
                typeof(PhoneLiveMomotalk).GetMethod("Recognized",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chat,new object[]{new AsrRecognitionResult(session,AsrRecognitionStatus.Done,"late isolated result","",AsrResultMode.AutoSendToLlm)});
                results.Add((started&&!runtime.Asr.Active&&relay.LatestRequestId==before&&chat.input.text=="Keep isolated voice draft"?"PASS":"FAIL")+" real microphone starts, navigation cancels, late result ignored");
                bool unchanged=historyBytes==null?!File.Exists(originalHistoryPath):File.ReadAllBytes(originalHistoryPath).SequenceEqual(historyBytes);results.Add((unchanged?"PASS":"FAIL")+" original user history bytes unchanged");
            }
            finally
            {
                Cleanup();Result=string.Join("\n",results);Directory.CreateDirectory(PhonePagesReviewChecks.Output);File.WriteAllText(PhonePagesReviewChecks.Output+"/real-chain.txt",Result);
            }
        }
    }
}
