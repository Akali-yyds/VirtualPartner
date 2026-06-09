using System;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [Serializable]
    public sealed class SceneOutlineSettings
    {
        public Color outlineColor = new Color(0.78f, 0.92f, 1f, 0.75f);
        [Range(2f, 8f)] public float widthPx = 4f;
        [Range(0f, 8f)] public float softnessPx = 1.5f;
        [Range(0f, 1f)] public float alpha = 0.75f;
        public LayerMask proxyLayer = 0;

        public float ClampedWidthPx => Mathf.Clamp(widthPx, 2f, 8f);
        public float ClampedSoftnessPx => Mathf.Clamp(softnessPx, 0f, 8f);
        public float ClampedAlpha => Mathf.Clamp01(alpha);
        public Color EffectiveColor => new Color(outlineColor.r, outlineColor.g, outlineColor.b, outlineColor.a * ClampedAlpha);
    }
}
