using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    // Built from the active shell so saved scenes and both generated phone variants share one implementation.
    public sealed class PhoneRecentTasksView : MonoBehaviour
    {
        private PhonePresentationShell shell;
        private PhoneAppHost host;
        private RectTransform panel,content,viewport;
        private ScrollRect scroll;
        private GameObject empty;
        private TMP_Text notice;
        private TMP_FontAsset font;
        private Sprite rounded;
        private PhoneVisualTheme theme;
        private const string NoticeKey="VirtualPartner.PhoneOS.RecentsNoticeSeen.v1";
        private GameObject noticePanel;
        private Coroutine hideMotion;
        private bool settling=true;
        private readonly HashSet<string> removing=new HashSet<string>();
        private readonly List<PhoneRecentTaskCard> cards=new List<PhoneRecentTaskCard>();
        private readonly List<GameObject> badges=new List<GameObject>();
        private float snapTarget;
        private bool dragging;
        private const float CardWidth=296, Step=316;
        private static readonly Color Ink=new Color(.19f,.17f,.21f), Pink=new Color(.88f,.40f,.57f);
        public bool Visible => panel!=null&&panel.gameObject.activeSelf;
        public int CardCount => cards.Count;
        public void Initialize(PhonePresentationShell owner)
        {
            shell=owner;host=shell.host;theme=PhoneVisualTheme.Current;font=theme.regular;rounded=theme.card;
            var screen=shell.navigationBackground.transform.parent;
            var launch=screen.Find("Home").GetComponentsInChildren<Button>(true);
            
            var navigation=screen.Find("Navigation");
            var button=navigation.Find("RecentUnavailable")??navigation.Find("Recent");
            if(button!=null)
            {
                button.name="Recent";var b=button.GetComponent<Button>();b.interactable=true;
                // Replace the whole event to avoid double binding in regenerated prefabs.
                b.onClick=new Button.ButtonClickedEvent();b.onClick.AddListener(shell.Recent);
                var glyph=button.GetComponentInChildren<PhoneGlyph>();if(glyph!=null)glyph.color=Ink;
            }
            panel=Rect(screen,"RecentTasks");panel.anchorMin=Vector2.zero;panel.anchorMax=Vector2.one;panel.offsetMin=new Vector2(0,44);panel.offsetMax=new Vector2(0,-(shell.liveMode?30:52));
            var backdrop=Image(panel,new Color(.95f,.96f,.98f,.96f));backdrop.sprite=null;
            Label(panel,"Title","Recent apps",20,16,340,32,22,TextAlignmentOptions.MidlineLeft).font=theme.semibold;
            var tip=Rect(panel,"FirstUseTip");Place(tip,20,54,368,66);Image(tip,Color.white);noticePanel=tip.gameObject;
            notice=Label(tip,"Explanation","Removing an app keeps replies and audio running.",12,8,294,50,12,TextAlignmentOptions.MidlineLeft);
            var dismiss=Rect(tip,"Dismiss");Place(dismiss,318,10,44,44);Image(dismiss,Color.clear);dismiss.gameObject.AddComponent<Button>().onClick.AddListener(DismissNotice);Label(dismiss,"Label","OK",0,0,44,44,12,TextAlignmentOptions.Center).color=theme.pink;
            noticePanel.SetActive(PlayerPrefs.GetInt(NoticeKey,0)==0);
            viewport=Rect(panel,"Viewport");viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(0,42);viewport.offsetMax=new Vector2(0,noticePanel.activeSelf?-130:-64);
            Image(viewport,Color.clear).sprite=null;viewport.gameObject.AddComponent<RectMask2D>();
            var taskScroll=viewport.gameObject.AddComponent<PhoneTaskScrollRect>();taskScroll.owner=this;scroll=taskScroll;scroll.horizontal=true;scroll.vertical=false;scroll.inertia=true;scroll.decelerationRate=.08f;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.viewport=viewport;
            content=Rect(viewport,"Cards");content.anchorMin=new Vector2(0,0);content.anchorMax=new Vector2(0,1);content.pivot=new Vector2(0,.5f);scroll.content=content;
            empty=Rect(panel,"Empty").gameObject;var er=(RectTransform)empty.transform;er.anchorMin=er.anchorMax=new Vector2(.5f,.5f);er.sizeDelta=new Vector2(340,180);er.anchoredPosition=Vector2.zero;
            Label(er,"Message","No recent apps",0,10,340,44,22,TextAlignmentOptions.Center);
            Label(er,"Hint","Open an app from your home screen.",0,62,340,40,14,TextAlignmentOptions.Center);
            var home=Rect(er,"GoHome");Place(home,74,122,192,44);Image(home,theme.pink);home.gameObject.AddComponent<Button>().onClick.AddListener(shell.Home);Label(home,"Label","Go to home",0,0,192,44,15,TextAlignmentOptions.Center).color=Color.white;
            Label(panel,"Hint","Swipe up to remove",24,0,360,30,12,TextAlignmentOptions.Center).rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom,8,30);
            host.TasksChanged+=Refresh;host.OverviewChanged+=SetVisible;
            foreach(var b in launch)if(b.name=="Open_momotalk")badges.Add(CreateBadge(b.transform));
            panel.gameObject.SetActive(false);
        }
        private void SetVisible(bool value)
        {
            if(hideMotion!=null){StopCoroutine(hideMotion);hideMotion=null;}
            var group=panel.GetComponent<CanvasGroup>();if(group==null)group=panel.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=value;
            if(value){panel.gameObject.SetActive(true);panel.SetAsLastSibling();Refresh();}
            else
            {
                CancelGesture();var transition=shell.GetComponent<PhoneTransitionCoordinator>();
                if(panel.gameObject.activeSelf&&host.HasCurrentApp&&transition!=null&&transition.Busy)
                {panel.SetSiblingIndex(shell.navigationBackground.transform.parent.Find("Apps").GetSiblingIndex());hideMotion=StartCoroutine(HideAfterTransition(transition));}
                else panel.gameObject.SetActive(false);
            }
        }
        private IEnumerator HideAfterTransition(PhoneTransitionCoordinator transition)
        {while(transition!=null&&transition.Busy)yield return null;if(!host.IsOverviewOpen)panel.gameObject.SetActive(false);hideMotion=null;}
        private void Refresh()
        {
            if(!Visible||!host.IsOverviewOpen)return;
            var oldIndex=Mathf.RoundToInt(-content.anchoredPosition.x/Step);
            var oldPositions=new Dictionary<string,Vector2>();foreach(var c in cards)if(c!=null)oldPositions[c.AppId]=((RectTransform)c.transform).anchoredPosition;
            foreach(var card in cards){card.gameObject.SetActive(false);Destroy(card.gameObject);}cards.Clear();
            Canvas.ForceUpdateCanvases();
            var width=viewport.rect.width;var margin=(width-CardWidth)*.5f;
            content.sizeDelta=new Vector2(Mathf.Max(width,host.RecentTasks.Count*Step-20+2*margin),0);
            int i=0;
            foreach(var task in host.RecentTasks)
            {
                var root=Rect(content,"Task_"+task.Definition.AppId);root.anchorMin=new Vector2(0,0);root.anchorMax=new Vector2(0,1);root.pivot=new Vector2(0,.5f);root.sizeDelta=new Vector2(CardWidth,-8);root.anchoredPosition=new Vector2(margin+i*Step,0);
                Image(root,Color.clear);var card=root.gameObject.AddComponent<PhoneRecentTaskCard>();card.Initialize(this,task.Definition.AppId,scroll);cards.Add(card);
                var icon=Rect(root,"Icon");Place(icon,8,8,32,32);var appIcon=Image(icon,Color.white);appIcon.sprite=PhoneVisualTheme.AppIcon(task.Definition.AppId);appIcon.type=UnityEngine.UI.Image.Type.Simple;appIcon.raycastTarget=false;
                Label(root,"AppName",task.Definition.DisplayName,50,8,190,36,16,TextAlignmentOptions.MidlineLeft);
                var close=Rect(root,"RemoveTask");Place(close,248,4,44,44);Image(close,Color.clear);var cb=close.gameObject.AddComponent<Button>();var id=task.Definition.AppId;cb.onClick.AddListener(()=>Remove(id));
                var cg=Rect(close,"Cross");Place(cg,12,12,20,20);cg.localEulerAngles=new Vector3(0,0,45);var cross=cg.gameObject.AddComponent<PhoneGlyph>();cross.kind=PhoneGlyphKind.Plus;cross.color=Ink;cross.raycastTarget=false;
                var body=Rect(root,"Preview");body.anchorMin=Vector2.zero;body.anchorMax=Vector2.one;body.offsetMin=new Vector2(0,8);body.offsetMax=new Vector2(0,-52);Image(body,theme.paper);body.gameObject.AddComponent<Mask>().showMaskGraphic=true;
                if(task.Preview!=null)
                {var image=Rect(body,"Snapshot");image.anchorMin=Vector2.zero;image.anchorMax=Vector2.one;image.offsetMin=image.offsetMax=Vector2.zero;var raw=image.gameObject.AddComponent<RawImage>();raw.texture=task.Preview;raw.raycastTarget=false;var aspect=image.gameObject.AddComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=(float)task.Preview.width/task.Preview.height;}
                else
                {var text=Label(body,"Placeholder",task.PrivatePreview?"Settings\n\nPrivate preview":"Open app to view its page",12,100,256,150,17,TextAlignmentOptions.Center);text.raycastTarget=false;}
                if(id=="momotalk")card.badge=CreateBadge(icon);
                if(oldPositions.TryGetValue(id,out var old)&&Mathf.Abs(old.x-root.anchoredPosition.x)>1)StartCoroutine(MoveCard(root,old,root.anchoredPosition));
                i++;
            }
            empty.SetActive(cards.Count==0);viewport.gameObject.SetActive(cards.Count>0);panel.Find("Hint").gameObject.SetActive(cards.Count>0);
            snapTarget=-Mathf.Clamp(oldIndex,0,Mathf.Max(0,cards.Count-1))*Step;
            content.anchoredPosition=new Vector2(snapTarget,0);dragging=false;settling=true;
        }
        private static PhoneGlyphKind Kind(string id)=>id=="momotalk"?PhoneGlyphKind.Chat:id=="camera"?PhoneGlyphKind.Camera:id=="settings"?PhoneGlyphKind.Settings:PhoneGlyphKind.Debug;
        public void Open(string id)=>host.OpenApp(id);
        public RectTransform PreviewRect(string id){var card=cards.Find(c=>c.AppId==id);return card!=null?card.transform.Find("Preview") as RectTransform:null;}
        public void DismissNotice(){PlayerPrefs.SetInt(NoticeKey,1);PlayerPrefs.Save();noticePanel.SetActive(false);viewport.offsetMax=new Vector2(0,-64);}
        public void Remove(string id){if(removing.Add(id))StartCoroutine(DismissAnimated(id));}
        private IEnumerator DismissAnimated(string id)
        {
            var card=cards.Find(c=>c.AppId==id);if(card==null){removing.Remove(id);yield break;}
            var root=(RectTransform)card.transform;var start=root.anchoredPosition;var fade=card.GetComponent<CanvasGroup>();if(fade==null)fade=card.gameObject.AddComponent<CanvasGroup>();fade.blocksRaycasts=false;
            for(float e=0;e<.18f;e+=Time.unscaledDeltaTime){if(root==null)break;var t=PhoneTransitionCoordinator.Ease(e/.18f);root.anchoredPosition=start+Vector2.up*(viewport.rect.height*t);fade.alpha=1-t;yield return null;}
            removing.Remove(id);host.DismissTask(id);
        }
        private IEnumerator MoveCard(RectTransform root,Vector2 from,Vector2 to)
        {for(float e=0;e<.18f;e+=Time.unscaledDeltaTime){if(root==null)yield break;root.anchoredPosition=Vector2.Lerp(from,to,PhoneTransitionCoordinator.Ease(e/.18f));yield return null;}if(root!=null)root.anchoredPosition=to;}
        public void ReturnCard(RectTransform card,Vector2 target){StartCoroutine(MoveCard(card,card.anchoredPosition,target));}
        public void BeginDrag(){dragging=true;settling=false;}
        public void EndDrag(){dragging=false;settling=false;snapTarget=-Mathf.Clamp(Mathf.RoundToInt(-(content.anchoredPosition.x+Mathf.Clamp(scroll.velocity.x*.12f,-Step,Step))/Step),0,Mathf.Max(0,cards.Count-1))*Step;}
        private void Update()
        {
            var runtime=shell!=null?shell.GetComponent<PhoneLiveRuntime>():null;bool unread=runtime!=null&&runtime.Ready&&runtime.Conversation.TotalUnreadCount>0;
            foreach(var badge in badges)if(badge!=null)badge.SetActive(unread);
            foreach(var card in cards)if(card!=null&&card.badge!=null)card.badge.SetActive(unread);
            if(Visible&&!dragging&&!settling&&Mathf.Abs(scroll.velocity.x)<200){settling=true;scroll.StopMovement();}
            if(Visible&&!dragging&&settling)content.anchoredPosition=new Vector2(Mathf.Lerp(content.anchoredPosition.x,snapTarget,1-Mathf.Exp(-18*Time.unscaledDeltaTime)),0);
        }
        public void CancelGesture()
        {
            if(scroll==null)return;
            foreach(var card in cards)if(card!=null)card.CancelGesture();
            if(EventSystem.current!=null)scroll.OnEndDrag(new PointerEventData(EventSystem.current));
            scroll.StopMovement();EndDrag();settling=true;
        }
        private void OnApplicationFocus(bool focused){if(!focused)CancelGesture();}
        private void OnDestroy(){if(host!=null){host.TasksChanged-=Refresh;host.OverviewChanged-=SetVisible;}}
        private GameObject CreateBadge(Transform parent)
        {var badge=Rect(parent,"UnreadBadge");badge.anchorMin=badge.anchorMax=new Vector2(1,1);badge.pivot=new Vector2(.5f,.5f);badge.sizeDelta=new Vector2(10,10);badge.anchoredPosition=new Vector2(-5,-5);Image(badge,Pink).raycastTarget=false;return badge.gameObject;}
        private static RectTransform Rect(Transform parent,string name)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
        private static void Place(RectTransform r,float x,float y,float width,float height)
        {r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);}
        private Image Image(RectTransform r,Color color){var image=r.gameObject.AddComponent<Image>();image.color=color;image.sprite=rounded;image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;return image;}
        private TMP_Text Label(Transform parent,string name,string value,float x,float y,float width,float height,float size,TextAlignmentOptions alignment)
        {var r=Rect(parent,name);Place(r,x,y,width,height);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=theme.ink;t.alignment=alignment;t.raycastTarget=false;return t;}
    }

    public sealed class PhoneTaskScrollRect : ScrollRect
    {
        public PhoneRecentTasksView owner;
        public override void OnBeginDrag(PointerEventData e){owner.BeginDrag();base.OnBeginDrag(e);}
        public override void OnEndDrag(PointerEventData e){base.OnEndDrag(e);owner.EndDrag();}
    }

    public sealed class PhoneRecentTaskCard : MonoBehaviour,IPointerDownHandler,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public GameObject badge;
        private PhoneRecentTasksView view;
        private string appId;
        public string AppId=>appId;
        private float lastY,lastTime,verticalSpeed;
        private ScrollRect scroll;
        private Vector2 start,position;
        private int axis;
        private bool moved,dragActive;
        public void Initialize(PhoneRecentTasksView owner,string id,ScrollRect scroller){view=owner;appId=id;scroll=scroller;}
        private Vector2 Local(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)scroll.transform,e.position,e.pressEventCamera,out var point);return point;}
        public void OnPointerDown(PointerEventData e){moved=false;}
        public void OnPointerClick(PointerEventData e){if(!moved)view.Open(appId);}
        public void OnBeginDrag(PointerEventData e){dragActive=true;moved=true;axis=0;start=Local(e);lastY=start.y;lastTime=Time.unscaledTime;verticalSpeed=0;position=((RectTransform)transform).anchoredPosition;view.BeginDrag();scroll.OnBeginDrag(e);}
        public void OnDrag(PointerEventData e)
        {if(!dragActive)return;var point=Local(e);var dt=Time.unscaledTime-lastTime;if(dt>.001f)verticalSpeed=(point.y-lastY)/dt;lastY=point.y;lastTime=Time.unscaledTime;var delta=point-start;if(axis==0&&delta.sqrMagnitude>36)axis=Mathf.Abs(delta.x)>=Mathf.Abs(delta.y)?1:2;if(axis==1)scroll.OnDrag(e);else if(axis==2)((RectTransform)transform).anchoredPosition=position+new Vector2(0,Mathf.Max(0,delta.y));}
        public void OnEndDrag(PointerEventData e)
        {if(!dragActive)return;dragActive=false;scroll.OnEndDrag(e);var distance=Local(e).y-start.y;var remove=axis==2&&(distance>80||(distance>24&&verticalSpeed>650));view.EndDrag();if(remove)view.Remove(appId);else view.ReturnCard((RectTransform)transform,position);}
        public void CancelGesture(){if(dragActive)((RectTransform)transform).anchoredPosition=position;axis=0;dragActive=false;}
        private void OnDisable(){CancelGesture();}
    }
}
