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
        [SerializeField] private Sprite leftBubbleSprite;
        [SerializeField] private Sprite rightBubbleSprite;
        [SerializeField] private Sprite inputBarBackground;
        [SerializeField] private Sprite unreadBadgeSprite;
        [SerializeField] private Sprite statusBadgeSprite;
        [SerializeField] private Sprite peachMarkSprite;

        [Header("Colors")]
        [SerializeField] private Color topBarColor = new Color32(0xF7, 0x88, 0xA6, 0xFF);
        [SerializeField] private Color contactBackgroundColor = new Color32(0xFA, 0xFB, 0xFD, 0xFF);
        [SerializeField] private Color contactItemColor = new Color32(0xE5, 0xEA, 0xEF, 0xFF);
        [SerializeField] private Color leftBubbleColor = new Color32(0x56, 0x66, 0x7C, 0xFF);
        [SerializeField] private Color rightBubbleColor = new Color32(0x5A, 0x98, 0xD4, 0xFF);
        [SerializeField] private Color inputBarColor = new Color32(0xF0, 0xF1, 0xF3, 0xFF);
        [SerializeField] private Color primaryTextColor = new Color32(0x2B, 0x33, 0x42, 0xFF);
        [SerializeField] private Color secondaryTextColor = new Color32(0x8C, 0x95, 0xA3, 0xFF);
        [SerializeField] private Color unreadBadgeColor = new Color32(0xF7, 0x88, 0xA6, 0xFF);

        public Sprite TopBarBackground => topBarBackground;
        public Sprite ContactListBackground => contactListBackground;
        public Sprite ContactItemBackground => contactItemBackground;
        public Sprite LeftBubbleSprite => leftBubbleSprite;
        public Sprite RightBubbleSprite => rightBubbleSprite;
        public Sprite InputBarBackground => inputBarBackground;
        public Sprite UnreadBadgeSprite => unreadBadgeSprite;
        public Sprite StatusBadgeSprite => statusBadgeSprite;
        public Sprite PeachMarkSprite => peachMarkSprite;
        public Color TopBarColor => topBarColor;
        public Color ContactBackgroundColor => contactBackgroundColor;
        public Color ContactItemColor => contactItemColor;
        public Color LeftBubbleColor => leftBubbleColor;
        public Color RightBubbleColor => rightBubbleColor;
        public Color InputBarColor => inputBarColor;
        public Color PrimaryTextColor => primaryTextColor;
        public Color SecondaryTextColor => secondaryTextColor;
        public Color UnreadBadgeColor => unreadBadgeColor;
    }
}
