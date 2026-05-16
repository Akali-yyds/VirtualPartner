using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    public enum MomotalkSceneSpeechBubbleMode
    {
        KeepVisible,
        Hide
    }

    public sealed class MomotalkConversationController : MonoBehaviour
    {
        private const float ChatAvatarSize = 104f;
        private const float MessageRowHeight = 112f;
        private const float MessageBubbleMinHeight = 78f;
        private const float TypingBubbleWidth = 136f;
        private const float TypingBubbleMinHeight = 68f;

        private readonly MomotalkHistoryStore historyStore = new MomotalkHistoryStore();
        private readonly Dictionary<int, MomotalkChatMessageView> typingViews = new Dictionary<int, MomotalkChatMessageView>();
        private readonly Dictionary<int, string> requestCharacterIds = new Dictionary<int, string>();
        private readonly Dictionary<string, int> unreadCounts = new Dictionary<string, int>();
        private readonly HashSet<int> clearedRequestIds = new HashSet<int>();

        private MomotalkUIManager uiManager;
        private LlmRelay llmRelay;
        private StagePlanPlayer stagePlanPlayer;
        private SpeechBubbleView speechBubbleView;
        private AsrManager asrManager;
        private CanvasGroup chatView;
        private CharacterRuntimeContext currentContext;
        private RectTransform scrollContent;
        private ScrollRect scrollRect;
        private InputField inputField;
        private Button sendButton;
        private Button micButton;
        private Button voiceCancelButton;
        private CanvasGroup voiceModeView;
        private Text voiceModeStatusText;
        private Text voiceModeBodyText;
        private Text voiceModeCancelText;
        private Image voiceModeBackground;
        private Graphic micGraphic;
        private Font uiFont;
        private Color micDefaultColor = Color.white;
        private bool micDefaultColorCaptured;
        private Coroutine hideVoiceModeRoutine;
        private bool subscribedToLlmRelay;
        private bool subscribedToStagePlanPlayer;
        private bool subscribedToAsrManager;

        public string LastHistoryPath => historyStore.LastResolvedPath;

        public event System.Action ContactsChanged;

        public void Configure(MomotalkUIManager manager, CanvasGroup chatCanvasGroup)
        {
            uiManager = manager;
            chatView = chatCanvasGroup;
            llmRelay = UnityEngine.Object.FindFirstObjectByType<LlmRelay>();
            stagePlanPlayer = UnityEngine.Object.FindFirstObjectByType<StagePlanPlayer>();
            speechBubbleView = UnityEngine.Object.FindFirstObjectByType<SpeechBubbleView>();
            asrManager = UnityEngine.Object.FindFirstObjectByType<AsrManager>();
            EnsureSubscriptions();
            EnsureChatUi();
        }

        private void OnDestroy()
        {
            if (sendButton != null)
                sendButton.onClick.RemoveListener(SendCurrentInput);
            if (inputField != null)
                inputField.onSubmit.RemoveListener(HandleInputSubmit);
            if (micButton != null)
                micButton.onClick.RemoveListener(StartVoiceMode);
            if (voiceCancelButton != null)
                voiceCancelButton.onClick.RemoveListener(CancelOrCloseVoiceMode);
            if (llmRelay != null && subscribedToLlmRelay)
                llmRelay.RequestFailed -= HandleLlmRequestFailed;
            if (stagePlanPlayer != null && subscribedToStagePlanPlayer)
                stagePlanPlayer.SpeechActionStarted -= HandleSpeechActionStarted;
            if (asrManager != null && subscribedToAsrManager)
                asrManager.RecognitionFinished -= HandleAsrRecognitionFinished;
        }

        private void Update()
        {
            if (asrManager != null && asrManager.Active && voiceModeView != null && voiceModeView.alpha > 0f)
                RefreshVoiceModeFromAsr();
        }

        public void SetPhoneOpen(bool open, MomotalkSceneSpeechBubbleMode speechBubbleMode)
        {
            if (speechBubbleView != null)
                speechBubbleView.SetSuppressed(open && speechBubbleMode == MomotalkSceneSpeechBubbleMode.Hide);
        }

        public void ShowConversation(CharacterRuntimeContext context)
        {
            currentContext = context;
            typingViews.Clear();
            EnsureChatUi();
            ClearMessages();

            var characterId = GetCharacterId(context);
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            ClearUnread(characterId);
            var messages = historyStore.LoadRecent(characterId);
            for (var i = 0; i < messages.Count; i++)
                CreateMessageView(messages[i], context != null && context.Profile != null ? context.Profile.AvatarIcon : null);

            RebuildPendingTypingViews(characterId, context != null && context.Profile != null ? context.Profile.AvatarIcon : null, messages);
            ScrollToBottom();
            FocusInputField();
            ContactsChanged?.Invoke();
            Debug.Log($"[VirtualPartner] Momotalk history path: {historyStore.GetPath(characterId)}", this);
        }

        public string GetContactSummary(CharacterRuntimeContext context)
        {
            var fallback = context != null && context.Profile != null ? context.Profile.MomotalkStatus : "Available";
            return historyStore.GetLastSummary(GetCharacterId(context), fallback);
        }

        public int GetUnreadCount(CharacterRuntimeContext context)
        {
            unreadCounts.TryGetValue(GetCharacterId(context), out var count);
            return count;
        }

        public void ClearConversation(CharacterRuntimeContext context)
        {
            var characterId = GetCharacterId(context);
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            CancelLlmForCharacter(characterId);
            historyStore.Clear(characterId);
            ClearUnread(characterId);

            if (IsLoadedConversation(characterId))
                ClearMessages();

            ContactsChanged?.Invoke();
        }

        public bool HasAnyUnread()
        {
            foreach (var pair in unreadCounts)
            {
                if (pair.Value > 0)
                    return true;
            }

            return false;
        }

        private void SendCurrentInput()
        {
            if (inputField == null || currentContext == null)
                return;

            EnsureSubscriptions();

            var text = inputField.text == null ? string.Empty : inputField.text.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            var characterId = GetCharacterId(currentContext);
            var avatar = currentContext.Profile != null ? currentContext.Profile.AvatarIcon : null;
            var historyContext = historyStore.BuildPromptContext(characterId, uiManager != null ? uiManager.LlmHistoryContextMessageCount : 0);

            LlmSubmitResult submit = null;
            if (llmRelay != null)
                submit = llmRelay.SubmitWithResult(text, historyContext);

            var userRecord = MomotalkHistoryStore.CreateMessage("user", text, "sent", submit != null ? submit.RequestId : 0, -1, -1);
            historyStore.Append(characterId, userRecord);
            CreateMessageView(userRecord, avatar);

            inputField.text = string.Empty;
            FocusInputField();

            if (submit == null)
            {
                AddSystemMessage(characterId, "LLM relay is missing.", "error", 0);
                return;
            }

            if (!submit.Accepted)
            {
                if (submit.RequestId > 0)
                    requestCharacterIds[submit.RequestId] = characterId;
                AddSystemMessage(characterId, submit.Message, "error", submit.RequestId);
                return;
            }

            requestCharacterIds[submit.RequestId] = characterId;
            ReplaceStaleTypingViews(submit.RequestId);
            var typingView = CreateTypingView(submit.RequestId, avatar);
            typingViews[submit.RequestId] = typingView;
            ContactsChanged?.Invoke();
            ScrollToBottom();
        }

        private void HandleInputSubmit(string text)
        {
            SendCurrentInput();
        }

        private void StartVoiceMode()
        {
            EnsureSubscriptions();
            EnsureChatUi();

            if (asrManager == null)
            {
                ShowVoiceMode("ASR unavailable", "ASR manager is missing.", true);
                return;
            }

            if (asrManager.Active)
            {
                RefreshVoiceModeFromAsr();
                return;
            }

            ShowVoiceMode("Listening", "Starting voice input...", false);
            if (!asrManager.StartRecognition(out var failureReason))
            {
                ShowVoiceMode("ASR unavailable", failureReason, true);
                return;
            }

            RefreshVoiceModeFromAsr();
        }

        private void CancelOrCloseVoiceMode()
        {
            if (asrManager != null && asrManager.Active)
            {
                asrManager.CancelRecognition();
                return;
            }

            HideVoiceMode();
        }

        private void HandleAsrRecognitionFinished(AsrRecognitionResult result)
        {
            if (result == null)
                return;

            switch (result.Status)
            {
                case AsrRecognitionStatus.Done:
                    HandleAsrDone(result);
                    break;
                case AsrRecognitionStatus.Error:
                    ShowVoiceMode("ASR error", string.IsNullOrWhiteSpace(result.Error) ? "ASR failed." : result.Error, true);
                    break;
                case AsrRecognitionStatus.Canceled:
                    ShowVoiceMode("Canceled", "Voice input canceled.", false);
                    ScheduleVoiceModeHide(0.5f);
                    break;
            }

            RefreshMicButtonState();
        }

        private void HandleAsrDone(AsrRecognitionResult result)
        {
            var recognizedText = result.Text == null ? string.Empty : result.Text.Trim();
            if (string.IsNullOrWhiteSpace(recognizedText))
            {
                ShowVoiceMode("No speech recognized", "No speech recognized.", true);
                return;
            }

            if (inputField != null)
                inputField.text = recognizedText;

            if (result.ResultMode == AsrResultMode.AutoSendToLlm)
            {
                ShowVoiceMode("Done", "Sending recognized text...", false);
                SendCurrentInput();
                ScheduleVoiceModeHide(0.75f);
                return;
            }

            ShowVoiceMode("Done", recognizedText, false);
            FocusInputField();
            ScheduleVoiceModeHide(0.75f);
        }

        private void HandleLlmRequestFailed(LlmRequestFailure failure)
        {
            if (failure == null || failure.RequestId <= 0)
                return;
            if (clearedRequestIds.Contains(failure.RequestId))
                return;

            var characterId = GetCharacterIdForRequest(failure.RequestId);
            if (string.IsNullOrWhiteSpace(characterId))
                characterId = GetCharacterId(currentContext);
            if (string.IsNullOrWhiteSpace(characterId))
                characterId = GetRuntimeCharacterId();

            ReplaceTypingWithSystem(characterId, failure.RequestId, failure.Message, "error", true);
        }

        private void HandleSpeechActionStarted(StagePlanSpeechEvent speech)
        {
            if (speech == null)
                return;
            if (speech.OwnerId != LlmRelay.LlmOwnerId)
                return;
            if (speech.RequestId <= 0)
                return;
            if (clearedRequestIds.Contains(speech.RequestId))
                return;
            if (stagePlanPlayer != null && speech.RequestId != stagePlanPlayer.CurrentLlmStagePlanRequestId)
                return;

            var characterId = string.IsNullOrWhiteSpace(speech.CharacterId) ? GetRuntimeCharacterId() : speech.CharacterId;
            if (string.IsNullOrWhiteSpace(characterId))
                return;
            var expectedCharacterId = GetCharacterIdForRequest(speech.RequestId);
            if (!string.IsNullOrWhiteSpace(expectedCharacterId)
                && !SameCharacter(expectedCharacterId, characterId))
                return;

            var record = MomotalkHistoryStore.CreateMessage("character", speech.Text, "shown", speech.RequestId, speech.StageIndex, speech.ActionIndex);
            historyStore.Append(characterId, record);
            requestCharacterIds.Remove(speech.RequestId);

            var avatar = GetAvatarForCharacter(characterId);
            if (IsLoadedConversation(characterId))
            {
                if (typingViews.TryGetValue(speech.RequestId, out var typingView) && typingView != null)
                {
                    typingView.Bind(record, avatar);
                    typingViews.Remove(speech.RequestId);
                }
                else
                {
                    CreateMessageView(record, avatar);
                }

                ScrollToBottom();
            }

            if (uiManager == null || !uiManager.IsCurrentChatVisible(characterId))
                IncrementUnread(characterId);

            ContactsChanged?.Invoke();
        }

        private void ReplaceStaleTypingViews(int newestRequestId)
        {
            var staleRequestIds = new List<int>();
            foreach (var pair in typingViews)
            {
                if (pair.Key == newestRequestId)
                    continue;

                staleRequestIds.Add(pair.Key);
            }

            for (var i = 0; i < staleRequestIds.Count; i++)
            {
                var requestId = staleRequestIds[i];
                var characterId = GetCharacterIdForRequest(requestId);
                if (string.IsNullOrWhiteSpace(characterId))
                    characterId = GetCharacterId(currentContext);
                if (uiManager != null && uiManager.ShowReplacedSystemMessage)
                    ReplaceTypingWithSystem(characterId, requestId, "Replaced by newer message", "replaced", true);
                else
                    RemoveTyping(requestId);
            }
        }

        private void ReplaceTypingWithSystem(string characterId, int requestId, string text, string status, bool save)
        {
            if (typingViews.TryGetValue(requestId, out var typingView) && typingView != null)
            {
                var record = MomotalkHistoryStore.CreateMessage("system", text, status, requestId, -1, -1);
                typingView.Bind(record, null);
                typingViews.Remove(requestId);
                requestCharacterIds.Remove(requestId);
                if (save)
                    historyStore.Append(characterId, record);
                ScrollToBottom();
                ContactsChanged?.Invoke();
                return;
            }

            if (save)
                AddSystemMessage(characterId, text, status, requestId);
            else
                requestCharacterIds.Remove(requestId);
        }

        private void AddSystemMessage(string characterId, string text, string status, int requestId)
        {
            var record = MomotalkHistoryStore.CreateMessage("system", text, status, requestId, -1, -1);
            historyStore.Append(characterId, record);
            if (requestId > 0)
                requestCharacterIds.Remove(requestId);
            if (IsLoadedConversation(characterId))
            {
                CreateMessageView(record, null);
                ScrollToBottom();
            }

            ContactsChanged?.Invoke();
        }

        private void RebuildPendingTypingViews(string characterId, Sprite avatar, List<MomotalkChatMessageRecord> visibleMessages)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            var pendingRequestIds = new List<int>();
            foreach (var pair in requestCharacterIds)
            {
                if (!SameCharacter(pair.Value, characterId))
                    continue;
                if (clearedRequestIds.Contains(pair.Key))
                    continue;
                if (HasResponseForRequest(pair.Key, visibleMessages))
                    continue;

                // Momotalk owns the pending visual state; LLM/StagePlan can briefly move
                // between internal states before the first speech event arrives.
                pendingRequestIds.Add(pair.Key);
            }

            for (var i = 0; i < pendingRequestIds.Count; i++)
            {
                var requestId = pendingRequestIds[i];
                typingViews[requestId] = CreateTypingView(requestId, avatar);
            }
        }

        private static bool HasResponseForRequest(int requestId, List<MomotalkChatMessageRecord> messages)
        {
            if (requestId <= 0 || messages == null)
                return false;

            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (message == null || message.requestId != requestId)
                    continue;
                if (message.sender == "character" || message.sender == "system")
                    return true;
            }

            return false;
        }

        private void IncrementUnread(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            unreadCounts.TryGetValue(characterId, out var count);
            unreadCounts[characterId] = count + 1;
        }

        private void ClearUnread(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            if (unreadCounts.Remove(characterId))
                ContactsChanged?.Invoke();
        }

        private void RemoveTyping(int requestId)
        {
            if (!typingViews.TryGetValue(requestId, out var view))
            {
                requestCharacterIds.Remove(requestId);
                return;
            }

            typingViews.Remove(requestId);
            requestCharacterIds.Remove(requestId);
            if (view != null)
                Destroy(view.gameObject);
        }

        private void CancelLlmForCharacter(string characterId)
        {
            var removedAnyPendingRequest = false;
            var playingRequestId = stagePlanPlayer != null ? stagePlanPlayer.CurrentLlmStagePlanRequestId : 0;
            var playingCharacterId = playingRequestId > 0 ? GetCharacterIdForRequest(playingRequestId) : string.Empty;
            var shouldStopPlaying = playingRequestId > 0
                && (string.IsNullOrWhiteSpace(playingCharacterId) || SameCharacter(playingCharacterId, characterId));
            var requestIds = new List<int>();
            foreach (var pair in requestCharacterIds)
            {
                if (SameCharacter(pair.Value, characterId))
                    requestIds.Add(pair.Key);
            }

            for (var i = 0; i < requestIds.Count; i++)
            {
                var requestId = requestIds[i];
                clearedRequestIds.Add(requestId);
                if (typingViews.ContainsKey(requestId))
                    removedAnyPendingRequest = true;
                RemoveTyping(requestId);
            }

            if (removedAnyPendingRequest && llmRelay != null)
                llmRelay.StopPendingRequest();

            if (!shouldStopPlaying)
                return;

            clearedRequestIds.Add(playingRequestId);
            requestCharacterIds.Remove(playingRequestId);
            if (llmRelay != null)
                llmRelay.StopLlmStagePlan();
        }

        private bool IsLoadedConversation(string characterId)
        {
            return string.Equals(GetCharacterId(currentContext), characterId, System.StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureSubscriptions()
        {
            if (llmRelay == null)
                llmRelay = UnityEngine.Object.FindFirstObjectByType<LlmRelay>();
            if (stagePlanPlayer == null)
                stagePlanPlayer = UnityEngine.Object.FindFirstObjectByType<StagePlanPlayer>();
            if (speechBubbleView == null)
                speechBubbleView = UnityEngine.Object.FindFirstObjectByType<SpeechBubbleView>();
            if (asrManager == null)
                asrManager = UnityEngine.Object.FindFirstObjectByType<AsrManager>();

            if (llmRelay != null && !subscribedToLlmRelay)
            {
                llmRelay.RequestFailed += HandleLlmRequestFailed;
                subscribedToLlmRelay = true;
            }

            if (stagePlanPlayer != null && !subscribedToStagePlanPlayer)
            {
                stagePlanPlayer.SpeechActionStarted += HandleSpeechActionStarted;
                subscribedToStagePlanPlayer = true;
            }

            if (asrManager != null && !subscribedToAsrManager)
            {
                asrManager.RecognitionFinished += HandleAsrRecognitionFinished;
                subscribedToAsrManager = true;
            }
        }

        private void EnsureChatUi()
        {
            if (chatView == null)
                return;

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
            RefreshMicButtonState();
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
            scrollObject.offsetMin = new Vector2(24f, 112f);
            scrollObject.offsetMax = new Vector2(-24f, -132f);
            var inputBar = chatRoot.Find("InputBar");
            scrollObject.SetSiblingIndex(inputBar != null ? inputBar.GetSiblingIndex() : chatRoot.childCount - 1);
            if (inputBar != null)
                inputBar.SetAsLastSibling();

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
            layout.padding = new RectOffset(20, 20, 24, 24);
            layout.spacing = 22f;
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
            var inputBar = chatRoot.Find("InputBar") as RectTransform;
            if (inputBar == null)
                return;

            var inputRoot = inputBar.Find("DisabledInputField") as RectTransform;
            if (inputRoot == null)
                return;

            inputField = inputRoot.GetComponent<InputField>();
            if (inputField == null)
                inputField = inputRoot.gameObject.AddComponent<InputField>();

            var textTransform = inputRoot.Find("InputText") as RectTransform;
            if (textTransform == null)
            {
                textTransform = new GameObject("InputText", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
                textTransform.SetParent(inputRoot, false);
            }

            textTransform.anchorMin = Vector2.zero;
            textTransform.anchorMax = Vector2.one;
            textTransform.offsetMin = new Vector2(16f, 0f);
            textTransform.offsetMax = new Vector2(-16f, 0f);
            var inputText = textTransform.GetComponent<Text>();
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.color = new Color(0.16f, 0.2f, 0.26f, 1f);
            inputText.fontSize = 25;
            inputText.raycastTarget = false;

            var placeholder = inputRoot.Find("Placeholder") != null
                ? inputRoot.Find("Placeholder").GetComponent<Text>()
                : null;
            if (placeholder != null)
            {
                placeholder.text = "Aa";
                placeholder.color = new Color(0.55f, 0.57f, 0.64f, 0.75f);
                placeholder.raycastTarget = false;
                if (placeholder.font != null)
                    uiFont = placeholder.font;
            }

            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.contentType = InputField.ContentType.Standard;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.interactable = true;
            inputField.onSubmit.RemoveListener(HandleInputSubmit);
            inputField.onSubmit.AddListener(HandleInputSubmit);

            var inputGraphic = inputRoot.GetComponent<Image>();
            if (inputGraphic != null)
            {
                inputGraphic.raycastTarget = true;
                inputField.targetGraphic = inputGraphic;
            }

            if (uiFont != null)
                inputText.font = uiFont;
            inputText.supportRichText = false;
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            inputText.verticalOverflow = VerticalWrapMode.Truncate;
            inputText.text = inputField.text;

            var sendTransform = inputBar.Find("SendIcon");
            if (sendTransform != null)
            {
                sendButton = sendTransform.GetComponent<Button>();
                if (sendButton == null)
                    sendButton = sendTransform.gameObject.AddComponent<Button>();
                var sendGraphic = sendTransform.GetComponent<Graphic>();
                if (sendGraphic != null)
                {
                    sendGraphic.raycastTarget = true;
                    sendButton.targetGraphic = sendGraphic;
                }
                sendButton.onClick.RemoveListener(SendCurrentInput);
                sendButton.onClick.AddListener(SendCurrentInput);
            }

            var micTransform = inputBar.Find("MicIcon");
            if (micTransform != null)
            {
                micButton = micTransform.GetComponent<Button>();
                if (micButton == null)
                    micButton = micTransform.gameObject.AddComponent<Button>();
                micGraphic = micTransform.GetComponent<Graphic>();
                if (micGraphic != null)
                {
                    if (!micDefaultColorCaptured)
                    {
                        micDefaultColor = micGraphic.color;
                        micDefaultColorCaptured = true;
                    }

                    micGraphic.raycastTarget = true;
                    micButton.targetGraphic = micGraphic;
                }

                micButton.onClick.RemoveListener(StartVoiceMode);
                micButton.onClick.AddListener(StartVoiceMode);
            }

            EnsureVoiceModePanel(chatRoot, inputBar);
            RefreshMicButtonState();
        }

        private void EnsureVoiceModePanel(RectTransform chatRoot, RectTransform inputBar)
        {
            if (voiceModeView != null)
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
            voiceModeBackground.color = new Color(0.96f, 0.98f, 1f, 0.96f);
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
            cancelImage.color = new Color(0.88f, 0.91f, 0.96f, 1f);
            voiceCancelButton = cancelRect.GetComponent<Button>();
            voiceCancelButton.targetGraphic = cancelImage;
            voiceCancelButton.onClick.RemoveListener(CancelOrCloseVoiceMode);
            voiceCancelButton.onClick.AddListener(CancelOrCloseVoiceMode);

            voiceModeCancelText = EnsurePanelText(cancelRect, "Text", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            voiceModeCancelText.text = "Cancel";
            HideVoiceMode();
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
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.color = new Color(0.16f, 0.2f, 0.26f, 1f);
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            if (uiFont != null)
                text.font = uiFont;
            return text;
        }

        private void ShowVoiceMode(string title, string body, bool error)
        {
            if (hideVoiceModeRoutine != null)
            {
                StopCoroutine(hideVoiceModeRoutine);
                hideVoiceModeRoutine = null;
            }

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

        private void HideVoiceMode()
        {
            if (hideVoiceModeRoutine != null)
            {
                StopCoroutine(hideVoiceModeRoutine);
                hideVoiceModeRoutine = null;
            }

            if (voiceModeView == null)
                return;

            voiceModeView.alpha = 0f;
            voiceModeView.interactable = false;
            voiceModeView.blocksRaycasts = false;
            RefreshMicButtonState();
        }

        private void ScheduleVoiceModeHide(float delay)
        {
            if (hideVoiceModeRoutine != null)
                StopCoroutine(hideVoiceModeRoutine);
            hideVoiceModeRoutine = StartCoroutine(HideVoiceModeAfterDelay(delay));
        }

        private System.Collections.IEnumerator HideVoiceModeAfterDelay(float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delay));
            hideVoiceModeRoutine = null;
            HideVoiceMode();
        }

        private void RefreshVoiceModeFromAsr()
        {
            if (asrManager == null)
                return;

            switch (asrManager.Status)
            {
                case AsrRecognitionStatus.Listening:
                    ShowVoiceMode("Listening", "Listening for your voice...", false);
                    break;
                case AsrRecognitionStatus.Recognizing:
                    ShowVoiceMode("Recognizing", "Recognizing speech...", false);
                    break;
                case AsrRecognitionStatus.Error:
                    ShowVoiceMode("ASR error", asrManager.LatestError, true);
                    break;
            }

            RefreshMicButtonState();
        }

        private void RefreshMicButtonState()
        {
            var active = asrManager != null && asrManager.Active;
            if (micButton != null)
                micButton.interactable = !active;
            if (micGraphic != null)
                micGraphic.color = active ? new Color(0.38f, 0.65f, 1f, 1f) : micDefaultColor;
        }

        private MomotalkChatMessageView CreateTypingView(int requestId, Sprite avatar)
        {
            var row = CreateRow("TypingRow", TextAnchor.MiddleLeft);
            var avatarImage = CreateAvatar(row.transform, avatar);
            var bubble = CreateBubble(row.transform, TypingBubbleWidth, TypingBubbleMinHeight, true);
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(bubble.transform, false);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(22f, 12f);
            textRect.offsetMax = new Vector2(-22f, -12f);
            text.alignment = TextAnchor.MiddleLeft;
            if (uiFont != null)
                text.font = uiFont;
            text.fontSize = 32;
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

        private MomotalkChatMessageView CreateMessageView(MomotalkChatMessageRecord record, Sprite avatar)
        {
            if (record == null || scrollContent == null)
                return null;

            var isUser = record.sender == "user";
            var isSystem = record.sender == "system";
            var row = CreateRow(record.sender + "Row", isUser ? TextAnchor.MiddleRight : (isSystem ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft));
            Image avatarImage = null;
            if (!isUser && !isSystem)
                avatarImage = CreateAvatar(row.transform, avatar);

            var width = isSystem ? 650f : Mathf.Clamp((record.text != null ? record.text.Length : 0) * 22f + 104f, 180f, 620f);
            var bubble = CreateBubble(row.transform, width, MessageBubbleMinHeight, false);
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter)).GetComponent<Text>();
            text.transform.SetParent(bubble.transform, false);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(26f, 14f);
            textRect.offsetMax = new Vector2(-26f, -14f);
            text.alignment = isSystem ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            if (uiFont != null)
                text.font = uiFont;
            text.fontSize = isSystem ? 25 : 32;
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
            bubble.GetComponent<Image>().type = Image.Type.Sliced;
            return bubble;
        }

        private void ClearMessages()
        {
            if (scrollContent == null)
                return;

            for (var i = scrollContent.childCount - 1; i >= 0; i--)
                Destroy(scrollContent.GetChild(i).gameObject);
        }

        private void ScrollToBottom()
        {
            if (scrollContent != null)
                LayoutRebuilder.MarkLayoutForRebuild(scrollContent);
            if (scrollRect != null && gameObject.activeInHierarchy)
                StartCoroutine(ScrollToBottomRoutine());
        }

        private void FocusInputField()
        {
            if (inputField != null && gameObject.activeInHierarchy)
                StartCoroutine(FocusInputFieldRoutine());
        }

        private System.Collections.IEnumerator ScrollToBottomRoutine()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (scrollContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        private System.Collections.IEnumerator FocusInputFieldRoutine()
        {
            yield return null;
            yield return null;
            if (inputField == null || !inputField.gameObject.activeInHierarchy)
                yield break;

            inputField.Select();
            inputField.ActivateInputField();
        }

        private string GetRuntimeCharacterId()
        {
            if (currentContext != null)
                return GetCharacterId(currentContext);

            var contexts = new List<CharacterRuntimeContext>();
            CharacterRegistry.GetRegisteredContexts(contexts);
            return contexts.Count > 0 ? contexts[0].CharacterId : string.Empty;
        }

        private string GetCharacterIdForRequest(int requestId)
        {
            return requestCharacterIds.TryGetValue(requestId, out var characterId) ? characterId : string.Empty;
        }

        private Sprite GetAvatarForCharacter(string characterId)
        {
            if (currentContext != null && SameCharacter(currentContext.CharacterId, characterId))
                return currentContext.Profile != null ? currentContext.Profile.AvatarIcon : null;

            CharacterRegistry.TryGet(characterId, out var context);
            return context != null && context.Profile != null ? context.Profile.AvatarIcon : null;
        }

        private static string GetCharacterId(CharacterRuntimeContext context)
        {
            return context != null ? context.CharacterId : string.Empty;
        }

        private static bool SameCharacter(string left, string right)
        {
            return string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
