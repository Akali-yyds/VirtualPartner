#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VirtualPartner.Runtime;
using VirtualPartner.Runtime.Experiments;
using Object=UnityEngine.Object;

namespace VirtualPartner.Editor
{
    public sealed class MotionLabWindow : EditorWindow
    {
        public const string ScenePath="Assets/Scenes/MotionLab.unity";
        public static string ProjectRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        public static string ResultsRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/MotionLab"));
        string prompt="站着不走动，右手抬到脸旁，小幅向外挥两下，然后放下。";
        string sample="",status="Ready",device="cpu";
        int route,variant,seed=11;
        bool audioSync,withSpeech;
        Process job;
        bool jobPlayed;
        double submitTime;
        Vector2 scroll;
        [MenuItem("VirtualPartner/Motion Lab/Open experiment panel")]
        public static void Open()=>GetWindow<MotionLabWindow>("Motion Lab");
        void OnEnable(){EditorApplication.update+=Poll;}
        void OnDisable(){EditorApplication.update-=Poll;CancelJob();}
        void Poll()
        {
            if(job!=null&&!jobPlayed&&EditorApplication.isPlaying&&File.Exists(Path.Combine(sample,route==0?"semantic.json":"motion.json")))
            {
                jobPlayed=true;
                try{var driver=Object.FindFirstObjectByType<MotionLabDriver>();driver.Load(sample);driver.waitForAudio=audioSync;driver.Play(submitTime);status="Playing raw result; benchmark/postprocessing may still be running";}
                catch(Exception error){status=error.Message;}
            }
            if(job!=null&&job.HasExited)
            {
                int exit=job.ExitCode;job.Dispose();job=null;
                status=exit==0?"Generated; inspect result.json and replay":"Generation failed; inspect result.json";
                if(exit==0&&!jobPlayed&&EditorApplication.isPlaying)
                {
                    try{var driver=Object.FindFirstObjectByType<MotionLabDriver>();driver.Load(sample);driver.waitForAudio=audioSync;driver.Play(submitTime);}
                    catch(Exception error){status=error.Message;}
                }
            }
            Repaint();
        }
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Motion route experiments",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Independent scene. All results retained. Generation, execution and human quality review are separate outcomes.",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                if(GUILayout.Button("Create / open isolated scene"))RunGuarded(()=>{BuildScene();status="Scene ready. Enter Play Mode.";});
            prompt=EditorGUILayout.TextArea(prompt,GUILayout.Height(80));
            route=EditorGUILayout.Popup("Route",route,new[]{"Semantic goals (existing LLM config)","MoMask","DiP probe","Official calibration fixture"});
            seed=EditorGUILayout.IntField("Seed",seed);device=EditorGUILayout.TextField("Device",device);
            withSpeech=EditorGUILayout.Toggle("Run real TTS concurrently",withSpeech);
            audioSync=EditorGUILayout.Toggle("Wait for actual audio",audioSync);
            if(audioSync&&!withSpeech)EditorGUILayout.HelpBox("Audio synchronization requires a real speech request.",MessageType.Warning);
            using(new EditorGUI.DisabledScope(job!=null||!EditorApplication.isPlaying||audioSync&&!withSpeech))
                if(GUILayout.Button("Generate and play"))RunGuarded(Generate);
            if(job!=null&&GUILayout.Button("Cancel generation")){CancelJob();status="Cancelled; partial evidence retained";}
            sample=EditorGUILayout.TextField("Sample directory",sample);
            variant=EditorGUILayout.Popup("MoMask source variant",variant,new[]{"Raw generated positions","Official BVH fit","Official foot IK"});
            if(GUILayout.Button("Choose sample")){var path=EditorUtility.OpenFolderPanel("Choose result directory",ResultsRoot,"");if(path!="")sample=path;}
            var current=Object.FindFirstObjectByType<MotionLabDriver>();
            using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying||current==null))
            {
                if(GUILayout.Button("Replay saved sample"))RunGuarded(()=>{current.EndMeasuredRequest();current.Load(sample,new[]{"motion.json","converted.json","foot-ik.json"}[variant]);current.waitForAudio=audioSync;current.Play();StartSpeech(current);});
                if(GUILayout.Button("Interrupt body motion"))current.Stop();
                if(GUILayout.Button("Reset body and holds"))current.ResetPose();
                if(GUILayout.Button("Save actual Game View screenshot"))
                {Directory.CreateDirectory(sample);ScreenCapture.CaptureScreenshot(Path.Combine(sample,"frame-"+DateTime.UtcNow.ToString("HHmmssfff")+".png"));}
            }
            EditorGUILayout.LabelField(status,EditorStyles.wordWrappedLabel);
            if(current!=null)
            {
                EditorGUILayout.LabelField(current.Status,EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("First relevant movement",current.FirstMotionSeconds.ToString("F3")+" s");
                if(current.Motion!=null)EditorGUILayout.TextArea(current.Motion.Describe(),GUILayout.MinHeight(90));
            }
            if(GUILayout.Button("Open all results")){Directory.CreateDirectory(ResultsRoot);EditorUtility.RevealInFinder(ResultsRoot);}
            EditorGUILayout.EndScrollView();
        }
        void RunGuarded(Action action){try{action();}catch(Exception error){status=error.Message;}}
        void Generate()
        {
            string[] routes={"semantic","momask","dip","fixture"};
            sample=Path.Combine(ResultsRoot,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+routes[route]);Directory.CreateDirectory(sample);
            string root="F:/Project/MotionExperiments";
            string python=Path.Combine(root,route==2?"dip-env/Scripts/python.exe":"momask-env/Scripts/python.exe");
            string script=Path.Combine(ProjectRoot,"Tools/MotionLab/lab.py");
            string config=Path.GetFullPath(Path.Combine(Application.dataPath,"../UserSettings/VirtualPartnerLlmConfig.json"));
            var start=new ProcessStartInfo(python){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                Arguments=Q(script)+" --route "+routes[route]+" --output "+Q(sample)+" --config "+Q(config)+" --text "+Q(prompt)+" --seed "+seed+" --device "+Q(device)};
            start.EnvironmentVariables["HF_HOME"]="F:/Project/MotionExperiments/hf-cache";
            start.EnvironmentVariables["PYTHONUTF8"]="1";
            var driver=Object.FindFirstObjectByType<MotionLabDriver>();
            jobPlayed=false;submitTime=driver.BeginMeasuredRequest(audioSync);job=Process.Start(start);StartSpeech(driver);status="Generating "+routes[route]+"; cold worker startup included";
        }
        static string Q(string value)
        {
            // Windows argv quoting: double backslashes before quotes and before closing quote.
            var result=new System.Text.StringBuilder("\"");int slashes=0;
            foreach(char c in value)
            {if(c=='\\'){slashes++;continue;}if(c=='\"'){result.Append('\\',slashes*2+1);result.Append(c);}else{result.Append('\\',slashes);result.Append(c);}slashes=0;}
            result.Append('\\',slashes*2);return result.Append('"').ToString();
        }
        void CancelJob(){if(job==null)return;try{if(!job.HasExited)job.Kill();}finally{job.Dispose();job=null;}}
        void StartSpeech(MotionLabDriver driver)
        {
            if(!withSpeech)return;
            if(driver.Tts==null){status="Blocked: experiment TTS missing";return;}
            if(!driver.Tts.StartSpeech(new StagePlanActionDto{type="speech",text="这是一条动作实验语音，我正在尝试按照你的要求做动作。"},5,out var error))status=error;
        }
        public static string BuildScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            if(File.Exists(ScenePath)){EditorSceneManager.OpenScene(ScenePath);return ScenePath;}
            var source=Object.FindFirstObjectByType<VirtualPartnerStage1Bootstrap>();
            if(source==null)throw new InvalidOperationException("Open PhoneOS scene first; source bindings are read from its bootstrap.");
            if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Source scene has unsaved user edits; save it before scene creation.");
            var serialized=new SerializedObject(source);
            var original=(GameObject)serialized.FindProperty("characterRoot").objectReferenceValue;
            var originalRoot=(Transform)serialized.FindProperty("boneRoot").objectReferenceValue;
            var map=(BoneMapProfile)serialized.FindProperty("boneMapProfile").objectReferenceValue;
            var idle=(AnimationClip)serialized.FindProperty("idleClip").objectReferenceValue;
            var profile=(CharacterProfile)serialized.FindProperty("characterProfile").objectReferenceValue;
            var sourceTts=(TtsManager)serialized.FindProperty("ttsManager").objectReferenceValue??source.GetComponent<TtsManager>();
            var sourceMouth=(MouthTextureController)serialized.FindProperty("mouthTextureController").objectReferenceValue??source.GetComponent<MouthTextureController>();
            if(sourceMouth==null)throw new InvalidOperationException("Source mouth controller unavailable.");
            string rootPath=AnimationUtility.CalculateTransformPath(originalRoot,original.transform);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var character=Object.Instantiate(original);character.name="MotionLab_Toki";SceneManager.MoveGameObjectToScene(character,scene);
            foreach(var behavior in character.GetComponentsInChildren<MonoBehaviour>(true))behavior.enabled=false;
            foreach(var animator in character.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            var host=new GameObject("MotionLab");var driver=host.AddComponent<MotionLabDriver>();
            driver.character=character;driver.boneRoot=character.transform.Find(rootPath);driver.boneMap=map;driver.idle=idle;driver.characterProfile=profile;
            driver.rig=AssetDatabase.LoadAssetAtPath<SpatialRigProfile>("Assets/VirtualPartner/Resources/TokiSpatialRig.asset");
            var mouth=host.AddComponent<MouthTextureController>();EditorUtility.CopySerialized(sourceMouth,mouth);
            var mouthSource=new SerializedObject(sourceMouth);var renderer=(Renderer)mouthSource.FindProperty("mouthRenderer").objectReferenceValue;
            if(renderer!=null)
            {
                var mouthTarget=new SerializedObject(mouth);
                string rendererPath=AnimationUtility.CalculateTransformPath(renderer.transform,original.transform);
                mouthTarget.FindProperty("mouthRenderer").objectReferenceValue=character.transform.Find(rendererPath)?.GetComponent<Renderer>();mouthTarget.ApplyModifiedPropertiesWithoutUndo();
            }
            host.AddComponent<ExpressionActionExecutor>();host.AddComponent<SpeechMouthDriver>();host.AddComponent<AudioSource>();
            var tts=host.AddComponent<TtsManager>();if(sourceTts!=null)EditorUtility.CopySerialized(sourceTts,tts);
            var ttsBindings=new SerializedObject(tts);ttsBindings.FindProperty("speechMouthDriver").objectReferenceValue=host.GetComponent<SpeechMouthDriver>();ttsBindings.FindProperty("audioSource").objectReferenceValue=host.GetComponent<AudioSource>();ttsBindings.ApplyModifiedPropertiesWithoutUndo();
            var frame=character.transform.rotation*Quaternion.LookRotation(driver.rig.bodyForward,driver.rig.bodyUp);
            var camera=new GameObject("Experiment Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
            var target=character.transform.position+frame*new Vector3(-.3f,.4f,0);camera.transform.position=target+frame*new Vector3(0,.1f,2.6f);camera.transform.LookAt(target);camera.fieldOfView=32;camera.nearClipPlane=.01f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.18f);driver.viewer=camera;
            var light=new GameObject("Key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(40,-25,0);
            RenderSettings.ambientLight=Color.gray;
            EditorSceneManager.SaveScene(scene,ScenePath);
            var oldScene=source.gameObject.scene;EditorSceneManager.CloseScene(oldScene,true);
            return ScenePath;
        }
    }
}
#endif
