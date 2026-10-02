using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VirtualPartner.Runtime.PhoneOS;
using Object=UnityEngine.Object;

namespace VirtualPartner.EditorTools
{
    /// <summary>Play-mode integration checks and real Game View captures for the visual acceptance gate.</summary>
    public static class PhoneVisualReviewChecks
    {
        public const string Output="Library/MCPForUnity/PhoneVisualReview";
        private static readonly List<string> passed=new List<string>(),failed=new List<string>();
        public static bool Running { get; private set; }
        public static string LastResult { get; private set; }
        private static bool originalBackground;
        [MenuItem("VirtualPartner/Phone OS/Run Visual Review Checks (Play Mode)")]
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play Mode in PhoneOS_VisualReview first.");
            if(Running)throw new InvalidOperationException("Checks are already running.");
            var shell=Object.FindFirstObjectByType<PhonePresentationShell>();
            if(shell==null)throw new InvalidOperationException("Visual review shell not found.");
            Directory.CreateDirectory(Output);passed.Clear();failed.Clear();Running=true;
            originalBackground=Application.runInBackground;Application.runInBackground=true;
            SetResolution(1920,1080);
            shell.StartCoroutine(Guarded(CheckRoutine(shell)));
        }
        private static IEnumerator Guarded(IEnumerator routine)
        {
            while(true)
            {
                object current;
                try {if(!routine.MoveNext())break;current=routine.Current;}
                catch(Exception e){failed.Add(e.ToString());break;}
                yield return current;
            }
            Running=false;
            Application.runInBackground=originalBackground;
            LastResult=$"{passed.Count} passed, {failed.Count} failed";
            File.WriteAllText(Output+"/checks.txt",LastResult+"\n\nPASS\n"+string.Join("\n",passed)+"\n\nFAIL\n"+string.Join("\n",failed));
            Debug.Log("[PhoneOS Review] "+LastResult);
        }
        private static void Check(bool condition,string name){(condition?passed:failed).Add(name);}
        private static IEnumerator CheckRoutine(PhonePresentationShell shell)
        {
            yield return new WaitForSecondsRealtime(.5f);
            Check(!shell.IsOpen,"Startup: phone is collapsed");
            Check(!shell.outsideBlocker.activeSelf,"Startup: scene is not blocked");
            var runtime=Object.FindFirstObjectByType<VirtualPartner.Runtime.VirtualPartnerStage1Bootstrap>();
            var scheduler=Object.FindFirstObjectByType<VirtualPartner.Runtime.AutonomousBehaviorScheduler>();
            Check(runtime!=null && runtime.isActiveAndEnabled,"Character runtime remains active during visual review");
            Check(scheduler!=null && scheduler.SchedulerActive,"Autonomous FSM starts alongside the phone preview");
            Check(AssetDatabase.LoadAssetAtPath<Sprite>(PhoneVisualStageBuilder.Stage+"/Rounded.png")!=null,"Rounded UI sprite imports correctly");
            var originalHeight=shell.HeightFraction;
            SetResolution(1920,1080);shell.Open();yield return new WaitForSecondsRealtime(.7f);
            shell.host.OpenApp("momotalk");yield return new WaitForSecondsRealtime(.3f);
            var app=(PhonePreviewApp)shell.host.CurrentApp;app.ShowPage(1);yield return new WaitForSecondsRealtime(.3f);
            var chat=app.GetComponentInChildren<PhoneChatPreview>(true);var draft=chat.input.text;
            Canvas.ForceUpdateCanvases();
            Check(chat.scroll.content.rect.height <= chat.scroll.viewport.rect.height,
                "Density: all five bilingual sample messages fit without scrolling");
            foreach(var sample in new[]{"好",new string('W',180),"中文长文本"+new string('中',160),"First line\n第二行\nThird line"})
            {
                var probe=Object.Instantiate(chat.outgoingTemplate,chat.scroll.content);
                probe.gameObject.SetActive(true);probe.body.text=sample;
                yield return null;yield return null;Canvas.ForceUpdateCanvases();
                Check(probe.body.preferredHeight<=probe.body.rectTransform.rect.height+2,
                    "Stress bubble: measured text fits for "+sample.Length+" characters");
                Check(probe.bubble.rect.width<=chat.scroll.viewport.rect.width-20,
                    "Stress bubble: text stays inside conversation width");
                Object.Destroy(probe.gameObject);
                yield return null;
            }
            Canvas.ForceUpdateCanvases();
            chat.input.text="测试 draft — unsent";chat.scroll.verticalNormalizedPosition=.6f;
            var position=chat.scroll.verticalNormalizedPosition;
            shell.Home();yield return new WaitForSecondsRealtime(.3f);shell.host.OpenApp("momotalk");yield return new WaitForSecondsRealtime(.3f);
            Check(ReferenceEquals(app,shell.host.CurrentApp),"App instances are cached across Home");
            Check(app.CurrentPage==1 && chat.input.text=="测试 draft — unsent","Conversation and input survive Home");
            Check(Mathf.Abs(chat.scroll.verticalNormalizedPosition-position)<.02f,"Scroll survives Home");
            shell.Collapse();yield return new WaitForSecondsRealtime(.3f);
            Check(!shell.deviceGroup.blocksRaycasts && !shell.outsideBlocker.activeSelf,"Collapsed phone releases input");
            shell.Open();yield return new WaitForSecondsRealtime(.3f);
            Check(ReferenceEquals(app,shell.host.CurrentApp)&&app.CurrentPage==1,"Collapsed phone restores current page");
            chat.input.text=draft;chat.scroll.verticalNormalizedPosition=1;
            var messageCount=chat.scroll.content.GetComponentsInChildren<PhoneTextBubble>().Length;
            chat.input.text="Line one\n第二行";
            Check(chat.input.onValidateInput(chat.input.text,0,'\n')=='\n',"Pasted multiline text keeps its newline");
            yield return null;
            Check(chat.scroll.content.GetComponentsInChildren<PhoneTextBubble>().Length==messageCount,"Pasting multiline text does not submit");
            chat.input.onSubmit.Invoke(chat.input.text);
            yield return new WaitForSecondsRealtime(.2f);
            Check(chat.scroll.content.GetComponentsInChildren<PhoneTextBubble>().Length==messageCount+1,"Submit event adds exactly one preview message");
            var sent=chat.scroll.content.GetChild(chat.scroll.content.childCount-1);
            if(sent.name=="PreviewMessage")Object.Destroy(sent.gameObject);
            chat.newMessagesButton.SetActive(false);
            chat.input.text=draft;chat.scroll.verticalNormalizedPosition=1;
            shell.host.OpenApp("debug");yield return new WaitForSecondsRealtime(.3f);
            var debug=(PhonePreviewApp)shell.host.CurrentApp;debug.ShowPage(11);debug.OpenApi();yield return new WaitForSecondsRealtime(.3f);
            var settings=(PhonePreviewApp)shell.host.CurrentApp;
            Check(settings.AppId=="settings"&&settings.CurrentPage==2,"Debug API link opens Settings configuration");
            shell.Back();yield return new WaitForSecondsRealtime(.1f);shell.Back();yield return new WaitForSecondsRealtime(.3f);
            Check(ReferenceEquals(debug,shell.host.CurrentApp)&&debug.CurrentPage==11,"Cross-app Back restores Debug detail");
            debug.ShowPage(2);debug.ShowPage(13);debug.OnBackPressed();
            Check(debug.CurrentPage==2,"Shared JSON editor returns to its actual source page");
            shell.host.OpenApp("camera");yield return new WaitForSecondsRealtime(.3f);
            var joystick=((MonoBehaviour)shell.host.CurrentApp).GetComponentInChildren<PhoneJoystick>();
            var center=RectTransformUtility.WorldToScreenPoint(null,joystick.transform.position);
            var evt=new PointerEventData(EventSystem.current){pointerId=17,button=PointerEventData.InputButton.Left,position=center+new Vector2(35,20)};
            joystick.OnPointerDown(evt);Check(joystick.Value.magnitude>.1f,"Joystick uses local pointer coordinates");
            evt.position=center+new Vector2(1000,1000);joystick.OnDrag(evt);Check(joystick.Value.magnitude<=1.001f,"Joystick clamps dragged pointer outside its bounds");
            shell.Collapse();Check(joystick.Value==Vector2.zero,"Collapsing phone releases joystick");shell.Open();
            foreach(var id in new[]{"momotalk","camera","settings","debug"})
            {
                shell.host.OpenApp(id);var inspected=(PhonePreviewApp)shell.host.CurrentApp;
                for(var page=0;page<inspected.pages.Length;page++)
                {
                    inspected.ShowPage(page);Canvas.ForceUpdateCanvases();
                    foreach(var text in inspected.pages[page].GetComponentsInChildren<TMP_Text>())
                    {
                        if(text.GetComponentInParent<TMP_InputField>()!=null||text.GetComponentInParent<PhoneTextBubble>()!=null||text.text.Length==0)continue;
                        var bounds=text.rectTransform.rect;if(bounds.width<=0||bounds.height<=0)continue;
                        Check(text.GetPreferredValues(text.text,bounds.width,10000).y<=bounds.height+3,$"{id}/{page}/{text.name}: label fits its allocated height");
                    }
                }
                inspected.ShowPage(0);
            }
            // Capture all four applications plus dense subpages at the baseline size.
            var ids=new[]{"home","momotalk","momotalk","settings","settings","camera","debug","debug","debug"};
            var pageIndices=new[]{0,0,1,1,2,0,0,11,13};
            var names=new[]{"home","contacts","chat","display","api","camera","debug","bone","json"};
            shell.SetHeight(.9f);
            for(var i=0;i<ids.Length;i++)
            {
                if(ids[i]=="home")shell.Home();else{shell.host.OpenApp(ids[i]);((PhonePreviewApp)shell.host.CurrentApp).ShowPage(pageIndices[i]);}
                yield return new WaitForSecondsRealtime(.45f);
                yield return new WaitForEndOfFrame();Capture(shell,"1920x1080-90-"+names[i]);
            }
            var sizes=new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3440,1440)};
            foreach(var size in sizes)
            {
                SetResolution(size.x,size.y);
                foreach(var height in new[]{.7f,.9f,.95f})
                {
                    shell.SetHeight(height);shell.host.OpenApp("momotalk");((PhonePreviewApp)shell.host.CurrentApp).ShowPage(1);
                    yield return new WaitForSecondsRealtime(.4f);
                    var corners=new Vector3[4];shell.device.GetWorldCorners(corners);
                    Check(corners[0].x>=0&&corners[0].y>=0&&corners[2].x<=Screen.width+1&&corners[2].y<=Screen.height+1,$"{size.x}x{size.y}/{height:P0}: device fits");
                    foreach(var bubble in ((MonoBehaviour)shell.host.CurrentApp).GetComponentsInChildren<PhoneTextBubble>())
                    {
                        Check(bubble.body.rectTransform.rect.width>60,$"{size.x}x{size.y}/{height:P0}: readable bubble width");
                        Check(bubble.body.preferredHeight<=bubble.body.rectTransform.rect.height+2,$"{size.x}x{size.y}/{height:P0}: bubble text fits height");
                    }
                    yield return new WaitForEndOfFrame();Capture(shell,$"{size.x}x{size.y}-{Mathf.RoundToInt(height*100)}-chat");
                }
            }
            SetResolution(1920,1080);shell.SetHeight(originalHeight);shell.Home();
            yield return new WaitForSecondsRealtime(.3f);shell.Collapse();
        }
        public static void SetResolution(int width,int height)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
            var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
            var type=group.GetType();var count=(int)type.GetMethod("GetTotalCount").Invoke(group,null);var index=-1;
            for(var i=0;i<count;i++)
            {var item=type.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});var it=item.GetType();if((int)it.GetProperty("width").GetValue(item)==width&&(int)it.GetProperty("height").GetValue(item)==height){index=i;break;}}
            if(index<0)
            {
                var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
                var size=Activator.CreateInstance(sizeType,new[]{Enum.Parse(kind,"FixedResolution"),(object)width,height,$"Phone review {width}x{height}"});
                type.GetMethod("AddCustomSize").Invoke(group,new[]{size});index=count;
            }
            var viewType=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,index);
            view.Show();view.Focus();view.Repaint();
        }
        private static void Capture(PhonePresentationShell shell,string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            var corners=new Vector3[4];shell.device.GetWorldCorners(corners);
            var x=Mathf.Clamp(Mathf.FloorToInt(corners[0].x)-12,0,texture.width-1);var y=Mathf.Clamp(Mathf.FloorToInt(corners[0].y)-12,0,texture.height-1);
            var width=Mathf.Min(Mathf.CeilToInt(corners[2].x)-x+12,texture.width-x);var height=Mathf.Min(Mathf.CeilToInt(corners[2].y)-y+12,texture.height-y);
            var crop=new Texture2D(width,height,TextureFormat.RGB24,false);crop.SetPixels(texture.GetPixels(x,y,width,height));crop.Apply();
            File.WriteAllBytes(Output+"/"+name+"-phone.png",crop.EncodeToPNG());Object.Destroy(texture);Object.Destroy(crop);
        }
    }
}
