using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [CreateAssetMenu(fileName = "MomotalkTheme", menuName = "Virtual Partner/Phone OS/Momotalk Theme")]
    public sealed class MomotalkTheme : ScriptableObject
    {
        [Header("Sprites")]
        [SerializeField] private Sprite topBarBackground;
        [SerializeField] private Sprite contactListBackground;
        [SerializeField] private Sprite contactItemBackground;
        [SerializeField] private Sprite chatBackgroundPattern;
        [SerializeField] private Sprite leftBubbleSprite;
        [SerializeField] private Sprite rightBubbleSprite;
        [SerializeField] private Sprite inputBarBackground;
        [SerializeField] private Sprite unreadBadgeSprite;
        [SerializeField] private Sprite fabBackgroundSprite;
        [SerializeField] private Sprite imageMessageMaskSprite;
        [SerializeField] private Sprite statusBadgeSprite;
        [SerializeField] private Sprite peachMarkSprite;

        [Header("Colors")]
        [SerializeField] private Color topBarColor = new Color32(0x12, 0x8C, 0x7E, 0xFF);
        [SerializeField] private Color appBarDarkColor = new Color32(0x07, 0x5E, 0x54, 0xFF);
        [SerializeField] private Color accentColor = new Color32(0x00, 0xA8, 0x84, 0xFF);
        [SerializeField] private Color contactBackgroundColor = Color.white;
        [SerializeField] private Color chatBackgroundColor = new Color32(0xEC, 0xE5, 0xDD, 0xFF);
        [SerializeField] private Color contactItemColor = Color.white;
        [SerializeField] private Color leftBubbleColor = Color.white;
        [SerializeField] private Color rightBubbleColor = new Color32(0xDC, 0xF8, 0xC6, 0xFF);
        [SerializeField] private Color inputBarColor = new Color32(0xF0, 0xF2, 0xF5, 0xFF);
        [SerializeField] private Color primaryTextColor = new Color32(0x20, 0x21, 0x24, 0xFF);
        [SerializeField] private Color secondaryTextColor = new Color32(0x66, 0x77, 0x81, 0xFF);
        [SerializeField] private Color mutedTextColor = new Color32(0x86, 0x96, 0xA0, 0xFF);
        [SerializeField] private Color dividerColor = new Color32(0xE9, 0xED, 0xEF, 0xFF);
        [SerializeField] private Color unreadBadgeColor = new Color32(0x25, 0xD3, 0x66, 0xFF);

        [Header("Layout")]
        [SerializeField] private float appBarHeight = 56f;
        [SerializeField] private float tabBarHeight = 48f;
        [SerializeField] private float contactItemHeight = 70f;
        [SerializeField] private float avatarSize = 48f;
        [SerializeField] private float chatAvatarSize = 40f;
        [SerializeField] private float unreadBadgeSize = 20f;
        [SerializeField] private float floatingActionButtonSize = 56f;
        [SerializeField] private float chatHorizontalPadding = 12f;
        [SerializeField] private float messageSpacing = 3f;
        [SerializeField] private float bubbleMinWidth = 68f;
        [SerializeField] private float bubbleMaxWidth = 336f;
        [SerializeField] private float bubbleHorizontalPadding = 10f;
        [SerializeField] private float bubbleVerticalPadding = 6f;
        [SerializeField] private float inputBarHeight = 58f;
        [SerializeField] private float inputPillHeight = 44f;
        [SerializeField] private float imageMessageMaxWidth = 328f;
        [SerializeField] private float imageMessageMaxHeight = 170f;

        [Header("Typography")]
        [SerializeField] private int titleFontSize = 20;
        [SerializeField] private int tabFontSize = 13;
        [SerializeField] private int contactNameFontSize = 16;
        [SerializeField] private int contactPreviewFontSize = 13;
        [SerializeField] private int messageFontSize = 14;
        [SerializeField] private int messageTimeFontSize = 11;
        [SerializeField] private int inputFontSize = 14;

        public Sprite TopBarBackground => topBarBackground;
        public Sprite ContactListBackground => contactListBackground;
        public Sprite ContactItemBackground => contactItemBackground;
        public Sprite ChatBackgroundPattern => chatBackgroundPattern;
        public Sprite LeftBubbleSprite => leftBubbleSprite;
        public Sprite RightBubbleSprite => rightBubbleSprite;
        public Sprite InputBarBackground => inputBarBackground;
        public Sprite UnreadBadgeSprite => unreadBadgeSprite;
        public Sprite FabBackgroundSprite => fabBackgroundSprite;
        public Sprite ImageMessageMaskSprite => imageMessageMaskSprite;
        public Sprite StatusBadgeSprite => statusBadgeSprite;
        public Sprite PeachMarkSprite => peachMarkSprite;
        public Color TopBarColor => topBarColor;
        public Color AppBarColor => topBarColor;
        public Color AppBarDarkColor => appBarDarkColor;
        public Color AccentColor => accentColor;
        public Color ContactBackgroundColor => contactBackgroundColor;
        public Color ChatBackgroundColor => chatBackgroundColor;
        public Color ContactItemColor => contactItemColor;
        public Color LeftBubbleColor => leftBubbleColor;
        public Color IncomingBubbleColor => leftBubbleColor;
        public Color RightBubbleColor => rightBubbleColor;
        public Color OutgoingBubbleColor => rightBubbleColor;
        public Color InputBarColor => inputBarColor;
        public Color PrimaryTextColor => primaryTextColor;
        public Color SecondaryTextColor => secondaryTextColor;
        public Color MutedTextColor => mutedTextColor;
        public Color DividerColor => dividerColor;
        public Color UnreadBadgeColor => unreadBadgeColor;
        public float AppBarHeight => Mathf.Max(44f, appBarHeight);
        public float TabBarHeight => Mathf.Max(36f, tabBarHeight);
        public float ContactItemHeight => Mathf.Max(56f, contactItemHeight);
        public float AvatarSize => Mathf.Max(32f, avatarSize);
        public float ChatAvatarSize => Mathf.Max(32f, chatAvatarSize);
        public float UnreadBadgeSize => Mathf.Max(16f, unreadBadgeSize);
        public float FloatingActionButtonSize => Mathf.Max(44f, floatingActionButtonSize);
        public float ChatHorizontalPadding => Mathf.Max(0f, chatHorizontalPadding);
        public float MessageSpacing => Mathf.Max(0f, messageSpacing);
        public float BubbleMinWidth => Mathf.Max(44f, bubbleMinWidth);
        public float BubbleMaxWidth => Mathf.Max(BubbleMinWidth, bubbleMaxWidth);
        public float BubbleHorizontalPadding => Mathf.Max(6f, bubbleHorizontalPadding);
        public float BubbleVerticalPadding => Mathf.Max(4f, bubbleVerticalPadding);
        public float InputBarHeight => Mathf.Max(52f, inputBarHeight);
        public float InputPillHeight => Mathf.Max(36f, inputPillHeight);
        public float ImageMessageMaxWidth => Mathf.Max(120f, imageMessageMaxWidth);
        public float ImageMessageMaxHeight => Mathf.Max(96f, imageMessageMaxHeight);
        public int TitleFontSize => Mathf.Max(1, titleFontSize);
        public int TabFontSize => Mathf.Max(1, tabFontSize);
        public int ContactNameFontSize => Mathf.Max(1, contactNameFontSize);
        public int ContactPreviewFontSize => Mathf.Max(1, contactPreviewFontSize);
        public int MessageFontSize => Mathf.Max(1, messageFontSize);
        public int MessageTimeFontSize => Mathf.Max(1, messageTimeFontSize);
        public int InputFontSize => Mathf.Max(1, inputFontSize);
    }
}
