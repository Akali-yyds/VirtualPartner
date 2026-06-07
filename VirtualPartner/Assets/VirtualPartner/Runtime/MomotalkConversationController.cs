using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public enum MomotalkSceneSpeechBubbleMode
    {
        KeepVisible,
        Hide
    }

    public sealed class MomotalkConversationController : MonoBehaviour
    {
        private readonly MomotalkHistoryStore historyStore = new MomotalkHistoryStore();
        private readonly Dictionary<int, MomotalkChatMessageView> typingViews = new Dictionary<int, MomotalkChatMessageView>();
        private readonly Dictionary<string, int> unreadCounts = new Dictionary<string, int>();
        private readonly List<int> requestIdBuffer = new List<int>();

        private readonly MomotalkChatView chat = new MomotalkChatView();
        private readonly MomotalkVoiceModeView voiceMode = new MomotalkVoiceModeView();

        private MomotalkUIManager uiManager;
        private LlmRelay llmRelay;
        private StagePlanPlayer stagePlanPlayer;
        private SpeechBubbleView speechBubbleView;
        private AsrManager asrManager;
        private MemorySystem memorySystem;
        private ConversationRequestRegistry requestRegistry;
        private CanvasGroup chatView;
        private CharacterRuntimeContext currentContext;
        private bool viewsConfigured;
        private bool subscribedToLlmRelay;
        private bool subscribedToStagePlanPlayer;
        private bool subscribedToAsrManager;

        public string LastHistoryPath => historyStore.LastResolvedPath;
        public string CurrentCharacterId => GetCharacterId(currentContext);
        public int TotalUnreadCount
        {
            get
            {
                var total = 0;
                foreach (var pair in unreadCounts)
                    total += Mathf.Max(0, pair.Value);
                return total;
            }
        }

        public event System.Action ContactsChanged;

        // Observability for debug panel: lifecycle counts from the single source of truth.
        public int ActiveRequestCount => requestRegistry != null ? requestRegistry.ActiveCount : 0;
        public int PendingRequestCount => requestRegistry != null ? requestRegistry.CountByStatus(RequestStatus.Pending) : 0;
        public int PlayingRequestCount => requestRegistry != null ? requestRegistry.CountByStatus(RequestStatus.Playing) : 0;

        public void Configure(MomotalkUIManager manager, CanvasGroup chatCanvasGroup)
        {
            uiManager = manager;
            chatView = chatCanvasGroup;
            EnsureViewsConfigured();
            EnsureSubscriptions();
            chat.RefreshMicInteractable(asrManager != null && asrManager.Active);
        }

        // Composition-root injection (VirtualPartnerStage1Bootstrap). Replaces the
        // previous FindFirstObjectByType lookups so dependencies are explicit.
        public void ConfigureRuntime(
            LlmRelay relay,
            StagePlanPlayer player,
            SpeechBubbleView speechBubble,
            AsrManager asr,
            MemorySystem memory,
            ConversationRequestRegistry registry)
        {
            llmRelay = relay;
            stagePlanPlayer = player;
            speechBubbleView = speechBubble;
            asrManager = asr;
            memorySystem = memory;
            requestRegistry = registry;
            EnsureSubscriptions();
            chat.RefreshMicInteractable(asrManager != null && asrManager.Active);
        }

        public void SetUiFont(Font font)
        {
            if (font == null)
                return;

            chat.SetUiFont(font);
            voiceMode.SetUiFont(font);
        }

        private void EnsureViewsConfigured()
        {
            if (chatView == null)
                return;

            chat.Configure(chatView, this);
            if (!viewsConfigured)
            {
                chat.SendRequested += SendCurrentInput;
                chat.MicRequested += StartVoiceMode;
                voiceMode.CancelRequested += CancelOrCloseVoiceMode;
                viewsConfigured = true;
            }

            voiceMode.Configure(chat.ChatRoot, chat.InputBar, chat.ResolveUiFont(), this);
        }

        private void OnDestroy()
        {
            if (viewsConfigured)
            {
                chat.SendRequested -= SendCurrentInput;
                chat.MicRequested -= StartVoiceMode;
                voiceMode.CancelRequested -= CancelOrCloseVoiceMode;
            }

            chat.Teardown();
            voiceMode.Teardown();

            if (llmRelay != null && subscribedToLlmRelay)
                llmRelay.RequestFailed -= HandleLlmRequestFailed;
            if (stagePlanPlayer != null && subscribedToStagePlanPlayer)
            {
                stagePlanPlayer.SpeechActionStarted -= HandleSpeechActionStarted;
                stagePlanPlayer.StagePlanFinished -= HandleStagePlanFinished;
            }
            if (asrManager != null && subscribedToAsrManager)
                asrManager.RecognitionFinished -= HandleAsrRecognitionFinished;
        }

        private void Update()
        {
            if (asrManager != null && asrManager.Active && voiceMode.IsVisible)
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
            EnsureViewsConfigured();
            chat.ClearMessages();

            var characterId = GetCharacterId(context);
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            ClearUnread(characterId);
            var avatar = context != null && context.Profile != null ? context.Profile.AvatarIcon : null;
            var messages = historyStore.LoadRecent(characterId);
            for (var i = 0; i < messages.Count; i++)
                chat.CreateMessageView(messages[i], avatar);

            RebuildPendingTypingViews(characterId, avatar, messages);
            chat.ScrollToBottom();
            chat.FocusInput();
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
                chat.ClearMessages();

            ContactsChanged?.Invoke();
        }

        public void ClearMemory(CharacterRuntimeContext context)
        {
            var characterId = GetCharacterId(context);
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            if (memorySystem == null)
                return;

            memorySystem.ClearMemory(characterId);
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

        public void OpenHistoryFolder()
        {
            var path = LastHistoryPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                var characterId = CurrentCharacterId;
                if (string.IsNullOrWhiteSpace(characterId))
                    return;

                path = historyStore.GetPath(characterId);
            }

            var folder = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(folder))
                return;

            Directory.CreateDirectory(folder);
            Application.OpenURL("file:///" + folder.Replace("\\", "/"));
        }

        private void SendCurrentInput()
        {
            if (currentContext == null)
                return;

            EnsureSubscriptions();

            var text = chat.InputText == null ? string.Empty : chat.InputText.Trim();
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
            chat.CreateMessageView(userRecord, avatar);

            chat.InputText = string.Empty;
            chat.FocusInput();

            if (submit == null)
            {
                AddSystemMessage(characterId, "LLM relay is missing.", "error", 0);
                return;
            }

            if (!submit.Accepted)
            {
                AddSystemMessage(characterId, submit.Message, "error", submit.RequestId);
                return;
            }

            if (requestRegistry != null)
            {
                requestRegistry.Register(submit.RequestId, characterId);
                requestRegistry.MarkOlderPendingReplaced(characterId, submit.RequestId);
            }
            if (memorySystem != null)
                memorySystem.RegisterUserMessage(characterId, submit.RequestId, text);
            ReplaceStaleTypingViews(submit.RequestId);
            var typingView = chat.CreateTypingView(submit.RequestId, avatar);
            typingViews[submit.RequestId] = typingView;
            ContactsChanged?.Invoke();
            chat.ScrollToBottom();
        }

        private void StartVoiceMode()
        {
            EnsureSubscriptions();
            EnsureViewsConfigured();

            if (asrManager == null)
            {
                voiceMode.Show("ASR unavailable", "ASR manager is missing.", true);
                return;
            }

            if (asrManager.Active)
            {
                RefreshVoiceModeFromAsr();
                return;
            }

            voiceMode.Show("Listening", "Starting voice input...", false);
            if (!asrManager.StartRecognition(out var failureReason))
            {
                voiceMode.Show("ASR unavailable", failureReason, true);
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

            voiceMode.Hide();
            chat.RefreshMicInteractable(asrManager != null && asrManager.Active);
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
                    voiceMode.Show("ASR error", string.IsNullOrWhiteSpace(result.Error) ? "ASR failed." : result.Error, true);
                    break;
                case AsrRecognitionStatus.Canceled:
                    voiceMode.Show("Canceled", "Voice input canceled.", false);
                    voiceMode.ScheduleHide(0.5f);
                    break;
            }

            chat.RefreshMicInteractable(asrManager != null && asrManager.Active);
        }

        private void HandleAsrDone(AsrRecognitionResult result)
        {
            var recognizedText = result.Text == null ? string.Empty : result.Text.Trim();
            if (string.IsNullOrWhiteSpace(recognizedText))
            {
                voiceMode.Show("No speech recognized", "No speech recognized.", true);
                return;
            }

            chat.InputText = recognizedText;

            if (result.ResultMode == AsrResultMode.AutoSendToLlm)
            {
                voiceMode.Show("Done", "Sending recognized text...", false);
                SendCurrentInput();
                voiceMode.ScheduleHide(0.75f);
                return;
            }

            voiceMode.Show("Done", recognizedText, false);
            chat.FocusInput();
            voiceMode.ScheduleHide(0.75f);
        }

        private void HandleLlmRequestFailed(LlmRequestFailure failure)
        {
            if (failure == null || failure.RequestId <= 0)
                return;
            if (IsClearedRequest(failure.RequestId))
                return;
            if (requestRegistry != null)
                requestRegistry.TrySetStatus(failure.RequestId, RequestStatus.Failed);

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
            if (IsClearedRequest(speech.RequestId))
                return;
            if (stagePlanPlayer != null && speech.RequestId != stagePlanPlayer.CurrentLlmStagePlanRequestId)
                return;

            if (requestRegistry != null)
                requestRegistry.TrySetStatus(speech.RequestId, RequestStatus.Playing);

            var characterId = string.IsNullOrWhiteSpace(speech.CharacterId) ? GetRuntimeCharacterId() : speech.CharacterId;
            if (string.IsNullOrWhiteSpace(characterId))
                return;
            var expectedCharacterId = GetCharacterIdForRequest(speech.RequestId);
            if (!string.IsNullOrWhiteSpace(expectedCharacterId)
                && !SameCharacter(expectedCharacterId, characterId))
                return;

            var record = MomotalkHistoryStore.CreateMessage("character", speech.Text, "shown", speech.RequestId, speech.StageIndex, speech.ActionIndex);
            historyStore.Append(characterId, record);
            if (memorySystem != null)
                memorySystem.RecordSpeech(speech);

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
                    chat.CreateMessageView(record, avatar);
                }

                chat.ScrollToBottom();
            }

            if (uiManager == null || !uiManager.IsCurrentChatVisible(characterId))
                IncrementUnread(characterId);

            ContactsChanged?.Invoke();
        }

        private void HandleStagePlanFinished(StagePlanFinishedEvent finished)
        {
            if (finished == null)
                return;
            if (finished.OwnerId != LlmRelay.LlmOwnerId)
                return;
            if (finished.RequestId <= 0)
                return;
            if (IsClearedRequest(finished.RequestId))
                return;

            if (requestRegistry != null)
                requestRegistry.TrySetStatus(finished.RequestId, RequestStatus.Finished);
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
                if (requestRegistry != null)
                    requestRegistry.TrySetStatus(requestId, RequestStatus.Replaced);
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
                if (save)
                    historyStore.Append(characterId, record);
                chat.ScrollToBottom();
                ContactsChanged?.Invoke();
                return;
            }

            if (save)
                AddSystemMessage(characterId, text, status, requestId);
        }

        private void AddSystemMessage(string characterId, string text, string status, int requestId)
        {
            var record = MomotalkHistoryStore.CreateMessage("system", text, status, requestId, -1, -1);
            historyStore.Append(characterId, record);
            if (IsLoadedConversation(characterId))
            {
                chat.CreateMessageView(record, null);
                chat.ScrollToBottom();
            }

            ContactsChanged?.Invoke();
        }

        private void RebuildPendingTypingViews(string characterId, Sprite avatar, List<MomotalkChatMessageRecord> visibleMessages)
        {
            if (string.IsNullOrWhiteSpace(characterId) || requestRegistry == null)
                return;

            // Non-terminal (Pending/Playing) requests are the ones still awaiting a
            // visible response; Momotalk owns the pending typing visual for them.
            requestIdBuffer.Clear();
            requestRegistry.GetNonTerminalRequestIds(characterId, requestIdBuffer);

            for (var i = 0; i < requestIdBuffer.Count; i++)
            {
                var requestId = requestIdBuffer[i];
                if (HasResponseForRequest(requestId, visibleMessages))
                    continue;

                typingViews[requestId] = chat.CreateTypingView(requestId, avatar);
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
                return;

            typingViews.Remove(requestId);
            if (view != null)
                Destroy(view.gameObject);
        }

        private void CancelLlmForCharacter(string characterId)
        {
            var playingRequestId = stagePlanPlayer != null ? stagePlanPlayer.CurrentLlmStagePlanRequestId : 0;
            var playingCharacterId = playingRequestId > 0 ? GetCharacterIdForRequest(playingRequestId) : string.Empty;
            var shouldStopPlaying = playingRequestId > 0
                && (string.IsNullOrWhiteSpace(playingCharacterId) || SameCharacter(playingCharacterId, characterId));

            // Snapshot the character's in-flight requests before they are canceled.
            requestIdBuffer.Clear();
            if (requestRegistry != null)
                requestRegistry.GetNonTerminalRequestIds(characterId, requestIdBuffer);

            if (requestRegistry != null)
                requestRegistry.CancelCharacter(characterId);

            var removedAnyPendingRequest = false;
            for (var i = 0; i < requestIdBuffer.Count; i++)
            {
                var requestId = requestIdBuffer[i];
                if (typingViews.ContainsKey(requestId))
                    removedAnyPendingRequest = true;
                RemoveTyping(requestId);
            }

            if (removedAnyPendingRequest && llmRelay != null)
                llmRelay.StopPendingRequest();

            if (!shouldStopPlaying)
                return;

            if (requestRegistry != null)
                requestRegistry.TrySetStatus(playingRequestId, RequestStatus.Canceled);
            if (llmRelay != null)
                llmRelay.StopLlmStagePlan();
        }

        private bool IsLoadedConversation(string characterId)
        {
            return string.Equals(GetCharacterId(currentContext), characterId, System.StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureSubscriptions()
        {
            if (llmRelay != null && !subscribedToLlmRelay)
            {
                llmRelay.RequestFailed += HandleLlmRequestFailed;
                subscribedToLlmRelay = true;
            }

            if (stagePlanPlayer != null && !subscribedToStagePlanPlayer)
            {
                stagePlanPlayer.SpeechActionStarted += HandleSpeechActionStarted;
                stagePlanPlayer.StagePlanFinished += HandleStagePlanFinished;
                subscribedToStagePlanPlayer = true;
            }

            if (asrManager != null && !subscribedToAsrManager)
            {
                asrManager.RecognitionFinished += HandleAsrRecognitionFinished;
                subscribedToAsrManager = true;
            }
        }

        private void RefreshVoiceModeFromAsr()
        {
            if (asrManager == null)
                return;

            switch (asrManager.Status)
            {
                case AsrRecognitionStatus.Listening:
                    voiceMode.Show("Listening", "Listening for your voice...", false);
                    break;
                case AsrRecognitionStatus.Recognizing:
                    voiceMode.Show("Recognizing", "Recognizing speech...", false);
                    break;
                case AsrRecognitionStatus.Error:
                    voiceMode.Show("ASR error", asrManager.LatestError, true);
                    break;
            }

            chat.RefreshMicInteractable(asrManager.Active);
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
            return requestRegistry != null ? requestRegistry.GetCharacterId(requestId) : string.Empty;
        }

        private bool IsClearedRequest(int requestId)
        {
            return requestRegistry != null
                && requestRegistry.TryGet(requestId, out var request)
                && request.Status == RequestStatus.Canceled;
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
