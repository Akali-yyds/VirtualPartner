using System.Collections.Generic;
using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using VirtualPartner.Runtime.PhoneOS;
using static VirtualPartner.EditorTools.PhoneVisualUI;

namespace VirtualPartner.EditorTools
{
    public static partial class PhoneVisualStageBuilder
    {
        private static void Avatar(Transform parent,float x,float y,float size)
        {
            var r=Panel(parent,"TokiAvatar",x,y,size,size,Pale);var mask=r.gameObject.AddComponent<Mask>();mask.showMaskGraphic=true;
            var a=Rect(r,"Portrait");Fill(a);var image=Image(a,Color.white);image.sprite=avatar;image.preserveAspect=true;
        }
        private static PhoneTextBubble Bubble(Transform content,string text,bool outgoing)
        {
            var row=Rect(content,outgoing?"Outgoing":"Incoming");row.sizeDelta=new Vector2(376,100);row.gameObject.AddComponent<LayoutElement>().preferredHeight=100;
            var bubble=Panel(row,"Bubble",0,0,280,92,outgoing?Pale:Color.white);bubble.anchorMin=bubble.anchorMax=new Vector2(outgoing?1:0,1);bubble.pivot=new Vector2(outgoing?1:0,1);
            var body=Text(bubble,"Message",text,10,7,260,50,15);Fill(body.rectTransform,10,6,10,20);body.alignment=TextAlignmentOptions.TopLeft;
            var timestamp=Text(bubble,"Time","09:41",16,60,240,14,10,Muted);timestamp.rectTransform.anchorMin=new Vector2(0,0);timestamp.rectTransform.anchorMax=new Vector2(1,0);timestamp.rectTransform.pivot=new Vector2(.5f,0);timestamp.rectTransform.offsetMin=new Vector2(10,3);timestamp.rectTransform.offsetMax=new Vector2(-10,17);timestamp.alignment=TextAlignmentOptions.MidlineRight;
            var view=row.gameObject.AddComponent<PhoneTextBubble>();view.bubble=bubble;view.body=body;view.outgoing=outgoing;return view;
        }
        private static GameObject BuildMomotalk()
        {
            var app=App("momotalk");
            var contacts=Page(app,"Contacts");Header(contacts,"Momotalk","A little closer, one message at a time.",app);
            contacts.Find("Header/Title").GetComponent<TMP_Text>().color=Pink;
            var search=Field(contacts,"SearchContacts","",20,78,368,40);((TMP_Text)search.placeholder).text="Search conversations";
            Text(contacts,"Section","CHATS",24,130,260,24,14,Pink,true);
            var contact=Button(contacts,"TokiConversation","",12,164,384,72);Avatar(contact.transform,12,12,48);
            Text(contact.transform,"Name","Toki",76,9,218,26,17,null,true);Text(contact.transform,"Message","Teacher, I'm here.",76,36,270,24,13,Muted);
            Text(contact.transform,"Time","09:41",312,10,62,20,11,Muted);PageLink(contact,app,1);
            Text(contacts,"EmptyHint","Your conversations belong here.",34,280,340,32,14,Muted).alignment=TextAlignmentOptions.Center;
            Icon(contacts,PhoneGlyphKind.Chat,190,330,28,Hex("CBBFC7"));

            var chat=Page(app,"Conversation");chat.GetComponent<Image>().color=Hex("F6EFF2");
            var header=Panel(chat,"ChatHeader",0,0,408,60,Paper,false);Click(IconButton(header,"Back",PhoneGlyphKind.Back,4,8),app.Back);Avatar(header,50,10,40);
            Text(header,"Name","Toki",104,6,230,27,17,null,true);Text(header,"Status","Available",104,32,230,20,12,Muted);
            PageLink(IconButton(header,"Details",PhoneGlyphKind.More,356,8),app,2);
            var messages=Scroll(chat,"Messages",60,60,out var messageScroll,16);
            Note(messages,"TODAY",22).alignment=TextAlignmentOptions.Center;
            Bubble(messages,"Teacher, I'm here.\nWhat would you like to do today?",false);
            Bubble(messages,"Let's take a short break.",true);
            Bubble(messages,"Of course. There's no need to rush.\n我们可以慢慢聊，今天过得怎么样？",false);
            Bubble(messages,"中英文都要清楚，长一点的消息也应该自然换行。",true);
            Bubble(messages,"I'll be right here.",false);
            var composer=Panel(chat,"Composer",0,638,408,60,Paper,false);composer.anchorMin=new Vector2(0,0);composer.anchorMax=new Vector2(1,0);composer.pivot=new Vector2(.5f,0);composer.offsetMin=Vector2.zero;composer.offsetMax=new Vector2(0,60);
            var input=Field(composer,"MessageInput","",12,8,282,44,true,true);((TMP_Text)input.placeholder).text="Message Toki…";
            PageLink(IconButton(composer,"Microphone",PhoneGlyphKind.Mic,298,8),app,3);
            var send=IconButton(composer,"SendPreview",PhoneGlyphKind.Send,352,8,44,Pale);
            var latest=Button(chat,"NewMessages","New messages",123,592,164,32,Pale);latest.gameObject.SetActive(false);
            var preview=chat.gameObject.AddComponent<PhoneChatPreview>();preview.input=input;preview.scroll=messageScroll;preview.newMessagesButton=latest.gameObject;
            var templates=Rect(app.transform,"Templates");templates.gameObject.SetActive(false);preview.outgoingTemplate=Bubble(templates,"",true);
            Click(send,preview.Send);Click(latest,preview.JumpToLatest);

            var details=Page(app,"ContactDetails");Header(details,"Contact","Momotalk",app,true);Avatar(details,160,115,88);
            Text(details,"Name","Toki",24,222,360,42,24,null,true).alignment=TextAlignmentOptions.Center;
            Text(details,"Identity","C&C · Millennium",24,265,360,30,14,Muted).alignment=TextAlignmentOptions.Center;
            var detailList=Scroll(details,"Actions",316,0,out _);
            PageLink(Row(detailList,"Chat history","Clear messages for this conversation"),app,4);
            PageLink(Row(detailList,"Long-term memory","Manage what Toki remembers"),app,5);
            Note(detailList,"Chat history and long-term memory are managed separately.",72);
            var voice=Page(app,"VoiceInput");Header(voice,"Voice input","Microphone is off in this preview",app,true);
            Icon(voice,PhoneGlyphKind.Mic,160,167,88,Pink);Text(voice,"Title","Speak naturally",24,288,360,48,22,null,true).alignment=TextAlignmentOptions.Center;
            Text(voice,"Info","Recognized words will appear in your draft before you send them.",42,350,324,96,17,Muted).alignment=TextAlignmentOptions.Center;
            PageLink(Button(voice,"Cancel","Back to conversation",48,496,312,56,Pale),app,1);
            var clearHistory=Page(app,"ClearHistory");Confirmation(clearHistory,app,"Clear chat history?","This removes this conversation's messages. Long-term memory is kept.","Clear preview messages",preview.ClearPreview);
            var clearMemory=Page(app,"ClearMemory");Confirmation(clearMemory,app,"Clear long-term memory?","This removes what the character remembers. Chat history is kept.","Preview action",null);
            app.parentPages=new[]{0,0,1,1,2,2};
            return SaveApp(app,contacts,chat,details,voice,clearHistory,clearMemory);
        }
        private static void Confirmation(RectTransform page,PhonePreviewApp app,string title,string description,string action,UnityEngine.Events.UnityAction callback)
        {
            Header(page,"Conversation settings","",app,true);Icon(page,PhoneGlyphKind.Chat,180,151,48,Pink);
            Text(page,"Question",title,28,239,352,88,23,null,true).alignment=TextAlignmentOptions.Center;
            Text(page,"Explanation",description,36,341,336,108,16,Muted).alignment=TextAlignmentOptions.Center;
            var b=Button(page,"Confirm",action,32,484,344,56,Pale);if(callback!=null)Click(b,callback);
            var state=page.gameObject.AddComponent<PhonePreviewAction>();state.feedback=Text(page,"Feedback","No live data is changed in visual preview.",32,565,344,64,13,Muted);Click(b,state.Preview);
        }
        private static GameObject BuildCamera()
        {
            var app=App("camera");var page=Page(app,"Controls");Header(page,"Camera","Find your favorite point of view.",app);
            var list=Scroll(page,"ControlsScroll",68,0,out _);
            var info=Card(list,"Mode",74);Icon(info,PhoneGlyphKind.Camera,18,20,32,Hex("869CBD"));Text(info,"Title","Scene camera",64,8,280,30,18,null,true);Text(info,"Hint","Control preview · scene stays still",64,40,280,24,13,Muted);
            var controls=Card(list,"Joysticks",280);Text(controls,"Hint","Drag to move. Release to stop.",18,14,332,28,14,Muted);
            Joystick(controls,"Pan",20,68,PhoneGlyphKind.Move);Joystick(controls,"Orbit",202,68,PhoneGlyphKind.Rotate);
            var zoom=Card(list,"Distance",98);Text(zoom,"Title","Distance",18,9,232,28,16,null,true);
            var value=Text(zoom,"Value","5.0",270,9,78,28,15,Muted);var slider=Slider(zoom,"Zoom",18,45,332,.8f,24,5);
            var action=zoom.gameObject.AddComponent<PhonePreviewAction>();action.slider=slider;action.resetValue=5;action.feedback=value;UnityEventTools.AddPersistentListener(slider.onValueChanged,action.UpdateValue);
            var row=Card(list,"Reset",54,Color.clear);var reset=Button(row,"ResetButton","Reset view",0,0,368,54,Pale);Click(reset,action.ResetValue);
            foreach(var joystick in page.GetComponentsInChildren<PhoneJoystick>())Click(reset,joystick.Release);
            return SaveApp(app,page);
        }
        private static void Joystick(Transform p,string title,float x,float y,PhoneGlyphKind glyph)
        {
            var pad=Panel(p,title,x,y,144,144,Hex("F4EFF3"));pad.pivot=new Vector2(.5f,.5f);pad.anchoredPosition+=new Vector2(72,-72);pad.GetComponent<Image>().raycastTarget=true;
            var ring=Icon(pad,PhoneGlyphKind.Home,0,0,144,Hex("D7C7D1"));ring.stroke=.8f;
            var knob=Panel(pad,"Knob",0,0,54,54,Pale);knob.anchorMin=knob.anchorMax=knob.pivot=new Vector2(.5f,.5f);knob.anchoredPosition=Vector2.zero;Icon(knob,glyph,13,13,28,Pink);
            var joystick=pad.gameObject.AddComponent<PhoneJoystick>();joystick.knob=knob;
            Text(p,title+"Label",title,x,y+151,144,30,17,null,true).alignment=TextAlignmentOptions.Center;
            joystick.readout=Text(p,title+"Value","X 0.00   Y 0.00",x-8,y+185,160,22,14,Muted);joystick.readout.alignment=TextAlignmentOptions.Center;
        }
        private static GameObject BuildSettings()
        {
            var app=App("settings");var root=Page(app,"SettingsHome");Header(root,"Settings","Make this space your own.",app);
            var list=Scroll(root,"Categories",68,0,out _);
            var summary=Card(list,"Profile",80,Pale);Icon(summary,PhoneGlyphKind.Phone,20,19,36,Pink);Text(summary,"Title","Your phone",72,9,268,30,20,null,true);Text(summary,"Subtitle","VirtualPartner OS",72,42,268,24,14,Muted);
            PageLink(Row(list,"Display","Phone size, wallpaper and clock"),app,1);
            PageLink(Row(list,"LLM connection","Model, endpoint and API key"),app,2);
            PageLink(Row(list,"Voice input","Review text before sending"),app,3);
            Note(list,"Visual preview\nConnection controls do not contact services yet.",82);
            var display=Page(app,"Display");Header(display,"Display","Changes preview immediately",app,true);
            var displayList=Scroll(display,"DisplayOptions",68,0,out _);
            var dimensions=Card(displayList,"PhoneSize",112);Text(dimensions,"Title","Phone height",18,12,242,30,17,null,true);
            var heightLabel=Text(dimensions,"Value","90%",278,12,72,30,17,Pink,true);
            var height=Slider(dimensions,"Height",18,39,332,.7f,.95f,.9f);Text(dimensions,"Hint","70%",18,84,120,22,12,Muted);Text(dimensions,"Max","95%",294,84,70,22,12,Muted);
            var wallpaper=Card(displayList,"Wallpaper",148);Text(wallpaper,"Title","Wallpaper",18,9,332,30,17,null,true);
            var settings=app.gameObject.AddComponent<PhonePreviewSettings>();settings.height=height;settings.heightLabel=heightLabel;
            var ids=new[]{"pink","lavender","sunrise"};
            for(var i=0;i<3;i++)
            {var b=Button(wallpaper,"Wallpaper_"+ids[i],"",18+i*115,42,104,92);var image=b.GetComponent<Image>();image.sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/VirtualPartner/UI/PhoneOS/Sprites/phoneos_wallpaper_"+ids[i]+".png");image.type=UnityEngine.UI.Image.Type.Simple;UnityEventTools.AddStringPersistentListener(b.onClick,settings.SetWallpaper,ids[i]);}
            var toggles=Card(displayList,"Preferences",130,Color.clear);settings.timeFormat=Toggle(toggles,"24-hour clock","Use 18:00 instead of 6:00 PM",0,true);settings.dock=Toggle(toggles,"Show Dock","Keep favorite apps within reach",68,true);
            var api=Page(app,"ApiConfiguration");Header(api,"LLM connection","Edit a connection draft",app,true);
            var apiList=Scroll(api,"ApiFields",68,0,out _);
            Note(apiList,"Preview form · no saved API key is loaded. Test and Save are inactive service actions.",80);
            LabeledField(apiList,"Model","",false,"Model name");LabeledField(apiList,"Base URL","",false,"https://api.example.com/v1");LabeledField(apiList,"Chat completions URL","",false,"Optional endpoint override");
            var key=LabeledField(apiList,"API key","",false,"Enter API key");key.contentType=TMP_InputField.ContentType.Password;
            var show=Card(apiList,"KeyVisibility",62,Color.clear);var showToggle=Toggle(show,"Show API key","Hidden by default",0);var keyAction=show.gameObject.AddComponent<PhonePreviewAction>();keyAction.text=key;UnityEventTools.AddPersistentListener(showToggle.onValueChanged,keyAction.ShowSecret);
            var options=Card(apiList,"RequestOptions",130,Color.clear);Toggle(options,"JSON response format","Request structured output",0,true);Toggle(options,"Developer role","Use the developer message role",68,true);
            LabeledField(apiList,"Interaction timeout (seconds)","120");
            var feedback=Note(apiList,"No connection test has been run.",62);ActionButtons(apiList,feedback,"Reload file","Load current","Test","Save");
            var voice=Page(app,"VoiceSettings");Header(voice,"Voice input","Speech to text",app,true);var voiceList=Scroll(voice,"VoiceOptions",68,0,out _);
            var auto=Card(voiceList,"SendMode",62,Color.clear);settings.autoSend=Toggle(auto,"Send automatically","Off: review and edit the transcript",0);
            Note(voiceList,"Leaving Momotalk cancels the current voice input. Replies already sent and character speech continue.",130);
            return SaveApp(app,root,display,api,voice);
        }
        private static TMP_InputField LabeledField(Transform content,string label,string value,bool multiline=false,string placeholder=null)
        {
            var card=Card(content,label,multiline?210:86);Text(card,"Label",label,16,4,336,26,14,null,true);
            var field=Field(card,"Input",value,14,34,340,multiline?162:42,multiline);
            if(placeholder!=null)((TMP_Text)field.placeholder).text=placeholder;return field;
        }
        private static void ActionButtons(Transform content,TMP_Text feedback,params string[] labels)
        {
            for(var i=0;i<labels.Length;i+=2)
            {
                var row=Card(content,"Actions",52,Color.clear);
                for(var j=0;j<2&&i+j<labels.Length;j++)
                {var b=Button(row,labels[i+j],labels[i+j],j*190,0,178,52,j==0?Color.white:Pale);var action=b.gameObject.AddComponent<PhonePreviewAction>();action.feedback=feedback;Click(b,action.Preview);}
            }
        }
    }
}
