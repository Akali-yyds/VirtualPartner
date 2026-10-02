using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VirtualPartner.Runtime.PhoneOS;

namespace VirtualPartner.EditorTools
{
    internal static class PhoneVisualUI
    {
        internal static TMP_FontAsset Regular, LightFont, Semibold;
        internal static Sprite Rounded;
        internal static readonly Color Ink = Hex("302C37"), Muted = Hex("817681"), Pink = Hex("DE7899"), Pale = Hex("F8E2EA"), Paper = Hex("FAF7F8"), Line = Hex("EBE4E8");
        internal static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        internal static RectTransform Rect(Transform parent, string name)
        { var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r; }
        internal static RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
        {var r=Rect(parent,name);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
        internal static void Fill(RectTransform r,float left=0,float top=0,float right=0,float bottom=0)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);}
        internal static Image Image(RectTransform r,Color color, bool rounded=false)
        {var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;if(rounded){image.sprite=Rounded;image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=2;}return image;}
        internal static RectTransform Panel(Transform p,string name,float x,float y,float w,float h,Color color,bool rounded=true)
        {var r=Box(p,name,x,y,w,h);Image(r,color,rounded);return r;}
        internal static TMP_Text Text(Transform p,string name,string text,float x,float y,float w,float h,float size=16,Color? color=null,bool bold=false)
        {
            var r=Box(p,name,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font=bold?Semibold:Regular;t.fontSize=size;t.text=text;t.color=color??Ink;
            t.raycastTarget=false;t.richText=false;t.textWrappingMode=TextWrappingModes.Normal;
            t.overflowMode=TextOverflowModes.Overflow;t.margin=Vector4.zero;
            t.alignment=TextAlignmentOptions.MidlineLeft;return t;
        }
        internal static PhoneGlyph Icon(Transform p,PhoneGlyphKind kind,float x,float y,float size,Color? color=null)
        {var r=Box(p,kind.ToString(),x,y,size,size);var g=r.gameObject.AddComponent<PhoneGlyph>();g.kind=kind;g.color=color??Ink;g.raycastTarget=false;return g;}
        internal static Button Button(Transform p,string name,string label,float x,float y,float w,float h,Color? color=null)
        {
            var r=Panel(p,name,x,y,w,h,color??Color.white);var img=r.GetComponent<Image>();img.raycastTarget=true;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=img;
            var colors=b.colors;colors.highlightedColor=Hex("F5E8EE");colors.pressedColor=Hex("E8C9D7");colors.fadeDuration=.09f;b.colors=colors;
            if(!string.IsNullOrEmpty(label)){var t=Text(r,"Label",label,12,0,w-24,h,14);t.alignment=TextAlignmentOptions.Center;}
            return b;
        }
        internal static Button IconButton(Transform p, string name, PhoneGlyphKind icon, float x,float y,float size=44,Color? color=null)
        {var b=Button(p,name,"",x,y,size,size,color??Color.clear);Icon(b.transform,icon,(size-24)/2,(size-24)/2,24);return b;}
        internal static void Click(Button button,UnityAction action)=>UnityEventTools.AddPersistentListener(button.onClick,action);
        internal static void PageLink(Button button,PhonePreviewApp app,int page)=>UnityEventTools.AddIntPersistentListener(button.onClick,app.ShowPage,page);
        internal static RectTransform Scroll(Transform p,string name,float top,float bottom,out ScrollRect scroll,int padding=20)
        {
            var r=Rect(p,name);Fill(r,0,top,0,bottom);Image(r,Color.clear).raycastTarget=true;
            scroll=r.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            var viewport=Rect(r,"Viewport");Fill(viewport);viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect(viewport,"Content");content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(padding,padding,8,16);layout.spacing=6;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=content;return content;
        }
        internal static RectTransform Card(Transform content,string name,float height,Color? color=null)
        {
            var r=Rect(content,name);Image(r,color??Color.white,true);
            var e=r.gameObject.AddComponent<LayoutElement>();e.preferredHeight=height;return r;
        }
        internal static TMP_Text Note(Transform content,string text,float height=48)
        {var r=Card(content,"Note",height,Color.clear);var t=Text(r,"Text",text,0,0,368,height,13,Muted);Fill(t.rectTransform);return t;}
        internal static Button Row(Transform content,string title,string subtitle,PhoneGlyphKind icon=PhoneGlyphKind.Chevron)
        {
            var r=Card(content,title,60);r.GetComponent<Image>().raycastTarget=true;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
            Text(r,"Title",title,16,6,300,25,17,null,true);Text(r,"Subtitle",subtitle,16,31,300,22,13,Muted);
            Icon(r,icon,326,20,20,Muted);return b;
        }
        internal static TMP_InputField Field(Transform p,string name,string value,float x,float y,float w,float h,bool multiline=false,bool chat=false)
        {
            var r=Panel(p,name,x,y,w,h,Hex("F3EFF2"));r.GetComponent<Image>().raycastTarget=true;
            TMP_InputField input=chat?r.gameObject.AddComponent<PhoneMessageInputField>():r.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=r.GetComponent<Image>();
            var area=Rect(r,"TextArea");Fill(area,12,8,12,8);area.gameObject.AddComponent<RectMask2D>();
            var t=Text(area,"Text","",0,0,w-28,h-20,15);Fill(t.rectTransform);t.alignment=TextAlignmentOptions.TopLeft;
            var placeholder=Text(area,"Placeholder","Enter text…",0,0,w-28,h-20,15,Muted);Fill(placeholder.rectTransform);placeholder.alignment=TextAlignmentOptions.TopLeft;
            input.textViewport=area;input.textComponent=(TMP_Text)t;input.placeholder=placeholder;input.fontAsset=Regular;
            input.lineType=multiline?TMP_InputField.LineType.MultiLineNewline:TMP_InputField.LineType.SingleLine;
            input.restoreOriginalTextOnEscape=false;
            input.text=value;input.caretColor=Pink;input.customCaretColor=true;input.selectionColor=new Color(.87f,.47f,.60f,.22f);return input;
        }
        internal static Slider Slider(Transform p,string name,float x,float y,float w,float min,float max,float value)
        {
            var r=Box(p,name,x,y,w,44);var s=r.gameObject.AddComponent<Slider>();
            Panel(r,"Track",0,19,w,6,Line);
            var fillArea=Box(r,"FillArea",10,19,w-20,6);var fill=Rect(fillArea,"Fill");Fill(fill);Image(fill,Pink,true);s.fillRect=fill;
            var handleArea=Box(r,"HandleArea",10,9,w-20,26);var handle=Box(handleArea,"Handle",0,0,26,0);handle.pivot=new Vector2(.5f,.5f);
            var img=Image(handle,Pink,true);img.raycastTarget=true;s.handleRect=handle;s.targetGraphic=img;s.minValue=min;s.maxValue=max;s.value=value;return s;
        }
        internal static Toggle Toggle(Transform p,string title,string subtitle,float y,bool value=false)
        {
            var r=Panel(p,title,0,y,368,62,Color.white);var toggle=r.gameObject.AddComponent<Toggle>();
            Text(r,"Title",title,16,5,272,25,16);Text(r,"Subtitle",subtitle,16,31,272,23,12,Muted);
            r.GetComponent<Image>().raycastTarget=true;
            var box=Panel(r,"SwitchTrack",308,19,44,26,Line);box.GetComponent<Image>().raycastTarget=true;
            var thumb=Panel(box,"Thumb",4,4,18,18,Color.white);
            toggle.targetGraphic=box.GetComponent<Image>();toggle.graphic=null;toggle.isOn=value;
            var visual=r.gameObject.AddComponent<PhoneSwitch>();visual.toggle=toggle;visual.track=box.GetComponent<Image>();visual.thumb=thumb;visual.Apply(value);
            return toggle;
        }
    }
}
