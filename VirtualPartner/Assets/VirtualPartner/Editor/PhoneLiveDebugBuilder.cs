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
        private static GameObject BuildLiveDebug()
        {
            var app=App("debug");var ui=app.gameObject.AddComponent<PhoneLiveDebug>();
            var home=Page(app,"DebugHome");Header(home,"Debug","Runtime tools",app);
            var list=Scroll(home,"Sections",68,0,out _);var pages=new List<RectTransform>{home};
            ui.summaries=new TMP_Text[12];
            for(var i=0;i<DebugSections.Length;i++)
            {
                PageLink(Row(list,DebugSections[i],DebugSubtitles[i]),app,i+1);
                var page=Page(app,DebugSections[i]);Header(page,DebugSections[i],"Live state and controls",app,true);pages.Add(page);
                var content=Scroll(page,"Details",68,54,out _);
                var status=Card(content,"Status",112);var label=Text(status,"Status","Starting…",14,8,340,96,14);label.overflowMode=TextOverflowModes.Ellipsis;label.alignment=TextAlignmentOptions.TopLeft;ui.summaries[i]=label;
                LiveDocument(Row(content,"Full diagnostics","Open scrollable status and copy"),ui,"status");
                switch(i)
                {
                    case 0:break;
                    case 1:ui.llmText=LabeledField(content,"User input","",true);LiveCommands(content,ui,"Submit","Stop LLM plan");LiveDocument(Row(content,"Prompt","View final prompt"),ui,"prompt");LiveDocument(Row(content,"Response","View raw response"),ui,"response");break;
                    case 2:LiveDocument(Row(content,"StagePlan JSON","Edit, paste and validate"),ui,"stage");LiveCommands(content,ui,"Load basic","Load full","Paste clipboard","Validate","Play","Replace","Stop","Clear");LiveDocument(Row(content,"Validation result","Errors and warnings"),ui,"validation");break;
                    case 3:LiveCommands(content,ui,"Open","Close","Show contacts","History folder");break;
                    case 4:LiveOption(content,ui,"Force mock failure","Simulate provider failure");LiveOption(content,ui,"Use 3D audio","Spatial character voice");ui.ttsText=LabeledField(content,"Test speech","老师，我在这里。",true);LiveCommands(content,ui,"TTS health","Real TTS test","Warmup test","Mock failure","Stop TTS");break;
                    case 5:LiveOption(content,ui,"Use mock ASR","Debug provider only");LiveOption(content,ui,"Auto-send to LLM","Debug result mode");LiveOption(content,ui,"ASR unavailable","Simulate unavailable service");LiveOption(content,ui,"ASR failure","Simulate recognition failure");ui.mockText=LabeledField(content,"Mock transcript","");LiveCommands(content,ui,"ASR health","Start real","Start mock","Cancel");break;
                    case 6:LiveCommands(content,ui,"Reload memory","Judge last turn","Memory folder","Clear decision");LiveDocument(Row(content,"Latest decision","Raw MemoryJudge response"),ui,"memory");break;
                    case 7:break;
                    case 8:LiveCommands(content,ui,"Enable / disable","Enter interaction","Exit interaction");break;
                    case 9:LiveCommands(content,ui,"Root interaction","Root exit");break;
                    case 10:
                        PageLink(Row(content,"Select bone","All registered control bones"),app,14);
                        var option=Card(content,"Overlay",62,Color.clear);ui.boneApply=Toggle(option,"Apply debug overlay","Effects persist until released",0);
                        ui.axes=new Slider[3];for(var axis=0;axis<3;axis++){var card=Card(content,"Rotation"+axis,80);Text(card,"Label",new[]{"X rotation","Y rotation","Z rotation"}[axis],16,4,300,24,16);ui.axes[axis]=Slider(card,"Angle",16,32,336,-45,45,0);}
                        LiveCommands(content,ui,"Refresh UI","Zero","Pin selected","Pin L/R pair","Unpin selected","Clear pins","Export selected","Export pinned");break;
                    case 11:ui.mouthIndex=LabeledField(content,"Mouth index","0");LiveCommands(content,ui,"Apply debug","Release debug");foreach(var expression in new[]{"neutral","smile","thinking","surprised","embarrassed"}){var button=Row(content,expression,"Test expression");UnityEventTools.AddStringPersistentListener(button.onClick,ui.Execute,"Expression:"+expression);}LiveCommands(content,ui,"Clear expression");break;
                }
            }
            Click(Row(list,"API configuration","Open Settings"),app.OpenApi);
            var editor=Page(app,"Document");Header(editor,"Text & JSON","Copy or edit the selected document",app,true);ui.documentTitle=editor.Find("Header/Title").GetComponent<TMP_Text>();
            var editorList=Scroll(editor,"DocumentScroll",68,60,out _);ui.document=LabeledField(editorList,"Document","",true);ui.document.GetComponent<RectTransform>().sizeDelta=new Vector2(340,438);ui.document.transform.parent.GetComponent<LayoutElement>().preferredHeight=482;
            var clipboard=Card(editorList,"Clipboard",44,Color.clear);Click(Button(clipboard,"Copy","Copy",0,0,178,44),ui.Copy);Click(Button(clipboard,"Paste","Paste",190,0,178,44),ui.Paste);pages.Add(editor);
            var selection=Page(app,"BoneSelection");Header(selection,"Select bone","Runtime bone map",app,true);ui.boneRows=Scroll(selection,"Bones",68,54,out _);pages.Add(selection);
            var templates=Rect(app.transform,"Templates");templates.gameObject.SetActive(false);ui.boneTemplate=Row(templates,"Bone","Select rotation controls");
            var footer=Rect(app.transform,"Feedback");Fill(footer,20,0,20,4);footer.anchorMin=new Vector2(0,0);footer.anchorMax=new Vector2(1,0);footer.sizeDelta=new Vector2(-40,48);footer.pivot=new Vector2(.5f,0);
            ui.feedback=Text(footer,"Text","",0,0,368,48,12);Fill(ui.feedback.rectTransform);ui.feedback.overflowMode=TextOverflowModes.Ellipsis;
            app.parentPages=new int[pages.Count];app.parentPages[13]=3;app.parentPages[14]=11;
            return SaveApp(app,pages.ToArray());
        }
        private static void LiveCommands(Transform content,PhoneLiveDebug ui,params string[] commands)
        {
            for(var i=0;i<commands.Length;i+=2){var row=Card(content,"Commands",44,Color.clear);for(var j=0;j<2&&i+j<commands.Length;j++){var b=Button(row,commands[i+j],commands[i+j],190*j,0,178,44,j==0?Color.white:Pale);UnityEventTools.AddStringPersistentListener(b.onClick,ui.Execute,commands[i+j]);}}
        }
        private static void LiveDocument(Button button,PhoneLiveDebug ui,string kind)=>UnityEventTools.AddStringPersistentListener(button.onClick,ui.OpenDocument,kind);
        private static void LiveOption(Transform content,PhoneLiveDebug ui,string option,string hint)
        {var card=Card(content,option,62,Color.clear);var toggle=Toggle(card,option,hint,0);var binding=toggle.gameObject.AddComponent<PhoneDebugOption>();binding.debug=ui;binding.option=option;}
    }
}
