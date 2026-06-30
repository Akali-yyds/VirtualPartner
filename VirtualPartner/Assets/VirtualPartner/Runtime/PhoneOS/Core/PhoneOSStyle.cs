using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [CreateAssetMenu(fileName = "PhoneOSStyle", menuName = "Virtual Partner/Phone OS/Style")]
    public sealed class PhoneOSStyle : ScriptableObject
    {
        [SerializeField] private Vector2 phoneReferenceSize = new Vector2(440f, 960f);
        [SerializeField] private Sprite wallpaperSprite;
        [SerializeField] private Sprite roundedPanelSprite;
        [SerializeField] private Color primaryTextColor = new Color32(0x24, 0x27, 0x30, 0xFF);
        [SerializeField] private Color secondaryTextColor = new Color32(0x69, 0x6D, 0x78, 0xFF);
        [SerializeField] private Color panelColor = new Color(1f, 1f, 1f, 0.90f);
        [SerializeField] private Vector2 appIconSize = new Vector2(52f, 52f);
        [SerializeField] private Vector2 appGridCellSize = new Vector2(96f, 104f);

        public Vector2 PhoneReferenceSize => phoneReferenceSize;
        public Sprite WallpaperSprite => wallpaperSprite;
        public Sprite RoundedPanelSprite => roundedPanelSprite;
        public Color PrimaryTextColor => primaryTextColor;
        public Color SecondaryTextColor => secondaryTextColor;
        public Color PanelColor => panelColor;
        public Vector2 AppIconSize => appIconSize;
        public Vector2 AppGridCellSize => appGridCellSize;
    }
}
