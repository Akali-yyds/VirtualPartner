using UnityEngine;
using System.Collections.Generic;
namespace VirtualPartner.Runtime.PhoneOS
{
    /// <summary>Shared page lifecycle for preview and live apps; business services have separate owners.</summary>
    public sealed class PhonePreviewApp : MonoBehaviour, IPhoneApp, IPhoneTaskReset
    {
        public string appId;
        public GameObject[] pages;
        public int[] parentPages;
        private int page;
        private Coroutine pageMotion;
        private GameObject errorDetails;
        public void SetDetailsModal(GameObject modal){errorDetails=modal;}
        private readonly Stack<int> history=new Stack<int>();
        public string AppId => appId;
        public int CurrentPage => page;
        public bool Suspended { get; private set; }
        public event System.Action StateChanged;
        public void OnOpen(object args = null) { if (args is int index) ShowPage(index); }
        public void OnClose() { }
        public void OnPause() { Suspended=true;
            var events=UnityEngine.EventSystems.EventSystem.current;
            if(events!=null&&events.currentSelectedGameObject!=null&&events.currentSelectedGameObject.transform.IsChildOf(transform))
            {var field=events.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>();if(field!=null)field.DeactivateInputField();events.SetSelectedGameObject(null);}
            StateChanged?.Invoke(); foreach (var joystick in GetComponentsInChildren<PhoneJoystick>(true)) joystick.Release(); }
        public void OnResume() { Suspended=false;StateChanged?.Invoke(); }
        public bool OnBackPressed()
        {
            if(errorDetails!=null){Destroy(errorDetails);errorDetails=null;return true;}
            if (page == 0) return false;
            ApplyPage(history.Count>0?history.Pop():(parentPages != null && page < parentPages.Length ? parentPages[page] : 0),true);
            return true;
        }
        public void ShowPage(int index)
        {
            if (index < 0 || index >= pages.Length) return;
            if(index==page)return;
            if(index==0)history.Clear();
            else if(history.Contains(index)){while(history.Count>0&&history.Pop()!=index){} }
            else history.Push(page);
            ApplyPage(index);
        }
        private void ApplyPage(int index,bool back=false)
        {
            if(pageMotion!=null){StopCoroutine(pageMotion);pageMotion=null;}
            foreach(var item in pages){var rt=(RectTransform)item.transform;rt.anchoredPosition=Vector2.zero;var cg=item.GetComponent<CanvasGroup>();if(cg!=null){cg.alpha=1;cg.blocksRaycasts=cg.interactable=true;}}
            foreach (var joystick in GetComponentsInChildren<PhoneJoystick>(true)) joystick.Release();
            for (var i = 0; i < pages.Length; i++) pages[i].SetActive(i == index);
            page = index; StateChanged?.Invoke();
            if(isActiveAndEnabled&&!Suspended&&PhoneVisualTheme.Current!=null)pageMotion=StartCoroutine(AnimatePage(pages[index],back));
        }
        private System.Collections.IEnumerator AnimatePage(GameObject target,bool back)
        {
            var rect=(RectTransform)target.transform;var group=target.GetComponent<CanvasGroup>();if(group==null)group=target.AddComponent<CanvasGroup>();
            float duration=PhoneVisualTheme.Current.pageDuration;
            for(float e=0;e<duration;e+=Time.unscaledDeltaTime){float t=PhoneTransitionCoordinator.Ease(e/duration);rect.anchoredPosition=new Vector2((back?-24:24)*(1-t),0);group.alpha=.5f+.5f*t;yield return null;}
            rect.anchoredPosition=Vector2.zero;group.alpha=1;pageMotion=null;
        }
        private void OnDisable()
        {
            if(pageMotion!=null)StopCoroutine(pageMotion);pageMotion=null;
            foreach(var item in pages){((RectTransform)item.transform).anchoredPosition=Vector2.zero;var group=item.GetComponent<CanvasGroup>();if(group!=null)group.alpha=1;}
        }
        public void ResetTaskView()
        {
            history.Clear();ApplyPage(0);
            if(errorDetails!=null){Destroy(errorDetails);errorDetails=null;}
            foreach(var scroll in GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
            {scroll.StopMovement();scroll.verticalNormalizedPosition=1;scroll.horizontalNormalizedPosition=0;}
        }
        public void Back() => GetComponentInParent<PhoneAppHost>().HandleBackPressed();
        public void OpenApi() => GetComponentInParent<PhoneAppHost>().OpenLinkedApp("settings", 2);
    }
}
