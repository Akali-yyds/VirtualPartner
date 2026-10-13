#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using UnityEngine;
using VirtualPartner.Runtime.Experiments;

namespace VirtualPartner.Editor
{
    public static class MotionDurationReview
    {
        public static string Status {get;private set;}="Ready";
        public static bool Running {get;private set;}
        public static string Start(string root)
        {
            if(Running)return "Already running";
            var driver=UnityEngine.Object.FindFirstObjectByType<MotionLabDriver>();
            if(!Application.isPlaying||driver==null)return "Enter isolated MotionLab scene first";
            driver.StartCoroutine(Review(driver,root));return "Started realtime measurements and 30 FPS offline capture";
        }
        static IEnumerator Review(MotionLabDriver d,string root)
        {
            Running=true;int priorCapture=Time.captureFramerate;
            try
            {
                foreach(var route in new[]{"momask","dip"})
                {
                    var directories=Directory.GetDirectories(Path.Combine(root,route));Array.Sort(directories,StringComparer.Ordinal);
                    foreach(var folder in directories)
                    {
                        if(!File.Exists(Path.Combine(folder,"motion.json")))continue;
                        Status=route+" / "+Path.GetFileName(folder)+" realtime";
                        Time.captureFramerate=0;d.ResetPose();yield return new WaitForSeconds(.6f);
                        double submitted=Time.realtimeSinceStartupAsDouble;d.Load(folder);double loaded=Time.realtimeSinceStartupAsDouble;
                        d.waitForAudio=false;d.Play(submitted);
                        while(d.Playing)yield return null;
                        var reports=Directory.GetFiles(d.OutputDirectory,"playback-*.json");Array.Sort(reports,StringComparer.Ordinal);
                        File.Copy(reports[reports.Length-1],Path.Combine(folder,"realtime-playback.json"),true);
                        File.WriteAllText(Path.Combine(folder,"unity-timing.json"),JsonUtility.ToJson(new Timing{loadSeconds=loaded-submitted,firstBodyMotionSeconds=d.FirstMotionSeconds},true));
                        d.ResetPose();yield return new WaitForSeconds(.6f);
                        d.Load(folder);Time.captureFramerate=30;d.Play();
                        Status=route+" / "+Path.GetFileName(folder)+" 30 FPS capture";
                        var frames=Path.Combine(folder,"review-30fps");Directory.CreateDirectory(frames);
                        int index=0;
                        while(d.Playing)
                        {
                            yield return new WaitForEndOfFrame();
                            MotionLabReview.SaveCamera(d.viewer,Path.Combine(frames,(index++).ToString("D5")+".png"));
                        }
                        // Show the release to Idle too; these 15 frames are explicitly outside model output.
                        for(int i=0;i<15;i++){yield return new WaitForEndOfFrame();MotionLabReview.SaveCamera(d.viewer,Path.Combine(frames,(index++).ToString("D5")+".png"));}
                        File.WriteAllText(Path.Combine(folder,"capture.json"),JsonUtility.ToJson(new Capture{fps=30,frames=index,tailSeconds=.5f},true));
                        Time.captureFramerate=0;
                    }
                }
                Status="COMPLETE";File.WriteAllText(Path.Combine(root,"unity-review-complete.txt"),DateTime.UtcNow.ToString("O"));
            }
            finally{Time.captureFramerate=priorCapture;d.ResetPose();Running=false;}
        }
        [Serializable] sealed class Timing{public double loadSeconds;public float firstBodyMotionSeconds;}
        [Serializable] sealed class Capture{public int fps,frames;public float tailSeconds;public string method="Offline Unity captureFramerate; realtime performance is recorded separately";}
    }
}
#endif
