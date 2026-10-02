using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    // Idempotent targeted upgrade: keeps serialized events, service owners and user scene objects.
    public static class PhoneVisualPolish
    {
        public static void Place(RectTransform r,float x,float y,float w,float h)
        {r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        public static void ApplyShell(PhonePresentationShell shell)
        {
            var t=PhoneVisualTheme.Current;if(t==null)return;
            var screen=shell.navigationBackground.transform.parent;var home=screen.Find("Home");
            foreach(var name in new[]{"Search","Weather","SecondaryClock","SecondaryDate","AnalogClock"})
            {var old=home.Find(name);if(old!=null)old.gameObject.SetActive(false);}
            foreach(Transform child in home)if(child.name.StartsWith("Open_")||child.name=="Momotalk"||child.name=="Camera"||child.name=="Settings"||child.name=="Debug")child.gameObject.SetActive(false);
            Place(shell.clockTime.rectTransform,24,72,360,92);shell.clockTime.font=t.light;shell.clockTime.fontSize=t.clock;shell.clockTime.lineSpacing=0;shell.clockTime.alignment=TextAlignmentOptions.Center;shell.clockTime.color=t.ink;
            Place(shell.clockDate.rectTransform,24,166,360,28);shell.clockDate.font=t.regular;shell.clockDate.fontSize=14;shell.clockDate.alignment=TextAlignmentOptions.Center;shell.clockDate.color=t.ink;
            var dock=(RectTransform)shell.dock.transform;dock.anchorMin=new Vector2(0,0);dock.anchorMax=new Vector2(1,0);dock.pivot=new Vector2(.5f,0);dock.offsetMin=new Vector2(16,20);dock.offsetMax=new Vector2(-16,116);
            dock.GetComponent<Image>().color=Color.clear;shell.dock.SetActive(true);
            var ids=new[]{"momotalk","camera","settings","debug"};var names=new[]{"Momotalk","Camera","Settings","Debug"};
            for(int i=0;i<4;i++)
            {
                var icon=dock.Find("Open_"+ids[i]);if(icon==null)continue;
                Place((RectTransform)icon,17+i*94,6,60,60);var image=icon.GetComponent<Image>();image.sprite=PhoneVisualTheme.AppIcon(ids[i]);image.type=Image.Type.Simple;image.color=Color.white;
                foreach(var glyph in icon.GetComponentsInChildren<PhoneGlyph>(true))glyph.gameObject.SetActive(false);
                var button=icon.GetComponent<Button>();var colors=button.colors;colors.highlightedColor=Color.white;colors.pressedColor=new Color(.82f,.82f,.82f);colors.fadeDuration=.08f;button.colors=colors;
                var label=dock.Find("Label_"+ids[i]);if(label==null){label=new GameObject("Label_"+ids[i],typeof(RectTransform),typeof(TextMeshProUGUI)).transform;label.SetParent(dock,false);}
                Place((RectTransform)label,i*94,72,94,24);var text=label.GetComponent<TMP_Text>();text.font=t.regular;text.text=names[i];text.fontSize=12;text.color=t.ink;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            }
            shell.wallpaper.color=Color.white;shell.statusBackground.color=shell.navigationBackground.color=Color.clear;
            foreach(var label in screen.Find("StatusBar").GetComponentsInChildren<TMP_Text>()){label.font=t.semibold;label.color=t.ink;label.fontSize=12;}
            foreach(var glyph in screen.Find("Navigation").GetComponentsInChildren<PhoneGlyph>())glyph.color=t.ink;
        }
        public static void ApplyApp(PhonePreviewApp app)
        {
            var t=PhoneVisualTheme.Current;if(t==null)return;
            if(app.appId=="settings")
            {
                var settings=app.GetComponent<PhonePreviewSettings>();if(settings!=null&&settings.dock!=null){settings.dock.gameObject.SetActive(false);var row=settings.dock.transform.parent.GetComponent<LayoutElement>();if(row!=null)row.preferredHeight=62;}
                return;
            }
            if(app.appId!="momotalk")return;
            var chat=app.transform.Find("Conversation");if(chat==null)return;
            chat.GetComponent<Image>().color=t.chat;
            var header=chat.Find("ChatHeader");header.GetComponent<Image>().color=t.paper;
            foreach(var label in header.GetComponentsInChildren<TMP_Text>()){label.font=label.name=="Name"?t.semibold:t.regular;label.color=label.name=="Name"?t.ink:t.muted;label.fontSize=label.name=="Name"?17:12;}
            var composer=chat.Find("Composer");composer.GetComponent<Image>().color=t.paper;
            var input=composer.Find("MessageInput").GetComponent<TMP_InputField>();input.textComponent.font=t.regular;input.textComponent.fontSize=16;input.textComponent.color=t.ink;var placeholder=input.placeholder as TMP_Text;if(placeholder!=null){placeholder.fontSize=16;placeholder.color=t.muted;}
            var background=input.GetComponent<Image>();background.sprite=t.control;background.type=Image.Type.Sliced;background.pixelsPerUnitMultiplier=1;background.color=new Color32(235,237,242,255);
            var send=composer.Find("SendPreview");send.GetComponent<Image>().sprite=t.control;send.GetComponent<Image>().color=t.pink;foreach(var g in send.GetComponentsInChildren<PhoneGlyph>())g.color=Color.white;
            var scroll=chat.Find("Messages").GetComponent<ScrollRect>();var layout=scroll.content.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(16,16,16,20);layout.spacing=10;
            foreach(var bubble in app.GetComponentsInChildren<PhoneTextBubble>(true))ApplyBubble(bubble);
        }
        public static void ApplyBubble(PhoneTextBubble row)
        {
            var t=PhoneVisualTheme.Current;if(t==null||row.body==null)return;
            row.body.font=t.regular;row.body.fontSize=t.body;row.body.color=t.ink;
            var image=row.bubble.GetComponent<Image>();image.sprite=t.bubble;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;image.color=row.outgoing?t.pale:Color.white;
            row.body.rectTransform.offsetMin=new Vector2(12,24);row.body.rectTransform.offsetMax=new Vector2(-12,-10);
            var time=row.bubble.Find("Time").GetComponent<TMP_Text>();time.font=t.regular;time.color=t.muted;time.fontSize=10;
        }
    }
}
