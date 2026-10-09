using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VirtualPartner.Runtime.Experiments
{
    [DefaultExecutionOrder(1000)]
    public sealed class MotionLabDriver : MonoBehaviour
    {
        public GameObject character;
        public Transform boneRoot;
        public BoneMapProfile boneMap;
        public SpatialRigProfile rig;
        public AnimationClip idle;
        public Camera viewer;
        public CharacterProfile characterProfile;
        public string Status {get;private set;}="Ready";
        public bool Playing {get;private set;}
        public float FirstMotionSeconds {get;private set;}=-1;
        public float PlaybackElapsed {get;private set;}
        public SpatialMotionRuntime Motion {get;private set;}
        public ActionCoordinator Coordinator {get;private set;}
        public Dictionary<string,Transform> Bones {get;private set;}
        public TtsManager Tts {get;private set;}
        public string OutputDirectory {get;private set;}
        public double AudioStartSeconds=>firstAudio;
        public double AudioFirstBytesSeconds=>firstBytes;
        public bool waitForAudio;
        AvatarPoseApplier applier;
        MotionLabRetargeter retarget;
        MotionLabClip clip;
        MotionLabSemantic semantic;
        int step;
        float idleTime,stepElapsed;
        double submitted,firstAudio=-1,firstBytes=-1;
        string ownershipId;
        readonly List<float> frameTimes=new List<float>();
        readonly List<string> diagnostics=new List<string>();
        readonly List<LineRenderer> lines=new List<LineRenderer>();
        Vector3[] before;
        Quaternion[] beforeRotations;
        bool initialized,audioGate;
        bool priorBackground;
        bool timingArmed;
        float interruptAfter=-1;
        float finalPositionError,finalOrientationError;
        int finalGeometrySamples;

        void Start()
        {
            priorBackground=Application.runInBackground;Application.runInBackground=true;
            applier=gameObject.AddComponent<AvatarPoseApplier>();applier.Configure(character,boneRoot);applier.CaptureBaseRotations();
            Coordinator=gameObject.AddComponent<ActionCoordinator>();Coordinator.Configure(applier);
            Bones=MotionLabSkeleton.Resolve(boneMap,boneRoot);
            Motion=gameObject.AddComponent<SpatialMotionRuntime>();Motion.profile=rig;Motion.Configure(boneMap,boneRoot,character.transform,Coordinator,applier);
            retarget=new MotionLabRetargeter(Bones,boneRoot,character.transform,rig,Coordinator);
            Tts=GetComponent<TtsManager>();
            if(Tts!=null)
            {
                var mouth=GetComponent<MouthTextureController>();mouth.Configure();
                var expression=GetComponent<ExpressionActionExecutor>();expression.Configure(characterProfile,mouth);
                var speech=GetComponent<SpeechMouthDriver>();speech.Configure(mouth,expression);
                Tts.Configure(characterProfile,speech,GetComponent<AudioSource>());
                Tts.SpeechPlaybackStarted+=OnAudio;
            }
            var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Sprites/Default");
            var material=new Material(shader);material.color=new Color(.14f,.7f,.85f);
            for(int i=1;i<22;i++)
            {
                var go=new GameObject("Source segment "+i);go.transform.SetParent(transform);var line=go.AddComponent<LineRenderer>();
                line.sharedMaterial=material;line.startWidth=line.endWidth=.007f;line.positionCount=2;line.enabled=false;lines.Add(line);
            }
            initialized=true;
        }
        public void Load(string directory,string sourceFile="motion.json")
        {
            if(!initialized)throw new InvalidOperationException("Enter Play Mode and wait for initialization.");
            Stop();OutputDirectory=directory;
            foreach(var line in lines)line.enabled=false;
            clip=null;semantic=null;
            if(File.Exists(Path.Combine(directory,"semantic.json")))
            {
                semantic=JsonUtility.FromJson<MotionLabSemantic>(File.ReadAllText(Path.Combine(directory,"semantic.json")));
                if(!string.IsNullOrEmpty(semantic.unsupported))throw new ArgumentException("Unsupported: "+semantic.unsupported);
                if(!semantic.reset&&(semantic.steps==null||semantic.steps.Length==0))throw new ArgumentException("No semantic steps.");
            }
            else {clip=JsonUtility.FromJson<MotionLabClip>(File.ReadAllText(Path.Combine(directory,sourceFile)));clip.Validate();}
            Status="Loaded: "+(clip?.route??"semantic")+"; wrist twist/physical balance require separate validation";
        }
        public void Play(double submittedAt=-1,bool interrupt=false)
        {
            if(clip==null&&semantic==null)throw new InvalidOperationException("Load a generated sample first.");
            ReleasePlayback();
            if(!timingArmed){submitted=submittedAt<0?Time.realtimeSinceStartupAsDouble:submittedAt;firstAudio=firstBytes=-1;}
            FirstMotionSeconds=-1;PlaybackElapsed=stepElapsed=0;step=0;diagnostics.Clear();frameTimes.Clear();
            finalPositionError=finalOrientationError=0;finalGeometrySamples=0;
            ownershipId="lab:"+Guid.NewGuid().ToString("N");interruptAfter=interrupt?1:-1;
            before=new Vector3[Bones.Count];beforeRotations=new Quaternion[Bones.Count];int n=0;foreach(var b in Bones.Values){before[n]=b.localPosition;beforeRotations[n++]=b.localRotation;}
            if(semantic?.reset==true){Motion.ResetPose();Status="Reset";SaveResult();return;}
            if(clip!=null)retarget.Begin(clip);
            audioGate=!waitForAudio||firstAudio>=0;Playing=true;Status=audioGate?"Playing":"Waiting for real audio";
            if(audioGate&&semantic!=null)BeginStep();
        }
        void OnAudio()
        {
            if((!Playing&&!timingArmed)||Tts==null||Tts.MockTtsEnabled&&Tts.CurrentProvider=="MockTTS"||!string.IsNullOrEmpty(Tts.LatestError)||!Tts.AudioSourcePlaying)return;
            firstAudio=Time.realtimeSinceStartupAsDouble-submitted;
            if(Playing&&!audioGate){audioGate=true;if(semantic!=null)BeginStep();}
        }
        public double BeginMeasuredRequest(bool synchronize)
        {submitted=Time.realtimeSinceStartupAsDouble;firstAudio=firstBytes=-1;FirstMotionSeconds=-1;timingArmed=true;waitForAudio=synchronize;return submitted;}
        public void EndMeasuredRequest(){SaveResult();timingArmed=false;}
        void BeginStep()
        {
            try
            {
                var original=semantic.steps[step];
                var goals=new List<MotionLabGoal>();
                foreach(var goal in original.goals)
                    if(goal.target=="neutral" || (goal.target=="down"&&(goal.part=="leftArm"||goal.part=="rightArm")))
                    {
                        if(!SpatialMotionContract.IsGroup(goal.part))throw new ArgumentException("Unknown neutral group");
                        Motion.ResetPose(goal.part);
                    }
                    else goals.Add(goal);
                if(goals.Count==0)return;
                var grounded=new MotionLabStep{duration=original.duration,completion=original.completion,holdSeconds=original.holdSeconds,goals=goals.ToArray()};
                var action=MotionLabSemanticPlanner.Build(grounded,Bones,character.transform,rig,viewer.transform);
                File.WriteAllText(Path.Combine(OutputDirectory,"resolved-step-"+step+".json"),JsonUtility.ToJson(action,true));
                bool accepted=Motion.TryStart(action,ownershipId+":"+step,out var error);
                if(!string.IsNullOrEmpty(error))diagnostics.Add("solver: "+error);
                if(!accepted){Status="Rejected: "+error;Playing=false;SaveResult();}
            }
            catch(Exception error){Status=error.Message;diagnostics.Add(error.Message);Playing=false;SaveResult();}
        }
        void Update()
        {
            if(!initialized)return;
            idleTime+=Time.deltaTime;applier.ApplyIdle(idle,idle==null?0:Mathf.Repeat(idleTime,idle.length));
            Tts?.ManualUpdate(Time.deltaTime);
            if(timingArmed&&Tts!=null&&Tts.StreamingReceivedBytes>0&&firstBytes<0)firstBytes=Time.realtimeSinceStartupAsDouble-submitted;
            if(Playing)
            {
                frameTimes.Add(Time.unscaledDeltaTime*1000);
                if(Tts!=null&&Tts.StreamingReceivedBytes>0&&firstBytes<0)firstBytes=Time.realtimeSinceStartupAsDouble-submitted;
                if(!audioGate&&((Tts!=null&&!string.IsNullOrEmpty(Tts.LatestError))||Time.realtimeSinceStartupAsDouble-submitted>90))
                {Status="Blocked: no real audio for synchronized playback";diagnostics.Add(Status);Playing=false;SaveResult();}
                if(Playing&&audioGate)
                {
                    PlaybackElapsed+=Time.deltaTime;stepElapsed+=Time.deltaTime;
                    if(clip!=null)
                    {
                        if(!retarget.Apply(clip,PlaybackElapsed,ownershipId,out var error)){Status=error;diagnostics.Add(error);ReleasePlayback();SaveResult();}
                        else DrawSource(retarget.LastSource);
                    }
                    if(interruptAfter>0&&PlaybackElapsed>=interruptAfter){Status="Interrupted at injected stop";ReleasePlayback();SaveResult();}
                    else if(clip!=null&&PlaybackElapsed>=(clip.frames.Length-1)/clip.fps){Status="Playback complete; quality unreviewed";ReleasePlayback();SaveResult();}
                    else if(semantic!=null&&stepElapsed>=StepPlaybackSeconds(semantic.steps[step])+.05f)
                    {step++;stepElapsed=0;if(step<semantic.steps.Length)BeginStep();else{Playing=false;Status="Semantic execution ended; inspect holds/diagnostics";SaveResult();}}
                }
            }
            Motion.Tick(Time.deltaTime);Coordinator.FinalizeFrame(Time.deltaTime);
            if(Playing&&audioGate)
            {
                if(semantic!=null)
                {
                    int measured=Motion.MeasureFinalGeometry(out var posError,out var rotError);
                    finalGeometrySamples+=measured;finalPositionError=Mathf.Max(finalPositionError,posError);finalOrientationError=Mathf.Max(finalOrientationError,rotError);
                    for(int s=0;s<=step&&s<semantic.steps.Length;s++){var failure=Motion.Failure(ownershipId+":"+s);if(!string.IsNullOrEmpty(failure)&&!diagnostics.Contains(failure))diagnostics.Add(failure);}
                }
                if(clip!=null&&PlaybackElapsed>=.25f)retarget.Measure();
                int n=0;bool relatedMovement=false;foreach(var b in Bones.Values)
                {if(Coordinator.GetOwner(b)==BoneOwner.SpatialMotion&&(Vector3.Distance(before[n],b.localPosition)>.002f||Quaternion.Angle(beforeRotations[n],b.localRotation)>.5f))relatedMovement=true;n++;}
                if(FirstMotionSeconds<0&&relatedMovement)FirstMotionSeconds=(float)(Time.realtimeSinceStartupAsDouble-submitted);
            }
        }
        void DrawSource(Vector3[] points)
        {
            if(points==null)return;
            var orientation=character.transform.rotation*Quaternion.LookRotation(rig.bodyForward,rig.bodyUp);
            float scale=(Bones["Head"].position-Bones["Foot:L"].position).magnitude/Mathf.Max(.1f,(points[15]-points[7]).magnitude);
            var offset=character.transform.position+orientation*new Vector3(-.75f,0,0);
            for(int i=1;i<22;i++){lines[i-1].enabled=true;lines[i-1].SetPosition(0,offset+orientation*(points[MotionLabSkeleton.Parents[i]]*scale));lines[i-1].SetPosition(1,offset+orientation*(points[i]*scale));}
        }
        public static float StepPlaybackSeconds(MotionLabStep value)=>value.duration+(value.completion=="timed"?Mathf.Max(0,value.holdSeconds):0);
        void ReleasePlayback(){Playing=false;if(Coordinator!=null&&!string.IsNullOrEmpty(ownershipId))Coordinator.ReleaseSpatial(ownershipId,.35f);Motion?.CancelMoving();}
        public void Stop(){ReleasePlayback();Status="Stopped; established semantic holds retained";}
        public void ResetPose(){Stop();Motion?.ResetPose();Status="Reset to Idle";}
        void SaveResult()
        {
            if(string.IsNullOrEmpty(OutputDirectory))return;
            frameTimes.Sort();
            var report=new PlaybackReport{status=Status,firstRelatedMotion=FirstMotionSeconds,firstAudio=firstAudio,firstBytes=firstBytes,
                frameCount=frameTimes.Count,frameMsP50=Percentile(.5f),frameMsP95=Percentile(.95f),diagnostics=diagnostics.ToArray(),
                directionError=clip==null?-1:retarget.MaxDirectionError,boneLengthChange=clip==null?-1:retarget.MaxBoneLengthChange,
                footTravelRelative=clip==null?-1:retarget.MaxFootTravel,geometry=Motion.GeometryReport,sync=waitForAudio?"audio-first":"action-first"};
            report.sourceFootTravelRelative=clip==null?-1:retarget.MaxSourceFootTravel;report.torsoIntersectionSamples=clip==null?-1:retarget.TorsoIntersectionSamples;
            report.finalGeometrySamples=finalGeometrySamples;report.finalPositionErrorRelative=finalPositionError;report.finalOrientationErrorDegrees=finalOrientationError;
            File.WriteAllText(Path.Combine(OutputDirectory,"playback-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json"),JsonUtility.ToJson(report,true));
        }
        float Percentile(float p)=>frameTimes.Count==0?0:frameTimes[Mathf.Min(frameTimes.Count-1,Mathf.FloorToInt((frameTimes.Count-1)*p))];
        void OnDisable(){if(Tts!=null){Tts.SpeechPlaybackStarted-=OnAudio;Tts.StopSpeech();}ResetPose();if(initialized)Application.runInBackground=priorBackground;if(lines.Count>0&&lines[0]!=null)Destroy(lines[0].sharedMaterial);}
        [Serializable] sealed class PlaybackReport
        {public string status,sync,geometry;public float firstRelatedMotion,frameMsP50,frameMsP95,directionError,boneLengthChange,footTravelRelative,sourceFootTravelRelative,finalPositionErrorRelative,finalOrientationErrorDegrees;public double firstAudio,firstBytes;public int frameCount,torsoIntersectionSamples,finalGeometrySamples;public string[] diagnostics;}
    }
}
