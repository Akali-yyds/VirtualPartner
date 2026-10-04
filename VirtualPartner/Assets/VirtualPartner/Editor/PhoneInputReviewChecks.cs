using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using VirtualPartner.Runtime.PhoneOS;
using Object=UnityEngine.Object;

namespace VirtualPartner.EditorTools
{
    public sealed class PhoneReviewCompositionInput : BaseInput
    {
        public string composition="";
        public override string compositionString=>composition;
    }

    public static class PhoneInputReviewChecks
    {
        public static string Run()
        {
            var runtime=Object.FindFirstObjectByType<PhoneLiveRuntime>();
            var source=((PhonePreviewApp)runtime.Shell.host.CurrentApp).GetComponent<PhoneLiveMomotalk>().input;
            var clone=Object.Instantiate(source,runtime.Shell.transform);
            clone.onValueChanged=new TMP_InputField.OnChangeEvent();clone.onSubmit=new TMP_InputField.SubmitEvent();
            var group=clone.gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;
            var module=EventSystem.current.currentInputModule;var previous=module.inputOverride;
            var fakeObject=new GameObject("Isolated IME regression input");var fake=fakeObject.AddComponent<PhoneReviewCompositionInput>();
            int passed=0;
            void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);passed++;}
            void Position(string name,int value)=>typeof(TMP_InputField).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(clone,value);
            void Append(char value)=>typeof(TMP_InputField).GetMethod("Append",BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(char)},null).Invoke(clone,new object[]{value});
            try
            {
                module.inputOverride=fake;
                clone.text="ab";clone.stringPosition=1;fake.composition="s'd";clone.ForceLabelUpdate();clone.textComponent.ForceMeshUpdate(true);
                Check(!clone.richText&&!clone.textComponent.richText,"Input and renderer use plain text");
                Check(clone.textComponent.text.Contains("s'd")&&!clone.textComponent.text.Contains("<u>"),"IME composition has no visible underline markup");
                Check(clone.text=="ab","Composition does not contaminate draft");
                // Reproduce a rendered IME range extending beyond the committed string.
                fake.composition="";Position("m_StringPosition",0);Position("m_StringSelectPosition",1);
                Position("m_CaretPosition",1);Position("m_CaretSelectPosition",4);
                Append('中');Check(clone.text=="中b","Replacing stale composition selection uses raw string offsets");
                clone.text="你好abc";clone.stringPosition=2;clone.selectionStringFocusPosition=5;Append('文');
                Check(clone.text=="你好文","Mixed Chinese/ASCII selection replacement");
                clone.text="<u>literal</u>";clone.ForceLabelUpdate();clone.textComponent.ForceMeshUpdate(true);
                Check(clone.textComponent.textInfo.characterCount>=14,"User-entered angle brackets remain literal (rendered count="+clone.textComponent.textInfo.characterCount+", text="+clone.textComponent.text+")");
                clone.text="";clone.lineType=TMP_InputField.LineType.MultiLineNewline;Append('a');Append('\n');Append('中');
                Check(clone.text=="a\n中","Multiline draft accepts newline and Chinese");
                return passed+" passed: plain text, composition, stale selection, mixed text, literal tags, newline";
            }
            finally{module.inputOverride=previous;Object.Destroy(clone.gameObject);Object.Destroy(fakeObject);}
        }
    }
}
