using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    [ExecuteAlways]
    public sealed class PhoneTextBubble : MonoBehaviour
    {
        public RectTransform bubble;
        public TMP_Text body;
        public bool outgoing;
        private float previousWidth = -1;
        private string previousText;
        private bool typing;
        public void SetTyping()
        {
            if(typing)return;
            typing=true;body.text="";body.gameObject.SetActive(false);
            var time=bubble.Find("Time");if(time!=null)time.gameObject.SetActive(false);
            bubble.sizeDelta=new Vector2(72,40);
            GetComponent<LayoutElement>().preferredHeight=42;
            var indicator=bubble.gameObject.AddComponent<PhoneTypingIndicator>();indicator.Build();
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform.parent);
        }
        private void OnEnable(){PhoneVisualPolish.ApplyBubble(this);previousWidth=-1;}
        private void LateUpdate()
        {
            if(typing)return;
            var width = ((RectTransform)transform).rect.width;
            if (Mathf.Abs(width - previousWidth) < .1f && previousText == body.text) return;
            previousWidth = width; previousText = body.text;
            var maxWidth = Mathf.Max(120, width * .82f);
            var natural = body.GetPreferredValues(body.text, 10000, 10000);
            var fittedWidth = Mathf.Clamp(natural.x + 24, 72, maxWidth);
            var height = Mathf.Ceil(body.GetPreferredValues(body.text, fittedWidth - 24, 10000).y) + 36;
            bubble.sizeDelta = new Vector2(fittedWidth, height);
            GetComponent<LayoutElement>().preferredHeight = height + 2;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform.parent);
        }
    }
}
