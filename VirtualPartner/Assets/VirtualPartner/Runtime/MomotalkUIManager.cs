using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    public sealed class MomotalkUIManager : MonoBehaviour
    {
        private enum MomotalkPage
        {
            ContactList,
            Chat
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

        [Header("Contacts")]
        [SerializeField] private RectTransform contactListRoot;
        [SerializeField] private MomotalkContactItemView contactItemTemplate;
        [SerializeField] private Text emptyContactsText;

        [Header("Chat")]
        [SerializeField] private Image chatAvatarImage;
        [SerializeField] private Text chatNameText;
        [SerializeField] private Text chatStatusText;

        private readonly List<CharacterRuntimeContext> contexts = new List<CharacterRuntimeContext>();
        private readonly List<GameObject> contactItems = new List<GameObject>();
        private Coroutine activeRoutine;
        private MomotalkPage lastPage = MomotalkPage.ContactList;
        private MomotalkPage visiblePage = MomotalkPage.ContactList;
        private CharacterRuntimeContext selectedContext;
        private bool isOpen;
        private bool loadingVisible;
        private float openX;
        private float closedX;

        public float LoadingDuration => loadingDuration;
        public bool RestoreLastPageInPlay => restoreLastPageInPlay;
        public bool IsOpen => isOpen;

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

        private IEnumerator OpenRoutine()
        {
            isOpen = true;
            ApplyPhoneLayout();

            SetActive(openButtonRoot, false);
            SetActive(outsideCloseOverlay, true);
            SetActive(phonePanel != null ? phonePanel.gameObject : null, true);
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

            yield return AnimatePanel(phonePanel != null ? phonePanel.anchoredPosition.x : closedX, closedX);

            SetActive(phonePanel != null ? phonePanel.gameObject : null, false);
            SetActive(openButtonRoot, true);
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
            visiblePage = page;
            lastPage = page;

            if (page == MomotalkPage.ContactList)
                RefreshContacts();
            else
                BindChatHeader(selectedContext);

            if (animate && gameObject.activeInHierarchy)
                StartCoroutine(FadeToPage(page));
            else
                SetPageImmediate(page);
        }

        private IEnumerator FadeToPage(MomotalkPage page)
        {
            var from = page == MomotalkPage.ContactList ? chatView : contactListView;
            var to = page == MomotalkPage.ContactList ? contactListView : chatView;
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

            SetPageImmediate(page);
        }

        private void SetPageImmediate(MomotalkPage page)
        {
            SetCanvasGroupVisible(loadingView, false, 0f);
            SetCanvasGroupVisible(contactListView, page == MomotalkPage.ContactList, page == MomotalkPage.ContactList ? 1f : 0f);
            SetCanvasGroupVisible(chatView, page == MomotalkPage.Chat, page == MomotalkPage.Chat ? 1f : 0f);
        }

        private void ShowOnly(CanvasGroup view)
        {
            SetCanvasGroupVisible(loadingView, loadingView == view, loadingView == view ? 1f : 0f);
            SetCanvasGroupVisible(contactListView, contactListView == view, contactListView == view ? 1f : 0f);
            SetCanvasGroupVisible(chatView, chatView == view, chatView == view ? 1f : 0f);
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

            for (var i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                if (context == null || context.Profile == null || contactItemTemplate == null || contactListRoot == null)
                    continue;

                var item = Instantiate(contactItemTemplate, contactListRoot);
                item.gameObject.SetActive(true);
                item.Configure(context, HandleContactClicked);
                contactItems.Add(item.gameObject);
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
            {
                chatAvatarImage.sprite = profile != null ? profile.AvatarIcon : null;
                chatAvatarImage.enabled = chatAvatarImage.sprite != null;
            }
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
    }
}
