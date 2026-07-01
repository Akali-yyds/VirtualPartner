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
        [SerializeField] private Color topBarColor = new Color32(0xF7, 0x8F, 0xB3, 0xFF);
        [SerializeField] private Color contactBackgroundColor = new Color32(0xFF, 0xF7, 0xFA, 0xFF);
        [SerializeField] private Color contactItemColor = Color.white;
        [SerializeField] private Color leftBubbleColor = Color.white;
        [SerializeField] private Color rightBubbleColor = new Color32(0xFF, 0xD4, 0xE2, 0xFF);
        [SerializeField] private Color inputBarColor = new Color32(0xFF, 0xF7, 0xFA, 0xFF);
        [SerializeField] private Color primaryTextColor = new Color32(0x24, 0x28, 0x2C, 0xFF);
        [SerializeField] private Color secondaryTextColor = new Color32(0x7A, 0x7F, 0x87, 0xFF);
        [SerializeField] private Color unreadBadgeColor = new Color32(0xFF, 0x6F, 0x9F, 0xFF);

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
