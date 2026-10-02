using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VirtualPartner.Runtime;
using VirtualPartner.Runtime.PhoneOS;
using Object=UnityEngine.Object;
namespace VirtualPartner.EditorTools
{
    public static class PhoneNavigationReviewChecks
    {
        public static bool Running {get;private set;}
        public static string Result {get;private set;}
        public const string Output="Library/MCPForUnity/PhoneNavigationReview";
        private static readonly List<string> results=new List<string>();
        public static void Run()
        {
            if(Running)return;
            var runtime=Object.FindFirstObjectByType<PhoneLiveRuntime>();
            if(!Application.isPlaying||runtime==null||!runtime.Ready)throw new InvalidOperationException("Play PhoneOS and wait for bootstrap.");
            Directory.CreateDirectory(Output);results.Clear();Running=true;
            runtime.StartCoroutine(Guard(Routine(runtime)));
        }
        private static IEnumerator Guard(IEnumerator routine)
        {
            while(true){object value;try{if(!routine.MoveNext())break;value=routine.Current;}catch(Exception error){results.Add("FAIL "+error);break;}yield return value;}
            Running=false;Result=$"{results.Count(r=>r.StartsWith("PASS"))} passed, {results.Count(r=>r.StartsWith("FAIL"))} failed";
            File.WriteAllText(Output+"/checks.txt",Result+"\n"+string.Join("\n",results));
        }
        private static void Check(bool condition,string label)=>results.Add((condition?"PASS ":"FAIL ")+label);
        private static IEnumerator Settle(){yield return new WaitForEndOfFrame();yield return new WaitForSecondsRealtime(.3f);}
        private static IEnumerator Routine(PhoneLiveRuntime runtime)
        {
            var shell=runtime.Shell;var host=shell.host;var height=shell.HeightFraction;
            PhoneVisualReviewChecks.SetResolution(1920,1080);shell.SetHeight(.9f);shell.Open();yield return Settle();
            foreach(var task in host.RecentTasks.ToArray())host.DismissTask(task.Definition.AppId);
            shell.Recent();yield return Settle();
            Check(host.IsOverviewOpen&&shell.GetComponent<PhoneRecentTasksView>().CardCount==0,"Empty overview");yield return CaptureFrame(shell,"empty");
            shell.Collapse();shell.Open();yield return Settle();Check(host.IsOverviewOpen,"Collapse restores overview");
            shell.Back();Check(!host.IsOverviewOpen&&!host.HasCurrentApp,"Back from empty overview returns home");
            shell.Back();Check(!shell.IsOpen,"Back from home collapses");shell.Open();
            shell.OpenApp("momotalk");yield return Settle();
            var chatApp=(PhonePreviewApp)host.CurrentApp;var chat=chatApp.GetComponent<PhoneLiveMomotalk>();
            var contexts=new List<CharacterRuntimeContext>();CharacterRegistry.GetRegisteredContexts(contexts);chat.Select(contexts[0]);yield return Settle();
            string draft=chat.input.text;chat.input.text="Navigation check 草稿";
            shell.Recent();yield return Settle();
            Check(host.IsOverviewOpen&&chatApp.Suspended&&!runtime.IsConversationVisible(chat.CharacterId),"Overview suspends chat and does not mark read");
            var preview=host.RecentTasks[0].Preview;Check(preview!=null,"Actual app thumbnail captured");
            host.DismissTask("momotalk");yield return Settle();
            Check(host.RecentTasks.Count==0&&chatApp.CurrentPage==0&&preview==null,"Dismiss resets page and releases preview");
            shell.Back();Check(!host.HasCurrentApp,"Removed origin falls back to home");
            shell.OpenApp("momotalk");yield return Settle();chat.Select(contexts[0]);yield return Settle();
            Check(ReferenceEquals(chatApp,host.CurrentApp)&&chat.input.text=="Navigation check 草稿","Dismiss retains cached instance and character draft");chat.input.text=draft;
            shell.OpenApp("settings");yield return Settle();var settingsApp=(PhonePreviewApp)host.CurrentApp;var settings=settingsApp.GetComponent<PhoneLiveSettings>();
            var originalModel=settings.model.text;settings.model.text="unsaved-navigation-check";settingsApp.ShowPage(2);settings.key.contentType=TMP_InputField.ContentType.Standard;
            shell.Recent();yield return Settle();var settingsTask=host.RecentTasks.First(t=>t.Definition.AppId=="settings");
            Check(settingsTask.PrivatePreview&&settingsTask.Preview==null,"Settings never captures configuration pixels");host.DismissTask("settings");
            shell.OpenApp("settings");yield return Settle();
            Check(settingsApp.CurrentPage==0&&settings.model.text=="unsaved-navigation-check"&&settings.key.contentType==TMP_InputField.ContentType.Password,"Settings draft retained, page reset, key hidden");settings.model.text=originalModel;
            shell.OpenApp("debug");yield return Settle();var debugApp=(PhonePreviewApp)host.CurrentApp;debugApp.ShowPage(2);debugApp.OpenApi();yield return Settle();
            Check(host.CurrentAppDefinition.AppId=="settings"&&((PhonePreviewApp)host.CurrentApp).CurrentPage==2,"Linked Settings API route");shell.Back();yield return Settle();
            Check(ReferenceEquals(host.CurrentApp,debugApp)&&debugApp.CurrentPage==2,"One Back restores Debug origin page");
            var debug=debugApp.GetComponent<PhoneLiveDebug>();var debugText=debug.llmText.text;debug.llmText.text="unsent debug draft";
            shell.Recent();yield return Settle();host.DismissTask("debug");shell.OpenApp("debug");yield return Settle();
            Check(debugApp.CurrentPage==0&&debug.llmText.text=="unsent debug draft","Debug reset keeps unsent edits");debug.llmText.text=debugText;
            shell.OpenApp("camera");yield return Settle();var cameraApp=(PhonePreviewApp)host.CurrentApp;var cameraUI=cameraApp.GetComponent<PhoneLiveCamera>();
            var driver=Object.FindFirstObjectByType<VirtualSceneCameraController>();var radius=driver.Radius;
            cameraUI.pan.OnPointerDown(new PointerEventData(EventSystem.current){pointerId=42,position=RectTransformUtility.WorldToScreenPoint(null,cameraUI.pan.transform.position+Vector3.right*30)});
            Check(cameraUI.pan.Value!=Vector2.zero,"Joystick receives pointer input");shell.Recent();yield return Settle();Check(cameraUI.pan.Value==Vector2.zero,"Overview releases joystick");
            host.DismissTask("camera");driver.SetRadius(radius);shell.OpenApp("camera");yield return Settle();Check(Mathf.Approximately(cameraUI.zoom.value,driver.Radius),"Camera reopening synchronizes zoom");
            shell.Home();shell.OpenApp("debug");shell.OpenApp("momotalk");yield return Settle();
            Check(!host.NavigationPending&&host.CurrentAppDefinition.AppId=="momotalk"&&!host.IsOverviewOpen,"Rapid navigation last request wins");
            Check(host.RecentTasks.Select(t=>t.Definition.AppId).Distinct().Count()==host.RecentTasks.Count,"Recent tasks unique");
            foreach(var id in new[]{"momotalk","settings","camera","debug"})
            {shell.OpenApp(id);yield return Settle();var app=(PhonePreviewApp)host.CurrentApp;foreach(var button in app.GetComponentsInChildren<Button>(true))for(int n=0;n<button.onClick.GetPersistentEventCount();n++)Check(button.onClick.GetPersistentMethodName(n)!="Preview",id+"/"+button.name+" has no preview service action");}
            shell.Recent();yield return Settle();Check(host.RecentTasks[0].Definition.AppId=="debug","Most recent first");yield return CaptureFrame(shell,"recent-apps");
            shell.Back();yield return Settle();yield return CaptureFrame(shell,"restored-debug");
            var bone=Object.FindFirstObjectByType<VirtualPartnerBoneDebugPanel>();var pinCount=bone.PinnedCount;
            bone.PinSelected();var afterPin=bone.PinnedCount;shell.Recent();yield return Settle();
            host.DismissTask("debug");Check(bone.PinnedCount==afterPin,"Dismiss Debug retains applied bone pins");if(afterPin>pinCount)bone.UnpinSelected();
            var card=shell.GetComponentsInChildren<PhoneRecentTaskCard>().First();
            var dismissedId=card.name.Substring("Task_".Length);var origin=RectTransformUtility.WorldToScreenPoint(null,card.transform.position);
            var pointer=new PointerEventData(EventSystem.current){pointerId=17,position=origin,pressPosition=origin};
            card.OnBeginDrag(pointer);pointer.position=origin+Vector2.up*180;card.OnDrag(pointer);card.OnEndDrag(pointer);yield return Settle();
            Check(!host.RecentTasks.Any(t=>t.Definition.AppId==dismissedId)&&shell.IsOpen,"Upward gesture removes task without collapsing phone");
            card=shell.GetComponentsInChildren<PhoneRecentTaskCard>().First();origin=RectTransformUtility.WorldToScreenPoint(null,card.transform.position);
            pointer=new PointerEventData(EventSystem.current){pointerId=18,position=origin,pressPosition=origin};
            var taskCount=host.RecentTasks.Count;card.OnBeginDrag(pointer);pointer.position=origin+Vector2.left*180;card.OnDrag(pointer);card.OnEndDrag(pointer);yield return Settle();
            Check(host.RecentTasks.Count==taskCount&&shell.IsOpen,"Horizontal drag switches cards without dismissal");
            card=shell.GetComponentsInChildren<PhoneRecentTaskCard>().First();var cardPosition=((RectTransform)card.transform).anchoredPosition;
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,card.transform.position);card.OnBeginDrag(pointer);pointer.position+=Vector2.up*40;card.OnDrag(pointer);shell.Collapse();shell.Open();yield return Settle();
            Check(((RectTransform)card.transform).anchoredPosition==cardPosition,"Collapse releases in-progress card drag");

            shell.OpenApp("debug");yield return Settle();debugApp.ShowPage(6);
            var mode=runtime.Asr.ResultMode;runtime.Asr.SetResultMode(AsrResultMode.AutoSendToLlm);
            var started=runtime.StartDebugRecognition(true,out var error);var session=runtime.Asr.CurrentSessionId;
            shell.Recent();yield return Settle();Check(started&&!runtime.Asr.Active,"Debug ASR cancels on overview");
            var request=runtime.Relay.LatestRequestId;
            typeof(PhoneLiveRuntime).GetMethod("DebugRecognitionFinished",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(runtime,new object[]{new AsrRecognitionResult(session,AsrRecognitionStatus.Done,"late debug transcript","",AsrResultMode.AutoSendToLlm)});
            Check(runtime.Relay.LatestRequestId==request,"Late Debug ASR cannot send after navigation");runtime.Asr.SetResultMode(mode);

            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3440,1440)})
            foreach(var fraction in new[]{.7f,.9f,.95f})
            {
                PhoneVisualReviewChecks.SetResolution(size.x,size.y);shell.SetHeight(fraction);if(!host.IsOverviewOpen)shell.Recent();yield return Settle();
                var corners=new Vector3[4];shell.device.GetWorldCorners(corners);
                Check(corners[0].y>=-1&&corners[2].y<=Screen.height+1&&corners[0].x>=-1&&corners[2].x<=Screen.width+1,$"Phone bounds {size.x}x{size.y} {fraction}");
                yield return CaptureFrame(shell,$"recent-{size.x}x{size.y}-{Mathf.RoundToInt(fraction*100)}");
            }
            PhoneVisualReviewChecks.SetResolution(1920,1080);shell.SetHeight(height);yield return Settle();
            Check(Object.FindObjectsByType<MomotalkConversationController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Single conversation owner after navigation");
            shell.Home();yield return Settle();
        }
        private static void Capture(PhonePresentationShell shell,string name)
        {
            // Routine captures after settling; callers always yield at least one frame before this point.
            shell.StartCoroutine(CaptureFrame(shell,name));
        }
        private static IEnumerator CaptureFrame(PhonePresentationShell shell,string name)
        {
            yield return new WaitForEndOfFrame();var frame=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",frame.EncodeToPNG());
            var corners=new Vector3[4];shell.device.GetWorldCorners(corners);int x=Mathf.Clamp(Mathf.FloorToInt(corners[0].x),0,frame.width-1),y=Mathf.Clamp(Mathf.FloorToInt(corners[0].y),0,frame.height-1);
            int w=Mathf.Min(Mathf.CeilToInt(corners[2].x)-x,frame.width-x),h=Mathf.Min(Mathf.CeilToInt(corners[2].y)-y,frame.height-y);
            if(w>0&&h>0){var crop=new Texture2D(w,h,TextureFormat.RGB24,false);crop.SetPixels(frame.GetPixels(x,y,w,h));crop.Apply();File.WriteAllBytes(Output+"/"+name+"-phone.png",crop.EncodeToPNG());Object.Destroy(crop);}Object.Destroy(frame);
        }
    }
}
