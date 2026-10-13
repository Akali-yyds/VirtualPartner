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
        readonly List<float> adaptationTimes=new List<float>();
        readonly System.Diagnostics.Stopwatch adaptationWatch=new System.Diagnostics.Stopwatch();
        readonly List<string> diagnostics=new List<string>();
        readonly List<LineRenderer> lines=new List<LineRenderer>();
        readonly List<Material> sourceMaterials=new List<Material>();
        readonly List<LineRenderer> referenceLines=new List<LineRenderer>();
        readonly List<TextMesh> referenceLabels=new List<TextMesh>();
        public bool SeparateSourceInView;
        Vector3[] before;
        Quaternion[] beforeRotations;
        bool initialized,audioGate;
        bool priorBackground;
        bool timingArmed;
        Transform[] idleBones;
        Quaternion[] idleRotations;
        Vector3[] idlePositions;
        float interruptAfter=-1;
        public bool UseAdaptation,Continuous,Paused;
        public bool FoundationMode,LatestCalibration;
        public MotionLabClip CurrentClip=>clip;
        string inputDirectory;
        public string InputDirectory=>inputDirectory;
        public float PlaybackSpeed=1;
        public MotionSegment CurrentSegment=new MotionSegment();
        public string Holds=>retarget?.DescribeHolds()??"";
        public MotionFrameDiagnostic AdaptationDiagnostic=>retarget?.Diagnostic;
        public float DirectionError=>retarget?.MaxDirectionError??0;
        public float BoneLengthChange=>retarget?.MaxBoneLengthChange??0;
        public Quaternion InspectionBodyFrame=>retarget.BindingBodyFrame;
        public float Duration=>clip==null?0:clip.frames.Length/clip.fps;
        public bool SegmentFinished {get;private set;}
        public bool HasHolds=>retarget!=null&&retarget.HasHolds;
        public bool CaptureDiagnostics=true;
        readonly List<MotionFrameDiagnostic> adaptationFrames=new List<MotionFrameDiagnostic>();
        int lastDiagnosticFrame=-1;
        float finalPositionError,finalOrientationError;
        int finalGeometrySamples;

        void Start()
        {
            priorBackground=Application.runInBackground;Application.runInBackground=true;
            applier=GetComponent<AvatarPoseApplier>()??gameObject.AddComponent<AvatarPoseApplier>();applier.Configure(character,boneRoot);applier.CaptureBaseRotations();
            idleBones=boneRoot.GetComponentsInChildren<Transform>(true);
            idleRotations=new Quaternion[idleBones.Length];idlePositions=new Vector3[idleBones.Length];
            for(int i=0;i<idleBones.Length;i++){idleRotations[i]=idleBones[i].localRotation;idlePositions[i]=idleBones[i].localPosition;}
            Coordinator=GetComponent<ActionCoordinator>()??gameObject.AddComponent<ActionCoordinator>();Coordinator.Configure(applier);
            Bones=MotionLabSkeleton.Resolve(boneMap,boneRoot);
            Motion=GetComponent<SpatialMotionRuntime>()??gameObject.AddComponent<SpatialMotionRuntime>();Motion.profile=rig;Motion.Configure(boneMap,boneRoot,character.transform,Coordinator,applier);
            retarget=new MotionLabRetargeter(Bones,boneRoot,character.transform,rig,Coordinator);
            ValidateFoundationBinding("");
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
            for(int i=1;i<22;i++)
            {
                var go=new GameObject("Source segment "+i);go.transform.SetParent(transform);var line=go.AddComponent<LineRenderer>();
                line.startWidth=line.endWidth=.007f;line.positionCount=2;line.enabled=false;
                var group=MotionSegment.Group(i);var color=group=="leftArm"||i==1||i==4||i==7||i==10?new Color(1,.35f,.2f):group=="rightArm"||i==2||i==5||i==8||i==11?new Color(.2f,.65f,1):Color.white;
                var material=new Material(shader);material.color=color;line.sharedMaterial=material;sourceMaterials.Add(material);
                line.startColor=line.endColor=color;lines.Add(line);
            }
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("Source reference "+i);go.transform.SetParent(transform);var line=go.AddComponent<LineRenderer>();
                var material=new Material(shader);material.color=i<2?new Color(.4f,.44f,.5f):new Color(.4f,1,.45f);sourceMaterials.Add(material);line.sharedMaterial=material;
                line.startWidth=line.endWidth=.003f;line.positionCount=i==3?3:2;line.enabled=false;referenceLines.Add(line);
            }
            foreach(var label in new[]{"L","R","reference (not face)"})
            {
                var go=new GameObject("Source "+label);go.transform.SetParent(transform);var text=go.AddComponent<TextMesh>();text.text=label;text.fontSize=48;text.characterSize=.008f;text.anchor=TextAnchor.MiddleCenter;go.SetActive(false);referenceLabels.Add(text);
            }
            initialized=true;
        }
        public void Load(string directory,string sourceFile="motion.json",bool preserve=false)
        {
            if(!initialized)throw new InvalidOperationException("Enter Play Mode and wait for initialization.");
            if(!preserve)Stop();inputDirectory=directory;
            OutputDirectory=Path.Combine(directory,"inspections",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+(FoundationMode?"foundation":UseAdaptation?"legacy-corrected":"legacy-raw"));Directory.CreateDirectory(OutputDirectory);
            foreach(var line in lines)line.enabled=false;
            foreach(var line in referenceLines)line.enabled=false;foreach(var label in referenceLabels)label.gameObject.SetActive(false);
            clip=null;semantic=null;
            if(File.Exists(Path.Combine(directory,"semantic.json")))
            {
                semantic=JsonUtility.FromJson<MotionLabSemantic>(File.ReadAllText(Path.Combine(directory,"semantic.json")));
                if(!string.IsNullOrEmpty(semantic.unsupported))throw new ArgumentException("Unsupported: "+semantic.unsupported);
                if(!semantic.reset&&(semantic.steps==null||semantic.steps.Length==0))throw new ArgumentException("No semantic steps.");
            }
            else {clip=JsonUtility.FromJson<MotionLabClip>(File.ReadAllText(Path.Combine(directory,sourceFile)));clip.Validate();}
            retarget.Adapted=UseAdaptation||FoundationMode;retarget.Foundation=FoundationMode;
            if(FoundationMode)ValidateFoundationBinding(directory);
            File.WriteAllText(Path.Combine(OutputDirectory,"foundation-binding.json"),RigSnapshot());
            if(UseAdaptation||FoundationMode)
            {
                var snapshot=Path.Combine(directory,"adaptation-profile.json");
                if(File.Exists(snapshot)&&!LatestCalibration)retarget.Calibration=JsonUtility.FromJson<MotionAdaptationProfile>(File.ReadAllText(snapshot));
                else retarget.Calibration=MotionAdaptationProfile.Load();
                File.WriteAllText(Path.Combine(OutputDirectory,"adaptation-profile.json"),JsonUtility.ToJson(retarget.Calibration,true));
            }
            Status="Loaded: "+(clip?.route??"semantic")+"; wrist twist/physical balance require separate validation";
        }
        public void Play(double submittedAt=-1,bool interrupt=false)
        {
            if(clip==null&&semantic==null)throw new InvalidOperationException("Load a generated sample first.");
            if(!Continuous)ReleasePlayback();
            if(!timingArmed){submitted=submittedAt<0?Time.realtimeSinceStartupAsDouble:submittedAt;firstAudio=firstBytes=-1;}
            FirstMotionSeconds=-1;PlaybackElapsed=stepElapsed=0;step=0;diagnostics.Clear();frameTimes.Clear();adaptationTimes.Clear();SegmentFinished=false;Paused=false;
            adaptationFrames.Clear();lastDiagnosticFrame=-1;
            finalPositionError=finalOrientationError=0;finalGeometrySamples=0;
            // Every segment owns its own writes. Excluded groups must not retain
            // the previous segment's final pose while a local segment plays.
            if(!string.IsNullOrEmpty(ownershipId))Coordinator.ReleaseSpatial(ownershipId,.35f);
            ownershipId="lab:"+Guid.NewGuid().ToString("N");interruptAfter=interrupt?1:-1;
            before=new Vector3[Bones.Count];beforeRotations=new Quaternion[Bones.Count];int n=0;foreach(var b in Bones.Values){before[n]=b.localPosition;beforeRotations[n++]=b.localRotation;}
            if(semantic?.reset==true){Motion.ResetPose();Status="Reset";SaveResult();return;}
            if(clip!=null){retarget.Adapted=UseAdaptation||FoundationMode;retarget.Foundation=FoundationMode;retarget.Segment=CurrentSegment;retarget.Begin(clip,Continuous);}
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
        public void SetMeasuredSubmission(double value){submitted=value;}
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
            if(initialized&&retarget==null){initialized=false;Start();}
            if(!initialized)return;
            // Animation clips only write keyed channels. Without this detached
            // baseline pass, unkeyed channels inherit last frame's spatial pose
            // and become the next Idle target, including pelvic translation.
            for(int i=0;i<idleBones.Length;i++){idleBones[i].localRotation=idleRotations[i];idleBones[i].localPosition=idlePositions[i];}
            if(!Paused)idleTime+=Time.deltaTime;applier.ApplyIdle(idle,idle==null?0:Mathf.Repeat(idleTime,idle.length));
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
                    if(!Paused&&!SegmentFinished){PlaybackElapsed+=Time.deltaTime*PlaybackSpeed;stepElapsed+=Time.deltaTime;}
                    if(clip!=null&&!SegmentFinished&&!Paused)
                    {
                        adaptationWatch.Restart();bool applied=retarget.Apply(clip,PlaybackElapsed,ownershipId,out var error);adaptationWatch.Stop();adaptationTimes.Add((float)adaptationWatch.Elapsed.TotalMilliseconds);
                        if(!applied){Status=error;diagnostics.Add(error);ReleasePlayback();SaveResult();}
                        else DrawSource(retarget.LastSource);
                    }
                    if(interruptAfter>0&&PlaybackElapsed>=interruptAfter){Status="Interrupted at injected stop";ReleasePlayback();SaveResult();}
                    else if(clip!=null&&!SegmentFinished&&PlaybackElapsed>=clip.frames.Length/clip.fps)
                    {Status="Playback complete; quality unreviewed";SegmentFinished=true;if(UseAdaptation||FoundationMode)retarget.EstablishHold(ownershipId);if(!Continuous)ReleasePlayback();SaveResult();}
                    else if(semantic!=null&&stepElapsed>=StepPlaybackSeconds(semantic.steps[step])+.05f)
                    {step++;stepElapsed=0;if(step<semantic.steps.Length)BeginStep();else{Playing=false;Status="Semantic execution ended; inspect holds/diagnostics";SaveResult();}}
                }
            }
            retarget.TickHolds(Paused?0:Time.deltaTime);Motion.Tick(Paused?0:Time.deltaTime);Coordinator.FinalizeFrame(Paused?0:Time.deltaTime);
            if(Playing&&audioGate)
            {
                if(semantic!=null)
                {
                    int measured=Motion.MeasureFinalGeometry(out var posError,out var rotError);
                    finalGeometrySamples+=measured;finalPositionError=Mathf.Max(finalPositionError,posError);finalOrientationError=Mathf.Max(finalOrientationError,rotError);
                    for(int s=0;s<=step&&s<semantic.steps.Length;s++){var failure=Motion.Failure(ownershipId+":"+s);if(!string.IsNullOrEmpty(failure)&&!diagnostics.Contains(failure))diagnostics.Add(failure);}
                }
                if(clip!=null&&PlaybackElapsed>=.25f)
                {
                    retarget.Measure(PlaybackElapsed>=(UseAdaptation||FoundationMode?retarget.Calibration.blendSeconds:.25f));
                    int frame=Mathf.FloorToInt(PlaybackElapsed*clip.fps);
                    if(CaptureDiagnostics&&!SegmentFinished&&frame!=lastDiagnosticFrame)
                    {var diagnostic=retarget.Diagnostic;diagnostic.time=PlaybackElapsed;adaptationFrames.Add(diagnostic);lastDiagnosticFrame=frame;}
                }
                int n=0;bool relatedMovement=false;foreach(var b in Bones.Values)
                {if(Coordinator.GetOwner(b)==BoneOwner.SpatialMotion&&(Vector3.Distance(before[n],b.localPosition)>.002f||Quaternion.Angle(beforeRotations[n],b.localRotation)>.5f))relatedMovement=true;n++;}
                if(FirstMotionSeconds<0&&relatedMovement)FirstMotionSeconds=(float)(Time.realtimeSinceStartupAsDouble-submitted);
            }
        }
        void DrawSource(Vector3[] points)
        {
            if(points==null)return;
            var orientation=retarget.SourcePlacement;
            float scale=retarget.SourceScale;
            var offset=character.transform.position+(SeparateSourceInView?-viewer.transform.right*.75f:orientation*new Vector3(-.75f,0,0));
            for(int i=1;i<22;i++){lines[i-1].enabled=true;lines[i-1].SetPosition(0,offset+orientation*(points[MotionLabSkeleton.Parents[i]]*scale));lines[i-1].SetPosition(1,offset+orientation*(points[i]*scale));}
            // Source zero-height plane and labelled root reference. The legacy
            // hip frame is inferred; none of these arrows measures facial facing.
            Vector3 OnFloor(Vector3 v)=>offset+orientation*v;
            for(int i=0;i<4;i++)referenceLines[i].enabled=true;
            referenceLines[0].SetPositions(new[]{OnFloor(new Vector3(-.3f,0,0)),OnFloor(new Vector3(.3f,0,0))});
            referenceLines[1].SetPositions(new[]{OnFloor(new Vector3(0,0,-.3f)),OnFloor(new Vector3(0,0,.3f))});
            var root=OnFloor(new Vector3(points[0].x*scale,.006f,points[0].z*scale));var direction=orientation*retarget.LastRootRotation*Vector3.forward;
            var tip=root+direction*.2f;var right=Vector3.Cross(Vector3.up,direction).normalized*.03f;
            referenceLines[2].SetPositions(new[]{root,tip});referenceLines[3].SetPositions(new[]{tip-direction*.04f-right,tip,tip-direction*.04f+right});
            referenceLabels[2].text=retarget.RootReference+" (not face)";
            for(int i=0;i<3;i++){referenceLabels[i].gameObject.SetActive(true);referenceLabels[i].transform.position=i==2?tip+Vector3.up*.025f:offset+orientation*(points[i==0?20:21]*scale)+Vector3.up*.035f;referenceLabels[i].transform.rotation=viewer.transform.rotation;}
        }
        public void RefreshSourceView()=>DrawSource(retarget.LastSource);
        public static float StepPlaybackSeconds(MotionLabStep value)=>value.duration+(value.completion=="timed"?Mathf.Max(0,value.holdSeconds):0);
        void ReleasePlayback(){Playing=false;if(Coordinator!=null&&!string.IsNullOrEmpty(ownershipId))Coordinator.ReleaseSpatial(ownershipId,.35f);Motion?.CancelMoving();}
        public void Stop(){ReleasePlayback();Status="Stopped; established semantic holds retained";}
        public void ResetPose(){Stop();retarget?.ClearHolds();Motion?.ResetPose();Paused=false;SegmentFinished=false;Status="Reset to Idle";}
        public void ReleaseParts(string[] groups){foreach(var group in groups)retarget.ReleaseHold(group);}
        public void EndSegment(){ReleasePlayback();SegmentFinished=false;}
        public void Seek(float time)
        {
            if(clip==null)return;
            float requested=Mathf.Clamp(time,0,Duration-.001f);
            // Rebuild state from the same baseline instead of reusing contact
            // anchors, velocities or entry poses from a later inspected frame.
            ResetInspectionPose();Continuous=false;Play();
            int last=Mathf.CeilToInt(requested*clip.fps);
            for(int frame=0;frame<=last;frame++)
            {
                float t=Mathf.Min(requested,frame/clip.fps);idleTime=t;
                for(int i=0;i<idleBones.Length;i++){idleBones[i].localRotation=idleRotations[i];idleBones[i].localPosition=idlePositions[i];}
                applier.ApplyIdle(idle,idle==null?0:Mathf.Repeat(t,idle.length));
                if(!retarget.Apply(clip,t,ownershipId,out var error)){ReleasePlayback();Status=error;return;}
                Coordinator.FinalizeFrame(1/clip.fps);
            }
            PlaybackElapsed=requested;Paused=true;SegmentFinished=false;Playing=true;retarget.Measure();DrawSource(retarget.LastSource);
        }
        public void ResetInspectionPose()
        {
            ResetPose();idleTime=0;
            for(int i=0;i<idleBones.Length;i++){idleBones[i].localRotation=idleRotations[i];idleBones[i].localPosition=idlePositions[i];}
            applier.ApplyIdle(idle,0);Coordinator.FinalizeFrame(1);
        }
        public string RigSnapshot()
        {
            var entries=new List<RigEntry>();foreach(var pair in Bones){var b=pair.Value;int i=Array.IndexOf(idleBones,b);entries.Add(new RigEntry{name=pair.Key,path=BonePath(b),parent=b.parent==null?"":BonePath(b.parent),localPosition=idlePositions[i],localRotation=idleRotations[i],worldPosition=b.position,worldRotation=b.rotation,localLength=idlePositions[i].magnitude,axisX=b.right,axisY=b.up,axisZ=b.forward});}
            var hierarchy=new List<RigEntry>();for(int i=0;i<idleBones.Length;i++)hierarchy.Add(new RigEntry{name=idleBones[i].name,path=BonePath(idleBones[i]),localPosition=idlePositions[i],localRotation=idleRotations[i]});
            return JsonUtility.ToJson(new RigReport{version=retarget.FoundationVersion,sourceNeckBinding=retarget.SourceNeckBinding,sourceNeckProvenance=sourceNeckProvenance,sourceNeckTemplateSha256=sourceNeckTemplateSha256,processingLayer=retarget.ProcessingLayer,reference="captured model local binding; current-world and axes are inspection only, NOT a recalibration",faceReference="binding bilateral hip axis cross up; eye-center alignment checked separately, not measured facial normal",bodyForward=rig.bodyForward,bodyUp=rig.bodyUp,bindingBodyForward=character.transform.InverseTransformDirection(retarget.BindingBodyFrame*Vector3.forward),bindingBodyUp=character.transform.InverseTransformDirection(retarget.BindingBodyFrame*Vector3.up),bones=entries.ToArray(),bindingHierarchy=hierarchy.ToArray()},true);
        }
        string BonePath(Transform bone){var path=bone.name;while(bone.parent!=null&&bone!=character.transform){bone=bone.parent;path=bone.name+"/"+path;}return path;}
        string sourceNeckProvenance,sourceNeckTemplateSha256;
        void ValidateFoundationBinding(string directory)
        {
            var path=Path.Combine(directory,"foundation-binding.json");
            var asset=Resources.Load<TextAsset>("TokiFoundationCalibration");
            var json=!LatestCalibration&&File.Exists(path)?File.ReadAllText(path):asset?.text;
            retarget.FoundationVersion=3;
            if(string.IsNullOrEmpty(json))return; // First capture is explicitly provisional.
            var snapshot=JsonUtility.FromJson<RigReport>(json);
            retarget.FoundationVersion=snapshot.version;
            retarget.SourceNeckBinding=snapshot.sourceNeckBinding;
            sourceNeckProvenance=snapshot.sourceNeckProvenance;sourceNeckTemplateSha256=snapshot.sourceNeckTemplateSha256;
            if(snapshot.version>=3&&Mathf.Abs(snapshot.sourceNeckBinding.magnitude-1)>.001f)throw new ArgumentException("Invalid calibrated source neck reference");
            if((snapshot.version<1||snapshot.version>3)||snapshot.bones==null)throw new ArgumentException("Unsupported foundation calibration snapshot");
            if(Vector3.Angle(snapshot.bodyForward,rig.bodyForward)>.01f||Vector3.Angle(snapshot.bodyUp,rig.bodyUp)>.01f)throw new ArgumentException("Foundation body basis differs from snapshot");
            if(snapshot.bindingBodyForward.sqrMagnitude>.5f&&Vector3.Angle(character.transform.TransformDirection(snapshot.bindingBodyForward),retarget.BindingBodyFrame*Vector3.forward)>.01f)throw new ArgumentException("Neutral binding basis differs from calibration snapshot");
            foreach(var e in snapshot.bones)
            {
                if(!Bones.TryGetValue(e.name,out var bone)||BonePath(bone)!=e.path)throw new ArgumentException("Foundation bone mapping differs: "+e.name);
                int i=Array.IndexOf(idleBones,bone);
                if(Vector3.Distance(idlePositions[i],e.localPosition)>.00001f||Quaternion.Angle(idleRotations[i],e.localRotation)>.1f)throw new ArgumentException("Foundation binding differs: "+e.name+". Compare with latest calibration explicitly.");
            }
        }
        [Serializable] sealed class RigReport{public int version;public string reference,faceReference,processingLayer,sourceNeckProvenance,sourceNeckTemplateSha256;public Vector3 sourceNeckBinding;public Vector3 bodyForward,bodyUp,bindingBodyForward,bindingBodyUp;public RigEntry[] bones,bindingHierarchy;}
        [Serializable] sealed class RigEntry{public string name,path,parent;public float localLength;public Vector3 localPosition,worldPosition,axisX,axisY,axisZ;public Quaternion localRotation,worldRotation;}
        public void UseLatestCalibration(){retarget.Calibration=MotionAdaptationProfile.Load();}
        void SaveResult()
        {
            if(string.IsNullOrEmpty(OutputDirectory))return;
            frameTimes.Sort();
            adaptationTimes.Sort();
            var report=new PlaybackReport{status=Status,firstRelatedMotion=FirstMotionSeconds,firstAudio=firstAudio,firstBytes=firstBytes,
                frameCount=frameTimes.Count,frameMsP50=Percentile(.5f),frameMsP95=Percentile(.95f),diagnostics=diagnostics.ToArray(),
                directionError=clip==null?-1:retarget.MaxDirectionError,boneLengthChange=clip==null?-1:retarget.MaxBoneLengthChange,
                footTravelRelative=clip==null?-1:retarget.MaxFootTravel,geometry=Motion.GeometryReport,sync=waitForAudio?"audio-first":"action-first"};
            report.sourceFootTravelRelative=clip==null?-1:retarget.MaxSourceFootTravel;report.torsoIntersectionSamples=clip==null?-1:retarget.TorsoIntersectionSamples;
            report.finalGeometrySamples=finalGeometrySamples;report.finalPositionErrorRelative=finalPositionError;report.finalOrientationErrorDegrees=finalOrientationError;
            report.layer=retarget.ProcessingLayer;
            report.sourceFormat=clip?.sourceFormat;report.coordinateVersion=clip?.coordinateVersion;
            report.rootOrientation=retarget.RootReference;
            report.applySamples=adaptationTimes.Count;report.applyMsP50=ApplyPercentile(.5f);report.applyMsP95=ApplyPercentile(.95f);report.applyMsMax=ApplyPercentile(1);
            report.settledDirectionError=clip==null?-1:retarget.MaxSettledDirectionError;
            File.WriteAllText(Path.Combine(OutputDirectory,"playback-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json"),JsonUtility.ToJson(report,true));
            if(CaptureDiagnostics&&adaptationFrames.Count>0)File.WriteAllText(Path.Combine(OutputDirectory,"adaptation-"+(FoundationMode?"foundation":UseAdaptation?"corrected":"original")+".json"),JsonUtility.ToJson(new AdaptationReport{frames=adaptationFrames.ToArray(),corrected=UseAdaptation&&!FoundationMode},true));
        }
        float Percentile(float p)=>frameTimes.Count==0?0:frameTimes[Mathf.Min(frameTimes.Count-1,Mathf.FloorToInt((frameTimes.Count-1)*p))];
        float ApplyPercentile(float p)=>adaptationTimes.Count==0?0:adaptationTimes[Mathf.FloorToInt((adaptationTimes.Count-1)*p)];
        void OnDisable(){if(Tts!=null){Tts.SpeechPlaybackStarted-=OnAudio;Tts.StopSpeech();}ResetPose();retarget?.Dispose();if(initialized)Application.runInBackground=priorBackground;foreach(var material in sourceMaterials)Destroy(material);}
        [Serializable] sealed class AdaptationReport{public bool corrected;public MotionFrameDiagnostic[] frames;}
        [Serializable] sealed class PlaybackReport
        {public string status,sync,geometry,layer,sourceFormat,coordinateVersion,rootOrientation;public float firstRelatedMotion,frameMsP50,frameMsP95,directionError,settledDirectionError,boneLengthChange,footTravelRelative,sourceFootTravelRelative,finalPositionErrorRelative,finalOrientationErrorDegrees,applyMsP50,applyMsP95,applyMsMax;public double firstAudio,firstBytes;public int frameCount,torsoIntersectionSamples,finalGeometrySamples,applySamples;public string[] diagnostics;}
    }
}
