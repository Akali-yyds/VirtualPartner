using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VirtualPartner.Runtime;
using VirtualPartner.Runtime.PhoneOS;
using Object=UnityEngine.Object;

namespace VirtualPartner.EditorTools
{
    public static class PhonePolishReviewChecks
    {
        public const string Output="Library/MCPForUnity/PhonePolishReview";
        public static bool Running {get;private set;}
        public static string Result {get;private set;}
        private static readonly List<string> checks=new List<string>();
        private static bool recording;
        private static float oldHeight;
        public static void Run()
        {
            var runtime=Object.FindFirstObjectByType<PhoneLiveRuntime>();
            if(!Application.isPlaying||runtime==null||!runtime.Ready)throw new InvalidOperationException("Live PhoneOS must be ready.");
            if(Running)return;Running=true;checks.Clear();Directory.CreateDirectory(Output);Directory.CreateDirectory(Output+"/frames");
            oldHeight=runtime.Shell.HeightFraction;runtime.StartCoroutine(Guard(Routine(runtime),runtime.Shell));
        }
        private static IEnumerator Guard(IEnumerator routine,PhonePresentationShell shell)
        {
            while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){checks.Add("FAIL "+e);break;}yield return next;}
            recording=false;shell.SetHeight(oldHeight);Running=false;Result=$"{checks.Count(x=>x.StartsWith("PASS"))} passed, {checks.Count(x=>x.StartsWith("FAIL"))} failed";File.WriteAllText(Output+"/checks.txt",Result+"\n"+string.Join("\n",checks));
        }
        private static void Check(bool ok,string label)=>checks.Add((ok?"PASS ":"FAIL ")+label);
        private static IEnumerator Settle(){yield return null;yield return new WaitForSecondsRealtime(.45f);}
        private static IEnumerator Routine(PhoneLiveRuntime runtime)
        {
            var shell=runtime.Shell;var host=shell.host;var recents=shell.GetComponent<PhoneRecentTasksView>();
            PhoneVisualReviewChecks.SetResolution(1920,1080);shell.SetHeight(.9f);shell.Open();shell.Home();yield return Settle();
            var home=shell.dock.transform.parent;Check(!home.Find("Search").gameObject.activeSelf&&!home.Find("Weather").gameObject.activeSelf,"No placeholder desktop services");
            Check(home.GetComponentsInChildren<Button>().Count()==4,"Exactly four desktop launchers");
            Check(!shell.clockTime.text.Contains("\n"),"Single-line live clock");shell.SetDock(false);Check(shell.dock.activeSelf,"Legacy hideDock cannot hide sole launchers");
            yield return Capture(shell,"home");
            shell.OpenApp("momotalk");yield return Settle();var app=(PhonePreviewApp)host.CurrentApp;var chat=app.GetComponent<PhoneLiveMomotalk>();
            var contexts=new List<CharacterRuntimeContext>();CharacterRegistry.GetRegisteredContexts(contexts);chat.Select(contexts[0]);yield return Settle();
            Check(chat.input.textComponent.fontSize==16,"Chat input typography");yield return Capture(shell,"chat-live");
            // View-only review samples: never sent, saved to history, or registered with memory.
            chat.enabled=false;var hidden=new List<GameObject>();foreach(Transform child in chat.scroll.content){if(child.gameObject.activeSelf){hidden.Add(child.gameObject);child.gameObject.SetActive(false);}}
            var samples=new List<PhoneTextBubble>();var texts=new[]{"视觉预览 · 未发送 / VISUAL REVIEW","早上好，Toki。","Good morning, Teacher.\n今天想一起做些什么？","Let's take a short break. 中英文混排也要清楚，长消息应自然换行。","ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ","Reply unavailable\nTap for details"};
            for(int i=0;i<texts.Length;i++){var row=Object.Instantiate(i==1||i==3?chat.outgoingTemplate:chat.incomingTemplate,chat.scroll.content);row.gameObject.SetActive(true);row.body.text=texts[i];row.bubble.Find("Time").GetComponent<TMP_Text>().text="09:41";samples.Add(row);}
            var error=samples.Last();error.body.color=PhoneVisualTheme.Current.pink;error.bubble.GetComponent<Image>().raycastTarget=true;error.bubble.gameObject.AddComponent<PhoneMessageDetails>().Details="VISUAL REVIEW — controlled error, not a service result.\nHTTP 503: upstream unavailable.";
            yield return Settle();chat.scroll.verticalNormalizedPosition=1;yield return Capture(shell,"chat-sample");
            Check(samples.All(x=>x.bubble.rect.width<=chat.scroll.content.rect.width*.83f),"Mixed language and continuous string bubble bounds");
            error.bubble.GetComponent<PhoneMessageDetails>().OnPointerClick(new PointerEventData(EventSystem.current));yield return Settle();Check(app.transform.Find("MessageErrorDetails")!=null,"Error details opens");shell.Back();yield return Settle();Check(app.CurrentPage==1&&app.transform.Find("MessageErrorDetails")==null,"Back closes error details before chat");
            shell.Recent();yield return Settle();yield return Capture(shell,"recent-first-use");recents.DismissNotice();yield return Settle();yield return Capture(shell,"recent-chat");
            shell.Back();yield return Settle();Check(app.CurrentPage==1&&!app.Suspended,"Recent restores chat");
            foreach(var sample in samples)Object.Destroy(sample.gameObject);foreach(var child in hidden)child.SetActive(true);chat.enabled=true;yield return Settle();
            shell.OpenApp("settings");yield return Settle();var settings=(PhonePreviewApp)host.CurrentApp;settings.ShowPage(2);yield return Settle();shell.Recent();yield return Settle();
            var privateTask=host.RecentTasks.First(t=>t.Definition.AppId=="settings");Check(privateTask.PrivatePreview&&privateTask.Preview==null,"Settings no texture capture");yield return Capture(shell,"recent-private");
            shell.OpenApp("camera");yield return Settle();shell.OpenApp("debug");yield return Settle();shell.Recent();yield return Settle();yield return Capture(shell,"recent-apps");
            shell.OpenApp("momotalk");shell.Home();shell.OpenApp("camera");yield return Settle();shell.Recent();shell.Home();shell.OpenApp("momotalk");yield return Settle();
            Check(host.CurrentAppDefinition.AppId=="momotalk"&&!host.NavigationPending,"Latest rapid navigation wins");
            Check(shell.GetComponentsInChildren<CanvasGroup>().All(g=>g.alpha>.99f),"No translucent pages after interruptions");
            Check(shell.GetComponentsInChildren<RectTransform>().All(r=>r.name!="PhoneTransition"),"Temporary transition overlays released");
            shell.Home();bool inside=true;int seen=0;double until=Time.realtimeSinceStartupAsDouble+.4;
            while(Time.realtimeSinceStartupAsDouble<until)
            {
                yield return new WaitForEndOfFrame();var screen=shell.navigationBackground.transform.parent as RectTransform;
                foreach(var overlay in screen.GetComponentsInChildren<RectTransform>().Where(r=>r.name=="PhoneTransition"))
                {seen++;var bounds=PhoneTransitionCoordinator.Bounds(overlay,screen);inside&=bounds.xMin>=screen.rect.xMin-1&&bounds.xMax<=screen.rect.xMax+1&&bounds.yMin>=screen.rect.yMin-1&&bounds.yMax<=screen.rect.yMax+1;}
            }
            Check(seen>0&&inside,"Departure overlay stays inside physical screen throughout animation");
            shell.OpenApp("momotalk");yield return Settle();
            var beforeDraft=chat.input.text;shell.Recent();yield return Settle();host.DismissTask("momotalk");shell.OpenApp("momotalk");yield return Settle();chat.Select(contexts[0]);yield return Settle();Check(chat.input.text==beforeDraft,"Draft survives dismissal and visual transition");
            // Record actual rendered frames and timestamps; the exported video follows wall time.
            shell.Home();yield return Settle();shell.Collapse();yield return Settle();recording=true;shell.StartCoroutine(Record(shell));
            yield return new WaitForSecondsRealtime(.5f);shell.Open();yield return new WaitForSecondsRealtime(.8f);shell.OpenApp("momotalk");yield return new WaitForSecondsRealtime(1f);
            shell.Home();yield return new WaitForSecondsRealtime(.8f);shell.Recent();yield return new WaitForSecondsRealtime(.8f);recents.Open("momotalk");yield return new WaitForSecondsRealtime(.8f);shell.Recent();yield return new WaitForSecondsRealtime(.8f);
            var card=shell.GetComponentsInChildren<PhoneRecentTaskCard>().First();var origin=RectTransformUtility.WorldToScreenPoint(null,card.transform.position);var pointer=new PointerEventData(EventSystem.current){position=origin,pressPosition=origin,pointerId=77};card.OnPointerDown(pointer);card.OnBeginDrag(pointer);
            for(int i=1;i<=8;i++){pointer.position=origin+Vector2.up*(i*18);card.OnDrag(pointer);yield return new WaitForSecondsRealtime(.04f);}card.OnEndDrag(pointer);yield return new WaitForSecondsRealtime(.9f);shell.Home();yield return new WaitForSecondsRealtime(.6f);recording=false;yield return Settle();
            foreach(var task in host.RecentTasks.ToArray())host.DismissTask(task.Definition.AppId);shell.Recent();yield return Settle();yield return Capture(shell,"empty");Check(recents.CardCount==0,"Empty overview after animated dismissal");
            shell.OpenApp("momotalk");yield return Settle();chat.Select(contexts[0]);yield return Settle();shell.Recent();yield return Settle();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(2560,1440),new Vector2Int(3440,1440)})foreach(var height in new[]{.7f,.9f,.95f})
            {PhoneVisualReviewChecks.SetResolution(size.x,size.y);shell.SetHeight(height);yield return Settle();var corners=new Vector3[4];shell.device.GetWorldCorners(corners);Check(corners[0].x>=0&&corners[0].y>=0&&corners[2].y<=Screen.height+1,$"Bounds {size} {height}");yield return Capture(shell,$"recent-{size.x}-{Mathf.RoundToInt(height*100)}");}
            PhoneVisualReviewChecks.SetResolution(1920,1080);shell.SetHeight(.9f);shell.Home();yield return Settle();
        }
        private static IEnumerator Capture(PhonePresentationShell shell,string name)
        {yield return new WaitForEndOfFrame();var frame=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",frame.EncodeToPNG());var crop=Crop(shell,frame);File.WriteAllBytes(Output+"/"+name+"-phone.png",crop.EncodeToPNG());Object.Destroy(crop);Object.Destroy(frame);}
        private static Texture2D Crop(PhonePresentationShell shell,Texture2D frame)
        {var c=new Vector3[4];shell.device.GetWorldCorners(c);int x=Mathf.Clamp(Mathf.FloorToInt(c[0].x),0,frame.width-1),y=Mathf.Clamp(Mathf.FloorToInt(c[0].y),0,frame.height-1);int w=Mathf.Min(Mathf.CeilToInt(c[2].x)-x,frame.width-x),h=Mathf.Min(Mathf.CeilToInt(c[2].y)-y,frame.height-y);var crop=new Texture2D(w,h,TextureFormat.RGB24,false);crop.SetPixels(frame.GetPixels(x,y,w,h));crop.Apply();return crop;}
        private static IEnumerator Record(PhonePresentationShell shell)
        {
            var times=new List<string>();int index=0;double start=Time.realtimeSinceStartupAsDouble;
            while(recording){yield return new WaitForEndOfFrame();double stamp=Time.realtimeSinceStartupAsDouble-start;var frame=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/frames/"+index.ToString("D5")+".jpg",frame.EncodeToJPG(92));Object.Destroy(frame);times.Add(stamp.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));index++;}
            File.WriteAllLines(Output+"/frames/times.txt",times);
        }
    }
}
