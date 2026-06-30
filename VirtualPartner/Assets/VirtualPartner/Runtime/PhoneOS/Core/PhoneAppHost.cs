using System.Collections;
using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneAppHost : MonoBehaviour
    {
        [SerializeField] private PhoneAppRegistry registry;
        [SerializeField] private GameObject homeLayer;
        [SerializeField] private GameObject appLayer;
        [SerializeField] private RectTransform appWindowContainer;
        [SerializeField] private float openDuration = 0.18f;
        [SerializeField] private float closeDuration = 0.14f;
        [SerializeField] private float homeReturnDuration = 0.14f;

        private GameObject currentAppObject;
        private IPhoneApp currentApp;
        private PhoneAppDefinition currentAppDefinition;
        private Coroutine transitionRoutine;
        private Coroutine homeTransitionRoutine;
        private bool isClosingCurrentApp;

        public IPhoneApp CurrentApp => currentApp;

        public PhoneAppDefinition CurrentAppDefinition => currentAppDefinition;

        public bool HasCurrentApp => currentAppObject != null;

        private void Awake()
        {
            SetHomeVisible(true);
            SetAppLayerVisible(false);
        }

        public bool OpenApp(string appId, object args = null)
        {
            if (registry == null)
            {
                Debug.LogWarning("[PhoneOS] Cannot open app: registry is not assigned.", this);
                return false;
            }

            var definition = registry.FindApp(appId);
            if (definition == null)
            {
                Debug.LogWarning($"[PhoneOS] Cannot open app: '{appId}' is not registered.", this);
                return false;
            }

            if (definition.AppPrefab == null)
            {
                Debug.LogWarning($"[PhoneOS] Cannot open app: '{appId}' has no appPrefab.", this);
                return false;
            }

            if (appWindowContainer == null)
            {
                Debug.LogWarning("[PhoneOS] Cannot open app: AppWindowContainer is not assigned.", this);
                return false;
            }

            CloseCurrentAppImmediate();

            SetHomeVisible(false);
            SetAppLayerVisible(true);
            appWindowContainer.gameObject.SetActive(true);
            currentAppDefinition = definition;
            currentAppObject = Instantiate(definition.AppPrefab, appWindowContainer);
            currentAppObject.name = "AppWindow_" + definition.AppId;
            currentAppObject.SetActive(true);

            var rectTransform = currentAppObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
            }

            var windowView = currentAppObject.GetComponentInChildren<PhoneAppWindowView>(true);
            if (windowView != null)
                windowView.Bind(definition);

            currentApp = FindPhoneApp(currentAppObject);
            currentApp?.OnOpen(args);

            PlayOpenAnimation(currentAppObject);
            Debug.Log($"[PhoneOS] Open app: {definition.AppId}", this);
            return true;
        }

        public void CloseCurrentApp()
        {
            if (currentAppObject == null || isClosingCurrentApp)
                return;

            var closingObject = currentAppObject;
            var closingApp = currentApp;

            currentApp = null;
            currentAppDefinition = null;
            isClosingCurrentApp = true;
            closingApp?.OnClose();

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            SetHomeVisible(true);
            PlayHomeReturnAnimation();
            transitionRoutine = StartCoroutine(CloseCurrentAppAnimated(closingObject));
        }

        public bool HandleBackPressed()
        {
            if (currentAppObject == null)
                return false;

            if (isClosingCurrentApp)
                return true;

            if (currentApp != null && currentApp.OnBackPressed())
                return true;

            CloseCurrentApp();
            return true;
        }

        private void PlayOpenAnimation(GameObject target)
        {
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);

            isClosingCurrentApp = false;
            transitionRoutine = StartCoroutine(AnimateWindow(target, 0f, 1f, 0.96f, 1f, openDuration, false));
        }

        private void PlayHomeReturnAnimation()
        {
            if (homeLayer == null)
                return;

            if (homeTransitionRoutine != null)
            {
                StopCoroutine(homeTransitionRoutine);
                homeTransitionRoutine = null;
            }

            homeTransitionRoutine = StartCoroutine(AnimateHomeReturn());
        }

        private IEnumerator CloseCurrentAppAnimated(GameObject closingObject)
        {
            yield return AnimateWindow(
                closingObject,
                GetCanvasGroupAlpha(closingObject, 1f),
                0f,
                GetRectScale(closingObject, 1f),
                0.96f,
                closeDuration,
                true);

            currentAppObject = null;
            isClosingCurrentApp = false;
            transitionRoutine = null;

            if (appWindowContainer != null)
                appWindowContainer.gameObject.SetActive(false);

            SetAppLayerVisible(false);
            SetHomeVisible(true);
        }

        private IEnumerator AnimateHomeReturn()
        {
            var canvasGroup = homeLayer.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = homeLayer.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0.85f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, homeReturnDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                canvasGroup.alpha = Mathf.Lerp(0.85f, 1f, eased);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            homeTransitionRoutine = null;
        }

        private static float GetCanvasGroupAlpha(GameObject target, float fallback)
        {
            if (target == null)
                return fallback;

            var canvasGroup = target.GetComponent<CanvasGroup>();
            return canvasGroup != null ? canvasGroup.alpha : fallback;
        }

        private static float GetRectScale(GameObject target, float fallback)
        {
            if (target == null)
                return fallback;

            var rectTransform = target.GetComponent<RectTransform>();
            return rectTransform != null ? rectTransform.localScale.x : fallback;
        }

        private IEnumerator AnimateWindow(GameObject target, float fromAlpha, float toAlpha, float fromScale, float toScale, float duration, bool destroyWhenDone)
        {
            if (target == null)
                yield break;

            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = target.AddComponent<CanvasGroup>();

            var rectTransform = target.GetComponent<RectTransform>();
            canvasGroup.alpha = fromAlpha;
            if (rectTransform != null)
                rectTransform.localScale = Vector3.one * fromScale;

            yield return null;

            var elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
                if (rectTransform != null)
                    rectTransform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, eased);
                yield return null;
            }

            if (target == null)
                yield break;

            canvasGroup.alpha = toAlpha;
            if (rectTransform != null)
                rectTransform.localScale = Vector3.one * toScale;

            if (destroyWhenDone)
                Destroy(target);
        }

        private void CloseCurrentAppImmediate()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            if (homeTransitionRoutine != null)
            {
                StopCoroutine(homeTransitionRoutine);
                homeTransitionRoutine = null;
            }

            if (currentApp != null)
                currentApp.OnClose();

            currentAppObject = null;
            currentApp = null;
            currentAppDefinition = null;
            isClosingCurrentApp = false;
            ClearAppWindowContainer();
            RestoreHomeIfNoAppWindows();
        }

        private void ClearAppWindowContainer()
        {
            if (appWindowContainer == null)
                return;

            for (var i = appWindowContainer.childCount - 1; i >= 0; i--)
            {
                var child = appWindowContainer.GetChild(i);
                if (child == null)
                    continue;

                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            appWindowContainer.gameObject.SetActive(false);
        }

        private void RestoreHomeIfNoAppWindows()
        {
            if (appWindowContainer != null && appWindowContainer.childCount > 0)
                return;

            if (appWindowContainer != null)
                appWindowContainer.gameObject.SetActive(false);

            SetAppLayerVisible(false);
            SetHomeVisible(true);
        }

        private void SetHomeVisible(bool visible)
        {
            if (homeLayer == null)
                return;

            homeLayer.SetActive(visible);
            if (!visible)
                return;

            var canvasGroup = homeLayer.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        private void SetAppLayerVisible(bool visible)
        {
            if (appLayer != null)
                appLayer.SetActive(visible);
        }

        private static IPhoneApp FindPhoneApp(GameObject root)
        {
            if (root == null)
                return null;

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPhoneApp app)
                    return app;
            }

            return null;
        }
    }
}
