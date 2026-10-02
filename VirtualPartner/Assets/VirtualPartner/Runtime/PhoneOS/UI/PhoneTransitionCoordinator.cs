using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    // Presentation only. App/service ownership stays in PhoneAppHost.
    public sealed class PhoneTransitionCoordinator : MonoBehaviour
    {
        private Coroutine motion;
        private RectTransform moving;
        private CanvasGroup group;
        private GameObject overlay;
        private Mask windowMask;
        private Image windowMaskImage;
        private CanvasGroup destinationGroup;
        private float destinationAlpha;
        private Rect? interruptedWorld;
        public bool Busy=>motion!=null;
        public void PreserveInterruptedPose()
        {
            var rect=moving!=null?moving:overlay!=null?overlay.transform as RectTransform:null;
            if(rect==null)return;var corners=new Vector3[4];rect.GetWorldCorners(corners);
            interruptedWorld=Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
        }
        private Rect ConsumePose(Rect fallback,RectTransform space)
        {
            if(!interruptedWorld.HasValue)return fallback;var pose=interruptedWorld.Value;interruptedWorld=null;
            var a=space.InverseTransformPoint(pose.min);var b=space.InverseTransformPoint(pose.max);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
        }
        public static Rect Bounds(RectTransform target,RectTransform space)
        {
            var corners=new Vector3[4];target.GetWorldCorners(corners);
            var a=space.InverseTransformPoint(corners[0]);var b=space.InverseTransformPoint(corners[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
        }
        public void Cancel()
        {
            if(motion!=null)StopCoroutine(motion);motion=null;
            if(moving!=null){moving.localScale=Vector3.one;moving.anchoredPosition=Vector2.zero;}
            if(group!=null){group.alpha=1;group.blocksRaycasts=group.interactable=true;}
            ReleaseWindowMask();
            RestoreDestination();
            if(overlay!=null)Destroy(overlay);overlay=null;moving=null;group=null;
        }
        public void Open(GameObject app,RectTransform source,RectTransform container)
        {
            Cancel();moving=(RectTransform)app.transform;
            group=app.GetComponent<CanvasGroup>();if(group==null)group=app.AddComponent<CanvasGroup>();
            windowMaskImage=app.GetComponent<Image>();if(windowMaskImage==null)windowMaskImage=app.AddComponent<Image>();
            windowMaskImage.sprite=PhoneVisualTheme.Current.card;windowMaskImage.type=Image.Type.Sliced;windowMaskImage.raycastTarget=false;windowMaskImage.enabled=true;
            windowMask=app.GetComponent<Mask>();if(windowMask==null)windowMask=app.AddComponent<Mask>();windowMask.showMaskGraphic=false;windowMask.enabled=true;
            var target=container.rect;var start=ConsumePose(source!=null?Bounds(source,container):target,container);
            motion=StartCoroutine(Expand(start,target,PhoneVisualTheme.Current.openDuration));
        }
        private IEnumerator Expand(Rect from,Rect to,float duration)
        {
            group.interactable=group.blocksRaycasts=false;
            for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                var t=Ease(elapsed/duration);moving.localScale=new Vector3(Mathf.Lerp(from.width/to.width,1,t),Mathf.Lerp(from.height/to.height,1,t),1);
                moving.anchoredPosition=Vector2.Lerp(from.center-to.center,Vector2.zero,t);group.alpha=Mathf.Lerp(.25f,1,Mathf.Min(1,t*2));yield return null;
            }
            moving.localScale=Vector3.one;moving.anchoredPosition=Vector2.zero;group.alpha=1;group.interactable=group.blocksRaycasts=true;ReleaseWindowMask();moving=null;group=null;motion=null;
        }
        public void Depart(PhoneTaskSnapshot task,RectTransform source,RectTransform destination,RectTransform screen)
        {
            Cancel();if(task==null||destination==null)return;
            var from=ConsumePose(Bounds(source,screen),screen);var to=Bounds(destination,screen);
            // Bounds are in the screen's local space; this overlay is center-anchored.
            from.position-=screen.rect.center;to.position-=screen.rect.center;
            if(destination.name=="Preview")
            {destinationGroup=destination.GetComponent<CanvasGroup>();if(destinationGroup==null)destinationGroup=destination.gameObject.AddComponent<CanvasGroup>();destinationAlpha=destinationGroup.alpha;destinationGroup.alpha=0;}
            var root=new GameObject("PhoneTransition",typeof(RectTransform),typeof(CanvasGroup),typeof(Image),typeof(Mask));overlay=root;
            var r=(RectTransform)root.transform;r.SetParent(screen,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(.5f,.5f);
            var image=root.GetComponent<Image>();image.sprite=PhoneVisualTheme.Current.card;image.type=Image.Type.Sliced;image.color=PhoneVisualTheme.Current.paper;image.raycastTarget=false;
            root.GetComponent<Mask>().showMaskGraphic=true;root.GetComponent<CanvasGroup>().blocksRaycasts=false;
            if(task.Preview!=null&&!task.PrivatePreview)
            {var child=new GameObject("Preview",typeof(RectTransform),typeof(RawImage));child.transform.SetParent(r,false);var cr=(RectTransform)child.transform;cr.anchorMin=Vector2.zero;cr.anchorMax=Vector2.one;cr.offsetMin=cr.offsetMax=Vector2.zero;var raw=child.GetComponent<RawImage>();raw.texture=task.Preview;raw.raycastTarget=false;}
            else
            {var child=new GameObject("PrivatePreview",typeof(RectTransform),typeof(TextMeshProUGUI));child.transform.SetParent(r,false);var cr=(RectTransform)child.transform;cr.anchorMin=Vector2.zero;cr.anchorMax=Vector2.one;cr.offsetMin=cr.offsetMax=Vector2.zero;var text=child.GetComponent<TMP_Text>();text.font=PhoneVisualTheme.Current.regular;text.fontSize=16;text.color=PhoneVisualTheme.Current.muted;text.text=task.PrivatePreview?"Settings\nPrivate preview":task.Definition.DisplayName;text.alignment=TextAlignmentOptions.Center;}
            motion=StartCoroutine(Shrink(r,from,to));
        }
        private IEnumerator Shrink(RectTransform r,Rect from,Rect to)
        {
            float duration=PhoneVisualTheme.Current.closeDuration;
            for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {float t=Ease(elapsed/duration);r.anchoredPosition=Vector2.Lerp(from.center,to.center,t);r.sizeDelta=Vector2.Lerp(from.size,to.size,t);yield return null;}
            RestoreDestination();Destroy(overlay);overlay=null;motion=null;
        }
        public static float Ease(float t)=>1-Mathf.Pow(1-Mathf.Clamp01(t),3);
        private void ReleaseWindowMask(){if(windowMask!=null)windowMask.enabled=false;if(windowMaskImage!=null)windowMaskImage.enabled=false;windowMask=null;windowMaskImage=null;}
        private void RestoreDestination(){if(destinationGroup!=null)destinationGroup.alpha=destinationAlpha;destinationGroup=null;}
        private void OnDisable()=>Cancel();
    }
}
