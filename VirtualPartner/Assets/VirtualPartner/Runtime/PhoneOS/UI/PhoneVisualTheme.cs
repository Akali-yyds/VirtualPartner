using TMPro;
using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [CreateAssetMenu(menuName="Virtual Partner/Phone OS/Visual Theme")]
    public sealed class PhoneVisualTheme : ScriptableObject
    {
        public TMP_FontAsset regular, semibold, light;
        public Color ink=new Color32(34,38,46,255), muted=new Color32(100,106,116,255);
        public Color paper=new Color32(248,249,251,255), chat=new Color32(244,245,248,255);
        public Color pink=new Color32(185,53,105,255), pale=new Color32(250,225,235,255);
        public Color separator=new Color32(225,228,234,255);
        public float title=22, body=16, caption=12, clock=64;
        public float openDuration=.22f, closeDuration=.20f, pageDuration=.18f;
        public Sprite card, bubble, control, disc;
        private static PhoneVisualTheme current;
        public static PhoneVisualTheme Current => current!=null?current:(current=Resources.Load<PhoneVisualTheme>("PhonePolish/Theme"));
        public static Sprite AppIcon(string id)=>Resources.Load<Sprite>("PhonePolish/app-"+id);
        public static Texture2D Symbol(PhoneGlyphKind kind)=>Resources.Load<Texture2D>("PhonePolish/"+kind);
    }
}
