using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [CreateAssetMenu(menuName = "VirtualPartner/Mouth Pose Profile")]
    public sealed class MouthPoseProfile : ScriptableObject
    {
        [SerializeField] private MouthPoseSet[] poseSets = Array.Empty<MouthPoseSet>();

        public IReadOnlyList<MouthPoseSet> PoseSets => poseSets;

        public bool TryFindPoseSet(string poseName, out MouthPoseSet poseSet)
        {
            poseSet = null;
            if (string.IsNullOrWhiteSpace(poseName) || poseSets == null)
                return false;

            for (var i = 0; i < poseSets.Length; i++)
            {
                var candidate = poseSets[i];
                if (candidate == null || !SameName(candidate.PoseName, poseName))
                    continue;

                poseSet = candidate;
                return true;
            }

            return false;
        }

        public bool TryGetNeutralPoseSet(out MouthPoseSet poseSet)
        {
            return TryFindPoseSet("neutral", out poseSet);
        }

        private static bool SameName(string left, string right)
        {
            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public sealed class MouthPoseSet
    {
        [SerializeField] private string poseName = "neutral";
        [SerializeField, Range(-1, 63)] private int closed = -1;
        [SerializeField, Range(-1, 63)] private int openSmall = 0;
        [SerializeField, Range(-1, 63)] private int openMid = 1;
        [SerializeField, Range(-1, 63)] private int openLarge = 3;

        public string PoseName => poseName;
        public int Closed => closed;
        public int OpenSmall => openSmall;
        public int OpenMid => openMid;
        public int OpenLarge => openLarge;
        public bool HasSpeakingMouth => openSmall >= 0 || openMid >= 0 || openLarge >= 0;

        public int GetSpeechIndex(float openness)
        {
            if (openness <= 0.2f)
                return closed;
            if (openness <= 0.5f)
                return ResolveOpen(openSmall, openMid, openLarge);
            if (openness <= 0.8f)
                return ResolveOpen(openMid, openLarge, openSmall);

            return ResolveOpen(openLarge, openMid, openSmall);
        }

        public int GetSpeechIndex(float openness, float smallThreshold, float midThreshold, float largeThreshold)
        {
            if (openness < smallThreshold)
                return closed;
            if (openness < midThreshold)
                return ResolveOpen(openSmall, openMid, openLarge);
            if (openness < largeThreshold)
                return ResolveOpen(openMid, openLarge, openSmall);

            return ResolveOpen(openLarge, openMid, openSmall);
        }

        public int GetRandomOpenIndex()
        {
            var validCount = 0;
            if (openSmall >= 0)
                validCount++;
            if (openMid >= 0)
                validCount++;
            if (openLarge >= 0)
                validCount++;

            if (validCount == 0)
                return -1;

            var target = UnityEngine.Random.Range(0, validCount);
            if (openSmall >= 0)
            {
                if (target == 0)
                    return openSmall;
                target--;
            }

            if (openMid >= 0)
            {
                if (target == 0)
                    return openMid;
                target--;
            }

            return openLarge >= 0 ? openLarge : -1;
        }

        private static int ResolveOpen(int first, int second, int third)
        {
            if (first >= 0)
                return first;
            if (second >= 0)
                return second;
            if (third >= 0)
                return third;
            return -1;
        }
    }
}
