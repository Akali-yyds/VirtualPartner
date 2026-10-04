using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualPartner.Runtime;
using VirtualPartner.Runtime.PhoneOS;
using Object=UnityEngine.Object;

namespace VirtualPartner.EditorTools
{
    public static class PhonePagesReviewChecks
    {
        public const string Output="Library/MCPForUnity/PhonePagesReview";
        public static bool Running {get;private set;}
        public static string Result {get;private set;}
        private static readonly List<string> results=new List<string>();
        private static bool recording;
        public static void Run()
        {
            var runtime=Object.FindFirstObjectByType<PhoneLiveRuntime>();
            if(!Application.isPlaying||runtime==null||!runtime.Ready)throw new InvalidOperationException("Live PhoneOS required.");
            if(Running)return;Running=true;results.Clear();Directory.CreateDirectory(Output);Directory.CreateDirectory(Output+"/frames");
            runtime.StartCoroutine(Guard(Routine(runtime),runtime));
        }
        private static IEnumerator Guard(IEnumerator routine,PhoneLiveRuntime runtime)
        {
            float height=runtime.Shell.HeightFraction;int width=Screen.width,screenHeight=Screen.height;
            while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){results.Add("FAIL "+e);break;}yield return next;}
            recording=false;runtime.Shell.SetHeight(height);PhoneVisualReviewChecks.SetResolution(width,screenHeight);
            Running=false;Result=$"{results.Count(x=>x.StartsWith("PASS"))} passed, {results.Count(x=>x.StartsWith("FAIL"))} failed";
            File.WriteAllText(Output+"/checks.txt",Result+"\n"+string.Join("\n",results));
        }
        private static void Check(bool condition,string label)=>results.Add((condition?"PASS ":"FAIL ")+label);
        private static IEnumerator Settle(){yield return null;yield return new WaitForSecondsRealtime(.4f);}
        private static IEnumerator Routine(PhoneLiveRuntime runtime)
        {
            var shell=runtime.Shell;var host=shell.host;PhoneVisualReviewChecks.SetResolution(1920,1080);shell.SetHeight(.9f);shell.Open();
            shell.OpenApp("settings");yield return Settle();var settingsApp=(PhonePreviewApp)host.CurrentApp;var settings=settingsApp.GetComponent<PhoneLiveSettings>();var presentation=settingsApp.GetComponent<PhoneSettingsPresentation>();
            settingsApp.ShowPage(2);yield return Settle();var advanced=settingsApp.GetComponentInChildren<PhoneDisclosure>(true);
            Check(!advanced.expanded&&!settings.endpoint.gameObject.activeInHierarchy,"Advanced initially folded");
            var original=settings.model.text;settings.model.text="unsaved-ui-check";advanced.Toggle();shell.Home();yield return Settle();shell.OpenApp("settings");yield return Settle();
            Check(advanced.expanded&&settings.model.text=="unsaved-ui-check","Home retains Advanced and draft");
            presentation.load.onClick.Invoke();yield return Settle();Check(settingsApp.HasModal&&settings.model.text=="unsaved-ui-check","Load current requires confirmation before replacing draft");
            yield return Capture(shell,"settings-confirm");shell.Back();Check(!settingsApp.HasModal&&settings.model.text=="unsaved-ui-check","Back cancels overwrite without discarding draft");
            presentation.reload.onClick.Invoke();Check(settingsApp.HasModal,"Reload file requires confirmation");shell.Back();
            host.DismissTask("settings");shell.OpenApp("settings");yield return Settle();
            Check(settingsApp.CurrentPage==0&&!advanced.expanded&&settings.model.text=="unsaved-ui-check","Task removal resets page/fold but keeps draft");settings.model.text=original;
            shell.OpenApp("debug");yield return Settle();var debugApp=(PhonePreviewApp)host.CurrentApp;var debug=debugApp.GetComponent<PhoneLiveDebug>();debugApp.ShowPage(2);debugApp.OpenApi();yield return Settle();
            PhoneModal.Show(settingsApp,"Review modal","Modal Back precedes linked return.");shell.Back();Check(host.CurrentApp==settingsApp&&!settingsApp.HasModal,"Modal Back precedes Debug-linked return");shell.Back();yield return Settle();Check(host.CurrentApp==debugApp&&debugApp.CurrentPage==2,"Next Back restores original Debug page");
            var order=debugApp.pages[0].GetComponentsInChildren<Button>(true).Where(b=>b.name!="Back").Select(b=>b.name).ToArray();
            Check(order.SequenceEqual(new[]{"Overview","LLM","Momotalk","TTS","ASR","Memory","API configuration","Character","FSM","StagePlan","Root","Bone","Expression / Mouth"}),"Debug grouped order preserves all 13 destinations");
            debug.OpenDocument("status");Check(debug.document.readOnly&&!debugApp.pages[13].GetComponentsInChildren<Button>(true).First(b=>b.name=="Paste").interactable,"Diagnostic document is read-only and Paste disabled");
            debugApp.ShowPage(3);debug.OpenDocument("stage");Check(!debug.document.readOnly&&debugApp.pages[13].GetComponentsInChildren<Button>(true).First(b=>b.name=="Paste").interactable,"StagePlan editor retains Paste");
            var mapping=new List<string>{"app\tpage\tcontrol\tcallback"};
            foreach(var id in new[]{"settings","camera","debug","momotalk"})
            {
                shell.OpenApp(id);yield return Settle();var app=(PhonePreviewApp)host.CurrentApp;
                for(int p=0;p<app.pages.Length;p++)foreach(var button in app.pages[p].GetComponentsInChildren<Button>(true))
                {for(int n=0;n<button.onClick.GetPersistentEventCount();n++)mapping.Add($"{id}\t{p}\t{button.name}\t{button.onClick.GetPersistentMethodName(n)}");}
            }
            mapping.Add("settings\t2\tLoad / Reload / Details\tPhoneSettingsPresentation.Start (runtime)");mapping.Add("all\tmodal\tConfirm / Cancel / Copy\tPhoneModal.Show (runtime)");mapping.Add("debug\t1-12\tFeedback details\tPhoneDebugFeedback.Start (runtime)");mapping.Add("momotalk\t0\tdynamic contacts\tPhoneLiveMomotalk.Select (runtime)");
            File.WriteAllLines(Output+"/control-mapping.tsv",mapping);
            var contexts=new List<CharacterRuntimeContext>();CharacterRegistry.GetRegisteredContexts(contexts);var chat=((PhonePreviewApp)host.CurrentApp).GetComponent<PhoneLiveMomotalk>();
            ((PhonePreviewApp)host.CurrentApp).ShowPage(0);chat.search.text="no-contact-ui-review-98341";yield return Settle();Check(chat.contacts.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="No matching contacts"),"Search no-results state");yield return Capture(shell,"contacts-no-results");chat.search.text="";
            if(contexts.Count>0){chat.Select(contexts[0]);((PhonePreviewApp)host.CurrentApp).ShowPage(2);yield return Settle();yield return Capture(shell,"contact-detail");}
            recording=true;shell.StartCoroutine(Record());
            foreach(var id in new[]{"settings","camera","debug","momotalk"})
            {
                shell.OpenApp(id);yield return Settle();var app=(PhonePreviewApp)host.CurrentApp;
                for(int p=0;p<app.pages.Length;p++)
                {
                    app.ShowPage(p);yield return Settle();yield return Capture(shell,id+"-"+p);
                    if(id=="settings"&&p==2){advanced.Toggle();yield return Settle();yield return Capture(shell,"settings-advanced");advanced.Collapse();}
                }
            }
            recording=false;yield return Settle();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3440,1440)})foreach(float height in new[]{.7f,.9f,.95f})
            {
                PhoneVisualReviewChecks.SetResolution(size.x,size.y);shell.SetHeight(height);yield return Settle();
                foreach(var id in new[]{"settings","camera","debug","momotalk"})
                {
                    shell.OpenApp(id);yield return Settle();var app=(PhonePreviewApp)host.CurrentApp;
                    for(int page=0;page<app.pages.Length;page++)
                    {
                        app.ShowPage(page);yield return new WaitForSecondsRealtime(.2f);Canvas.ForceUpdateCanvases();
                        var rect=(RectTransform)app.pages[page].transform;bool inside=true;
                        foreach(var control in app.pages[page].GetComponentsInChildren<Selectable>())
                        {if(control.GetComponentInParent<ScrollRect>()!=null)continue;var b=PhoneTransitionCoordinator.Bounds((RectTransform)control.transform,rect);inside&=b.xMin>=rect.rect.xMin-1&&b.xMax<=rect.rect.xMax+1&&b.yMin>=rect.rect.yMin-1&&b.yMax<=rect.rect.yMax+1;}
                        Check(inside,$"Fixed controls accessible: {size.x}x{size.y}/{height:F2}/{id}/{page}");
                    }
                    if(id=="camera"){var camera=app.GetComponent<PhoneLiveCamera>();Check(camera.pan.transform.position.y>camera.orbit.transform.position.y,"Pan above Rotate "+size+" "+height);yield return Settle();yield return Capture(shell,$"camera-{size.x}-{Mathf.RoundToInt(height*100)}");}
                }
            }
            PhoneVisualReviewChecks.SetResolution(1920,1080);shell.SetHeight(.9f);shell.Home();
        }
        private static IEnumerator Capture(PhonePresentationShell shell,string name)
        {yield return new WaitForEndOfFrame();var frame=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",frame.EncodeToPNG());Object.Destroy(frame);}
        private static IEnumerator Record()
        {
            var times=new List<string>();int index=0;double start=Time.realtimeSinceStartupAsDouble;
            while(recording){yield return new WaitForEndOfFrame();var frame=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/frames/"+(index++).ToString("D5")+".jpg",frame.EncodeToJPG(88));Object.Destroy(frame);times.Add((Time.realtimeSinceStartupAsDouble-start).ToString("F6",System.Globalization.CultureInfo.InvariantCulture));}
            File.WriteAllLines(Output+"/frames/times.txt",times);
        }
    }
}
