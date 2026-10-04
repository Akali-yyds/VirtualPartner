using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    /// <summary>Fixed-size, purely visual typing cue. Never modifies conversation state.</summary>
    public sealed class PhoneTypingIndicator : MonoBehaviour
    {
        private readonly RectTransform[] dots=new RectTransform[3];
        private float started;
        public void Build()
        {
            started=Time.unscaledTime;
            for(int i=0;i<dots.Length;i++)
            {
                var dot=new GameObject("TypingDot"+i,typeof(RectTransform),typeof(Image));
                var rect=dot.GetComponent<RectTransform>();rect.SetParent(transform,false);
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(6,6);
                var image=dot.GetComponent<Image>();image.sprite=PhoneVisualTheme.Current.disc;
                image.color=PhoneVisualTheme.Current.muted;image.raycastTarget=false;dots[i]=rect;
            }
            Update();
        }
        public static float Offset(float seconds,int dot)
        {
            const float period=1.25f,hop=.5f,delay=.14f;
            float phase=Mathf.Repeat(seconds-dot*delay,period);
            if(phase>=hop)return 0;
            float sine=Mathf.Sin(Mathf.PI*phase/hop);
            return 4*sine*sine;
        }
        private void Update()
        {
            for(int i=0;i<dots.Length;i++)if(dots[i]!=null)
                dots[i].anchoredPosition=new Vector2((i-1)*13,Offset(Time.unscaledTime-started,i));
        }
    }
}
