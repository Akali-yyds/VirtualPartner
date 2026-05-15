using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    public sealed class MomotalkUIManager : MonoBehaviour
    {
        private enum MomotalkPage
        {
            ContactList,
            Chat,
            ChatInfo
        }

        [Header("Timing")]
        [SerializeField] private float loadingDuration = 0.8f;
        [SerializeField] private float slideDuration = 0.28f;
        [SerializeField] private float pageFadeDuration = 0.16f;
        [SerializeField] private bool restoreLastPageInPlay = true;

        [Header("Layout")]
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private RectTransform phonePanel;
        [SerializeField] private float phoneHeightRatio = 0.9f;
        [SerializeField] private float phoneAspectWidthOverHeight = 9f / 16f;
        [SerializeField] private float rightMargin = 24f;
        [SerializeField] private float phoneLogicalHeight = 1920f;

        [Header("Buttons")]
        [SerializeField] private GameObject openButtonRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject outsideCloseOverlay;
        [SerializeField] private Button outsideCloseButton;
        [SerializeField] private Button backButton;

        [Header("Views")]
        [SerializeField] private CanvasGroup loadingView;
        [SerializeField] private Image loadingIcon;
        [SerializeField] private CanvasGroup contactListView;
        [SerializeField] private CanvasGroup chatView;
        [SerializeField] private CanvasGroup chatInfoView;

        [Header("Contacts")]
        [SerializeField] private RectTransform contactListRoot;
        [SerializeField] private MomotalkContactItemView contactItemTemplate;
        [SerializeField] private Text emptyContactsText;

        [Header("Chat")]
        [SerializeField] private Image chatAvatarImage;
        [SerializeField] private Text chatNameText;
        [SerializeField] private Text chatStatusText;

        [Header("Conversation")]
        [SerializeField] private int llmHistoryContextMessageCount;
        [SerializeField] private bool showReplacedSystemMessage = true;
        [SerializeField] private MomotalkSceneSpeechBubbleMode sceneSpeechBubbleModeWhenMomotalkOpen = MomotalkSceneSpeechBubbleMode.KeepVisible;
        [SerializeField] private Image openButtonUnreadDot;

        private readonly List<CharacterRuntimeContext> contexts = new List<CharacterRuntimeContext>();
        private readonly List<GameObject> contactItems = new List<GameObject>();
        private readonly MomotalkContactSettingsStore contactSettingsStore = new MomotalkContactSettingsStore();
        private MomotalkConversationController conversationController;
        private Coroutine activeRoutine;
        private MomotalkPage lastPage = MomotalkPage.ContactList;
        private MomotalkPage visiblePage = MomotalkPage.ContactList;
        private CharacterRuntimeContext selectedContext;
        private InputField contactSearchInput;
        private Button moreButton;
        private Button chatInfoBackButton;
        private Toggle stickyToggle;
        private Image stickyToggleTrack;
        private RectTransform stickyToggleKnob;
        private Button clearHistoryButton;
        private CanvasGroup clearConfirmView;
        private Text chatInfoNameText;
        private Image chatInfoAvatarImage;
        private Sprite pinIconSprite;
        private string contactSearchText = string.Empty;
        private bool isOpen;
        private bool loadingVisible;
        private bool suppressStickyCallback;
        private float openX;
        private float closedX;

        public float LoadingDuration => loadingDuration;
        public bool RestoreLastPageInPlay => restoreLastPageInPlay;
        public bool IsOpen => isOpen;
        public int LlmHistoryContextMessageCount => Mathf.Max(0, llmHistoryContextMessageCount);
        public bool ShowReplacedSystemMessage => showReplacedSystemMessage;

        private void Awake()
        {
            if (canvasRoot == null)
                canvasRoot = transform as RectTransform;

            if (openButton != null)
                openButton.onClick.AddListener(Open);
            if (outsideCloseButton != null)
                outsideCloseButton.onClick.AddListener(Close);
            if (backButton != null)
                backButton.onClick.AddListener(ShowContactList);

            if (contactItemTemplate != null)
                contactItemTemplate.gameObject.SetActive(false);

            conversationController = GetComponent<MomotalkConversationController>();
            if (conversationController == null)
                conversationController = gameObject.AddComponent<MomotalkConversationController>();
            conversationController.Configure(this, chatView);
            conversationController.ContactsChanged += HandleConversationContactsChanged;
            pinIconSprite = LoadIconSprite("VirtualPartner/UI/Momotalk/Icons/pin_icon.png") ?? CreateFallbackPinSprite();
            EnsureContactSearchInput();
            EnsureMoreButton();
            EnsureChatInfoView();
            EnsureOpenButtonUnreadDot();

            ApplyPhoneLayout();
            SetClosedImmediate();
        }

        private void OnDestroy()
        {
            if (openButton != null)
                openButton.onClick.RemoveListener(Open);
            if (outsideCloseButton != null)
                outsideCloseButton.onClick.RemoveListener(Close);
            if (backButton != null)
                backButton.onClick.RemoveListener(ShowContactList);
            if (contactSearchInput != null)
                contactSearchInput.onValueChanged.RemoveListener(HandleSearchChanged);
            if (moreButton != null)
                moreButton.onClick.RemoveListener(ShowChatInfo);
            if (chatInfoBackButton != null)
                chatInfoBackButton.onClick.RemoveListener(ShowChatFromInfo);
            if (stickyToggle != null)
                stickyToggle.onValueChanged.RemoveListener(HandleStickyChanged);
            if (clearHistoryButton != null)
                clearHistoryButton.onClick.RemoveListener(ShowClearHistoryConfirmation);
            if (conversationController != null)
                conversationController.ContactsChanged -= HandleConversationContactsChanged;
        }

        private void Update()
        {
            ApplyPhoneLayout();

            if (loadingVisible && loadingIcon != null)
            {
                var pulse = 1f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.035f;
                loadingIcon.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        public void Open()
        {
            if (activeRoutine != null)
                StopCoroutine(activeRoutine);

            activeRoutine = StartCoroutine(OpenRoutine());
        }

        public void Close()
        {
            if (!isOpen && (phonePanel == null || !phonePanel.gameObject.activeSelf))
                return;

            if (activeRoutine != null)
                StopCoroutine(activeRoutine);

            activeRoutine = StartCoroutine(CloseRoutine());
        }

        public void ShowContactList()
        {
            selectedContext = null;
            lastPage = MomotalkPage.ContactList;
            ShowPage(MomotalkPage.ContactList, true);
        }

        private void ShowChatInfo()
        {
            if (selectedContext == null)
                return;

            ShowPage(MomotalkPage.ChatInfo, true);
        }

        private void ShowChatFromInfo()
        {
            if (selectedContext == null)
            {
                ShowContactList();
                return;
            }

            ShowPage(MomotalkPage.Chat, true);
        }

        private IEnumerator OpenRoutine()
        {
            isOpen = true;
            ApplyPhoneLayout();

            SetActive(openButtonRoot, false);
            SetActive(outsideCloseOverlay, true);
            SetActive(phonePanel != null ? phonePanel.gameObject : null, true);
            if (conversationController != null)
                conversationController.SetPhoneOpen(true, sceneSpeechBubbleModeWhenMomotalkOpen);
            ShowOnly(loadingView);
            loadingVisible = true;

            var loadingEnd = Time.unscaledTime + Mathf.Max(0f, loadingDuration);
            yield return AnimatePanel(phonePanel.anchoredPosition.x, openX);

            while (Time.unscaledTime < loadingEnd)
                yield return null;

            loadingVisible = false;
            var pageToShow = restoreLastPageInPlay ? lastPage : MomotalkPage.ContactList;
            if (pageToShow == MomotalkPage.Chat && selectedContext == null)
                pageToShow = MomotalkPage.ContactList;

            ShowPage(pageToShow, false);
            activeRoutine = null;
        }

        private IEnumerator CloseRoutine()
        {
            isOpen = false;
            loadingVisible = false;
            SetActive(outsideCloseOverlay, false);
            SetClearConfirmationVisible(false);
            if (conversationController != null)
                conversationController.SetPhoneOpen(false, sceneSpeechBubbleModeWhenMomotalkOpen);

            yield return AnimatePanel(phonePanel != null ? phonePanel.anchoredPosition.x : closedX, closedX);

            SetActive(phonePanel != null ? phonePanel.gameObject : null, false);
            SetActive(openButtonRoot, true);
            UpdateOpenButtonUnreadDot();
            activeRoutine = null;
        }

        private IEnumerator AnimatePanel(float fromX, float toX)
        {
            if (phonePanel == null)
                yield break;

            var duration = Mathf.Max(0.01f, slideDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                SetPanelX(Mathf.Lerp(fromX, toX, t));
                yield return null;
            }

            SetPanelX(toX);
        }

        private void ShowPage(MomotalkPage page, bool animate)
        {
            var previousPage = visiblePage;
            visiblePage = page;
            if (page != MomotalkPage.ChatInfo)
                lastPage = page;

            if (page == MomotalkPage.ContactList)
                RefreshContacts();
            else if (page == MomotalkPage.Chat)
            {
                BindChatHeader(selectedContext);
                if (conversationController != null)
                    conversationController.ShowConversation(selectedContext);
            }
            else
            {
                BindChatInfo(selectedContext);
            }

            if (animate && gameObject.activeInHierarchy)
                StartCoroutine(FadeToPage(previousPage, page));
            else
                SetPageImmediate(page);
        }

        private IEnumerator FadeToPage(MomotalkPage fromPage, MomotalkPage toPage)
        {
            var from = GetPageGroup(fromPage);
            var to = GetPageGroup(toPage);
            SetCanvasGroupVisible(loadingView, false, 0f);
            SetCanvasGroupVisible(to, true, 0f);

            var duration = Mathf.Max(0.01f, pageFadeDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                SetCanvasGroupVisible(from, false, 1f - t);
                SetCanvasGroupVisible(to, true, t);
                yield return null;
            }

            SetPageImmediate(toPage);
        }

        private void SetPageImmediate(MomotalkPage page)
        {
            SetCanvasGroupVisible(loadingView, false, 0f);
            SetCanvasGroupVisible(contactListView, page == MomotalkPage.ContactList, page == MomotalkPage.ContactList ? 1f : 0f);
            SetCanvasGroupVisible(chatView, page == MomotalkPage.Chat, page == MomotalkPage.Chat ? 1f : 0f);
            SetCanvasGroupVisible(chatInfoView, page == MomotalkPage.ChatInfo, page == MomotalkPage.ChatInfo ? 1f : 0f);
            if (page != MomotalkPage.ChatInfo)
                SetClearConfirmationVisible(false);
        }

        private void ShowOnly(CanvasGroup view)
        {
            SetCanvasGroupVisible(loadingView, loadingView == view, loadingView == view ? 1f : 0f);
            SetCanvasGroupVisible(contactListView, contactListView == view, contactListView == view ? 1f : 0f);
            SetCanvasGroupVisible(chatView, chatView == view, chatView == view ? 1f : 0f);
            SetCanvasGroupVisible(chatInfoView, chatInfoView == view, chatInfoView == view ? 1f : 0f);
            if (view != chatInfoView)
                SetClearConfirmationVisible(false);
        }

        private void RefreshContacts()
        {
            ClearContacts();
            CharacterRegistry.GetRegisteredContexts(contexts);

            if (contexts.Count == 0)
            {
                if (emptyContactsText != null)
                {
                    emptyContactsText.text = "No registered character";
                    emptyContactsText.gameObject.SetActive(true);
                }

                Debug.LogWarning("[VirtualPartner] Momotalk contact list has no registered character.", this);
                return;
            }

            if (emptyContactsText != null)
                emptyContactsText.gameObject.SetActive(false);

            var addedAny = false;
            AddContactItems(true, ref addedAny);
            AddContactItems(false, ref addedAny);

            if (!addedAny && emptyContactsText != null)
            {
                emptyContactsText.text = "No matching character";
                emptyContactsText.gameObject.SetActive(true);
            }

            UpdateOpenButtonUnreadDot();
        }

        private void AddContactItems(bool sticky, ref bool addedAny)
        {
            for (var i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                if (context == null || context.Profile == null || contactItemTemplate == null || contactListRoot == null)
                    continue;

                var isSticky = contactSettingsStore.IsSticky(context.CharacterId);
                if (isSticky != sticky)
                    continue;

                var summary = conversationController != null
                    ? conversationController.GetContactSummary(context)
                    : context.Profile.MomotalkStatus;
                if (!MatchesContactSearch(context, summary))
                    continue;

                var item = Instantiate(contactItemTemplate, contactListRoot);
                item.gameObject.SetActive(true);
                item.Configure(context, HandleContactClicked);
                item.SetStickyState(isSticky, pinIconSprite);
                if (conversationController != null)
                {
                    item.SetConversationState(
                        summary,
                        conversationController.GetUnreadCount(context));
                }
                contactItems.Add(item.gameObject);
                addedAny = true;
            }
        }

        private void ClearContacts()
        {
            for (var i = 0; i < contactItems.Count; i++)
            {
                if (contactItems[i] != null)
                    Destroy(contactItems[i]);
            }

            contactItems.Clear();
        }

        private void HandleContactClicked(CharacterRuntimeContext context)
        {
            selectedContext = context;
            lastPage = MomotalkPage.Chat;
            BindChatHeader(context);
            ShowPage(MomotalkPage.Chat, true);
        }

        private void BindChatHeader(CharacterRuntimeContext context)
        {
            var profile = context != null ? context.Profile : null;
            if (chatNameText != null)
                chatNameText.text = profile != null ? profile.DisplayName : string.Empty;
            if (chatStatusText != null)
                chatStatusText.text = profile != null ? profile.MomotalkStatus : string.Empty;
            if (chatAvatarImage != null)
                chatAvatarImage.gameObject.SetActive(false);
        }

        private void BindChatInfo(CharacterRuntimeContext context)
        {
            var profile = context != null ? context.Profile : null;
            if (chatInfoNameText != null)
                chatInfoNameText.text = profile != null ? profile.DisplayName : string.Empty;
            if (chatInfoAvatarImage != null)
            {
                var avatar = profile != null ? profile.AvatarIcon : null;
                MomotalkAvatarUtility.SetAvatar(chatInfoAvatarImage, avatar, avatar != null);
            }

            var sticky = context != null && contactSettingsStore.IsSticky(context.CharacterId);
            suppressStickyCallback = true;
            if (stickyToggle != null)
                stickyToggle.SetIsOnWithoutNotify(sticky);
            suppressStickyCallback = false;
            UpdateStickyToggleVisual(sticky);
        }

        private bool MatchesContactSearch(CharacterRuntimeContext context, string summary)
        {
            var term = contactSearchText == null ? string.Empty : contactSearchText.Trim();
            if (string.IsNullOrWhiteSpace(term))
                return true;

            var profile = context != null ? context.Profile : null;
            return ContainsSearchText(profile != null ? profile.DisplayName : string.Empty, term)
                || ContainsSearchText(context != null ? context.CharacterId : string.Empty, term)
                || ContainsSearchText(profile != null ? profile.MomotalkStatus : string.Empty, term)
                || ContainsSearchText(summary, term);
        }

        private static bool ContainsSearchText(string value, string term)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsureContactSearchInput()
        {
            if (contactSearchInput != null || contactListView == null)
                return;

            var searchBar = FindChildRecursive(contactListView.transform, "SearchBar") as RectTransform;
            if (searchBar == null)
                return;

            contactSearchInput = searchBar.GetComponent<InputField>();
            if (contactSearchInput == null)
                contactSearchInput = searchBar.gameObject.AddComponent<InputField>();

            var placeholder = searchBar.Find("SearchPlaceholder") != null
                ? searchBar.Find("SearchPlaceholder").GetComponent<Text>()
                : null;
            if (placeholder != null)
            {
                placeholder.text = "Search";
                placeholder.raycastTarget = false;
                var placeholderRect = placeholder.transform as RectTransform;
                if (placeholderRect != null)
                {
                    placeholderRect.anchorMin = Vector2.zero;
                    placeholderRect.anchorMax = Vector2.one;
                    placeholderRect.offsetMin = new Vector2(96f, 0f);
                    placeholderRect.offsetMax = new Vector2(-32f, 0f);
                }
            }

            var inputTextRect = searchBar.Find("SearchInputText") as RectTransform;
            if (inputTextRect == null)
            {
                inputTextRect = new GameObject("SearchInputText", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
                inputTextRect.SetParent(searchBar, false);
            }

            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = new Vector2(96f, 0f);
            inputTextRect.offsetMax = new Vector2(-32f, 0f);

            var inputText = inputTextRect.GetComponent<Text>();
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.color = new Color(0.16f, 0.2f, 0.26f, 1f);
            inputText.font = GetUiFont();
            inputText.fontSize = 28;
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            inputText.verticalOverflow = VerticalWrapMode.Truncate;
            inputText.supportRichText = false;
            inputText.raycastTarget = false;

            contactSearchInput.textComponent = inputText;
            contactSearchInput.placeholder = placeholder;
            contactSearchInput.contentType = InputField.ContentType.Standard;
            contactSearchInput.lineType = InputField.LineType.SingleLine;
            contactSearchInput.interactable = true;
            contactSearchInput.onValueChanged.RemoveListener(HandleSearchChanged);
            contactSearchInput.onValueChanged.AddListener(HandleSearchChanged);

            var graphic = searchBar.GetComponent<Image>();
            if (graphic != null)
            {
                graphic.raycastTarget = true;
                contactSearchInput.targetGraphic = graphic;
            }
        }

        private void EnsureMoreButton()
        {
            if (moreButton != null || chatView == null)
                return;

            var topBar = FindChildRecursive(chatView.transform, "TopBar") as RectTransform;
            if (topBar == null)
                return;

            var buttonRect = topBar.Find("MoreButton") as RectTransform;
            if (buttonRect == null)
            {
                buttonRect = new GameObject("MoreButton", typeof(RectTransform), typeof(Button), typeof(MomotalkUIButtonFeedback)).GetComponent<RectTransform>();
                buttonRect.SetParent(topBar, false);
            }

            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-90f, 0f);
            buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 78f);
            buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 78f);

            var label = buttonRect.Find("MoreText") != null
                ? buttonRect.Find("MoreText").GetComponent<Text>()
                : null;
            if (label == null)
            {
                label = new GameObject("MoreText", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                label.transform.SetParent(buttonRect, false);
            }

            var labelRect = label.transform as RectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.text = "···";
            label.font = GetUiFont();
            label.fontSize = 42;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = true;

            moreButton = buttonRect.GetComponent<Button>();
            moreButton.targetGraphic = label;
            moreButton.onClick.RemoveListener(ShowChatInfo);
            moreButton.onClick.AddListener(ShowChatInfo);
        }

        private void EnsureChatInfoView()
        {
            if (chatInfoView != null || chatView == null)
                return;

            var parent = chatView.transform.parent as RectTransform;
            if (parent == null)
                return;

            var root = new GameObject("ChatInfoView", typeof(RectTransform), typeof(CanvasGroup), typeof(Image)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.SetSiblingIndex(chatView.transform.GetSiblingIndex() + 1);
            root.GetComponent<Image>().color = new Color(0.97f, 0.98f, 1f, 1f);
            chatInfoView = root.GetComponent<CanvasGroup>();

            BuildChatInfoHeader(root);
            BuildChatInfoContent(root);
            BuildClearConfirmation(root);
            SetCanvasGroupVisible(chatInfoView, false, 0f);
        }

        private void BuildChatInfoHeader(RectTransform root)
        {
            var topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            topBar.SetParent(root, false);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.anchoredPosition = Vector2.zero;
            topBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 176f);
            topBar.GetComponent<Image>().color = new Color(0.965f, 0.545f, 0.647f, 1f);

            var backRect = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MomotalkUIButtonFeedback)).GetComponent<RectTransform>();
            backRect.SetParent(topBar, false);
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0.5f, 0.5f);
            backRect.anchoredPosition = new Vector2(92f, 0f);
            backRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 78f);
            backRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 78f);
            var backHitArea = backRect.GetComponent<Image>();
            backHitArea.color = new Color(1f, 1f, 1f, 0f);
            backHitArea.raycastTarget = true;
            var backText = CreateText(backRect, "BackText", "<", 48, Color.white, TextAnchor.MiddleCenter);
            chatInfoBackButton = backRect.GetComponent<Button>();
            chatInfoBackButton.targetGraphic = backHitArea;
            chatInfoBackButton.onClick.RemoveListener(ShowChatFromInfo);
            chatInfoBackButton.onClick.AddListener(ShowChatFromInfo);

            var title = CreateText(topBar, "Title", "Chat Info", 40, Color.white, TextAnchor.MiddleCenter);
            var titleRect = title.transform as RectTransform;
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(170f, 0f);
            titleRect.offsetMax = new Vector2(-170f, 0f);
        }

        private void BuildChatInfoContent(RectTransform root)
        {
            var avatarMask = new GameObject("Avatar", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<Image>();
            avatarMask.transform.SetParent(root, false);
            var avatarRect = avatarMask.transform as RectTransform;
            avatarRect.anchorMin = new Vector2(0.5f, 1f);
            avatarRect.anchorMax = new Vector2(0.5f, 1f);
            avatarRect.pivot = new Vector2(0.5f, 1f);
            avatarRect.anchoredPosition = new Vector2(0f, -260f);
            avatarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 420f);
            avatarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 420f);
            chatInfoAvatarImage = MomotalkAvatarUtility.EnsureCircularAvatarImage(avatarMask);

            chatInfoNameText = CreateText(root, "Name", string.Empty, 44, new Color(0.12f, 0.14f, 0.18f, 1f), TextAnchor.MiddleCenter);
            var nameRect = chatInfoNameText.transform as RectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -708f);
            nameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 760f);
            nameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 72f);

            BuildStickyRow(root, -820f);
            BuildClearHistoryRow(root, -990f);
        }

        private void BuildStickyRow(RectTransform root, float y)
        {
            var row = CreateInfoRow(root, "StickyRow", y);
            var iconCircle = CreateIconCircle(row, new Color(0.86f, 0.97f, 0.91f, 1f));
            var pin = new GameObject("PinIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            pin.transform.SetParent(iconCircle, false);
            var pinRect = pin.transform as RectTransform;
            pinRect.anchorMin = new Vector2(0.5f, 0.5f);
            pinRect.anchorMax = new Vector2(0.5f, 0.5f);
            pinRect.pivot = new Vector2(0.5f, 0.5f);
            pinRect.anchoredPosition = Vector2.zero;
            pinRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 42f);
            pinRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 42f);
            pin.sprite = pinIconSprite;
            pin.color = new Color(0.12f, 0.72f, 0.48f, 1f);
            pin.preserveAspect = true;
            pin.raycastTarget = false;

            CreateRowTitle(row, "Sticky on Top", "Keep this chat at the top of your chat list.");

            var toggleRect = new GameObject("StickyToggle", typeof(RectTransform), typeof(Toggle)).GetComponent<RectTransform>();
            toggleRect.SetParent(row, false);
            toggleRect.anchorMin = new Vector2(1f, 0.5f);
            toggleRect.anchorMax = new Vector2(1f, 0.5f);
            toggleRect.pivot = new Vector2(1f, 0.5f);
            toggleRect.anchoredPosition = new Vector2(-34f, 0f);
            toggleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 112f);
            toggleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 58f);

            stickyToggleTrack = new GameObject("Track", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            stickyToggleTrack.transform.SetParent(toggleRect, false);
            var trackRect = stickyToggleTrack.transform as RectTransform;
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;
            stickyToggleTrack.raycastTarget = true;

            stickyToggleKnob = new GameObject("Knob", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            stickyToggleKnob.SetParent(toggleRect, false);
            stickyToggleKnob.anchorMin = new Vector2(0.5f, 0.5f);
            stickyToggleKnob.anchorMax = new Vector2(0.5f, 0.5f);
            stickyToggleKnob.pivot = new Vector2(0.5f, 0.5f);
            stickyToggleKnob.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 46f);
            stickyToggleKnob.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 46f);
            var knobImage = stickyToggleKnob.GetComponent<Image>();
            knobImage.sprite = MomotalkChatMessageView.GetCircleMaskSprite();
            knobImage.color = Color.white;
            knobImage.raycastTarget = false;

            stickyToggle = toggleRect.GetComponent<Toggle>();
            stickyToggle.targetGraphic = stickyToggleTrack;
            stickyToggle.graphic = null;
            stickyToggle.onValueChanged.RemoveListener(HandleStickyChanged);
            stickyToggle.onValueChanged.AddListener(HandleStickyChanged);
            UpdateStickyToggleVisual(false);
        }

        private void BuildClearHistoryRow(RectTransform root, float y)
        {
            var row = CreateInfoRow(root, "ClearHistoryRow", y);
            var iconCircle = CreateIconCircle(row, new Color(1f, 0.9f, 0.91f, 1f));
            var clearMark = CreateText(iconCircle, "IconText", "X", 34, new Color(0.96f, 0.32f, 0.34f, 1f), TextAnchor.MiddleCenter);
            clearMark.raycastTarget = false;
            CreateRowTitle(row, "Clear Chat History", "Delete all messages in this chat.");
            var arrow = CreateText(row, "Arrow", ">", 42, new Color(0.62f, 0.64f, 0.68f, 1f), TextAnchor.MiddleCenter);
            var arrowRect = arrow.transform as RectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-34f, 0f);
            arrowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 60f);
            arrowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80f);

            clearHistoryButton = row.gameObject.AddComponent<Button>();
            clearHistoryButton.targetGraphic = row.GetComponent<Image>();
            clearHistoryButton.onClick.RemoveListener(ShowClearHistoryConfirmation);
            clearHistoryButton.onClick.AddListener(ShowClearHistoryConfirmation);
            row.gameObject.AddComponent<MomotalkUIButtonFeedback>();
        }

        private RectTransform CreateInfoRow(RectTransform root, string name, float y)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            row.SetParent(root, false);
            row.anchorMin = new Vector2(0.5f, 1f);
            row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, y);
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 900f);
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 140f);
            row.GetComponent<Image>().color = Color.white;
            return row;
        }

        private RectTransform CreateIconCircle(RectTransform row, Color color)
        {
            var iconCircle = new GameObject("IconCircle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            iconCircle.SetParent(row, false);
            iconCircle.anchorMin = new Vector2(0f, 0.5f);
            iconCircle.anchorMax = new Vector2(0f, 0.5f);
            iconCircle.pivot = new Vector2(0.5f, 0.5f);
            iconCircle.anchoredPosition = new Vector2(84f, 0f);
            iconCircle.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 82f);
            iconCircle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 82f);
            var image = iconCircle.GetComponent<Image>();
            image.sprite = MomotalkChatMessageView.GetCircleMaskSprite();
            image.color = color;
            image.raycastTarget = false;
            return iconCircle;
        }

        private void CreateRowTitle(RectTransform row, string title, string subtitle)
        {
            var titleText = CreateText(row, "Title", title, 32, new Color(0.08f, 0.1f, 0.14f, 1f), TextAnchor.MiddleLeft);
            var titleRect = titleText.transform as RectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 0.5f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(150f, 24f);
            titleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 560f);
            titleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50f);

            var subtitleText = CreateText(row, "Subtitle", subtitle, 24, new Color(0.42f, 0.45f, 0.5f, 1f), TextAnchor.MiddleLeft);
            var subtitleRect = subtitleText.transform as RectTransform;
            subtitleRect.anchorMin = new Vector2(0f, 0.5f);
            subtitleRect.anchorMax = new Vector2(1f, 0.5f);
            subtitleRect.pivot = new Vector2(0f, 0.5f);
            subtitleRect.anchoredPosition = new Vector2(150f, -24f);
            subtitleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 560f);
            subtitleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 44f);
        }

        private void BuildClearConfirmation(RectTransform root)
        {
            var overlay = new GameObject("ClearHistoryConfirm", typeof(RectTransform), typeof(CanvasGroup), typeof(Image)).GetComponent<RectTransform>();
            overlay.SetParent(root, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.32f);
            clearConfirmView = overlay.GetComponent<CanvasGroup>();

            var dialog = new GameObject("Dialog", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            dialog.SetParent(overlay, false);
            dialog.anchorMin = new Vector2(0.5f, 0.5f);
            dialog.anchorMax = new Vector2(0.5f, 0.5f);
            dialog.pivot = new Vector2(0.5f, 0.5f);
            dialog.anchoredPosition = Vector2.zero;
            dialog.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 720f);
            dialog.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 330f);
            dialog.GetComponent<Image>().color = Color.white;

            var title = CreateText(dialog, "Title", "Clear Chat History?", 34, new Color(0.1f, 0.12f, 0.16f, 1f), TextAnchor.MiddleCenter);
            var titleRect = title.transform as RectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -44f);
            titleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60f);

            var body = CreateText(dialog, "Body", "Delete all messages in this chat.", 25, new Color(0.42f, 0.45f, 0.5f, 1f), TextAnchor.MiddleCenter);
            var bodyRect = body.transform as RectTransform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.anchoredPosition = new Vector2(0f, -118f);
            bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60f);

            var cancelButton = CreateDialogButton(dialog, "CancelButton", "Cancel", new Vector2(-170f, -230f), new Color(0.92f, 0.93f, 0.95f, 1f), new Color(0.22f, 0.24f, 0.28f, 1f));
            cancelButton.onClick.AddListener(() => SetClearConfirmationVisible(false));

            var clearButton = CreateDialogButton(dialog, "ClearButton", "Clear", new Vector2(170f, -230f), new Color(0.96f, 0.32f, 0.34f, 1f), Color.white);
            clearButton.onClick.AddListener(ConfirmClearHistory);
            SetClearConfirmationVisible(false);
        }

        private Button CreateDialogButton(RectTransform parent, string name, string label, Vector2 position, Color background, Color textColor)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MomotalkUIButtonFeedback)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 230f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 72f);
            var image = rect.GetComponent<Image>();
            image.color = background;

            var text = CreateText(rect, "Text", label, 28, textColor, TextAnchor.MiddleCenter);
            var textRect = text.transform as RectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var button = rect.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private void HandleSearchChanged(string value)
        {
            contactSearchText = value ?? string.Empty;
            if (visiblePage == MomotalkPage.ContactList)
                RefreshContacts();
        }

        private void HandleStickyChanged(bool sticky)
        {
            if (suppressStickyCallback || selectedContext == null)
                return;

            contactSettingsStore.SetSticky(selectedContext.CharacterId, sticky);
            UpdateStickyToggleVisual(sticky);
            if (visiblePage == MomotalkPage.ContactList)
                RefreshContacts();
        }

        private void ShowClearHistoryConfirmation()
        {
            SetClearConfirmationVisible(true);
        }

        private void ConfirmClearHistory()
        {
            SetClearConfirmationVisible(false);
            if (conversationController != null && selectedContext != null)
                conversationController.ClearConversation(selectedContext);
            BindChatInfo(selectedContext);
            UpdateOpenButtonUnreadDot();
        }

        private void SetClearConfirmationVisible(bool visible)
        {
            SetCanvasGroupVisible(clearConfirmView, visible, visible ? 1f : 0f);
        }

        private void UpdateStickyToggleVisual(bool sticky)
        {
            if (stickyToggleTrack != null)
                stickyToggleTrack.color = sticky
                    ? new Color(0.12f, 0.78f, 0.42f, 1f)
                    : new Color(0.72f, 0.74f, 0.78f, 1f);
            if (stickyToggleKnob != null)
                stickyToggleKnob.anchoredPosition = new Vector2(sticky ? 26f : -26f, 0f);
        }

        private CanvasGroup GetPageGroup(MomotalkPage page)
        {
            if (page == MomotalkPage.ContactList)
                return contactListView;
            if (page == MomotalkPage.ChatInfo)
                return chatInfoView;

            return chatView;
        }

        private Text CreateText(Transform parent, string name, string text, int fontSize, Color color, TextAnchor alignment)
        {
            var textComponent = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            textComponent.transform.SetParent(parent, false);
            var rect = textComponent.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            textComponent.text = text;
            textComponent.font = GetUiFont();
            textComponent.fontSize = fontSize;
            textComponent.color = color;
            textComponent.alignment = alignment;
            textComponent.raycastTarget = false;
            textComponent.supportRichText = false;
            return textComponent;
        }

        private Font GetUiFont()
        {
            if (chatNameText != null && chatNameText.font != null)
                return chatNameText.font;
            if (emptyContactsText != null && emptyContactsText.font != null)
                return emptyContactsText.font;

            var text = GetComponentInChildren<Text>(true);
            return text != null ? text.font : null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == childName)
                    return child;

                var match = FindChildRecursive(child, childName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static Sprite LoadIconSprite(string relativeAssetPath)
        {
            var path = Path.Combine(Application.dataPath, relativeAssetPath);
            if (!File.Exists(path))
                return null;

            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                    return null;

                texture.wrapMode = TextureWrapMode.Clamp;
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VirtualPartner] Momotalk icon load failed: {exception.Message}");
                return null;
            }
        }

        private static Sprite CreateFallbackPinSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(1f, 1f, 1f, 0f);
            var white = Color.white;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var draw = false;
                    var headDistance = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 18f));
                    if (headDistance <= 13f)
                        draw = true;
                    if (x >= 18 && x <= 46 && y >= 28 && y <= 36)
                        draw = true;
                    if (x >= 28 && x <= 36 && y >= 34 && y <= 54)
                        draw = true;
                    if (Mathf.Abs((x - 32f) - (y - 52f) * 0.35f) < 3f && y >= 50 && y <= 62)
                        draw = true;

                    texture.SetPixel(x, y, draw ? white : clear);
                }
            }

            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void ApplyPhoneLayout()
        {
            if (canvasRoot == null || phonePanel == null)
                return;

            var rootHeight = canvasRoot.rect.height;
            if (rootHeight <= 0f)
                rootHeight = 1080f;

            var height = rootHeight * Mathf.Clamp(phoneHeightRatio, 0.1f, 1f);
            var logicalHeight = Mathf.Max(1f, phoneLogicalHeight);
            var logicalWidth = logicalHeight * phoneAspectWidthOverHeight;
            var scale = height / logicalHeight;
            var visualWidth = logicalWidth * scale;

            phonePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, logicalWidth);
            phonePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, logicalHeight);
            phonePanel.localScale = new Vector3(scale, scale, 1f);

            openX = -rightMargin - visualWidth * 0.5f;
            closedX = visualWidth * 0.5f + rightMargin;

            if (!isOpen && activeRoutine == null)
                SetPanelX(closedX);
        }

        private void SetClosedImmediate()
        {
            isOpen = false;
            loadingVisible = false;
            SetPanelX(closedX);
            SetActive(phonePanel != null ? phonePanel.gameObject : null, false);
            SetActive(outsideCloseOverlay, false);
            SetActive(openButtonRoot, true);
            if (conversationController != null)
                conversationController.SetPhoneOpen(false, sceneSpeechBubbleModeWhenMomotalkOpen);
            UpdateOpenButtonUnreadDot();
            visiblePage = MomotalkPage.ContactList;
            ShowOnly(contactListView);
        }

        private void SetPanelX(float x)
        {
            if (phonePanel == null)
                return;

            var position = phonePanel.anchoredPosition;
            position.x = x;
            phonePanel.anchoredPosition = position;
        }

        private static void SetCanvasGroupVisible(CanvasGroup group, bool visible, float alpha)
        {
            if (group == null)
                return;

            group.alpha = alpha;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            group.gameObject.SetActive(visible || alpha > 0f);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        public bool IsCurrentChatVisible(string characterId)
        {
            return isOpen
                && visiblePage == MomotalkPage.Chat
                && selectedContext != null
                && string.Equals(selectedContext.CharacterId, characterId, System.StringComparison.OrdinalIgnoreCase);
        }

        private void HandleConversationContactsChanged()
        {
            if (visiblePage == MomotalkPage.ContactList && contactListView != null && contactListView.gameObject.activeInHierarchy)
                RefreshContacts();

            UpdateOpenButtonUnreadDot();
        }

        private void EnsureOpenButtonUnreadDot()
        {
            if (openButtonUnreadDot == null)
            {
                if (openButtonRoot == null)
                    return;

                var dotObject = new GameObject("UnreadDot", typeof(RectTransform), typeof(Image));
                dotObject.transform.SetParent(openButtonRoot.transform, false);
                openButtonUnreadDot = dotObject.GetComponent<Image>();
            }

            var rect = openButtonUnreadDot.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-10f, -10f);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 22f);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 22f);
            }

            openButtonUnreadDot.sprite = MomotalkChatMessageView.GetCircleMaskSprite();
            openButtonUnreadDot.type = Image.Type.Simple;
            openButtonUnreadDot.preserveAspect = false;
            openButtonUnreadDot.color = new Color(0.95f, 0.18f, 0.28f, 1f);
            openButtonUnreadDot.raycastTarget = false;
        }

        private void UpdateOpenButtonUnreadDot()
        {
            EnsureOpenButtonUnreadDot();
            if (openButtonUnreadDot != null)
                openButtonUnreadDot.gameObject.SetActive(!isOpen && conversationController != null && conversationController.HasAnyUnread());
        }
    }
}
