using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    /// <summary>
    /// Owns the procedural Momotalk chat UI: the message scroll view, input bar,
    /// and message/typing bubble construction. It raises <see cref="SendRequested"/>
    /// and <see cref="MicRequested"/> so the conversation controller stays focused on
    /// orchestration rather than UI building.
    /// </summary>
    public sealed class MomotalkChatView
    {
        private const float ChatAvatarSize = 104f;
        private const float MessageRowHeight = 118f;
        private const float MessageBubbleMinHeight = 84f;
        private const float TypingBubbleWidth = 136f;
        private const float TypingBubbleMinHeight = 76f;

        private CanvasGroup chatView;
        private MonoBehaviour coroutineHost;
        private RectTransform scrollContent;
        private ScrollRect scrollRect;
        private RectTransform inputBar;
        private InputField inputField;
        private Button sendButton;
        private Button micButton;
        private Graphic micGraphic;
        private Font uiFont;
        private Color micDefaultColor = Color.white;
        private bool micDefaultColorCaptured;

        public event Action SendRequested;
        public event Action MicRequested;

        public RectTransform ChatRoot => chatView != null ? chatView.transform as RectTransform : null;
        public RectTransform InputBar => inputBar;
        public bool IsBuilt => scrollRect != null && scrollContent != null;

        public string InputText
        {
            get => inputField != null && inputField.text != null ? inputField.text : string.Empty;
            set
            {
                if (inputField != null)
                    inputField.text = value ?? string.Empty;
            }
        }

        public void Configure(CanvasGroup chatCanvasGroup, MonoBehaviour host)
        {
            chatView = chatCanvasGroup;
            coroutineHost = host;
            ResolveUiFont();
            EnsureBuilt();
        }

        public void Teardown()
        {
            if (sendButton != null)
                sendButton.onClick.RemoveListener(OnSendClicked);
            if (inputField != null)
                inputField.onSubmit.RemoveListener(OnInputSubmit);
            if (micButton != null)
                micButton.onClick.RemoveListener(OnMicClicked);
        }

        public void SetUiFont(Font font)
        {
            if (font == null)
                return;

            uiFont = font;
            ApplyFontToExistingTexts();
        }

        public void EnsureBuilt()
        {
            if (chatView == null)
                return;

            ResolveUiFont();

            var chatRoot = chatView.transform as RectTransform;
            if (chatRoot == null)
                return;

            var emptyArea = chatRoot.Find("EmptyMessageArea");
            if (emptyArea != null)
                emptyArea.gameObject.SetActive(false);
            var profileRow = chatRoot.Find("ProfileRow");
            if (profileRow != null)
                profileRow.gameObject.SetActive(false);

            EnsureScrollView(chatRoot);
            EnsureInputBar(chatRoot);
        }

        public void RefreshMicInteractable(bool asrActive)
        {
            if (micButton != null)
                micButton.interactable = !asrActive;
            if (micGraphic != null)
                micGraphic.color = micDefaultColor;
        }

        private void EnsureScrollView(RectTransform chatRoot)
        {
            if (scrollRect != null && scrollContent != null)
                return;

            var scrollObject = chatRoot.Find("MessageScrollView") as RectTransform;
            if (scrollObject == null)
            {
                scrollObject = new GameObject("MessageScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect)).GetComponent<RectTransform>();
                scrollObject.SetParent(chatRoot, false);
            }

            scrollObject.anchorMin = Vector2.zero;
            scrollObject.anchorMax = Vector2.one;
            scrollObject.offsetMin = new Vector2(36f, 128f);
            scrollObject.offsetMax = new Vector2(-36f, -132f);
            var existingInputBar = chatRoot.Find("InputBar");
            scrollObject.SetSiblingIndex(existingInputBar != null ? existingInputBar.GetSiblingIndex() : chatRoot.childCount - 1);
            if (existingInputBar != null)
                existingInputBar.SetAsLastSibling();

            var background = scrollObject.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0f);
            background.raycastTarget = true;

            var viewport = scrollObject.Find("Viewport") as RectTransform;
            if (viewport == null)
            {
                viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<RectTransform>();
                viewport.SetParent(scrollObject, false);
            }

            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = false;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            scrollContent = viewport.Find("Content") as RectTransform;
            if (scrollContent == null)
            {
                scrollContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
                scrollContent.SetParent(viewport, false);
            }

            scrollContent.anchorMin = new Vector2(0f, 1f);
            scrollContent.anchorMax = new Vector2(1f, 1f);
            scrollContent.pivot = new Vector2(0.5f, 1f);
            scrollContent.anchoredPosition = Vector2.zero;
            scrollContent.sizeDelta = new Vector2(0f, 0f);

            var layout = scrollContent.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 28, 34);
            layout.spacing = 36f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = scrollContent.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = scrollContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        private void EnsureInputBar(RectTransform chatRoot)
        {
            inputBar = MomotalkInputBarLayout.EnsureBar(chatRoot);
            if (inputBar == null)
                return;

            var elements = MomotalkInputBarLayout.Apply(inputBar, ResolveUiFont());
            if (elements == null || elements.InputField == null || elements.InputText == null)
                return;

            inputField = elements.InputField;
            inputField.textComponent = elements.InputText;
            inputField.placeholder = elements.Placeholder;
            inputField.contentType = InputField.ContentType.Standard;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.interactable = true;
            inputField.onSubmit.RemoveListener(OnInputSubmit);
            inputField.onSubmit.AddListener(OnInputSubmit);

            if (elements.InputBackground != null)
            {
                elements.InputBackground.raycastTarget = true;
                inputField.targetGraphic = elements.InputBackground;
            }

            ApplyTextFont(elements.InputText);
            elements.InputText.text = inputField.text;

            sendButton = elements.SendButton;
            if (sendButton != null)
            {
                if (elements.SendGraphic != null)
                {
                    elements.SendGraphic.raycastTarget = true;
                    elements.SendGraphic.color = Color.white;
                    sendButton.targetGraphic = elements.SendGraphic;
                }

                sendButton.onClick.RemoveListener(OnSendClicked);
                sendButton.onClick.AddListener(OnSendClicked);
            }

            micButton = elements.MicButton;
            micGraphic = elements.MicGraphic;
            if (micButton != null && micGraphic != null)
            {
                micButton.transition = Selectable.Transition.None;
                if (!micDefaultColorCaptured)
                {
                    micDefaultColor = Color.white;
                    micDefaultColorCaptured = true;
                }

                micGraphic.color = micDefaultColor;
                micGraphic.raycastTarget = true;
                micButton.targetGraphic = micGraphic;
                micButton.onClick.RemoveListener(OnMicClicked);
                micButton.onClick.AddListener(OnMicClicked);
            }
        }

        private void OnSendClicked()
        {
            SendRequested?.Invoke();
        }

        private void OnInputSubmit(string text)
        {
            SendRequested?.Invoke();
        }

        private void OnMicClicked()
        {
            MicRequested?.Invoke();
        }

        public MomotalkChatMessageView CreateTypingView(int requestId, Sprite avatar)
        {
            var row = CreateRow("TypingRow", TextAnchor.MiddleLeft);
            var avatarImage = CreateAvatar(row.transform, avatar);
            var bubble = CreateBubble(row.transform, TypingBubbleWidth, TypingBubbleMinHeight, true);
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(bubble.transform, false);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 20f);
            textRect.offsetMax = new Vector2(-20f, -20f);
            text.alignment = TextAnchor.MiddleLeft;
            ApplyTextFont(text);
            text.fontSize = 32;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var dots = new Image[3];
            var dotsRoot = new GameObject("Dots", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            dotsRoot.SetParent(bubble.transform, false);
            dotsRoot.anchorMin = Vector2.zero;
            dotsRoot.anchorMax = Vector2.one;
            dotsRoot.offsetMin = new Vector2(24f, 0f);
            dotsRoot.offsetMax = new Vector2(-24f, 0f);
            var dotsLayout = dotsRoot.GetComponent<HorizontalLayoutGroup>();
            dotsLayout.childAlignment = TextAnchor.MiddleCenter;
            dotsLayout.spacing = 10f;
            dotsLayout.childControlWidth = true;
            dotsLayout.childControlHeight = true;
            dotsLayout.childForceExpandWidth = false;
            dotsLayout.childForceExpandHeight = false;

            for (var i = 0; i < dots.Length; i++)
            {
                var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
                dot.transform.SetParent(dotsRoot, false);
                dot.sprite = MomotalkChatMessageView.GetCircleMaskSprite();
                dot.type = Image.Type.Simple;
                dot.color = new Color(0.78f, 0.80f, 0.86f, 0.8f);
                dot.raycastTarget = false;
                var dotLayout = dot.GetComponent<LayoutElement>();
                dotLayout.minWidth = 14f;
                dotLayout.minHeight = 14f;
                dotLayout.preferredWidth = 14f;
                dotLayout.preferredHeight = 14f;
                dots[i] = dot;
            }

            var view = row.AddComponent<MomotalkChatMessageView>();
            view.Initialize(avatarImage, bubble.GetComponent<Image>(), text, dots);
            view.BindTyping(requestId, avatar);
            return view;
        }

        public MomotalkChatMessageView CreateMessageView(MomotalkChatMessageRecord record, Sprite avatar)
        {
            if (record == null || scrollContent == null)
                return null;

            var isUser = record.sender == "user";
            var isSystem = record.sender == "system";
            var row = CreateRow(record.sender + "Row", isUser ? TextAnchor.MiddleRight : (isSystem ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft));
            Image avatarImage = null;
            if (!isUser && !isSystem)
                avatarImage = CreateAvatar(row.transform, avatar);

            var width = isSystem ? 650f : Mathf.Clamp((record.text != null ? record.text.Length : 0) * 22f + 132f, 210f, 620f);
            var bubble = CreateBubble(row.transform, width, MessageBubbleMinHeight, false);
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter)).GetComponent<Text>();
            text.transform.SetParent(bubble.transform, false);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 20f);
            textRect.offsetMax = new Vector2(-20f, -20f);
            text.alignment = isSystem ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            ApplyTextFont(text);
            text.fontSize = isSystem ? 25 : 32;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var view = row.AddComponent<MomotalkChatMessageView>();
            view.Initialize(avatarImage, bubble.GetComponent<Image>(), text, null);
            view.Bind(record, avatar);
            return view;
        }

        private GameObject CreateRow(string name, TextAnchor alignment)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(scrollContent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 18f;
            var layoutElement = row.GetComponent<LayoutElement>();
            layoutElement.minHeight = MessageRowHeight;
            layoutElement.preferredHeight = MessageRowHeight;
            layoutElement.flexibleWidth = 1f;
            return row;
        }

        private Image CreateAvatar(Transform parent, Sprite avatar)
        {
            var maskImage = new GameObject("Avatar", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(LayoutElement)).GetComponent<Image>();
            maskImage.transform.SetParent(parent, false);
            maskImage.GetComponent<LayoutElement>().preferredWidth = ChatAvatarSize;
            maskImage.GetComponent<LayoutElement>().preferredHeight = ChatAvatarSize;

            var image = MomotalkAvatarUtility.EnsureCircularAvatarImage(maskImage);
            MomotalkAvatarUtility.SetAvatar(image, avatar, avatar != null);
            return image;
        }

        private RectTransform CreateBubble(Transform parent, float width, float minHeight, bool typing)
        {
            var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            bubble.SetParent(parent, false);
            var layout = bubble.GetComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minHeight = minHeight;
            bubble.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            var image = bubble.GetComponent<Image>();
            var sprite = typing
                ? MomotalkUIStyle.Texture("chat_bubble_left.png", 18f)
                : MomotalkUIStyle.Texture("chat_bubble_right.png", 18f);
            MomotalkUIStyle.ApplySliced(image, sprite, Color.white, false);
            return bubble;
        }

        public Font ResolveUiFont()
        {
            if (uiFont != null)
                return uiFont;

            var chatText = chatView != null ? chatView.GetComponentInChildren<Text>(true) : null;
            if (chatText != null && chatText.font != null)
            {
                uiFont = chatText.font;
                return uiFont;
            }

            if (coroutineHost != null)
            {
                var rootText = coroutineHost.GetComponentInChildren<Text>(true);
                if (rootText != null && rootText.font != null)
                    uiFont = rootText.font;
            }

            return uiFont;
        }

        private void ApplyTextFont(Text text)
        {
            if (text == null)
                return;

            var font = ResolveUiFont();
            if (font != null)
                text.font = font;
        }

        private void ApplyFontToExistingTexts()
        {
            if (chatView == null || uiFont == null)
                return;

            var texts = chatView.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                    texts[i].font = uiFont;
            }
        }

        public void ClearMessages()
        {
            if (scrollContent == null)
                return;

            for (var i = scrollContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(scrollContent.GetChild(i).gameObject);
        }

        public void ScrollToBottom()
        {
            if (scrollContent != null)
                LayoutRebuilder.MarkLayoutForRebuild(scrollContent);
            if (scrollRect != null && coroutineHost != null && coroutineHost.gameObject.activeInHierarchy)
                coroutineHost.StartCoroutine(ScrollToBottomRoutine());
        }

        public void FocusInput()
        {
            if (inputField != null && coroutineHost != null && coroutineHost.gameObject.activeInHierarchy)
                coroutineHost.StartCoroutine(FocusInputFieldRoutine());
        }

        private IEnumerator ScrollToBottomRoutine()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (scrollContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        private IEnumerator FocusInputFieldRoutine()
        {
            yield return null;
            yield return null;
            if (inputField == null || !inputField.gameObject.activeInHierarchy)
                yield break;

            inputField.Select();
            inputField.ActivateInputField();
        }
    }
}
