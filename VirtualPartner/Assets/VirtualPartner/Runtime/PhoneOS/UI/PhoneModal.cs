using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    public static class PhoneModal
    {
        public static void Show(PhonePreviewApp app,string title,string message,Action confirm=null,string action="OK")
        {
            app.CloseModal();var root=PhonePagePolish.Rect(app.transform,"ConfirmationModal");PhonePagePolish.Fill(root);var bg=root.gameObject.AddComponent<Image>();bg.color=new Color(0,0,0,.25f);
            var card=PhonePagePolish.Rect(root,"Card");card.anchorMin=new Vector2(0,.5f);card.anchorMax=new Vector2(1,.5f);card.pivot=new Vector2(.5f,.5f);card.offsetMin=new Vector2(20,-178);card.offsetMax=new Vector2(-20,178);PhonePagePolish.Surface(card,Color.white);
            PhonePagePolish.Label(card,"Title",title,20,20,328,56,22,true);
            var area=PhonePagePolish.Rect(card,"Scroll");PhoneVisualPolish.Place(area,20,84,328,172);area.gameObject.AddComponent<Image>().color=Color.clear;area.gameObject.AddComponent<RectMask2D>();var scroll=area.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=area;
            var text=PhonePagePolish.Label(area,"Message",message,0,0,328,172,16);text.alignment=TextAlignmentOptions.TopLeft;text.richText=false;var tr=text.rectTransform;tr.anchorMin=new Vector2(0,1);tr.anchorMax=Vector2.one;tr.sizeDelta=Vector2.zero;tr.pivot=new Vector2(.5f,1);tr.anchoredPosition=Vector2.zero;text.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=tr;
            var cancel=PhonePagePolish.Action(card,"Cancel",confirm==null?"Close":"Cancel",20,284,150,48);cancel.onClick.AddListener(app.CloseModal);
            var ok=PhonePagePolish.Action(card,"Confirm",confirm==null?"Copy":action,198,284,150,48,true);ok.onClick.AddListener(()=>{if(confirm==null){GUIUtility.systemCopyBuffer=message;return;}app.CloseModal();confirm();});app.SetDetailsModal(root.gameObject);
        }
    }
}
