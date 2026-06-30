using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneOSController : MonoBehaviour
    {
        [SerializeField] private PhoneAppHost appHost;

        public void HandleBackPressed()
        {
            if (appHost != null && appHost.HandleBackPressed())
            {
                Debug.Log("[PhoneOS] Navigation clicked: Back", this);
                return;
            }

            Debug.Log("[PhoneOS] Navigation clicked: Back", this);
        }

        public void HandleHomePressed()
        {
            if (appHost != null && appHost.HasCurrentApp)
            {
                appHost.CloseCurrentApp();
                Debug.Log("[PhoneOS] Navigation clicked: Home", this);
                return;
            }

            Debug.Log("[PhoneOS] Navigation clicked: Home", this);
        }

        public void HandleRecentPressed()
        {
            Debug.Log("[PhoneOS] Navigation clicked: Recent", this);
        }
    }
}
