using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneChatPreview : MonoBehaviour
    {
        public TMP_InputField input;
        public ScrollRect scroll;
        public PhoneTextBubble outgoingTemplate;
        public GameObject newMessagesButton;
        private bool submitPending;
        private const string DraftKey="VirtualPartner.PhoneOS.VisualPreview.Draft.Toki";
        private void Start()
        {
            input.SetTextWithoutNotify(PlayerPrefs.GetString(DraftKey,""));
            input.onValueChanged.AddListener(StoreDraft);
            input.onSubmit.AddListener(QueueSubmission);
            newMessagesButton.SetActive(false);
        }
        private void OnDisable()
        {
            submitPending=false;
        }
        private void QueueSubmission(string value)
        {
            if(input is PhoneMessageInputField message && message.CanSubmit)submitPending=true;
        }
        private void LateUpdate() { if(submitPending){submitPending=false;Send();} }
        private void StoreDraft(string value)=>PlayerPrefs.SetString(DraftKey,value);
        public void Send()
        {
            if(string.IsNullOrWhiteSpace(input.text))return;
            var atBottom=scroll.verticalNormalizedPosition<.04f;
            var row=Instantiate(outgoingTemplate,scroll.content);
            row.gameObject.name="PreviewMessage";row.body.text=input.text.Trim();row.gameObject.SetActive(true);
            input.text="";input.ActivateInputField();
            if(atBottom)JumpToLatest();else newMessagesButton.SetActive(true);
        }
        public void JumpToLatest() { if(isActiveAndEnabled)StartCoroutine(ScrollAfterLayout()); }
        private IEnumerator ScrollAfterLayout()
        {yield return null;yield return null;Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;newMessagesButton.SetActive(false);}
        public void ClearPreview()
        {
            newMessagesButton.SetActive(false);
            for(var i=scroll.content.childCount-1;i>=0;i--)
            {var child=scroll.content.GetChild(i);if(child.GetComponent<PhoneTextBubble>()!=null)Destroy(child.gameObject);}
        }
    }
}
