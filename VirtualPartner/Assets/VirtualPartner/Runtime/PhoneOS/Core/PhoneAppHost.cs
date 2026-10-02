using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    public interface IPhoneTaskReset { void ResetTaskView(); }

    public sealed class PhoneTaskSnapshot
    {
        public PhoneAppDefinition Definition { get; internal set; }
        public Texture2D Preview { get; internal set; }
        public bool PrivatePreview { get; internal set; }
    }

    [DisallowMultipleComponent]
    public sealed class PhoneAppHost : MonoBehaviour
    {
        [SerializeField] private PhoneAppRegistry registry;
        [SerializeField] private GameObject homeLayer, appLayer;
        [SerializeField] private RectTransform appWindowContainer;
        [SerializeField] private float openDuration = .18f, closeDuration = .14f, homeReturnDuration = .14f;
        private GameObject currentAppObject;
        private IPhoneApp currentApp;
        private PhoneAppDefinition currentAppDefinition;
        private readonly Dictionary<string, GameObject> cachedApps = new Dictionary<string, GameObject>();
        private readonly List<PhoneTaskSnapshot> tasks = new List<PhoneTaskSnapshot>();
        private string linkedOrigin, linkedTarget;
        private int linkedPage;
        private bool suspended;
        private Coroutine transition, departure;
        private Action pendingDeparture;
        private bool completingDeparture;
        private PhoneTransitionCoordinator visuals;
        private PhoneTransitionCoordinator Visuals { get { if(visuals==null)visuals=GetComponent<PhoneTransitionCoordinator>();if(visuals==null)visuals=gameObject.AddComponent<PhoneTransitionCoordinator>();return visuals; } }
        public bool NavigationPending => departure != null || (visuals!=null&&visuals.Busy);
        private RectTransform DockIcon(string id){var shell=GetComponent<PhonePresentationShell>();return shell!=null?shell.dock.transform.Find("Open_"+id) as RectTransform:null;}
        private RectTransform Screen => GetComponent<PhonePresentationShell>().navigationBackground.transform.parent as RectTransform;
        public event Action<PhoneAppDefinition> ApplicationChanged;
        public event Action TasksChanged;
        public event Action<bool> OverviewChanged;
        public bool IsOverviewOpen { get; private set; }
        public IReadOnlyList<PhoneTaskSnapshot> RecentTasks => tasks.AsReadOnly();
        public IPhoneApp CurrentApp => currentApp;
        public PhoneAppDefinition CurrentAppDefinition => currentAppDefinition;
        public bool HasCurrentApp => currentAppObject != null;
        public RectTransform WindowContainer => appWindowContainer;
        public void Configure(PhoneAppRegistry apps, GameObject home, GameObject layer, RectTransform container)
        { registry=apps;homeLayer=home;appLayer=layer;appWindowContainer=container; }
        private void Awake() { if(homeLayer!=null)homeLayer.SetActive(true);if(appLayer!=null)appLayer.SetActive(false); }
        public void SetSuspended(bool value)
        {
            if(suspended==value)return;

            suspended=value;if(value&&visuals!=null)visuals.Cancel();
            if(!IsOverviewOpen){if(value)currentApp?.OnPause();else currentApp?.OnResume();}
        }
        public bool OpenLinkedApp(string id, object args=null)
        {
            if(DeferDeparture(()=>OpenLinkedApp(id,args)))return true;
            var origin=currentAppDefinition?.AppId;
            if(!OpenApp(id,args))return false;
            if(origin!=null&&origin!=id){linkedOrigin=origin;linkedTarget=id;linkedPage=(currentApp as PhonePreviewApp)?.CurrentPage??0;}
            return true;
        }
        public bool OpenApp(string id, object args=null)
        {
            var definition=registry!=null?registry.FindApp(id):null;
            if(definition==null||definition.AppPrefab==null||appWindowContainer==null)return false;
            if(DeferDeparture(()=>OpenApp(id,args)))return true;
            var launchSource=IsOverviewOpen?GetComponent<PhoneRecentTasksView>().PreviewRect(id):DockIcon(id);
            ClearLink();StopTransition();PauseAndHide();
            IsOverviewOpen=false;
            currentAppDefinition=definition;
            bool created=!cachedApps.TryGetValue(id,out currentAppObject)||currentAppObject==null;
            if(created)
            {currentAppObject=Instantiate(definition.AppPrefab,appWindowContainer);cachedApps[id]=currentAppObject;}
            currentAppObject.name="AppWindow_"+id;
            var rect=(RectTransform)currentAppObject.transform;
            rect.pivot=new Vector2(.5f,.5f);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;
            appLayer.SetActive(true);appWindowContainer.gameObject.SetActive(true);currentAppObject.SetActive(true);homeLayer.SetActive(false);
            currentApp=FindPhoneApp(currentAppObject);
            if(currentApp is PhonePreviewApp styled)PhoneVisualPolish.ApplyApp(styled);
            currentAppObject.GetComponentInChildren<PhoneAppWindowView>(true)?.Bind(definition);
            if(created||args!=null)currentApp?.OnOpen(args);
            if(suspended)currentApp?.OnPause();else currentApp?.OnResume();
            var task=tasks.Find(t=>t.Definition.AppId==id);
            if(task==null)task=new PhoneTaskSnapshot{Definition=definition};else tasks.Remove(task);
            task.PrivatePreview=id=="settings";tasks.Insert(0,task);
            Visuals.Open(currentAppObject,launchSource,appWindowContainer);
            OverviewChanged?.Invoke(false);ApplicationChanged?.Invoke(definition);TasksChanged?.Invoke();
            return true;
        }
        public void CloseCurrentApp()
        {
            if(DeferDeparture(CloseCurrentApp))return;
            var departing=tasks.Find(t=>t.Definition==currentAppDefinition);var icon=DockIcon(currentAppDefinition?.AppId);
            StopTransition();PauseAndHide();ClearLink();
            currentAppObject=null;currentApp=null;currentAppDefinition=null;IsOverviewOpen=false;
            appLayer.SetActive(false);homeLayer.SetActive(true);
            OverviewChanged?.Invoke(false);ApplicationChanged?.Invoke(null);
            Visuals.Depart(departing,appWindowContainer,icon,Screen);
        }
        public void ToggleOverview(){if(IsOverviewOpen)ExitOverview();else ShowOverview();}
        public void ShowOverview()
        {
            if(IsOverviewOpen)return;
            if(DeferDeparture(ShowOverview))return;
            StopTransition();PauseAndHide();IsOverviewOpen=true;
            homeLayer.SetActive(false);appLayer.SetActive(false);
            OverviewChanged?.Invoke(true);ApplicationChanged?.Invoke(null);
            var task=tasks.Find(t=>t.Definition==currentAppDefinition);
            if(task!=null)Visuals.Depart(task,appWindowContainer,GetComponent<PhoneRecentTasksView>().PreviewRect(task.Definition.AppId),Screen);
        }
        public void ExitOverview()
        {
            if(!IsOverviewOpen)return;
            var source=HasCurrentApp?GetComponent<PhoneRecentTasksView>().PreviewRect(currentAppDefinition.AppId):null;
            IsOverviewOpen=false;
            homeLayer.SetActive(!HasCurrentApp);appLayer.SetActive(HasCurrentApp);
            if(HasCurrentApp){appWindowContainer.gameObject.SetActive(true);currentAppObject.SetActive(true);if(!suspended)currentApp?.OnResume();}
            if(HasCurrentApp)Visuals.Open(currentAppObject,source,appWindowContainer);
            OverviewChanged?.Invoke(false);ApplicationChanged?.Invoke(currentAppDefinition);
        }
        public void DismissTask(string id)
        {
            var task=tasks.Find(t=>t.Definition.AppId==id);if(task==null)return;
            tasks.Remove(task);ReleasePreview(task);
            if(linkedOrigin==id||linkedTarget==id)ClearLink();
            if(cachedApps.TryGetValue(id,out var root)&&root!=null)
            {
                FindPhoneApp(root)?.OnPause();
                foreach(var component in root.GetComponents<MonoBehaviour>())if(component is IPhoneTaskReset reset)reset.ResetTaskView();
                root.SetActive(false);
            }
            if(currentAppDefinition!=null&&currentAppDefinition.AppId==id)
            {StopTransition();currentApp=null;currentAppObject=null;currentAppDefinition=null;if(!IsOverviewOpen){appLayer.SetActive(false);homeLayer.SetActive(true);}ApplicationChanged?.Invoke(null);}
            TasksChanged?.Invoke();
        }
        public bool HandleBackPressed()
        {
            if(departure!=null&&!completingDeparture){pendingDeparture=()=>HandleBackPressed();return true;}
            if(IsOverviewOpen){ExitOverview();return true;}
            if(!HasCurrentApp)return false;
            var page=currentApp as PhonePreviewApp;
            if(linkedOrigin!=null&&currentAppDefinition.AppId==linkedTarget&&(page==null||page.CurrentPage==linkedPage))
            {var origin=linkedOrigin;ClearLink();OpenApp(origin);return true;}
            if(currentApp!=null&&currentApp.OnBackPressed())return true;
            CloseCurrentApp();return true;
        }
        private void ClearLink(){linkedOrigin=null;linkedTarget=null;}
        private void PauseAndHide()
        {
            if(currentAppObject==null)return;
            if(!suspended&&!IsOverviewOpen)currentApp?.OnPause();
            currentAppObject.SetActive(false);
        }
        private void StopTransition()
        {
            if(visuals!=null)visuals.Cancel();
            if(transition!=null){StopCoroutine(transition);transition=null;}
            foreach(var root in cachedApps.Values)Normalize(root);
            Normalize(homeLayer);
        }
        private static void Normalize(GameObject root)
        {if(root==null)return;root.transform.localScale=Vector3.one;var group=root.GetComponent<CanvasGroup>();if(group!=null){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}}
        private IEnumerator Animate(GameObject root,float duration)
        {
            var group=root.GetComponent<CanvasGroup>();if(group==null)group=root.AddComponent<CanvasGroup>();
            for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {var t=1-Mathf.Pow(1-elapsed/Mathf.Max(.01f,duration),3);group.alpha=Mathf.Lerp(.3f,1,t);root.transform.localScale=Vector3.one*Mathf.Lerp(.97f,1,t);yield return null;}
            Normalize(root);transition=null;
        }
        private bool DeferDeparture(Action action)
        {
            if(completingDeparture)return false;
            if(departure!=null){pendingDeparture=action;return true;}
            if(visuals!=null&&visuals.Busy)
            {
                // A partial transition is not a valid task thumbnail. Keep the last complete
                // snapshot and continue the next transition from the current visual rectangle.
                visuals.PreserveInterruptedPose();completingDeparture=true;
                try{action.Invoke();}finally{completingDeparture=false;}
                return true;
            }
            if(!HasCurrentApp||IsOverviewOpen||suspended)return false;
            pendingDeparture=action;
            StopTransition();currentApp?.OnPause();
            departure=StartCoroutine(CompleteDeparture());
            return true;
        }
        private IEnumerator CompleteDeparture()
        {
            // Let resolution/layout changes reach a rendered frame before sampling its pixel coordinates.
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            try{CaptureCurrent();}catch(Exception error){Debug.LogException(error,this);}
            var action=pendingDeparture;pendingDeparture=null;departure=null;
            completingDeparture=true;
            try{action?.Invoke();}finally{completingDeparture=false;if(!IsOverviewOpen&&!suspended&&currentApp is PhonePreviewApp page&&page.Suspended)currentApp.OnResume();}
        }
        private void CaptureCurrent()
        {
            if(!HasCurrentApp||IsOverviewOpen||suspended||!currentAppObject.activeInHierarchy)return;
            var task=tasks.Find(t=>t.Definition==currentAppDefinition);if(task==null)return;
            ReleasePreview(task);
            // Never put configuration values in a texture, including a key temporarily revealed by the user.
            task.PrivatePreview=currentAppDefinition.AppId=="settings";
            if(task.PrivatePreview)return;
            var corners=new Vector3[4];appWindowContainer.GetWorldCorners(corners);
            var canvas=appWindowContainer.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            Texture2D frame=null;
            try
            {
                frame=ScreenCapture.CaptureScreenshotAsTexture();if(frame==null)return;
                int x=Mathf.Clamp(Mathf.CeilToInt(min.x),0,frame.width-1),y=Mathf.Clamp(Mathf.CeilToInt(min.y),0,frame.height-1);
                int width=Mathf.Min(Mathf.FloorToInt(max.x)-x,frame.width-x),height=Mathf.Min(Mathf.FloorToInt(max.y)-y,frame.height-y);
                if(width<1||height<1)return;
                task.Preview=new Texture2D(width,height,TextureFormat.RGB24,false){name="PhoneTask_"+currentAppDefinition.AppId};
                task.Preview.SetPixels(frame.GetPixels(x,y,width,height));task.Preview.Apply(false,false);
            }
            finally{if(frame!=null)Destroy(frame);}
        }
        private static void ReleasePreview(PhoneTaskSnapshot task){if(task.Preview!=null)Destroy(task.Preview);task.Preview=null;}
        private static IPhoneApp FindPhoneApp(GameObject root)
        {foreach(var component in root.GetComponents<MonoBehaviour>())if(component is IPhoneApp app)return app;return null;}
        private void OnDestroy(){foreach(var task in tasks)ReleasePreview(task);foreach(var root in cachedApps.Values)if(root!=null)FindPhoneApp(root)?.OnClose();}
    }
}
