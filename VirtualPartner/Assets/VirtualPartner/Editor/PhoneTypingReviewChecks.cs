using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using VirtualPartner.Runtime;
using VirtualPartner.Runtime.PhoneOS;
using Object=UnityEngine.Object;

namespace VirtualPartner.EditorTools
{
    public static class PhoneTypingReviewChecks
    {
        public static string Run()
        {
            var obj=new GameObject("Isolated typing checks");obj.SetActive(false);
            var player=obj.AddComponent<StagePlanPlayer>();var controller=obj.AddComponent<MomotalkConversationController>();
            void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
            int passed=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);passed++;}
            var history=new MomotalkHistoryStore(Path.GetFullPath("Library/MCPForUnity/TypingReview/"+Guid.NewGuid().ToString("N")));
            var registry=new ConversationRequestRegistry();registry.Register(17,"test","turn-test");
            Set(controller,"historyStore",history);Set(controller,"requestRegistry",registry);Set(controller,"stagePlanPlayer",player);
            var stages=(List<StagePlanStageDto>)typeof(StagePlanPlayer).GetField("activeStages",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player);
            try
            {
                Check(controller.ReadPending("test").Count==1,"Initial request is waiting");
                history.Append("test",MomotalkHistoryStore.CreateMessage("character","First reply","shown",17,0,0,"turn-test"));
                Set(player,"playing",true);Set(player,"activeOwnerId",LlmRelay.LlmOwnerId);Set(player,"activeRequestId",17);Set(player,"currentStageIndex",0);
                stages.Add(new StagePlanStageDto{actions=new[]{new StagePlanActionDto{type="speech",text="First reply"}}});
                stages.Add(new StagePlanStageDto{actions=new[]{new StagePlanActionDto{type="speech",text="Second reply"}}});
                Check(controller.ReadPending("test").Count==1,"First reply does not hide later speech");
                Set(player,"currentStageIndex",1);
                Check(controller.ReadPending("test").Count==0,"No waiting after final speech displayed");
                var actionType=typeof(StagePlanPlayer).GetNestedType("RunningStageAction",BindingFlags.NonPublic);
                var kind=typeof(StagePlanPlayer).GetNestedType("StageActionKind",BindingFlags.NonPublic);
                var action=Activator.CreateInstance(actionType,new object[]{1,0,"speech",Enum.Parse(kind,"Speech"),new StagePlanActionDto{type="speech",text="Second reply"}});
                var actions=(IList)typeof(StagePlanPlayer).GetField("runningActions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player);actions.Add(action);
                Check(controller.ReadPending("test").Count==1,"Current speech waits for playback-start event");
                actionType.GetProperty("SpeechEventRaised").SetValue(action,true);
                Check(controller.ReadPending("test").Count==0,"Playback event removes typing cue");
                stages.Add(new StagePlanStageDto{actions=new[]{new StagePlanActionDto{type="facing"}}});
                Check(!player.HasPendingSpeech(17),"Trailing action-only stage is not typing");
                Set(player,"activePlanStreaming",true);Set(player,"activePlanStreamComplete",false);
                Check(player.HasPendingSpeech(17),"Open stream retains continuation cue");
                Set(player,"activePlanStreamComplete",true);Check(!player.HasPendingSpeech(17),"Stream completion clears unknown continuation");
                foreach(var state in new[]{RequestStatus.Finished,RequestStatus.Failed,RequestStatus.Replaced})
                {registry.Register(17,"test","turn-test");registry.TrySetStatus(17,state);Check(controller.ReadPending("test").Count==0,"Terminal request clears cue: "+state);}
                Check(PhoneTypingIndicator.Offset(.25f,0)>PhoneTypingIndicator.Offset(.25f,1),"Dots animate out of phase");
                for(int i=0;i<3;i++)for(float t=0;t<3;t+=.01f){float y=PhoneTypingIndicator.Offset(t,i);if(y<0||y>4.001f)throw new Exception("Dot outside fixed bounds");}
                passed++;
                var result=passed+" passed: request lifecycle, subsequent speech, playback delay, stream, terminal states and motion bounds";
                Directory.CreateDirectory("Library/MCPForUnity/TypingReview");File.WriteAllText("Library/MCPForUnity/TypingReview/checks.txt",result);return result;
            }
            finally{Object.DestroyImmediate(obj);}
        }
    }
}
