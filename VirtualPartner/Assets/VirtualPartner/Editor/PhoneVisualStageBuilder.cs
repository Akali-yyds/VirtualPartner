using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using VirtualPartner.Runtime.PhoneOS;
using static VirtualPartner.EditorTools.PhoneVisualUI;
using Object=UnityEngine.Object;

namespace VirtualPartner.EditorTools
{
    public static partial class PhoneVisualStageBuilder
    {
        private static bool live;
        public static string Stage=>live?"Assets/VirtualPartner/UI/PhoneOS/LiveStage":"Assets/VirtualPartner/UI/PhoneOS/VisualStage";
        public static string ScenePath=>live?"Assets/Scenes/PhoneOS.unity":"Assets/Scenes/PhoneOS_VisualReview.unity";
        private const string Fonts="Assets/VirtualPartner/UI/PhoneOS/Fonts";
        private static Sprite avatar;
        private static readonly string[] AppIds={"momotalk","camera","settings","debug"};
        private static readonly string[] AppNames={"Momotalk","Camera","Settings","Debug"};
        private static readonly PhoneGlyphKind[] AppIcons={PhoneGlyphKind.Chat,PhoneGlyphKind.Camera,PhoneGlyphKind.Settings,PhoneGlyphKind.Debug};

        [MenuItem("VirtualPartner/Phone OS/Build Visual Review")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before rebuilding the visual scene.");
            if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene before building visual review.");
            Directory.CreateDirectory(Stage);AssetDatabase.Refresh();
            PrepareAssets();PhonePolishAssets.Prepare();
            var prefabs=new[]{BuildMomotalk(),BuildCamera(),BuildSettings(),live?BuildLiveDebug():BuildDebug()};
            var registry=AssetDatabase.LoadAssetAtPath<PhoneAppRegistry>(Stage+"/Registry.asset");
            if(registry==null){registry=ScriptableObject.CreateInstance<PhoneAppRegistry>();AssetDatabase.CreateAsset(registry,Stage+"/Registry.asset");}
            var definitions=new List<PhoneAppDefinition>();
            for(var i=0;i<AppIds.Length;i++)
            {
                var path=Stage+"/"+AppNames[i]+".asset";
                var definition=AssetDatabase.LoadAssetAtPath<PhoneAppDefinition>(path);
                if(definition==null){definition=ScriptableObject.CreateInstance<PhoneAppDefinition>();AssetDatabase.CreateAsset(definition,path);}
                var data=new SerializedObject(definition);
                data.FindProperty("appId").stringValue=AppIds[i];data.FindProperty("displayName").stringValue=AppNames[i];
                data.FindProperty("appPrefab").objectReferenceValue=prefabs[i];data.FindProperty("order").intValue=i;
                data.FindProperty("allowBackground").boolValue=true;data.FindProperty("showInDock").boolValue=true;data.ApplyModifiedPropertiesWithoutUndo();
                definitions.Add(definition);
            }
            var registryData=new SerializedObject(registry);var apps=registryData.FindProperty("apps");apps.arraySize=definitions.Count;
            for(var i=0;i<definitions.Count;i++)apps.GetArrayElementAtIndex(i).objectReferenceValue=definitions[i];registryData.ApplyModifiedPropertiesWithoutUndo();
            var root=BuildShell(registry);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Stage+"/PhoneVisualRoot.prefab");Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            // Build from the user's scene without modifying that scene or its existing prefab overrides.
            if(SceneManager.GetActiveScene().path!=ScenePath)
            {
                var source=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Single);
                EditorSceneManager.SaveScene(source,ScenePath,true);
            }
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            foreach(var item in scene.GetRootGameObjects())
            {
                if(item.name=="PhoneVisualRoot")Object.DestroyImmediate(item);
                else if(new[]{"PhoneRoot","MomotalkCanvas","SceneCameraControlCanvas"}.Contains(item.name))item.SetActive(false);
                // The bootstrap also advances idle, StagePlan and the autonomous FSM every frame.
                // Isolate preview service calls at the UI, never by stopping the character runtime.
                else if(item.name=="VirtualPartnerBootstrap")item.SetActive(true);
            }
            PrefabUtility.InstantiatePrefab(prefab,scene);
            if(live)
            {
                foreach(var panel in Object.FindObjectsByType<VirtualPartner.Runtime.VirtualPartnerRuntimeDebugPanel>(FindObjectsSortMode.None))panel.SetVisible(false);
            }
            EditorSceneManager.SaveScene(scene);
            if(live)
            {
                var existing=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Select(s=>new EditorBuildSettingsScene(s.path,s.path=="Assets/Scenes/SampleScene.unity"?false:s.enabled));
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(existing).ToArray();
            }
            Debug.Log("[PhoneOS] Visual review ready. Character runtime and FSM are enabled; phone service buttons remain preview-only.");
        }

        private static void PrepareAssets()
        {
            Regular=Font("NotoSans-Regular.ttf");LightFont=Font("NotoSans-Light.ttf");Semibold=Font("NotoSans-SemiBold.ttf");
            var chinese=Font("NotoSansSC-Regular.otf");
            foreach(var font in new[]{Regular,LightFont,Semibold}){font.fallbackFontAssetTable=new List<TMP_FontAsset>{chinese};EditorUtility.SetDirty(font);}
            var roundPath=Stage+"/Rounded.png";
            if(!File.Exists(roundPath))
            {
                var tex=new Texture2D(128,128,TextureFormat.RGBA32,false);
                for(var y=0;y<128;y++)for(var x=0;x<128;x++)
                {var q=new Vector2(Mathf.Max(Mathf.Abs(x-63.5f)-31.5f,0),Mathf.Max(Mathf.Abs(y-63.5f)-31.5f,0));tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(32-q.magnitude)));}
                tex.Apply();File.WriteAllBytes(roundPath,tex.EncodeToPNG());Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(roundPath);
                var importer=(TextureImporter)AssetImporter.GetAtPath(roundPath);importer.textureType=TextureImporterType.Sprite;importer.spriteBorder=new Vector4(34,34,34,34);importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            }
            var roundedImporter=(TextureImporter)AssetImporter.GetAtPath(roundPath);
            roundedImporter.spriteImportMode=SpriteImportMode.Single;
            roundedImporter.spriteBorder=new Vector4(34,34,34,34);
            roundedImporter.SaveAndReimport();
            Rounded=AssetDatabase.LoadAssetAtPath<Sprite>(roundPath);
            if(Rounded==null)throw new InvalidOperationException("Rounded UI texture did not import as a single sprite.");
            avatar=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Character/Toki/Avatar/Toki_Avater.png");
            if(avatar==null)throw new InvalidOperationException("Toki avatar must be imported as a sprite.");
        }
        private static TMP_FontAsset Font(string source)
        {
            var path=Fonts+"/"+Path.GetFileNameWithoutExtension(source)+" SDF.asset";
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(font!=null)return font;
            font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(Fonts+"/"+source),72,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            font.name=Path.GetFileNameWithoutExtension(source)+" SDF";font.isMultiAtlasTexturesEnabled=true;
            AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
            font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,!?;:'\"-—–…/\\()[]{}+%=<>°·_\n");
            EditorUtility.SetDirty(font);return font;
        }

        private static GameObject BuildShell(PhoneAppRegistry registry)
        {
            var root=Rect(null,"PhoneVisualRoot");var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            root.gameObject.AddComponent<CanvasScaler>();root.gameObject.AddComponent<GraphicRaycaster>();
            var shell=root.gameObject.AddComponent<PhonePresentationShell>();if(live)root.gameObject.AddComponent<PhoneLiveRuntime>();shell.liveMode=live;var host=root.gameObject.AddComponent<PhoneAppHost>();shell.host=host;
            var blocker=Rect(root,"OutsideClick");Fill(blocker);Image(blocker,Color.clear).raycastTarget=true;
            var blockerButton=blocker.gameObject.AddComponent<Button>();blockerButton.transition=Selectable.Transition.None;Click(blockerButton,shell.Collapse);shell.outsideBlocker=blocker.gameObject;
            var device=Box(root,"PhoneDevice",0,0,456,912);device.anchorMin=device.anchorMax=new Vector2(1,.5f);device.pivot=new Vector2(1,.5f);device.anchoredPosition=new Vector2(-24,0);shell.device=device;shell.deviceGroup=device.gameObject.AddComponent<CanvasGroup>();
            var style=AssetDatabase.LoadAssetAtPath<PhoneOSStyle>("Assets/VirtualPartner/UI/PhoneOS/Styles/PhoneOSStyle.asset");
            var shadow=Image(Box(device,"PhoneShadow",-8,-8,472,928),Color.white);shadow.sprite=style.PhoneShadowSprite;
            var frame=Image(Box(device,"PhoneFrame",0,0,456,912),Color.white);frame.sprite=style.PhoneFrameSprite;frame.raycastTarget=true;
            var screen=Panel(device,"Screen",24,64,408,794,Paper,false);screen.gameObject.AddComponent<RectMask2D>();screen.GetComponent<Image>().raycastTarget=true;
            shell.wallpaper=Image(Box(screen,"Wallpaper",0,0,408,794),new Color(1,1,1,.68f));
            shell.wallpapers=new[]{"pink","lavender","sunrise"}.Select(id=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/VirtualPartner/UI/PhoneOS/Sprites/phoneos_wallpaper_"+id+".png")).ToArray();shell.wallpaper.sprite=shell.wallpapers[0];
            var home=Rect(screen,"Home");Fill(home,0,live?30:52,0,44);BuildHome(home,shell);
            var appLayer=Rect(screen,"Apps");Fill(appLayer,0,live?30:52,0,44);
            var windows=Rect(appLayer,"Windows");Fill(windows);host.Configure(registry,home.gameObject,appLayer.gameObject,windows);appLayer.gameObject.SetActive(false);
            var status=Panel(screen,"StatusBar",0,0,408,30,new Color(1,1,1,.12f),false);shell.statusBackground=status.GetComponent<Image>();
            shell.statusTime=Text(status,"Time","09:41",20,2,175,26,12,null,true);
            Icon(status,PhoneGlyphKind.Signal,308,4,20);Icon(status,PhoneGlyphKind.Wifi,334,4,20);Icon(status,PhoneGlyphKind.Battery,362,3,25);
            var preview=Panel(screen,"PreviewRibbon",0,30,408,22,Pale,false);var p=Text(preview,"Label","VISUAL PREVIEW  ·  SAMPLE DATA",0,0,408,22,11,Hex("99536D"),true);p.alignment=TextAlignmentOptions.Center;preview.gameObject.SetActive(!live);
            var nav=Panel(screen,"Navigation",0,750,408,44,new Color(1,1,1,.12f),false);shell.navigationBackground=nav.GetComponent<Image>();
            Click(IconButton(nav,"Recent",PhoneGlyphKind.Recent,56,0),shell.Recent);
            Click(IconButton(nav,"Home",PhoneGlyphKind.Home,182,0),shell.Home);Click(IconButton(nav,"Back",PhoneGlyphKind.Back,308,0),shell.Back);
            var entry=IconButton(root,"OpenPhone",PhoneGlyphKind.Phone,0,0,60,Color.white);var er=(RectTransform)entry.transform;er.anchorMin=er.anchorMax=new Vector2(1,.5f);er.pivot=new Vector2(1,.5f);er.anchoredPosition=new Vector2(-24,0);Click(entry,shell.Open);shell.entryButton=entry.gameObject;
            shell.unreadDot=Panel(entry.transform,"Unread",43,3,12,12,Pink).gameObject;shell.unreadDot.SetActive(false);
            PhoneVisualPolish.ApplyShell(shell);
            return root.gameObject;
        }
        private static void BuildHome(RectTransform home,PhonePresentationShell shell)
        {
            var search=Panel(home,"Search",24,24,360,54,new Color(1,1,1,.85f));Icon(search,PhoneGlyphKind.Search,16,15,24,Muted);Text(search,"Hint","Search",54,0,160,54,16,Muted);Text(search,"Preview","Preview",261,0,80,54,14,Muted);
            shell.clockTime=Text(home,"Clock","09\n41",26,145,190,148,60,Ink);shell.clockTime.font=LightFont;shell.clockTime.lineSpacing=-10;shell.clockTime.alignment=TextAlignmentOptions.TopLeft;
            shell.clockDate=Text(home,"Date","Tue, Sep 8",28,291,190,24,13);
            var weather=Panel(home,"Weather",222,106,162,220,new Color(1,1,1,.92f));
            Icon(weather,PhoneGlyphKind.Sun,24,54,34,Hex("EDB447"));Text(weather,"Temperature","24°",70,46,84,52,36);
            Text(weather,"Condition","Weather preview",16,156,140,23,13).alignment=TextAlignmentOptions.Center;
            Text(weather,"Preview","Not connected",16,183,140,20,12,Muted).alignment=TextAlignmentOptions.Center;
            shell.secondaryTime=Text(home,"SecondaryClock","09:41",24,394,180,43,30);
            shell.secondaryDate=Text(home,"SecondaryDate","Tue, September 8",25,437,180,25,12);
            var clock=Panel(home,"AnalogClock",250,370,112,112,Color.white);clock.GetComponent<Image>().pixelsPerUnitMultiplier=.55f;
            shell.hourHand=Box(clock,"HourHand",54,28,4,30);Image(shell.hourHand,Ink);shell.hourHand.pivot=new Vector2(.5f,0);shell.hourHand.anchorMin=shell.hourHand.anchorMax=new Vector2(.5f,.5f);shell.hourHand.anchoredPosition=Vector2.zero;
            shell.minuteHand=Box(clock,"MinuteHand",55,15,2,43);Image(shell.minuteHand,Muted);shell.minuteHand.pivot=new Vector2(.5f,0);shell.minuteHand.anchorMin=shell.minuteHand.anchorMax=new Vector2(.5f,.5f);shell.minuteHand.anchoredPosition=Vector2.zero;
            Panel(clock,"Center",53,53,6,6,Ink);
            for(var i=0;i<4;i++)HomeIcon(home,shell,i,24+i*92,534,true);
            var dock=Panel(home,"Dock",24,626,360,68,Color.clear);shell.dock=dock.gameObject;
            for(var i=0;i<4;i++)HomeIcon(dock,shell,i,6+i*88,7,false);
        }
        private static void HomeIcon(Transform p,PhonePresentationShell shell,int i,float x,float y,bool label)
        {
            var colors=new[]{Pink,Hex("869CBD"),Hex("A198AC"),Hex("657887")};
            var b=Button(p,"Open_"+AppIds[i],"",x+16,y,52,52,colors[i]);Icon(b.transform,AppIcons[i],12,12,28,Color.white);
            UnityEventTools.AddStringPersistentListener(b.onClick,shell.OpenApp,AppIds[i]);
            if(label){var t=Text(p,AppNames[i],AppNames[i],x-4,y+57,92,22,13);t.alignment=TextAlignmentOptions.Center;}
        }
        private static PhonePreviewApp App(string id)
        {var r=Box(null,id,0,0,408,698);var app=r.gameObject.AddComponent<PhonePreviewApp>();app.appId=id;return app;}
        private static RectTransform Page(PhonePreviewApp app,string name)
        {var r=Rect(app.transform,name);Fill(r);Image(r,Paper).raycastTarget=true;return r;}
        private static void Header(RectTransform p,string title,string subtitle,PhonePreviewApp app,bool back=false)
        {
            var h=Panel(p,"Header",0,0,408,68,Paper,false);
            if(back)Click(IconButton(h,"Back",PhoneGlyphKind.Back,8,8),app.Back);
            Text(h,"Title",title,back?56:24,4,back?328:360,34,22,null,true);
            Text(h,"Subtitle",subtitle,back?58:26,38,back?324:356,24,12,Muted);
        }
        private static GameObject SaveApp(PhonePreviewApp app,params RectTransform[] pages)
        {
            app.pages=pages.Select(p=>p.gameObject).ToArray();
            if(app.parentPages==null||app.parentPages.Length!=pages.Length)app.parentPages=new int[pages.Length];
            for(var i=0;i<pages.Length;i++)pages[i].gameObject.SetActive(i==0);
            if(live)BindLiveApp(app);
            PhoneVisualPolish.ApplyApp(app);
            var prefab=PrefabUtility.SaveAsPrefabAsset(app.gameObject,Stage+"/"+app.appId+".prefab");Object.DestroyImmediate(app.gameObject);return prefab;
        }
    }
}
