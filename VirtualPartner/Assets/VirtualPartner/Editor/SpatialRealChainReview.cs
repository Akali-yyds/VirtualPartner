#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using VirtualPartner.Runtime;
using Object=UnityEngine.Object;

namespace VirtualPartner.Editor
{
    public static class SpatialRealChainReview
    {
        public static bool Running {get;private set;}
        public static string Result {get;private set;}
        private static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance);
        public static string Start()
        {
            if(Running)return "Already running";
            var controller=Object.FindFirstObjectByType<MomotalkConversationController>();var relay=Object.FindFirstObjectByType<LlmRelay>();
            if(controller==null||relay==null||relay.RequestPending||controller.ActiveRequestCount>0)return "BLOCKED: idle conversation runtime required.";
            controller.StartCoroutine(Run(controller,relay));return "Real requests started with isolated history and memory detached; existing credential stays in runtime.";
        }
        private static IEnumerator Run(MomotalkConversationController controller,LlmRelay relay)
        {
            Running=true;Result="Running";
            var characters=new List<CharacterRuntimeContext>();CharacterRegistry.GetRegisteredContexts(characters);
            if(characters.Count==0){Running=false;Result="BLOCKED: no character";yield break;}
            var context=characters[0];var player=context.StagePlanPlayer;var motion=Object.FindFirstObjectByType<SpatialMotionRuntime>();var tts=Object.FindFirstObjectByType<TtsManager>();
            var history=Field(controller.GetType(),"historyStore");var cm=Field(controller.GetType(),"memorySystem");var rm=Field(relay.GetType(),"memorySystem");
            var unread=Field(controller.GetType(),"unreadCounts");var counts=(Dictionary<string,int>)unread.GetValue(controller);var oldCounts=new Dictionary<string,int>(counts);
            object oldHistory=history.GetValue(controller),oldCm=cm.GetValue(controller),oldRm=rm.GetValue(relay);
            var lines=new List<string>();string directory=SpatialMotionReview.Output+"/real-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss");Directory.CreateDirectory(directory);
            bool presented=false,audio=false,finished=false;var failures=new List<string>();
            Action<StagePlanSpeechEvent> onSpeech=e=>presented=true;Action onAudio=()=>audio=true;Action<StagePlanFinishedEvent> onFinish=e=>finished=true;Action<int,string> onFail=(id,error)=>failures.Add(error);
            try
            {
                history.SetValue(controller,new MomotalkHistoryStore(Path.GetFullPath(directory+"/history")));cm.SetValue(controller,null);rm.SetValue(relay,null);
                player.SpeechActionStarted+=onSpeech;player.StagePlanFinished+=onFinish;player.BodyActionFailed+=onFail;tts.SpeechPlaybackStarted+=onAudio;
                var prompts=new[]{
                    "简短问候，用右手在脸旁小幅挥两下，然后自然放下。使用空间轨迹。",
                    "换左手，在肩膀以下轻轻挥一下，再把双手摊开表示疑问，然后放下。",
                    "双臂分别画不同的小弧线，同时轻轻转头和倾身，脚不要动。",
                    "双脚不动，缓慢浅蹲，再恢复站姿。",
                    "右手放在胸前附近保持，直到我让你停止。不要精确接触身体。",
                    "保持当前姿态，简短说一句今天天气怎么样，不用真实天气服务。",
                    "右手小幅挥一下，然后恢复刚才保持的姿态。",
                    "好了，可以停止保持并复位了。"};
                for(int i=0;i<prompts.Length;i++)
                {
                    if(i<4)motion.ResetPose();yield return new WaitForSeconds(.5f);
                    presented=false;audio=false;finished=false;failures.Clear();
                    controller.SendMessage(context,"[动作验收测试，不写入长期记忆] "+prompts[i]);
                    double deadline=Time.realtimeSinceStartupAsDouble+120;bool captured=false;
                    while(Time.realtimeSinceStartupAsDouble<deadline && (!finished||relay.RequestPending||tts.Active))
                    {
                        if(!captured && presented && motion.HasAny){yield return new WaitForEndOfFrame();SpatialMotionReview.SaveFrame("real-"+i+".png");captured=true;}
                        if(!relay.RequestPending && !player.IsPlaying && !string.IsNullOrEmpty(relay.LastError))break;
                        yield return null;
                    }
                    var status=finished&&string.IsNullOrEmpty(relay.LastError)&&failures.Count==0?"EXECUTION_PASS":"FAIL_OR_BLOCKED";
                    lines.Add(status+" scenario="+i+" presented="+presented+" audioEvent="+audio+" realAudio="+(audio && string.IsNullOrEmpty(tts.LatestError))+" provider="+tts.CurrentProvider+" errors="+string.Join(" | ",failures)+" relay="+relay.LastError+" tts="+tts.LatestError+"\n"+motion.Describe()+"\n"+relay.GetComponent<PerformanceLatency>()?.Describe());
                    File.WriteAllText(directory+"/plan-"+i+".json",!string.IsNullOrEmpty(relay.LastExtractedStagePlan)?relay.LastExtractedStagePlan:relay.LastReceivedStageJson??"");File.WriteAllLines(directory+"/results.txt",lines);
                    if(relay.RequestPending)relay.StopPendingRequest();if(player.IsPlaying)relay.StopLlmStagePlan();
                    if(!string.IsNullOrEmpty(relay.LastError) && (relay.LastError.Contains("HTTP") || relay.LastError.Contains("connect"))){lines.Add("Remaining scenarios blocked by LLM service failure.");break;}
                }
            }
            finally
            {
                relay.StopPendingRequest();relay.StopLlmStagePlan();motion.ResetPose();
                history.SetValue(controller,oldHistory);cm.SetValue(controller,oldCm);rm.SetValue(relay,oldRm);counts.Clear();foreach(var pair in oldCounts)counts[pair.Key]=pair.Value;
                player.SpeechActionStarted-=onSpeech;player.StagePlanFinished-=onFinish;player.BodyActionFailed-=onFail;tts.SpeechPlaybackStarted-=onAudio;
                Result=string.Join("\n",lines);File.WriteAllText(directory+"/results.txt",Result);Running=false;
            }
        }
    }
}
#endif
