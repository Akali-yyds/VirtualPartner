using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
namespace VirtualPartner.Runtime.PhoneOS
{
    /// <summary>Desktop chat submission is a key event, never a character-validation side effect.</summary>
    public sealed class PhoneMessageInputField : TMP_InputField
    {
        private bool composing;
        private int compositionFrame=-100;
        public bool CanSubmit => !composing && Time.frameCount-compositionFrame>1;
        protected override void OnEnable()
        {
            base.OnEnable();
#if ENABLE_INPUT_SYSTEM
            if(Keyboard.current!=null)Keyboard.current.onIMECompositionChange+=OnComposition;
#endif
            onValidateInput=NormalizeNewline;
        }
        protected override void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if(Keyboard.current!=null)Keyboard.current.onIMECompositionChange-=OnComposition;
#endif
            composing=false;base.OnDisable();
        }
#if ENABLE_INPUT_SYSTEM
        private void OnComposition(IMECompositionString value){composing=value.Count>0;compositionFrame=Time.frameCount;}
#endif
        public override void OnUpdateSelected(BaseEventData eventData)
        {
            var shift=false;
#if ENABLE_INPUT_SYSTEM
            shift=Keyboard.current!=null&&(Keyboard.current.leftShiftKey.isPressed||Keyboard.current.rightShiftKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            shift=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
            composing=!string.IsNullOrEmpty(Input.compositionString);
#endif
            lineType=shift||!CanSubmit?LineType.MultiLineNewline:LineType.MultiLineSubmit;
            base.OnUpdateSelected(eventData);
        }
        protected override bool IsValidChar(char c)=>c=='\v'||base.IsValidChar(c);
        private char NormalizeNewline(string current,int index,char character)
        {
            if(character=='\n'||character=='\r'||character=='\v')return CanSubmit?'\n':'\0';
            return character;
        }
    }
}
