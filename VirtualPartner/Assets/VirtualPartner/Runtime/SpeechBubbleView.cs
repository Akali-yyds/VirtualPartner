using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SpeechBubbleView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform anchorRoot;
        [SerializeField] private Camera worldCamera;

        [Header("Settings")]
        [SerializeField] private string anchorBoneName = "Bip001 Head";
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.28f, 0f);
        [SerializeField] private Vector2 bubbleSize = new Vector2(320f, 86f);

        [Header("Runtime Status")]
        [SerializeField] private bool visible;
        [SerializeField] private bool suppressed;
        [SerializeField, TextArea(2, 4)] private string currentText;

        private Transform anchor;
        private GUIStyle bubbleStyle;
        private GUIStyle textStyle;

        public bool IsVisible => visible;
        public bool Suppressed => suppressed;
        public string CurrentText => currentText;

        public void Configure(Transform root)
        {
            anchorRoot = root;
            ResolveAnchor();
        }

        private void Start()
        {
            ResolveAnchor();
        }

        public void Show(string text)
        {
            currentText = text;
            visible = !string.IsNullOrWhiteSpace(currentText);
        }

        public void Clear()
        {
            currentText = string.Empty;
            visible = false;
        }

        public void SetSuppressed(bool value)
        {
            suppressed = value;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || suppressed || !visible || string.IsNullOrWhiteSpace(currentText))
                return;

            var cameraToUse = worldCamera != null ? worldCamera : Camera.main;
            if (cameraToUse == null)
                return;

            if (anchor == null)
                ResolveAnchor();

            var target = anchor != null ? anchor : anchorRoot;
            if (target == null)
                return;

            var screenPosition = cameraToUse.WorldToScreenPoint(target.position + worldOffset);
            if (screenPosition.z <= 0f)
                return;

            EnsureStyles();

            var x = Mathf.Clamp(screenPosition.x - bubbleSize.x * 0.5f, 8f, Screen.width - bubbleSize.x - 8f);
            var y = Mathf.Clamp(Screen.height - screenPosition.y - bubbleSize.y, 8f, Screen.height - bubbleSize.y - 8f);
            var rect = new Rect(x, y, bubbleSize.x, bubbleSize.y);

            GUI.Box(rect, GUIContent.none, bubbleStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, rect.height - 20f), currentText, textStyle);
        }

        private void ResolveAnchor()
        {
            anchor = null;
            if (anchorRoot == null)
                return;

            if (string.IsNullOrWhiteSpace(anchorBoneName))
            {
                anchor = anchorRoot;
                return;
            }

            var children = anchorRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].name != anchorBoneName)
                    continue;

                anchor = children[i];
                return;
            }

            anchor = anchorRoot;
        }

        private void EnsureStyles()
        {
            if (bubbleStyle != null && textStyle != null)
                return;

            bubbleStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 8, 8)
            };
            bubbleStyle.normal.textColor = Color.white;

            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            texture.Apply();
            bubbleStyle.normal.background = texture;

            textStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                wordWrap = true
            };
            textStyle.normal.textColor = Color.white;
        }
    }
}
