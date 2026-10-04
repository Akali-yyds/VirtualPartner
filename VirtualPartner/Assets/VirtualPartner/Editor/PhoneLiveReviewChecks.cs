using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using VirtualPartner.Runtime;
using VirtualPartner.Runtime.PhoneOS;
using Object=UnityEngine.Object;
namespace VirtualPartner.EditorTools
{
    public static class PhoneLiveReviewChecks
    {
        public static bool Running {get;private set;}
        public static string Result {get;private set;}
        public const string Output="Library/MCPForUnity/PhoneLiveReview";
        private static readonly List<string> passed=new List<string>(),failed=new List<string>();
        [MenuItem("VirtualPartner/Phone OS/Run Live UI Checks (Play Mode)")]
        public static void Run()
        {
            var runtime=Object.FindFirstObjectByType<PhoneLiveRuntime>();if(!Application.isPlaying||runtime==null||!runtime.Ready)throw new InvalidOperationException("Play the live PhoneOS scene first.");
            if(Running)return;Running=true;passed.Clear();failed.Clear();Directory.CreateDirectory(Output);Application.runInBackground=true;
            PhoneVisualReviewChecks.SetResolution(1920,1080);runtime.StartCoroutine(Guard(CheckRoutine(runtime)));
        }
        private static IEnumerator Guard(IEnumerator routine)
        {
            while(true){object item;try{if(!routine.MoveNext())break;item=routine.Current;}catch(Exception e){failed.Add(e.ToString());break;}yield return item;}
            Running=false;Result=$"{passed.Count} passed, {failed.Count} failed";File.WriteAllText(Output+"/checks.txt",Result+"\nPASS\n"+string.Join("\n",passed)+"\nFAIL\n"+string.Join("\n",failed));
        }
        private static void Check(bool ok,string label)=>(ok?passed:failed).Add(label);
        private static IEnumerator CheckRoutine(PhoneLiveRuntime runtime)
        {
            yield return new WaitForSecondsRealtime(.6f);
            var shell=runtime.Shell;
            Check(Object.FindObjectsByType<MomotalkConversationController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Exactly one conversation controller");
            Check(runtime.Conversation.ExternalPresentation,"Conversation service has no legacy view binding");
            Check(Object.FindFirstObjectByType<AutonomousBehaviorScheduler>().SchedulerActive,"FSM active");
            var legacy=JsonUtility.FromJson<PhoneSettingsData>("{\"wallpaperId\":\"pink\",\"use24HourTime\":false,\"showDock\":false}");legacy.Normalize();
            Check(!legacy.use24HourTime&&!legacy.showDock&&Mathf.Approximately(legacy.heightFraction,.9f)&&!legacy.autoSendVoice,"Old display settings upgrade without losing preferences");
            shell.OpenApp("momotalk");yield return new WaitForSecondsRealtime(.4f);
            var app=(PhonePreviewApp)shell.host.CurrentApp;var chat=app.GetComponent<PhoneLiveMomotalk>();var contexts=new List<CharacterRuntimeContext>();CharacterRegistry.GetRegisteredContexts(contexts);chat.Select(contexts[0]);yield return new WaitForSecondsRealtime(.3f);
            Check(chat.contacts.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length==contexts.Count,"Contacts come from character registry");
            Check(!app.GetComponentsInChildren<PhoneChatPreview>(true).Any(),"No simulated-send component in live Momotalk");
            Check(chat.scroll.content.GetComponentsInChildren<PhoneTextBubble>().Length>=runtime.Conversation.ReadMessages(chat.CharacterId).Count,"Existing history rendered");
            var draft=chat.input.text;chat.input.text="Integration draft / 中文\nsecond line";
            shell.Home();yield return new WaitForSecondsRealtime(.3f);shell.OpenApp("momotalk");yield return new WaitForSecondsRealtime(.3f);
            Check(ReferenceEquals(app,shell.host.CurrentApp)&&app.CurrentPage==1&&chat.input.text.Contains("中文"),"Chat page and unsent draft survive Home");
            shell.Collapse();yield return new WaitForSecondsRealtime(.3f);shell.Open();yield return new WaitForSecondsRealtime(.3f);
            Check(chat.input.text.Contains("中文"),"Draft survives collapse");chat.input.text=draft;
            shell.OpenApp("settings");yield return new WaitForSecondsRealtime(.3f);var settingsApp=(PhonePreviewApp)shell.host.CurrentApp;settingsApp.ShowPage(2);yield return null;var settings=settingsApp.GetComponent<PhoneLiveSettings>();
            Check(settings.key.contentType==TMP_InputField.ContentType.Password,"API key masked");
            var original=settings.model.text;settings.model.text="unsaved-review-draft";shell.Home();yield return new WaitForSecondsRealtime(.3f);shell.OpenApp("settings");yield return new WaitForSecondsRealtime(.3f);
            Check(settings.model.text=="unsaved-review-draft"&&runtime.Relay.CreateConfigDraft().model==original,"Unsaved API edit survives navigation without applying");settings.model.text=original;
            var timeout=settings.timeout.text;settings.timeout.text="not a number";settings.Save();Check(settings.feedback.text.Contains("positive timeout"),"Invalid timeout rejected before file write");settings.timeout.text=timeout;
            shell.OpenApp("camera");yield return new WaitForSecondsRealtime(.3f);var cameraApp=(PhonePreviewApp)shell.host.CurrentApp;var ui=cameraApp.GetComponent<PhoneLiveCamera>();var camera=Object.FindFirstObjectByType<VirtualSceneCameraController>();
            var initial=camera.FocusTarget.position;var center=RectTransformUtility.WorldToScreenPoint(null,ui.pan.transform.position);var pointer=new PointerEventData(EventSystem.current){pointerId=47,button=PointerEventData.InputButton.Left,position=center+new Vector2(30,0)};
            ui.pan.OnPointerDown(pointer);yield return new WaitForSecondsRealtime(.35f);Check(Vector3.Distance(initial,camera.FocusTarget.position)>.05f,"Held joystick continuously moves real camera");
            shell.Collapse();var stopped=camera.FocusTarget.position;yield return new WaitForSecondsRealtime(.3f);Check(ui.pan.Value==Vector2.zero&&Vector3.Distance(stopped,camera.FocusTarget.position)<.001f,"Collapse stops camera immediately");
            shell.Open();ui.zoom.value=camera.MaxRadius;Check(Mathf.Approximately(camera.Radius,camera.MaxRadius),"Zoom slider reaches actual controller boundary");ui.ResetView();Check(Vector3.Distance(initial,camera.FocusTarget.position)<.001f,"Reset restores scene focus position");
            shell.OpenApp("debug");yield return new WaitForSecondsRealtime(.4f);var debugApp=(PhonePreviewApp)shell.host.CurrentApp;var debug=debugApp.GetComponent<PhoneLiveDebug>();var bone=Object.FindFirstObjectByType<VirtualPartnerBoneDebugPanel>();
            Check(debug.boneRows.childCount==bone.Bones.Count&&bone.Bones.Count>0,"All real bones exposed");
            debugApp.ShowPage(11);bone.PinSelected();var pins=bone.PinnedCount;shell.Home();yield return new WaitForSecondsRealtime(.3f);
            Check(pins>0&&bone.PinnedCount==pins,"Pinned bone effect survives leaving Debug");bone.UnpinSelected();
            shell.OpenApp("debug");yield return new WaitForSecondsRealtime(.3f);debugApp.ShowPage(2);debugApp.OpenApi();yield return new WaitForSecondsRealtime(.3f);shell.Back();yield return new WaitForSecondsRealtime(.3f);
            Check(ReferenceEquals(debugApp,shell.host.CurrentApp)&&debugApp.CurrentPage==2,"Settings API Back restores Debug source");
            var originalJson=Object.FindFirstObjectByType<StagePlanDebugPanel>().Json;debugApp.ShowPage(3);debug.OpenDocument("stage");debug.document.text="{ invalid";shell.Back();debug.Execute("Validate");
            Check(Object.FindFirstObjectByType<StagePlanDebugPanel>().Result.StartsWith("Invalid"),"JSON editor reaches existing validator");Object.FindFirstObjectByType<StagePlanDebugPanel>().Json=originalJson;
            // Isolated character history exercises failure/clear semantics without touching Toki data.
            var testObject=new GameObject("Phone history test");var service=testObject.AddComponent<MomotalkConversationController>();service.UseExternalPresentation(_=>false);
            var profile=ScriptableObject.CreateInstance<CharacterProfile>();var serialized=new SerializedObject(profile);serialized.FindProperty("characterId").stringValue="phone-ui-test-"+Guid.NewGuid().ToString("N");serialized.ApplyModifiedPropertiesWithoutUndo();
            var context=new CharacterRuntimeContext(profile,null,null,null,null,null,null,null,null,null,null,null);
            service.SendMessage(context,"Controlled failure");var records=service.ReadMessages(context.CharacterId);
            Check(records.Count==2&&records[1].sender=="system"&&records[1].status=="error","Missing relay persists an explicit failure without a fake reply");
            service.ClearConversation(context);Check(service.ReadMessages(context.CharacterId).Count==0,"Clear history uses existing history store");Object.Destroy(testObject);Object.Destroy(profile);
            foreach(var id in new[]{"momotalk","settings","camera","debug"})
            {
                shell.OpenApp(id);yield return new WaitForSecondsRealtime(.3f);var checkedApp=(PhonePreviewApp)shell.host.CurrentApp;
                foreach(var button in checkedApp.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    for(var listener=0;listener<button.onClick.GetPersistentEventCount();listener++)
                        Check(button.onClick.GetPersistentMethodName(listener)!="Preview",id+"/"+button.name+": no preview service action remains");
                for(var page=0;page<checkedApp.pages.Length;page++)
                {
                    checkedApp.ShowPage(page);yield return null;Canvas.ForceUpdateCanvases();
                    foreach(var label in checkedApp.pages[page].GetComponentsInChildren<TMP_Text>())
                    {
                        if(label.GetComponentInParent<TMP_InputField>()!=null||label.GetComponentInParent<PhoneTextBubble>()!=null||label.overflowMode==TextOverflowModes.Ellipsis)continue;
                        var bounds=label.rectTransform.rect;if(bounds.width>0&&bounds.height>0)Check(label.GetPreferredValues(label.text,bounds.width,10000).y<=bounds.height+3,id+"/"+page+"/"+label.name+": label fits");
                    }
                }
            }
            var ids=new[]{"home","momotalk","settings","camera","debug","debug"};var pages=new[]{0,1,1,0,0,11};var names=new[]{"home","chat","display","camera","debug","bone"};
            for(var i=0;i<ids.Length;i++){if(ids[i]=="home")shell.Home();else{shell.OpenApp(ids[i]);yield return new WaitForSecondsRealtime(.3f);((PhonePreviewApp)shell.host.CurrentApp).ShowPage(pages[i]);}yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Capture(shell,names[i]);}
            shell.Home();yield return new WaitForSecondsRealtime(.3f);shell.Collapse();
        }
        private static void Capture(PhonePresentationShell shell,string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            var corners=new Vector3[4];shell.device.GetWorldCorners(corners);var x=Mathf.Max(0,(int)corners[0].x-10);var y=Mathf.Max(0,(int)corners[0].y-10);var width=Mathf.Min(texture.width-x,(int)(corners[2].x-corners[0].x)+20);var height=Mathf.Min(texture.height-y,(int)(corners[2].y-corners[0].y)+20);
            var crop=new Texture2D(width,height);crop.SetPixels(texture.GetPixels(x,y,width,height));crop.Apply();File.WriteAllBytes(Output+"/"+name+"-phone.png",crop.EncodeToPNG());Object.Destroy(texture);Object.Destroy(crop);
        }
    }
}
