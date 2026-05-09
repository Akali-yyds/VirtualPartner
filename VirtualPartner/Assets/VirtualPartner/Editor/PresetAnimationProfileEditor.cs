using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VirtualPartner.Runtime;

namespace VirtualPartner.Editor
{
    [CustomEditor(typeof(PresetAnimationProfile))]
    public sealed class PresetAnimationProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Rebuild Bone Paths From Clip"))
                RebuildBonePathsFromClips();
        }

        private void RebuildBonePathsFromClips()
        {
            serializedObject.Update();
            Undo.RecordObject(target, "Rebuild preset animation bone paths");

            var entriesProperty = serializedObject.FindProperty("entries");
            for (var i = 0; i < entriesProperty.arraySize; i++)
            {
                var entryProperty = entriesProperty.GetArrayElementAtIndex(i);
                var clipProperty = entryProperty.FindPropertyRelative("clip");
                var bonePathsProperty = entryProperty.FindPropertyRelative("bonePaths");
                var clip = clipProperty.objectReferenceValue as AnimationClip;

                if (clip == null)
                    continue;

                var paths = CollectBoneRootRotationPaths(clip);
                bonePathsProperty.arraySize = paths.Count;
                for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
                    bonePathsProperty.GetArrayElementAtIndex(pathIndex).stringValue = paths[pathIndex];
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static List<string> CollectBoneRootRotationPaths(AnimationClip clip)
        {
            var paths = new List<string>();
            var seen = new HashSet<string>();
            var bindings = AnimationUtility.GetCurveBindings(clip);

            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding.type != typeof(Transform))
                    continue;
                if (!IsLocalRotationProperty(binding.propertyName))
                    continue;
                if (string.IsNullOrWhiteSpace(binding.path) || !binding.path.StartsWith("bone_root/"))
                    continue;

                var relativePath = binding.path.Substring("bone_root/".Length);
                if (string.IsNullOrWhiteSpace(relativePath) || !seen.Add(relativePath))
                    continue;

                paths.Add(relativePath);
            }

            paths.Sort();
            return paths;
        }

        private static bool IsLocalRotationProperty(string propertyName)
        {
            return propertyName.Contains("m_LocalRotation")
                || propertyName.Contains("localRotation")
                || propertyName.Contains("localEulerAngles");
        }
    }
}
