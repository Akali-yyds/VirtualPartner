using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneMessageDetails : MonoBehaviour,IPointerClickHandler
    {
        public string Details;
        public void OnPointerClick(PointerEventData e)
        {
            if(string.IsNullOrEmpty(Details))return;
            var app=GetComponentInParent<PhonePreviewApp>();if(app==null)return;
            var theme=PhoneVisualTheme.Current;
            var root=new GameObject("MessageErrorDetails",typeof(RectTransform),typeof(Image));root.transform.SetParent(app.transform,false);
            var r=(RectTransform)root.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;root.GetComponent<Image>().color=theme.paper;
            app.SetDetailsModal(root);
            Label(root.transform,"Title","Message details",20,16,300,36,22);
            var close=new GameObject("Close",typeof(RectTransform),typeof(Image),typeof(Button));close.transform.SetParent(root.transform,false);PhoneVisualPolish.Place((RectTransform)close.transform,344,12,44,44);close.GetComponent<Image>().color=Color.clear;Label(close.transform,"Label","×",0,0,44,44,24);close.GetComponent<Button>().onClick.AddListener(()=>app.OnBackPressed());
            var viewport=new GameObject("Scroll",typeof(RectTransform),typeof(Image),typeof(ScrollRect),typeof(RectMask2D));viewport.transform.SetParent(r,false);var vr=(RectTransform)viewport.transform;vr.anchorMin=Vector2.zero;vr.anchorMax=Vector2.one;vr.offsetMin=new Vector2(20,70);vr.offsetMax=new Vector2(-20,-68);viewport.GetComponent<Image>().color=Color.clear;
            var body=Label(vr,"Details",Details,0,0,368,500,14);var br=body.rectTransform;br.anchorMin=new Vector2(0,1);br.anchorMax=Vector2.one;br.sizeDelta=new Vector2(0,0);br.pivot=new Vector2(.5f,1);br.anchoredPosition=Vector2.zero;body.alignment=TextAlignmentOptions.TopLeft;body.richText=false;var fit=body.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=vr;scroll.content=br;scroll.horizontal=false;
            var copy=new GameObject("Copy",typeof(RectTransform),typeof(Image),typeof(Button));copy.transform.SetParent(r,false);var cr=(RectTransform)copy.transform;cr.anchorMin=cr.anchorMax=new Vector2(.5f,0);cr.pivot=new Vector2(.5f,0);cr.sizeDelta=new Vector2(200,44);cr.anchoredPosition=new Vector2(0,12);copy.GetComponent<Image>().color=theme.pale;Label(cr,"Label","Copy details",12,0,180,44,14);copy.GetComponent<Button>().onClick.AddListener(()=>GUIUtility.systemCopyBuffer=Details);
        }
        private static TMP_Text Label(Transform parent,string name,string value,float x,float y,float width,float height,float size)
        {var obj=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));obj.transform.SetParent(parent,false);PhoneVisualPolish.Place((RectTransform)obj.transform,x,y,width,height);var text=obj.GetComponent<TMP_Text>();text.font=PhoneVisualTheme.Current.regular;text.fontSize=size;text.color=PhoneVisualTheme.Current.ink;text.text=value;text.raycastTarget=false;return text;}
    }
}
