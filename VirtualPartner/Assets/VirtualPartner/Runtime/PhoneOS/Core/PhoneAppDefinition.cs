using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [CreateAssetMenu(fileName = "PhoneAppDefinition", menuName = "Virtual Partner/Phone OS/App Definition")]
    public sealed class PhoneAppDefinition : ScriptableObject
    {
        [SerializeField] private string appId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private bool showOnHomeScreen = true;
        [SerializeField] private bool showInDock;
        [SerializeField] private int order;

        public string AppId => appId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public bool ShowOnHomeScreen => showOnHomeScreen;
        public bool ShowInDock => showInDock;
        public int Order => order;
    }
}
