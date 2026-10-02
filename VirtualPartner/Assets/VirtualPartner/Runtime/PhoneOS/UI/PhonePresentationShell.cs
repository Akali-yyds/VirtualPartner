using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhonePresentationShell : MonoBehaviour
    {
        public bool liveMode;
        public RectTransform device;
        public CanvasGroup deviceGroup;
        public GameObject outsideBlocker, entryButton, unreadDot, dock;
        public PhoneAppHost host;
        public Image wallpaper, statusBackground, navigationBackground;
        public Sprite[] wallpapers;
        public TMP_Text statusTime, clockTime, clockDate, secondaryTime, secondaryDate;
        public RectTransform hourHand, minuteHand;
        public bool IsOpen { get; private set; }
        public float HeightFraction { get; private set; } = .9f;
        public bool Use24HourTime { get; private set; } = true;
        public bool ShowDock { get; private set; } = true;
        public bool AutoSendVoice { get; private set; }
        public string WallpaperId { get; private set; } = "pink";
        private Coroutine motion;
        private int lastMinute = -1;
        private readonly string[] wallpaperIds = { "pink", "lavender", "sunrise" };
        // Visual review never changes the user's live settings or service configuration.
        private const string PreviewKey = "VirtualPartner.PhoneOS.VisualPreview.Settings.v1";
        private void Start()
        {
            var data = JsonUtility.FromJson<PreviewSettings>(PlayerPrefs.GetString(PreviewKey, "{}"));
            HeightFraction = data.height == 0 ? .9f : Mathf.Clamp(data.height, .7f, .95f);
            Use24HourTime = !data.twelveHour; ShowDock = !data.hideDock; AutoSendVoice = data.autoSend;
            if(liveMode)
            {
                var saved=PhoneSettingsStore.Load();HeightFraction=saved.heightFraction;Use24HourTime=saved.use24HourTime;ShowDock=saved.showDock;AutoSendVoice=saved.autoSendVoice;data.wallpaper=saved.wallpaperId;
            }
            ShowDock=true; PhoneVisualPolish.ApplyShell(this);
            SetWallpaperVisual(data.wallpaper); dock.SetActive(true);
            host.ApplicationChanged += OnAppChanged;
            IsOpen = false; deviceGroup.alpha = 0;
            deviceGroup.interactable = deviceGroup.blocksRaycasts = false;
            outsideBlocker.SetActive(false); entryButton.SetActive(true); unreadDot.SetActive(false);
            host.SetSuspended(true); LayoutDevice(); UpdateClock();
            var recents=GetComponent<PhoneRecentTasksView>();if(recents==null)recents=gameObject.AddComponent<PhoneRecentTasksView>();recents.Initialize(this);
        }
        private void Update()
        {
            LayoutDevice();
            if (DateTime.Now.Minute != lastMinute) UpdateClock();
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && IsOpen) Back();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape) && IsOpen) Back();
#endif
        }
        private void LayoutDevice()
        {
            var area = ((RectTransform)transform).rect;
            var scale = Mathf.Min(area.height * HeightFraction / 912f, (area.width - 48) / 456f);
            device.localScale = Vector3.one * Mathf.Max(.1f, scale);
        }
        public void Open() => SetOpen(true);
        public void Collapse() => SetOpen(false);
        private void SetOpen(bool value)
        {
            if (IsOpen == value) return;
            IsOpen = value; host.SetSuspended(!value);
            if(!value){var recents=GetComponent<PhoneRecentTasksView>();if(recents!=null)recents.CancelGesture();}
            if(!value && EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(null);
            outsideBlocker.SetActive(value); entryButton.SetActive(!value);
            deviceGroup.interactable = deviceGroup.blocksRaycasts = value;
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(AnimateVisibility(value));
        }
        private IEnumerator AnimateVisibility(bool open)
        {
            var alpha = deviceGroup.alpha;
            var startX = device.anchoredPosition.x;
            for (var t = 0f; t < .2f; t += Time.unscaledDeltaTime)
            {
                var v = 1 - Mathf.Pow(1 - t / .2f, 3);
                deviceGroup.alpha = Mathf.Lerp(alpha, open ? 1 : 0, v);
                device.anchoredPosition = new Vector2(Mathf.Lerp(startX, open ? -24 : 30, v), 0);
                yield return null;
            }
            deviceGroup.alpha = open ? 1 : 0; device.anchoredPosition = new Vector2(open ? -24 : 30, 0);
            motion = null;
        }
        public void Back() { if (!host.HandleBackPressed()) Collapse(); }
        public void Recent() => host.ToggleOverview();
        public void Home() => host.CloseCurrentApp();
        public void OpenApp(string id) { Open(); host.OpenApp(id); }
        public void SetHeight(float value) { HeightFraction = Mathf.Clamp(value, .7f, .95f); Save(); }
        public void Set24Hour(bool value) { Use24HourTime = value; UpdateClock(); Save(); }
        public void SetDock(bool value) { ShowDock = true; dock.SetActive(true); }
        public void SetAutoSend(bool value) { AutoSendVoice = value; Save(); }
        public void SetWallpaper(string id) { SetWallpaperVisual(id); Save(); }
        private void SetWallpaperVisual(string id)
        {
            var index = Array.IndexOf(wallpaperIds, id); if (index < 0) index = 0;
            WallpaperId = wallpaperIds[index];
            if (wallpapers != null && index < wallpapers.Length) wallpaper.sprite = wallpapers[index];
        }
        private void UpdateClock()
        {
            var now = DateTime.Now; lastMinute = now.Minute;
            statusTime.text = now.ToString(Use24HourTime ? "HH:mm" : "h:mm tt", CultureInfo.InvariantCulture);
            clockTime.text = now.ToString(Use24HourTime ? "HH:mm" : "h:mm tt", CultureInfo.InvariantCulture);
            clockDate.text = now.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
            if(secondaryTime!=null)secondaryTime.text=statusTime.text;
            if(secondaryDate!=null)secondaryDate.text=now.ToString("ddd, MMMM d",CultureInfo.InvariantCulture);
            if(hourHand!=null)hourHand.localEulerAngles=new Vector3(0,0,-(now.Hour%12*30+now.Minute*.5f));
            if(minuteHand!=null)minuteHand.localEulerAngles=new Vector3(0,0,-now.Minute*6);
        }
        private void OnAppChanged(PhoneAppDefinition definition)
        {
            var color = host.IsOverviewOpen ? (Color)new Color32(242,244,249,255) : definition == null ? Color.clear : PhoneVisualTheme.Current.paper;
            statusBackground.color = navigationBackground.color = color;
        }
        private void Save()
        {
            if(liveMode)PhoneSettingsStore.Save(new PhoneSettingsData{heightFraction=HeightFraction,use24HourTime=Use24HourTime,showDock=ShowDock,autoSendVoice=AutoSendVoice,wallpaperId=WallpaperId});
            else PlayerPrefs.SetString(PreviewKey,JsonUtility.ToJson(new PreviewSettings{height=HeightFraction,twelveHour=!Use24HourTime,hideDock=!ShowDock,autoSend=AutoSendVoice,wallpaper=WallpaperId}));
        }
        private void OnApplicationQuit() => PlayerPrefs.Save();
        private void OnDestroy() { if (host != null) host.ApplicationChanged -= OnAppChanged; }
        [Serializable] private sealed class PreviewSettings
        { public float height; public bool twelveHour, hideDock, autoSend; public string wallpaper; }
    }
}
