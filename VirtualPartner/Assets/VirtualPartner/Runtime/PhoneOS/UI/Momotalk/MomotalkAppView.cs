using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class MomotalkAppView : MonoBehaviour, IPhoneApp
    {
        private enum MomotalkPage
        {
            ContactList,
            Chat
        }

        [SerializeField] private string appId = "momotalk";
        [SerializeField] private PhoneAppWindowView windowView;
        [SerializeField] private GameObject contactListPage;
        [SerializeField] private GameObject chatPage;
        [SerializeField] private MomotalkContactListView contactListView;
        [SerializeField] private MomotalkChatView chatView;

        private MomotalkPage currentPage = MomotalkPage.ContactList;

        public string AppId => appId;

        public void OnOpen(object args = null)
        {
            ResolveReferences();
            BindViews();

            if (windowView != null)
            {
                windowView.SetTitle("Momotalk");
                windowView.SetDescription(string.Empty);
            }

            ShowContactList();
        }

        public void OnClose()
        {
            ShowContactList();
        }

        public void OnPause()
        {
        }

        public void OnResume()
        {
        }

        public bool OnBackPressed()
        {
            if (currentPage == MomotalkPage.Chat)
            {
                ShowContactList();
                return true;
            }

            return false;
        }

        public void OpenChat(string contactId, string displayName)
        {
            var safeName = string.IsNullOrWhiteSpace(displayName) ? "Toki" : displayName;
            if (chatView != null)
                chatView.SetContact(safeName);

            ShowChat();
        }

        private void ResolveReferences()
        {
            if (windowView == null)
                windowView = GetComponent<PhoneAppWindowView>();
            if (contactListView == null)
                contactListView = GetComponentInChildren<MomotalkContactListView>(true);
            if (chatView == null)
                chatView = GetComponentInChildren<MomotalkChatView>(true);
            if (contactListPage == null && contactListView != null)
                contactListPage = contactListView.gameObject;
            if (chatPage == null && chatView != null)
                chatPage = chatView.gameObject;
        }

        private void BindViews()
        {
            if (contactListView != null)
                contactListView.Bind(OpenChat);
            if (chatView != null)
                chatView.SetContact("Toki");
        }

        private void ShowContactList()
        {
            currentPage = MomotalkPage.ContactList;
            SetPageActive(contactListPage, true);
            SetPageActive(chatPage, false);

            if (windowView != null)
                windowView.SetTitle("Momotalk");
        }

        private void ShowChat()
        {
            currentPage = MomotalkPage.Chat;
            SetPageActive(contactListPage, false);
            SetPageActive(chatPage, true);

            if (windowView != null)
                windowView.SetTitle("Toki");
        }

        private static void SetPageActive(GameObject page, bool active)
        {
            if (page != null)
                page.SetActive(active);
        }
    }
}
