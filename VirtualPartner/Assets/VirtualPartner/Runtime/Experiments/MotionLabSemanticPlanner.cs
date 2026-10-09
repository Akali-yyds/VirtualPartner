using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime.Experiments
{
    // Experimental spatial grounding only; no canned wave clips or instruction-to-pose lookup.
    public static class MotionLabSemanticPlanner
    {
        static StagePlanRotationDto V(Vector3 p) => new StagePlanRotationDto {x=p.x,y=p.y,z=p.z};

        public static StagePlanActionDto Build(MotionLabStep step, Dictionary<string,Transform> bones,
            Transform character, SpatialRigProfile rig, Transform viewer)
        {
            if (step == null || step.goals == null || step.goals.Length == 0 || step.goals.Length > 5 ||
                !SpatialMotionContract.Finite(step.duration) || step.duration < .5f || step.duration > 8)
                throw new ArgumentException("semantic_validation: invalid goals/duration.");
            var tracks = new List<SpatialTrackDto>();
            float height = Vector3.Distance(bones["Head"].position, bones["Foot:L"].position);
            Quaternion frame = character.rotation * Quaternion.LookRotation(rig.bodyForward, rig.bodyUp);
            var inverse = Quaternion.Inverse(frame);
            foreach (var g in step.goals)
            {
                if (g == null || !SpatialMotionContract.IsGroup(g.part) || !SpatialMotionContract.Finite(g.extent) || g.extent < .1f || g.extent > 1)
                    throw new ArgumentException("semantic_validation: invalid part/extent.");
                var track = new SpatialTrackDto {group=g.part,anchor="body"};
                if (g.part == "leftArm" || g.part == "rightArm")
                {
                    if (g.motion != "reach" && g.motion != "wave" && g.motion != "spread") throw new ArgumentException("Unsupported arm motion: " + g.motion);
                    bool left = g.part == "leftArm"; string side=left?":L":":R"; float sign=left?-1:1;
                    Vector3 shoulder = bones["UpperArm"+side].position;
                    float reach = Vector3.Distance(shoulder,bones["Forearm"+side].position) + Vector3.Distance(bones["Forearm"+side].position,bones["Hand"+side].position);
                    Vector3 shoulderBody = inverse*(shoulder-character.position)/height;
                    float r = reach / height;
                    float y;
                    switch(g.target)
                    {
                        case "waist": y=(inverse*(bones["Pelvis"].position-shoulder)).y/height;break;
                        case "chest": y=(inverse*(bones["Chest"].position-shoulder)).y/height;break;
                        case "face": y=(inverse*(bones["Head"].position-shoulder)).y/height;break;
                        case "shoulder": case "viewer": case "forward": y=0;break;
                        default: throw new ArgumentException("Unsupported arm target: "+g.target);
                    }
                    // Reach reflects the measured chain; semantic height is never silently clamped.
                    float lateral=g.motion=="spread"?sign*r*.65f:sign*r*.18f;
                    if(g.direction=="outward") lateral=sign*r*(.25f+.25f*g.extent);
                    else if(g.direction=="left") lateral=-r*.5f;
                    else if(g.direction=="right") lateral=r*.5f;
                    else if(g.direction!="front" && !string.IsNullOrEmpty(g.direction)) throw new ArgumentException("Unknown direction.");
                    Vector3 offset=new Vector3(lateral,y,r*.6f);
                    if(g.target=="viewer")
                    {
                        if(viewer==null)throw new ArgumentException("Viewer target unavailable.");
                        offset=(inverse*(viewer.position-shoulder)).normalized*r*(.65f+.28f*g.extent);
                    }
                    Vector3 target=shoulderBody+offset;
                    Vector3 hint=shoulderBody+new Vector3(sign*r*.55f,-r*.65f,r*.15f);
                    Vector3 palm=Vector3.zero,fingers=Vector3.zero;
                    switch(g.palm)
                    {
                        case "up": palm=Vector3.up;fingers=Vector3.forward;break;
                        case "front": palm=Vector3.forward;fingers=Vector3.up;break;
                        case "inward": palm=new Vector3(-sign,0,0);fingers=Vector3.up;break;
                        case "unconstrained": case null: case "":break;
                        default:throw new ArgumentException("Unknown palm relation.");
                    }
                    var keys=new List<SpatialKeyDto>();
                    Action<float,Vector3,bool> add=(t,p,stop)=>keys.Add(new SpatialKeyDto {time=t,position=V(p),hint=V(hint),palm=palm==Vector3.zero?null:V(palm),fingers=fingers==Vector3.zero?null:V(fingers),stop=stop});
                    if(g.motion=="wave")
                    {
                        if(g.cycles<1||g.cycles>4)throw new ArgumentException("Wave cycles must be 1..4.");
                        add(step.duration*.3f,target,false);
                        for(int k=1;k<=g.cycles*2;k++) add(step.duration*(.3f+.7f*k/(g.cycles*2)),target+new Vector3(sign*r*g.extent*.15f*(k%2==1?1:0),0,0),k==g.cycles*2);
                    }
                    else add(step.duration,target,true);
                    track.keys=keys.ToArray();
                }
                else if(g.part=="support")
                {
                    if(g.motion!="crouch"||g.target!="down")throw new ArgumentException("Support only supports shallow crouch.");
                    track.keys=new[]{new SpatialKeyDto{time=step.duration*.5f,position=V(new Vector3(0,-.09f*g.extent,0)),stop=true},new SpatialKeyDto{time=step.duration,position=V(Vector3.zero),stop=true}};
                }
                else
                {
                    string bone=g.part=="head"?"Head":"Chest";
                    // Experimental calibrated semantic axes of this character, not model-provided Euler guesses.
                    Vector3 angles;
                    if(g.motion=="turn"&&(g.target=="left"||g.target=="right")) angles=new Vector3(0,(g.target=="left"?-1:1)*25*g.extent,0);
                    else if(g.motion=="lean"&&g.target=="forward")angles=new Vector3(12*g.extent,0,0);
                    else throw new ArgumentException("Unsupported head/torso relation.");
                    track.keys=new[]{new SpatialKeyDto{time=step.duration,bones=new[]{new StagePlanBonePoseDto{bone=bone,side="None",rotation=V(angles)}},stop=true}};
                }
                tracks.Add(track);
            }
            var action = new StagePlanActionDto{type="spatialPose",tracks=tracks.ToArray(),completion=new SpatialCompletionDto{mode=step.completion,seconds=step.holdSeconds}};
            if(!SpatialMotionContract.Validate(action,out var error))throw new ArgumentException("semantic_validation: "+error);
            return action;
        }
    }
}
