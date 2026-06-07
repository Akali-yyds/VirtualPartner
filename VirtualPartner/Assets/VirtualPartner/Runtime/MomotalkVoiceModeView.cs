using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    /// <summary>
    /// Owns the Momotalk voice-mode overlay panel (status/body text, cancel button).
    /// Construction and visibility live here; the conversation controller drives it
    /// from ASR state and listens to <see cref="CancelRequested"/>.
    /// </summary>
    public sealed class MomotalkVoiceModeView
    {
        private MonoBehaviour coroutineHost;
        private Font uiFont;
        private CanvasGroup voiceModeView;
        private Image voiceModeBackground;
        private Text voiceModeStatusText;
        private Text voiceModeBodyText;
        private Text voiceModeCancelText;
        private Button voiceCancelButton;
        private Coroutine hideVoiceModeRoutine;

        public event Action CancelRequested;

        public bool IsVisible => voiceModeView != null && voiceModeView.alpha > 0f;

        public void Configure(RectTransform chatRoot, RectTransform inputBar, Font font, MonoBehaviour host)
        {
            coroutineHost = host;
            uiFont = font;
            EnsurePanel(chatRoot, inputBar);
        }

        public void SetUiFont(Font font)
        {
            if (font != null)
                uiFont = font;
        }

        public void Teardown()
        {
            if (voiceCancelButton != null)
                voiceCancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        private void EnsurePanel(RectTransform chatRoot, RectTransform inputBar)
        {
            if (voiceModeView != null || chatRoot == null)
                return;

            var panel = chatRoot.Find("VoiceModePanel") as RectTransform;
            if (panel == null)
            {
                panel = new GameObject("VoiceModePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image)).GetComponent<RectTransform>();
                panel.SetParent(chatRoot, false);
            }

            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.offsetMin = new Vector2(44f, 118f);
            panel.offsetMax = new Vector2(-44f, 218f);
            if (inputBar != null)
                panel.SetSiblingIndex(inputBar.GetSiblingIndex());

            voiceModeView = panel.GetComponent<CanvasGroup>();
            voiceModeBackground = panel.GetComponent<Image>();
            MomotalkUIStyle.ApplyRounded(voiceModeBackground, new Color(0.96f, 0.98f, 1f, 0.96f), 18, true);
            MomotalkUIStyle.ApplySoftShadow(voiceModeBackground, new Color(0.18f, 0.23f, 0.32f, 0.12f), new Vector2(0f, -4f));
            voiceModeBackground.raycastTarget = true;

            voiceModeStatusText = EnsurePanelText(panel, "Status", 26, TextAnchor.MiddleLeft, new Vector2(24f, 48f), new Vector2(-160f, 92f));
            voiceModeBodyText = EnsurePanelText(panel, "Body", 22, TextAnchor.MiddleLeft, new Vector2(24f, 10f), new Vector2(-160f, 52f));

            var cancelRect = panel.Find("CancelButton") as RectTransform;
            if (cancelRect == null)
            {
                cancelRect = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MomotalkUIButtonFeedback)).GetComponent<RectTransform>();
                cancelRect.SetParent(panel, false);
            }

            cancelRect.anchorMin = new Vector2(1f, 0.5f);
            cancelRect.anchorMax = new Vector2(1f, 0.5f);
            cancelRect.pivot = new Vector2(1f, 0.5f);
            cancelRect.anchoredPosition = new Vector2(-18f, 0f);
            cancelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 122f);
            cancelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 54f);
            var cancelImage = cancelRect.GetComponent<Image>();
            MomotalkUIStyle.ApplyRounded(cancelImage, new Color(0.88f, 0.91f, 0.96f, 1f), 16, true);
            voiceCancelButton = cancelRect.GetComponent<Button>();
            voiceCancelButton.targetGraphic = cancelImage;
            voiceCancelButton.onClick.RemoveListener(OnCancelClicked);
            voiceCancelButton.onClick.AddListener(OnCancelClicked);

            voiceModeCancelText = EnsurePanelText(cancelRect, "Text", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            voiceModeCancelText.text = "Cancel";
            Hide();
        }

        private Text EnsurePanelText(RectTransform parent, string name, int fontSize, TextAnchor alignment, Vector2 offsetMin, Vector2 offsetMax)
        {
            var textRect = parent.Find(name) as RectTransform;
            if (textRect == null)
            {
                textRect = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
                textRect.SetParent(parent, false);
            }

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = offsetMin;
            textRect.offsetMax = offsetMax;
            var text = textRect.GetComponent<Text>();
            MomotalkUIStyle.ApplyText(text, fontSize, MomotalkUIStyle.TextPrimary, alignment, uiFont);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void OnCancelClicked()
        {
            CancelRequested?.Invoke();
        }

        public void Show(string title, string body, bool error)
        {
            CancelScheduledHide();

            if (voiceModeView == null)
                return;

            voiceModeView.alpha = 1f;
            voiceModeView.interactable = true;
            voiceModeView.blocksRaycasts = true;
            if (voiceModeStatusText != null)
                voiceModeStatusText.text = title ?? string.Empty;
            if (voiceModeBodyText != null)
                voiceModeBodyText.text = body ?? string.Empty;
            if (voiceModeCancelText != null)
                voiceModeCancelText.text = error ? "Close" : "Cancel";
            if (voiceModeBackground != null)
                voiceModeBackground.color = error ? new Color(1f, 0.93f, 0.94f, 0.98f) : new Color(0.96f, 0.98f, 1f, 0.96f);
        }

        public void Hide()
        {
            CancelScheduledHide();

            if (voiceModeView == null)
                return;

            voiceModeView.alpha = 0f;
            voiceModeView.interactable = false;
            voiceModeView.blocksRaycasts = false;
        }

        public void ScheduleHide(float delay)
        {
            CancelScheduledHide();
            if (coroutineHost != null)
                hideVoiceModeRoutine = coroutineHost.StartCoroutine(HideAfterDelay(delay));
        }

        private void CancelScheduledHide()
        {
            if (hideVoiceModeRoutine != null && coroutineHost != null)
                coroutineHost.StopCoroutine(hideVoiceModeRoutine);
            hideVoiceModeRoutine = null;
        }

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delay));
            hideVoiceModeRoutine = null;
            Hide();
        }
    }
}
