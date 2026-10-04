using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneLiveDebug : MonoBehaviour, IPhoneTaskReset
    {
        public TMP_Text[] summaries;
        public TMP_Text feedback,documentTitle;
        public TMP_InputField document,llmText,ttsText,mockText,mouthIndex;
        public Slider[] axes;
        public Toggle boneApply;
        public Button boneTemplate;
        public Transform boneRows;
        private PhoneLiveRuntime runtime;
        private PhonePreviewApp app;
        private StagePlanDebugPanel stage;
        private VirtualPartnerBoneDebugPanel bone;
        private AutonomousBehaviorScheduler fsm;
        private RootOrientationController root;
        private TtsManager tts;
        private MemorySystem memory;
        private ExpressionActionExecutor expression;
        private MouthTextureController mouth;
        private string documentKind="";
        private float nextRefresh;
        private bool ready;
        private readonly List<GameObject> boneButtons=new List<GameObject>();
        public void ResetTaskView(){nextRefresh=0;runtime?.CancelDebugRecognition();}
        private void PageChanged(){nextRefresh=0;if(ready&&!app.Suspended&&app.CurrentPage==11)SyncBone();if(app.Suspended||app.CurrentPage!=6)runtime?.CancelDebugRecognition();}
        private void OnDisable(){runtime?.CancelDebugRecognition();}
        private void OnDestroy(){if(app!=null)app.StateChanged-=PageChanged;}
        private void Start()
        {
            runtime=GetComponentInParent<PhoneLiveRuntime>();app=GetComponent<PhonePreviewApp>();
            app.StateChanged+=PageChanged;stage=FindFirstObjectByType<StagePlanDebugPanel>();bone=FindFirstObjectByType<VirtualPartnerBoneDebugPanel>();fsm=FindFirstObjectByType<AutonomousBehaviorScheduler>();root=FindFirstObjectByType<RootOrientationController>();
            tts=FindFirstObjectByType<TtsManager>();memory=FindFirstObjectByType<MemorySystem>();expression=FindFirstObjectByType<ExpressionActionExecutor>();mouth=FindFirstObjectByType<MouthTextureController>();
            document.onValueChanged.AddListener(text=>{if(documentKind=="stage")stage.Json=text;});
            mockText.onValueChanged.AddListener(text=>runtime.Asr.SetMockText(text));
            for(var i=0;i<axes.Length;i++){var axis=i;axes[i].onValueChanged.AddListener(v=>{var rot=bone.Rotation;rot[axis]=v;bone.Rotation=rot;});}
            boneApply.onValueChanged.AddListener(value=>bone.Apply=value);
        }
        private void Update()
        {
            if(runtime==null||!runtime.Ready)return;
            if(!ready){ready=true;mockText.SetTextWithoutNotify(runtime.Asr.MockText);RefreshBones();SyncBone();}
            feedback.transform.parent.gameObject.SetActive(app.CurrentPage>0&&!string.IsNullOrWhiteSpace(feedback.text));
            if(app.Suspended||!runtime.Shell.IsOpen||Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.25f;
            var page=app.CurrentPage;
            if(page>0&&page<=12)summaries[page-1].text=Summary(page);
        }
        private string Summary(int page)
        {
            switch(page){
                case 1:return $"LLM: {runtime.Relay.StatusText}\nFSM: {fsm.State} · {fsm.CurrentActionName}\nTTS: {tts.StatusText}\nASR: {runtime.Asr.Status}";
                case 2:return $"{runtime.Relay.StatusText}\n{runtime.Relay.LastError}\nRequests: {runtime.Conversation.ActiveRequestCount}";
                case 3:return $"{FindFirstObjectByType<StagePlanPlayer>().StatusText}\n{stage.Result}";
                case 4:return $"{runtime.Conversation.CurrentCharacterId}\nUnread: {runtime.Conversation.TotalUnreadCount}\nActive requests: {runtime.Conversation.ActiveRequestCount}";
                case 5:return $"{tts.StatusText}\n{tts.HealthStatusText}\n{tts.LatestError}";
                case 6:return $"{runtime.Asr.Status}\n{runtime.Asr.HealthStatusText}\n{runtime.Asr.LatestError}";
                case 7:return $"{memory.StatusText}\n{memory.LastMessage}";
                case 8:return $"Registered characters: {CharacterRegistry.RegisteredCount}\n{runtime.Conversation.CurrentCharacterId}";
                case 9:return $"Enabled: {fsm.SchedulerActive}\n{fsm.State} · {fsm.CurrentActionName}\nInteraction: {fsm.IsInUserInteraction} ({fsm.InteractionRemaining:0.0}s)";
                case 10:return $"Position: {root.Root.position}\nYaw: {root.Root.eulerAngles.y:0.0}\n{root.ActiveTarget}";
                case 11:return $"Selected: {(bone.Bones.Count>0?bone.Bones[bone.SelectedIndex].DisplayName:"None")}\nApply: {bone.Apply} · Pinned: {bone.PinnedCount}\nRotation: {bone.Rotation}";
                case 12:return $"Expression: {expression.CurrentExpression}\nMouth: {mouth.CurrentMouthIndex}\n{expression.LastMessage}";
                default:return "";
            }
        }
        public void Execute(string command)
        {
            if(!ready){feedback.text="Runtime is starting.";return;}
            try
            {
                feedback.text="Done: "+command;
                switch(command)
                {
                    case "Submit":
                        var characters=new List<CharacterRuntimeContext>();CharacterRegistry.GetRegisteredContexts(characters);
                        var target=characters.Find(c=>c.CharacterId==runtime.Conversation.CurrentCharacterId)??(characters.Count>0?characters[0]:null);
                        if(target==null){feedback.text="No active character.";break;}
                        runtime.Conversation.SendMessage(target,llmText.text);feedback.text=runtime.Relay.StatusText;break;
                    case "Stop LLM plan":runtime.Relay.StopLlmStagePlan();break;
                    case "Load basic":stage.LoadBasic();break;
                    case "Load full":stage.LoadFull();break;
                    case "Paste clipboard":stage.Json=GUIUtility.systemCopyBuffer;break;
                    case "Validate":stage.ValidateCurrentJson();feedback.text=stage.Result;break;
                    case "Play":stage.PlayCurrentJson(false);feedback.text=stage.Result;break;
                    case "Replace":stage.PlayCurrentJson(true);feedback.text=stage.Result;break;
                    case "Stop":stage.StopCurrentPlayback();break;
                    case "Clear":stage.Clear();break;
                    case "Open":runtime.Shell.OpenApp("momotalk");break;
                    case "Close":runtime.Shell.Collapse();break;
                    case "Show contacts":runtime.Shell.host.OpenLinkedApp("momotalk",0);break;
                    case "History folder":runtime.Conversation.OpenHistoryFolder();break;
                    case "TTS health":tts.RequestHealthCheck();break;
                    case "Real TTS test":case "Warmup test":case "Mock failure":
                        tts.SetMockFailureMode(command=="Mock failure");
                        if(!tts.StartSpeech(new StagePlanActionDto{type="speech",text=ttsText.text,emotion="neutral",speed=1},1,out var reason))feedback.text=reason;break;
                    case "Stop TTS":tts.StopSpeech("Stopped from phone Debug.");break;
                    case "ASR health":runtime.Asr.RequestHealthCheck();break;
                    case "Start real":if(!runtime.StartDebugRecognition(false,out var realError))feedback.text=realError;break;
                    case "Start mock":if(!runtime.StartDebugRecognition(true,out var mockError))feedback.text=mockError;break;
                    case "Cancel":runtime.Asr.CancelRecognition();break;
                    case "Reload memory":memory.ReloadMemory();break;
                    case "Judge last turn":memory.QueueLatestTurnForDebug();break;
                    case "Memory folder":memory.OpenMemoryFolder();break;
                    case "Clear decision":memory.ClearLatestDecision();break;
                    case "Enable / disable":fsm.SetSchedulerActive(!fsm.SchedulerActive);break;
                    case "Enter interaction":fsm.EnterUserInteraction();break;
                    case "Exit interaction":fsm.ExitUserInteraction();break;
                    case "Root interaction":root.EnterUserInteraction();break;
                    case "Root exit":root.ExitUserInteraction();break;
                    case "Refresh UI":bone.RefreshControlInstances();RefreshBones();SyncBone();break;
                    case "Zero":bone.Rotation=Vector3.zero;SyncBone();break;
                    case "Pin selected":bone.PinSelected();break;
                    case "Pin L/R pair":bone.PinSelectedPair();break;
                    case "Unpin selected":bone.UnpinSelected();break;
                    case "Clear pins":bone.ClearPins();break;
                    case "Export selected":bone.ExportSelectedJson();OpenDocument("bone");break;
                    case "Export pinned":bone.ExportPinnedStagePlan();OpenDocument("bone");break;
                    case "Apply debug":if(!int.TryParse(mouthIndex.text,out var index)||index < -1||index>63){feedback.text="Mouth index must be -1 to 63.";break;}mouth.SetDebugMouthIndex(index);break;
                    case "Release debug":mouth.ReleaseDebugOverride();break;
                    case "Clear expression":expression.ClearExpression();break;
                    default:if(command.StartsWith("Expression:")){if(!expression.StartExpression(command.Substring(11),.3f,out var error))feedback.text=error;}else throw new InvalidOperationException("Unbound command: "+command);break;
                }
            }
            catch(Exception error){feedback.text=error.Message;Debug.LogException(error,this);}
        }
        public void SetOption(string option,bool value)
        {
            switch(option){case "Force mock failure":tts.SetMockFailureMode(value);break;case "Use 3D audio":tts.SetUse3DAudio(value);break;
                case "Use mock ASR":runtime.Asr.SetProviderMode(value?AsrProviderMode.Mock:AsrProviderMode.RealService);break;
                case "Auto-send to LLM":runtime.Asr.SetResultMode(value?AsrResultMode.AutoSendToLlm:AsrResultMode.FillInputOnly);break;
                case "ASR unavailable":runtime.Asr.SetUnavailable(value);break;case "ASR failure":runtime.Asr.SetMockFailureMode(value);break;}
        }
        public bool ReadOption(string option)=>option switch {
            "Force mock failure"=>tts.ForceMockFailure,"Use 3D audio"=>tts.Use3DAudio,"Use mock ASR"=>runtime.Asr.ProviderMode==AsrProviderMode.Mock,
            "Auto-send to LLM"=>runtime.Asr.ResultMode==AsrResultMode.AutoSendToLlm,"ASR unavailable"=>runtime.Asr.AsrUnavailable,"ASR failure"=>runtime.Asr.ForceMockFailure,_=>false};
        public void OpenDocument(string kind)
        {
            documentKind=kind;document.readOnly=kind!="stage";foreach(var button in app.pages[13].GetComponentsInChildren<Button>(true))if(button.name=="Paste")button.interactable=!document.readOnly;documentTitle.text=kind=="stage"?"StagePlan JSON":"Details · "+kind;
            var text=kind switch {"stage"=>stage.Json,"validation"=>stage.Result,"prompt"=>runtime.Relay.BuildPromptPreview(),"response"=>runtime.Relay.LastRawResponse,"memory"=>memory.LatestRawMemoryJudgeResponse,"bone"=>bone.ExportedJson,_=>Snapshot(app.CurrentPage)};
            document.SetTextWithoutNotify(text??"");app.ShowPage(13);
        }
        private string Snapshot(int page)
        {
            object[] sources=page switch {
                1=>new object[]{runtime.Relay,fsm,tts,runtime.Asr,memory},2=>new object[]{runtime.Relay},3=>new object[]{FindFirstObjectByType<StagePlanPlayer>()},4=>new object[]{runtime.Conversation},5=>new object[]{tts},6=>new object[]{runtime.Asr},7=>new object[]{memory},
                8=>new object[]{FindFirstObjectByType<VirtualPartnerStage1Bootstrap>()},9=>new object[]{fsm},10=>new object[]{root,FindFirstObjectByType<LocomotionActionExecutor>(),FindFirstObjectByType<MovementConstraintController>()},11=>new object[]{bone,FindFirstObjectByType<ActionCoordinator>()},12=>new object[]{mouth,expression,FindFirstObjectByType<SpeechMouthDriver>()},_=>Array.Empty<object>()};
            var result=new StringBuilder();result.AppendLine(Summary(page));
            if(page==8)
            {
                var characters=new List<CharacterRuntimeContext>();CharacterRegistry.GetRegisteredContexts(characters);
                foreach(var character in characters)result.AppendLine($"{character.Profile.DisplayName} ({character.CharacterId})\nStatus: {character.Profile.MomotalkStatus}\nProfile: {character.Profile.name}\nRuntime root: {character.RuntimeRoot?.name}\nStagePlan: {character.StagePlanPlayer!=null} · Bones: {character.AvatarPoseApplier!=null} · FSM: {character.AutonomousBehaviorScheduler!=null}\n");
            }
            foreach(var source in sources)
            {
                if(source==null)continue;result.AppendLine(source.GetType().Name);
                // Public diagnostic properties only; never reflect private configuration/key storage.
                foreach(var property in source.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly))
                {if(!property.CanRead||property.GetIndexParameters().Length!=0)continue;var type=property.PropertyType;if(type!=typeof(string)&&!type.IsPrimitive&&!type.IsEnum)continue;result.AppendLine(property.Name+": "+property.GetValue(source));}
                result.AppendLine();
            }
            return result.ToString();
        }
        private void RefreshBones()
        {
            foreach(var item in boneButtons)Destroy(item);boneButtons.Clear();
            for(var i=0;i<bone.Bones.Count;i++)
            {var index=i;var button=Instantiate(boneTemplate,boneRows);button.gameObject.SetActive(true);button.GetComponentInChildren<TMP_Text>().text=bone.Bones[i].DisplayName;button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(()=>{bone.SelectBone(index);SyncBone();app.ShowPage(11);});boneButtons.Add(button.gameObject);}
        }
        private void SyncBone()
        {
            if(bone.Bones.Count==0)return;var entry=bone.Bones[bone.SelectedIndex].Entry;
            for(var i=0;i<axes.Length;i++){axes[i].minValue=entry.RangeMin;axes[i].maxValue=entry.RangeMax;axes[i].interactable=entry.IsAxisEnabled(i);axes[i].SetValueWithoutNotify(bone.Rotation[i]);}
            boneApply.SetIsOnWithoutNotify(bone.Apply);boneApply.GetComponent<PhoneSwitch>().Apply(bone.Apply);
        }
        public void Copy()=>GUIUtility.systemCopyBuffer=document.text;
        public void Paste(){if(!document.readOnly)document.text=GUIUtility.systemCopyBuffer;}
    }
}
