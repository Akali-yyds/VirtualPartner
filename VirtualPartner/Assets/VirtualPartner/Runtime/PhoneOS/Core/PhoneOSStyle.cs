using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [CreateAssetMenu(fileName = "PhoneOSStyle", menuName = "Virtual Partner/Phone OS/Style")]
    public sealed class PhoneOSStyle : ScriptableObject
    {
        [SerializeField] private Vector2 phoneReferenceSize = new Vector2(440f, 960f);
        [SerializeField] private Sprite wallpaperSprite;
        [SerializeField] private Sprite roundedPanelSprite;
        [SerializeField] private Sprite navigationBackIcon;
        [SerializeField] private Sprite navigationHomeIcon;
        [SerializeField] private Sprite navigationRecentIcon;
        [SerializeField] private Color primaryTextColor = new Color32(0x24, 0x28, 0x2C, 0xFF);
        [SerializeField] private Color secondaryTextColor = new Color32(0x66, 0x6A, 0x70, 0xFF);
        [SerializeField] private Color mutedTextColor = new Color32(0x8A, 0x8D, 0x93, 0xFF);
        [SerializeField] private Color panelColor = new Color(1f, 1f, 1f, 0.94f);
        [SerializeField] private Color navigationIconColor = new Color32(0x18, 0x1A, 0x1D, 0xEA);
        [SerializeField] private Color dockIconLabelColor = new Color32(0x24, 0x28, 0x2C, 0xFF);
        [SerializeField] private Vector2 appIconSize = new Vector2(52f, 52f);
        [SerializeField] private Vector2 appGridCellSize = new Vector2(96f, 104f);
        [SerializeField] private Vector2 dockIconSize = new Vector2(52f, 52f);
        [SerializeField] private Vector2 dockCellSize = new Vector2(96f, 104f);
        [SerializeField] private float appIconLabelFontSize = 12f;
        [SerializeField] private float statusBarFontSize = 10f;
        [SerializeField] private float widgetSmallFontSize = 12f;
        [SerializeField] private float widgetMediumFontSize = 16f;
        [SerializeField] private float clockLargeFontSize = 60f;

        public Vector2 PhoneReferenceSize => phoneReferenceSize;
        public Sprite WallpaperSprite => wallpaperSprite;
        public Sprite RoundedPanelSprite => roundedPanelSprite;
        public Sprite NavigationBackIcon => navigationBackIcon;
        public Sprite NavigationHomeIcon => navigationHomeIcon;
        public Sprite NavigationRecentIcon => navigationRecentIcon;
        public Color PrimaryTextColor => primaryTextColor;
        public Color SecondaryTextColor => secondaryTextColor;
        public Color MutedTextColor => mutedTextColor;
        public Color PanelColor => panelColor;
        public Color NavigationIconColor => navigationIconColor;
        public Color DockIconLabelColor => dockIconLabelColor;
        public Vector2 AppIconSize => appIconSize;
        public Vector2 AppGridCellSize => appGridCellSize;
        public Vector2 DockIconSize => dockIconSize;
        public Vector2 DockCellSize => dockCellSize;
        public float AppIconLabelFontSize => appIconLabelFontSize;
        public float StatusBarFontSize => statusBarFontSize;
        public float WidgetSmallFontSize => widgetSmallFontSize;
        public float WidgetMediumFontSize => widgetMediumFontSize;
        public float ClockLargeFontSize => clockLargeFontSize;
    }
}
