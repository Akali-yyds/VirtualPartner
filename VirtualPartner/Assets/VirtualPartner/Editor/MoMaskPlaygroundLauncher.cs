#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using VirtualPartner.Runtime.Experiments;

namespace VirtualPartner.Editor
{
    [InitializeOnLoad]
    public static class MoMaskPlaygroundLauncher
    {
        static MoMaskPlaygroundLauncher(){EditorApplication.playModeStateChanged+=Changed;}
        [MenuItem("VirtualPartner/Motion Lab/Open MoMask playground")]
        public static void Open()
        {
            if(EditorApplication.isPlaying)
            {
                if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=MotionLabWindow.ScenePath)throw new InvalidOperationException("Exit daily scene Play Mode before opening MotionLab.");
                Attach();return;
            }
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene edits first.");
            MotionLabWindow.BuildScene();SessionState.SetBool("MoMaskPlayground.Open",true);EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange value)
        {
            if(value==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("MoMaskPlayground.Open",false))
            {SessionState.SetBool("MoMaskPlayground.Open",false);Attach();}
        }
        static void Attach()
        {
            var driver=UnityEngine.Object.FindFirstObjectByType<MotionLabDriver>();
            if(driver==null)throw new InvalidOperationException("MotionLab driver missing.");
            if(driver.GetComponent<MoMaskPlayground>()==null)driver.gameObject.AddComponent<MoMaskPlayground>();
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        }
    }
}
#endif
