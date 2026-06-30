using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class EmptyPhoneAppView : MonoBehaviour, IPhoneApp
    {
        [SerializeField] private string appId;
        [SerializeField] private string title;
        [SerializeField] private string description;
        [SerializeField] private PhoneAppWindowView windowView;

        public string AppId => appId;

        public void OnOpen(object args = null)
        {
            if (windowView == null)
                windowView = GetComponent<PhoneAppWindowView>();

            if (windowView != null)
            {
                windowView.SetTitle(string.IsNullOrWhiteSpace(title) ? appId : title);
                windowView.SetDescription(description);
            }
        }

        public void OnClose()
        {
        }

        public void OnPause()
        {
        }

        public void OnResume()
        {
        }

        public bool OnBackPressed()
        {
            return false;
        }
    }
}
