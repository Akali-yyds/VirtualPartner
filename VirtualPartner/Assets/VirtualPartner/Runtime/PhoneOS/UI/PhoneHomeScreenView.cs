using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneHomeScreenView : MonoBehaviour
    {
        [SerializeField] private PhoneOSStyle style;
        [SerializeField] private PhoneAppRegistry registry;
        [SerializeField] private PhoneAppIconView appIconPrefab;
        [SerializeField] private Transform appGridRoot;
        [SerializeField] private Transform dockRoot;
        [SerializeField] private bool rebuildOnStart = true;

        private readonly List<PhoneAppIconView> generatedIcons = new List<PhoneAppIconView>();

        public void Rebuild()
        {
            ClearGeneratedIcons();
            if (registry == null || appIconPrefab == null)
                return;

            ApplyStyleToGrid(appGridRoot, false);
            ApplyStyleToGrid(dockRoot, true);
            BuildIcons(registry.GetHomeScreenApps(), appGridRoot, false);
            BuildIcons(registry.GetDockApps(), dockRoot, true);
        }

        private void Start()
        {
            if (rebuildOnStart)
                Rebuild();
        }

        private void BuildIcons(List<PhoneAppDefinition> apps, Transform parent, bool dockIcons)
        {
            if (apps == null || parent == null)
                return;

            for (var i = 0; i < apps.Count; i++)
            {
                var icon = Instantiate(appIconPrefab, parent);
                icon.name = "AppIcon_" + apps[i].AppId;
                icon.gameObject.SetActive(true);
                icon.ApplyStyle(style, dockIcons);
                icon.Bind(apps[i], HandleAppClicked);
                generatedIcons.Add(icon);
            }
        }

        private void ApplyStyleToGrid(Transform root, bool dockGrid)
        {
            if (style == null || root == null)
                return;

            var grid = root.GetComponent<GridLayoutGroup>();
            if (grid != null)
                grid.cellSize = dockGrid ? style.DockCellSize : style.AppGridCellSize;
        }

        private void ClearGeneratedIcons()
        {
            for (var i = generatedIcons.Count - 1; i >= 0; i--)
            {
                var icon = generatedIcons[i];
                if (icon == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(icon.gameObject);
                else
                    DestroyImmediate(icon.gameObject);
            }

            generatedIcons.Clear();
        }

        private void HandleAppClicked(PhoneAppDefinition app)
        {
            if (app == null)
                return;

            Debug.Log($"[PhoneOS] App clicked: {app.AppId}", this);
        }
    }
}
