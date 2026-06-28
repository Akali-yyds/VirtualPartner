using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneNavigationBarView : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button recentButton;

        private void Awake()
        {
            BindButton(backButton, "Back");
            BindButton(homeButton, "Home");
            BindButton(recentButton, "Recent");
        }

        private void BindButton(Button button, string actionName)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Debug.Log($"[PhoneOS] Navigation clicked: {actionName}", this));
        }
    }
}
