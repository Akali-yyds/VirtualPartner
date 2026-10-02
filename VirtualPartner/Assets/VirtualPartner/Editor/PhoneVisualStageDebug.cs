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
        private static readonly string[] DebugSections={"Overview","LLM","StagePlan","Momotalk","TTS","ASR","Memory","Character","FSM","Root","Bone","Expression / Mouth"};
        private static readonly string[] DebugSubtitles={"Runtime and service status","Requests, prompts and responses","Validate and play action plans","Conversations and unread messages","Speech synthesis and playback","Recognition and microphone status","Long-term memory and decisions","Active character and identity","Autonomous behavior scheduler","Orientation and locomotion","Pose parameters and pinned bones","Expression and mouth overrides"};
        private static GameObject BuildDebug()
        {
            var app=App("debug");var root=Page(app,"DebugHome");Header(root,"Debug","Inspect, test and understand.",app);
            var list=Scroll(root,"Sections",68,0,out _);
            var intro=Card(list,"PreviewStatus",72,Hex("E9EEF3"));Icon(intro,PhoneGlyphKind.Debug,18,18,32,Hex("657887"));Text(intro,"Title","Developer tools",64,6,290,30,18,null,true);Text(intro,"Info","Sample controls · visual preview",64,36,290,24,12,Muted);
            var pages=new List<RectTransform>{root};
            for(var i=0;i<DebugSections.Length;i++)
            {PageLink(Row(list,DebugSections[i],DebugSubtitles[i]),app,i+1);var p=Page(app,DebugSections[i].Replace(" / ",""));Header(p,DebugSections[i],"Preview controls · no runtime changes",app,true);pages.Add(p);}
            Click(Row(list,"API configuration","Open LLM connection in Settings"),app.OpenApi);
            var editor=Page(app,"JsonEditor");Header(editor,"Text & JSON","Editable preview · copy and paste",app,true);
            var editorList=Scroll(editor,"Document",68,0,out _);var json=LabeledField(editorList,"Document","{\n  \"preview\": true,\n  \"character\": \"Toki\",\n  \"message\": \"This is an editable visual sample.\"\n}",true);
            json.GetComponent<RectTransform>().sizeDelta=new Vector2(340,354);json.transform.parent.GetComponent<LayoutElement>().preferredHeight=416;
            var editorFeedback=Note(editorList,"Changes remain in this preview session.",48);var tools=Card(editorList,"Clipboard",52,Color.clear);
            var action=tools.gameObject.AddComponent<PhonePreviewAction>();action.text=json;action.feedback=editorFeedback;
            Click(Button(tools,"Copy","Copy",0,0,115,52),action.Copy);Click(Button(tools,"Paste","Paste",126,0,115,52),action.Paste);Click(Button(tools,"Clear","Clear",252,0,115,52),action.Clear);
            var editorIndex=pages.Count;pages.Add(editor);
            var boneList=Page(app,"BoneSelection");Header(boneList,"Select bone","Preview bone list",app,true);var bones=Scroll(boneList,"Bones",68,0,out _);
            foreach(var bone in new[]{"Head","Neck","Spine","Chest","UpperArm_L","UpperArm_R","LowerArm_L","LowerArm_R","Hand_L","Hand_R","UpperLeg_L","UpperLeg_R"})
                PageLink(Row(bones,bone,"View rotation parameters"),app,11);
            var boneIndex=pages.Count;pages.Add(boneList);
            for(var i=0;i<DebugSections.Length;i++)
            {
                var content=Scroll(pages[i+1],"Details",68,0,out _);
                DebugDetail(content,app,i,editorIndex,boneIndex);
            }
            app.parentPages=new int[pages.Count];app.parentPages[editorIndex]=3;app.parentPages[boneIndex]=11;
            return SaveApp(app,pages.ToArray());
        }
        private static void Status(Transform p,string title,string value)
        {var r=Card(p,title,64);Text(r,"Label",title,16,5,336,22,13,Muted);Text(r,"Value",value,16,28,336,28,18,null,true);}
        private static void DebugDetail(Transform content,PhonePreviewApp app,int section,int editor,int bones)
        {
            TMP_Text feedback;
            switch(section)
            {
                case 0:
                    Status(content,"Runtime","Visual review mode");
                    Status(content,"Character","Toki / CH0187");
                    Status(content,"LLM · StagePlan","Not connected in preview");
                    Status(content,"TTS · ASR","Not connected in preview");
                    Status(content,"Memory · History","Live data is not loaded");break;
                case 1:
                    Status(content,"Request","No live request");LabeledField(content,"User input","Teacher, we can continue now.",true);
                    feedback=Note(content,"Service actions are preview-only.");ActionButtons(content,feedback,"Submit","Stop LLM plan");
                    PageLink(Row(content,"Prompt & response","View, edit and copy sample text"),app,editor);break;
                case 2:
                    Status(content,"Player","No active preview plan");
                    PageLink(Row(content,"StagePlan JSON","Open the full-page editor"),app,editor);
                    feedback=Note(content,"Validation and playback connect after visual approval.",70);
                    ActionButtons(content,feedback,"Load basic","Load full","Paste clipboard","Validate","Play","Replace","Stop","Clear");
                    PageLink(Row(content,"Validation result","View details and copy"),app,editor);break;
                case 3:
                    Status(content,"Conversation","Toki · preview");Status(content,"Unread messages","No live history loaded");
                    feedback=Note(content,"Navigation actions will target the new Momotalk App.",70);
                    ActionButtons(content,feedback,"Open","Close","Show contacts","History folder");break;
                case 4:
                    Status(content,"Speech service","Not connected");
                    DebugToggle(content,"Force mock failure","Simulate a provider error");DebugToggle(content,"Use 3D audio","Spatial character voice");
                    LabeledField(content,"Test speech","Teacher, we can continue now.",true);
                    feedback=Note(content,"The preview does not synthesize or play audio.",64);
                    ActionButtons(content,feedback,"Health check","Real TTS test","Warmup test","Mock failure","Stop TTS");break;
                case 5:
                    Status(content,"Microphone","Off in visual preview");Status(content,"Service · Engine · VAD","Not connected");
                    DebugToggle(content,"Use mock ASR","Test without microphone input");DebugToggle(content,"Auto-send to LLM","Submit the recognized transcript");
                    DebugToggle(content,"ASR unavailable","Simulate unavailable service");DebugToggle(content,"Force mock failure","Simulate recognition failure");
                    LabeledField(content,"Mock transcript","今天过得怎么样？");feedback=Note(content,"No microphone session is started here.",64);
                    ActionButtons(content,feedback,"Health check","Start real","Start mock","Cancel");break;
                case 6:
                    Status(content,"Character memory","No live memory loaded");
                    PageLink(Row(content,"Latest decision","Inspect raw judge response"),app,editor);
                    feedback=Note(content,"Memory and chat history remain separate.",60);
                    ActionButtons(content,feedback,"Reload memory","Judge last turn","Memory folder","Clear decision");break;
                case 7:
                    Status(content,"Character","Toki");Status(content,"Profile ID","CH0187");Status(content,"Runtime bindings","Not connected in preview");break;
                case 8:
                    Status(content,"Scheduler","Preview · inactive");Status(content,"Interaction state","No live state loaded");
                    feedback=Note(content,"Scheduler actions are not applied in preview.",64);ActionButtons(content,feedback,"Enable / disable","Enter interaction","Exit interaction");break;
                case 9:
                    Status(content,"Root orientation","No live state loaded");Status(content,"Locomotion","No active movement");
                    feedback=Note(content,"Movement constraints and action ownership remain unchanged.",78);ActionButtons(content,feedback,"Enter interaction","Exit");break;
                case 10:
                    PageLink(Row(content,"Selected bone","Head · choose a bone"),app,bones);DebugToggle(content,"Apply debug overlay","Live preview after integration");
                    for(var axis=0;axis<3;axis++)
                    {var card=Card(content,"Rotation"+axis,92);Text(card,"Axis",new[]{"X rotation","Y rotation","Z rotation"}[axis],16,6,230,28,15,null,true);var v=Text(card,"Value","0.0",276,6,76,28,14,Muted);var slider=Slider(card,"Angle",16,42,336,-45,45,0);var action=card.gameObject.AddComponent<PhonePreviewAction>();action.feedback=v;action.slider=slider;UnityEventTools.AddPersistentListener(slider.onValueChanged,action.UpdateValue);}
                    feedback=Note(content,"Preview angles do not change the character.",62);
                    ActionButtons(content,feedback,"Refresh UI","Zero","Pin selected","Pin L/R pair","Unpin selected","Clear pins","Export selected","Export pinned");
                    PageLink(Row(content,"Exported JSON","Inspect and copy bonePose output"),app,editor);break;
                case 11:
                    Status(content,"Expression","No debug override");Status(content,"Mouth","No debug override");
                    LabeledField(content,"Mouth index","0");
                    feedback=Note(content,"Applied overrides will stay active until released.",70);
                    ActionButtons(content,feedback,"Apply debug","Release debug","Neutral","Happy","Sad","Angry","Surprised","Clear expression");break;
            }
        }
        private static void DebugToggle(Transform content,string title,string subtitle)
        {var card=Card(content,title,62,Color.clear);Toggle(card,title,subtitle,0);}
    }
}
