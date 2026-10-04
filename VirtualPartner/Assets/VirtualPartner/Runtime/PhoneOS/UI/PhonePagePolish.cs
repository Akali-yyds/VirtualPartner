using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    // Additive prefab upgrade; existing controls, serialized references and page ids are retained.
    public static class PhonePagePolish
    {
        private static PhoneVisualTheme Theme=>PhoneVisualTheme.Current;
        public static RectTransform Rect(Transform parent,string name){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
        public static void Fill(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        public static Image Surface(RectTransform r,Color color){var image=r.GetComponent<Image>();if(image==null)image=r.gameObject.AddComponent<Image>();image.color=color;image.sprite=Theme.control;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;return image;}
        public static TMP_Text Label(Transform p,string name,string value,float x,float y,float w,float h,float size=16,bool bold=false)
        {var r=Rect(p,name);PhoneVisualPolish.Place(r,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=bold?Theme.semibold:Theme.regular;t.fontSize=size;t.color=Theme.ink;t.text=value;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;t.richText=false;return t;}
        public static Button Action(Transform p,string name,string value,float x,float y,float w,float h,bool primary=false)
        {var r=Rect(p,name);PhoneVisualPolish.Place(r,x,y,w,h);Surface(r,primary?Theme.pink:Theme.chat);var b=r.gameObject.AddComponent<Button>();var label=Label(r,"Label",value,8,0,w-16,h,14,true);label.alignment=TextAlignmentOptions.Center;label.color=primary?Color.white:Theme.ink;return b;}
        private static Transform Section(Transform p,string title)
        {var r=Rect(p,"Group_"+title);r.gameObject.AddComponent<LayoutElement>().preferredHeight=36;var label=Label(r,"Title",title.ToUpperInvariant(),4,8,352,24,12,true);label.color=Theme.muted;return r;}
        private static Transform Content(Transform page,string scroll)=>page.Find(scroll+"/Viewport/Content");
        private static void Hidden(Transform t){if(t!=null)t.gameObject.SetActive(false);}
        private static void Header(Transform page)
        {var h=page.Find("Header");if(h==null)return;h.GetComponent<Image>().color=Theme.paper;var title=h.Find("Title").GetComponent<TMP_Text>();title.font=Theme.semibold;title.fontSize=22;title.color=Theme.ink;Hidden(h.Find("Subtitle"));PhoneVisualPolish.Place(title.rectTransform,title.rectTransform.anchoredPosition.x,8,title.rectTransform.sizeDelta.x,40);}
        private static void Row(Button b)
        {
            if(b.transform.Find("Title")==null)return;
            Surface((RectTransform)b.transform,Color.white).sprite=null;var title=b.transform.Find("Title").GetComponent<TMP_Text>();title.font=Theme.regular;title.fontSize=16;title.color=Theme.ink;
            var subtitle=b.transform.Find("Subtitle");if(subtitle!=null){var t=subtitle.GetComponent<TMP_Text>();t.fontSize=12;t.color=Theme.muted;t.overflowMode=TextOverflowModes.Ellipsis;}
            if(b.transform.Find("Divider")==null){var line=Rect(b.transform,"Divider");line.anchorMin=new Vector2(0,0);line.anchorMax=new Vector2(1,0);line.offsetMin=new Vector2(16,0);line.offsetMax=new Vector2(-16,1);line.gameObject.AddComponent<Image>().color=Theme.separator;}
        }
        private static PhoneDisclosure Fold(Transform p,string title,IEnumerable<GameObject> items)
        {
            var button=Action(p,title,title,0,0,368,48);button.gameObject.AddComponent<LayoutElement>().preferredHeight=48;
            var fold=button.gameObject.AddComponent<PhoneDisclosure>();fold.title=title;fold.label=button.GetComponentInChildren<TMP_Text>();fold.items=items.ToArray();fold.Apply();return fold;
        }
        public static void Apply(PhonePreviewApp app)
        {
            if(Theme==null||app.pages==null||app.pages.Length==0)return;
            var marker=app.GetComponent<PhonePagePresentation>();if(marker!=null&&marker.upgraded){Finish(app,marker);return;}
            if(marker==null)marker=app.gameObject.AddComponent<PhonePagePresentation>();
            foreach(var page in app.pages){var bg=page.GetComponent<Image>();if(bg!=null)bg.color=Theme.paper;Header(page.transform);}
            foreach(var text in app.GetComponentsInChildren<TMP_Text>(true))
            {text.font=text.font!=null&&text.font.name.Contains("SemiBold")?Theme.semibold:Theme.regular;text.color=text.name=="Subtitle"||text.name=="Hint"||text.name=="Placeholder"?Theme.muted:Theme.ink;}
            foreach(var field in app.GetComponentsInChildren<TMP_InputField>(true)){Surface((RectTransform)field.transform,Theme.chat);field.textComponent.fontSize=16;field.textComponent.color=Theme.ink;field.caretColor=Theme.pink;}
            foreach(var b in app.GetComponentsInChildren<Button>(true))Row(b);
            foreach(var sc in app.GetComponentsInChildren<ScrollRect>(true)){var layout=sc.content.GetComponent<VerticalLayoutGroup>();if(layout!=null){layout.spacing=4;layout.padding=new RectOffset(20,20,8,16);}}
            if(app.appId=="settings")Settings(app);
            if(app.appId=="camera")Camera(app);
            if(app.appId=="debug")Debug(app);
            if(app.appId=="momotalk")Contacts(app);
            marker.upgraded=true;Finish(app,marker);
        }
        private static void Finish(PhonePreviewApp app,PhonePagePresentation marker)
        {
            if(marker.presentationVersion>=2)return;
            if(app.appId=="debug")for(int p=1;p<=12;p++)
            {
                var c=Content(app.pages[p].transform,"Details");if(c==null)continue;var status=c.Find("Status");if(status==null)continue;
                status.GetComponent<LayoutElement>().preferredHeight=96;
                var duplicate=status.Find("StatusDetails");if(duplicate!=null){duplicate.gameObject.SetActive(false);if(Application.isPlaying)Object.Destroy(duplicate.gameObject);else Object.DestroyImmediate(duplicate.gameObject);}
            }
            if(app.appId=="camera")foreach(var pad in app.GetComponentsInChildren<PhoneJoystick>(true))
            {var image=pad.GetComponent<Image>();image.sprite=Theme.disc;image.type=Image.Type.Simple;var knob=pad.knob.GetComponent<Image>();knob.sprite=Theme.disc;knob.type=Image.Type.Simple;}
            if(app.appId=="settings")
            {
                var list=Content(app.pages[2].transform,"ApiFields");
                foreach(var field in list.GetComponentsInChildren<TMP_InputField>(true)){var bg=field.transform.parent.GetComponent<Image>();if(bg!=null){bg.sprite=null;bg.color=Color.clear;}}
                var voice=Content(app.pages[3].transform,"VoiceOptions");foreach(Transform item in voice)if(item.name=="Note")Hidden(item);
            }
            if(app.appId=="momotalk")
            {
                var list=Content(app.pages[2].transform,"Actions");foreach(Transform item in list)if(item.name=="Note")Hidden(item);
                var memory=list.Find("Long-term memory/Subtitle");if(memory!=null)memory.GetComponent<TMP_Text>().text="Clear this character's saved memories";
            }
            var debug=app.GetComponent<PhoneLiveDebug>();
            if(debug!=null)
            {
                for(int i=1;i<=12;i++)
                {
                    var content=Content(app.pages[i].transform,"Details");if(content==null)continue;
                    var commands=content.Cast<Transform>().Where(c=>c.name.StartsWith("Command_")).ToArray();
                    var first=commands.FirstOrDefault();if(first!=null&&content.Find("Group_Actions")==null){var heading=Section(content,"Actions");heading.SetSiblingIndex(first.GetSiblingIndex());}
                }
            }
            marker.presentationVersion=2;
        }
        private static void Settings(PhonePreviewApp app)
        {
            var home=Content(app.pages[0].transform,"Categories");Hidden(home.Find("Profile"));foreach(Transform c in home)if(c.name=="Note")Hidden(c);
            Section(home,"Preferences").SetAsFirstSibling();
            var list=Content(app.pages[2].transform,"ApiFields");if(list==null)return;
            var feedback=app.GetComponent<PhoneLiveSettings>();var notes=list.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="Text"&&t.transform.parent.name=="Note").ToArray();foreach(var note in notes)if(feedback==null||note!=feedback.feedback)Hidden(note.transform.parent);
            var actions=list.GetComponentsInChildren<Button>(true).Where(b=>new[]{"Load current","Reload file","Test","Save"}.Contains(b.name)).ToDictionary(b=>b.name);
            var order=new List<Transform>{list.Find("Model"),list.Find("Base URL"),list.Find("API key"),list.Find("KeyVisibility")};
            var advanced=new[]{list.Find("Chat completions URL"),list.Find("RequestOptions"),list.Find("Interaction timeout (seconds)")};var fold=Fold(list,"Advanced",advanced.Select(x=>x.gameObject));order.Add(fold.transform);order.AddRange(advanced);
            foreach(var item in order)item.SetAsLastSibling();
            var main=actions["Test"].transform.parent;main.SetAsLastSibling();var secondary=actions["Load current"].transform.parent;secondary.SetAsLastSibling();
            foreach(var b in actions.Values){Surface((RectTransform)b.transform,b.name=="Save"?Theme.pink:Theme.chat);b.GetComponentInChildren<TMP_Text>().color=b.name=="Save"?Color.white:Theme.ink;}
            var status=Rect(list,"ConnectionStatus");status.gameObject.AddComponent<LayoutElement>().preferredHeight=44;var summary=Label(status,"Summary","Not tested",4,0,280,44,12);summary.color=Theme.muted;var details=Action(status,"Details","Details",278,0,90,44);
            if(feedback!=null){feedback.feedback.transform.parent.gameObject.SetActive(false);var presentation=app.gameObject.AddComponent<PhoneSettingsPresentation>();presentation.load=actions["Load current"];presentation.reload=actions["Reload file"];presentation.test=actions["Test"];presentation.save=actions["Save"];presentation.details=details;presentation.status=summary;}
            var display=Content(app.pages[1].transform,"DisplayOptions");foreach(Transform c in display){var image=c.GetComponent<Image>();if(image!=null)image.sprite=Theme.control;}
        }
        private static void Camera(PhonePreviewApp app)
        {
            var page=app.pages[0].transform;var scroll=page.Find("ControlsScroll").GetComponent<ScrollRect>();scroll.enabled=false;
            var content=scroll.content;content.GetComponent<VerticalLayoutGroup>().enabled=false;content.GetComponent<ContentSizeFitter>().enabled=false;Fill(content);content.offsetMin=new Vector2(20,0);content.offsetMax=new Vector2(-20,0);
            Hidden(content.Find("Mode"));var joysticks=content.Find("Joysticks");PhoneVisualPolish.Place((RectTransform)joysticks,0,0,368,424);joysticks.GetComponent<Image>().color=Color.clear;Hidden(joysticks.Find("Hint"));
            for(int i=0;i<2;i++)
            {var name=i==0?"Pan":"Orbit";var pad=joysticks.Find(name).GetComponent<PhoneJoystick>();var r=(RectTransform)pad.transform;PhoneVisualPolish.Place(r,100,28+i*210,168,168);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition+=new Vector2(84,-84);pad.radius=54;Surface(r,Theme.chat);pad.readout.gameObject.SetActive(false);
                var ring=pad.GetComponentInChildren<PhoneGlyph>();if(ring!=null&&ring.kind==PhoneGlyphKind.Home)ring.gameObject.SetActive(false);Surface(pad.knob,Color.white);foreach(var icon in pad.knob.GetComponentsInChildren<PhoneGlyph>())icon.color=Theme.ink;
                var title=joysticks.Find(name+"Label").GetComponent<TMP_Text>();PhoneVisualPolish.Place(title.rectTransform,0,i*210,368,26);title.text=i==0?"Pan · move on the ground":"Rotate · around the character";title.fontSize=14;title.alignment=TextAlignmentOptions.Center;}
            var zoom=content.Find("Distance");PhoneVisualPolish.Place((RectTransform)zoom,0,436,368,84);Surface((RectTransform)zoom,Color.white);zoom.Find("Title").GetComponent<TMP_Text>().text="Zoom";
            var reset=content.Find("Reset");PhoneVisualPolish.Place((RectTransform)reset,0,534,368,48);var button=reset.GetComponentInChildren<Button>();PhoneVisualPolish.Place((RectTransform)button.transform,0,0,368,48);Surface((RectTransform)button.transform,Theme.chat);
        }
        private static void Debug(PhonePreviewApp app)
        {
            var list=Content(app.pages[0].transform,"Sections");var rows=list.GetComponentsInChildren<Button>(true).ToDictionary(b=>b.name);
            var groups=new[]{new[]{"Diagnostics","Overview"},new[]{"Services & conversations","LLM","Momotalk","TTS","ASR","Memory","API configuration"},new[]{"Character & actions","Character","FSM","StagePlan","Root","Bone","Expression / Mouth"}};
            foreach(var group in groups){Section(list,group[0]).SetAsLastSibling();foreach(var name in group.Skip(1))if(rows.TryGetValue(name,out var b))b.transform.SetAsLastSibling();}
            for(int i=1;i<=12&&i<app.pages.Length;i++)
            {
                var content=Content(app.pages[i].transform,"Details");if(content==null)continue;
                var blocks=content.Cast<Transform>().ToList();Section(content,"Current state").SetAsFirstSibling();
                var testing=blocks.Where(b=>b.name=="Force mock failure"||b.name=="Use mock ASR"||b.name=="ASR unavailable"||b.name=="ASR failure"||b.name=="Mock transcript").ToList();
                // Keep existing command events while splitting pairs into purpose-specific rows.
                foreach(var row in blocks.Where(b=>b.name=="Commands"))
                foreach(var b in row.GetComponentsInChildren<Button>().ToArray())
                {var wrapper=Rect(content,"Command_"+b.name);wrapper.gameObject.AddComponent<LayoutElement>().preferredHeight=44;b.transform.SetParent(wrapper,false);PhoneVisualPolish.Place((RectTransform)b.transform,0,0,368,44);var label=b.GetComponentInChildren<TMP_Text>();PhoneVisualPolish.Place(label.rectTransform,12,0,344,44);label.alignment=TextAlignmentOptions.Center;Surface((RectTransform)b.transform,Theme.chat);if(b.name=="Mock failure"||b.name=="Start mock")testing.Add(wrapper);}
                foreach(var row in blocks.Where(b=>b.name=="Commands"))Hidden(row);
                var stop=content.Cast<Transform>().Where(c=>c.name.StartsWith("Command_")&&new[]{"Stop LLM plan","Stop","Stop TTS","Cancel","Unpin selected","Clear pins","Release debug","Clear expression","Exit interaction","Root exit"}.Contains(c.name.Substring(8))).ToArray();
                if(testing.Count>0){var fold=Fold(content,"Test tools",testing.Select(t=>t.gameObject));fold.transform.SetAsLastSibling();foreach(var t in testing)t.SetAsLastSibling();}
                if(stop.Length>0){Section(content,"Stop & release").SetAsLastSibling();foreach(var t in stop)t.SetAsLastSibling();}
                var status=content.Find("Status");if(status!=null){Surface((RectTransform)status,Theme.chat);status.GetComponent<LayoutElement>().preferredHeight=96;var label=status.GetComponentInChildren<TMP_Text>();label.fontSize=13;PhoneVisualPolish.Place(label.rectTransform,12,8,344,80);}
            }
            var ui=app.GetComponent<PhoneLiveDebug>();if(ui!=null){ui.feedback.overflowMode=TextOverflowModes.Ellipsis;var footer=ui.feedback.transform.parent;Surface((RectTransform)footer,Theme.paper);ui.feedback.rectTransform.offsetMax=new Vector2(-82,0);var more=Action(footer,"FeedbackDetails","Details",282,0,86,44);var binding=more.gameObject.AddComponent<PhoneDebugFeedback>();binding.debug=ui;}
        }
        private static void Contacts(PhonePreviewApp app)
        {
            var page=app.pages[0].transform;var section=page.Find("Section");if(section!=null)section.GetComponent<TMP_Text>().color=Theme.muted;
            var ui=app.GetComponent<PhoneLiveMomotalk>();if(ui!=null){var row=ui.contactTemplate;Surface((RectTransform)row.transform,Color.white).sprite=null;var message=row.transform.Find("Message").GetComponent<TMP_Text>();message.overflowMode=TextOverflowModes.Ellipsis;message.rectTransform.sizeDelta=new Vector2(242,24);var name=row.transform.Find("Name").GetComponent<TMP_Text>();name.rectTransform.sizeDelta=new Vector2(210,26);var time=row.transform.Find("Time").GetComponent<TMP_Text>();PhoneVisualPolish.Place(time.rectTransform,288,9,58,22);time.alignment=TextAlignmentOptions.MidlineRight;time.fontSize=11;var badge=Rect(row.transform,"UnreadCount");PhoneVisualPolish.Place(badge,312,37,34,24);Surface(badge,Theme.pink);var label=Label(badge,"Count","",0,0,34,24,11,true);label.color=Color.white;label.alignment=TextAlignmentOptions.Center;badge.gameObject.SetActive(false);}
            var details=app.pages[2].transform;var actions=Content(details,"Actions");Section(actions,"Conversation management").SetAsFirstSibling();
            for(int i=4;i<=5;i++){var p=app.pages[i].transform;var label=p.Find("Confirm/Label").GetComponent<TMP_Text>();label.text=i==4?"Clear chat history…":"Clear long-term memory…";Surface((RectTransform)label.transform.parent,Theme.pale);}
            var voice=app.pages[3].transform;voice.Find("Info").GetComponent<TMP_Text>().color=Theme.muted;voice.Find("Header/Subtitle").gameObject.SetActive(false);
        }
    }
}
