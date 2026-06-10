using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VirtualPartner.Runtime;

namespace VirtualPartner.EditorTools
{
    public static class SceneCameraControlSetupUtility
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string IconFolder = "Assets/VirtualPartner/UI/SceneCamera/Icons";
        private const string CanvasName = "SceneCameraControlCanvas";
        private const string RootName = "SceneCameraModeRoot";
        private const int SortingOrder = 160;

        public static string Apply()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);

            EnsureFolder(IconFolder);
            var cameraIcon = GetOrCreateIcon(IconFolder + "/camera_icon.png", DrawCameraIcon);
            var resetIcon = GetOrCreateIcon(IconFolder + "/reset_icon.png", DrawResetIcon);
            var exitIcon = GetOrCreateIcon(IconFolder + "/exit_icon.png", DrawExitIcon);
            var debugIcon = GetOrCreateIcon(IconFolder + "/debug_icon.png", DrawDebugIcon);
            var buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/VirtualPartner/UI/Momotalk/Textures/unread_dot.png");

            var canvasRect = EnsureCanvas();
            var root = FindOrCreateRect(RootName, canvasRect);
            Stretch(root);

            var entryGroupRect = FindOrCreateRect("EntryGroup", root);
            Stretch(entryGroupRect);
            var entryGroup = GetOrAdd<CanvasGroup>(entryGroupRect.gameObject);

            var modeGroupRect = FindOrCreateRect("ModeGroup", root);
            Stretch(modeGroupRect);
            var modeGroup = GetOrAdd<CanvasGroup>(modeGroupRect.gameObject);

            var entryButton = CreateRoundButton(
                "CameraEntryButton",
                entryGroupRect,
                new Vector2(1f, 0f),
                new Vector2(-76f, 180f),
                new Color32(0x69, 0xA8, 0xE8, 0xFF),
                cameraIcon,
                buttonSprite,
                180f);

            var debugButton = CreateRoundButton(
                "RuntimeDebugToggleButton",
                entryGroupRect,
                new Vector2(1f, 0f),
                new Vector2(-76f, 284f),
                new Color32(0x5B, 0x6A, 0x7E, 0xFF),
                debugIcon,
                buttonSprite);

            var resetButton = CreateRoundButton(
                "CameraResetButton",
                modeGroupRect,
                new Vector2(1f, 0f),
                new Vector2(-180f, 76f),
                new Color32(0x68, 0x78, 0x8E, 0xFF),
                resetIcon,
                buttonSprite);

            var exitButton = CreateRoundButton(
                "CameraExitButton",
                modeGroupRect,
                new Vector2(1f, 0f),
                new Vector2(-76f, 76f),
                new Color32(0xFA, 0x97, 0xAD, 0xFF),
                exitIcon,
                buttonSprite);

            var view = GetOrAdd<SceneCameraModeView>(root.gameObject);
            var inputDriver = GetOrAdd<VirtualSceneCameraInputDriver>(root.gameObject);
            var controller = GetOrAdd<SceneCameraModeController>(root.gameObject);
            var cameraController = UnityEngine.Object.FindFirstObjectByType<VirtualSceneCameraController>(FindObjectsInactive.Include);
            var debugPanel = UnityEngine.Object.FindFirstObjectByType<VirtualPartnerRuntimeDebugPanel>(FindObjectsInactive.Include);
            var momotalk = UnityEngine.Object.FindFirstObjectByType<MomotalkUIManager>(FindObjectsInactive.Include);
            var roomRoot = GameObject.Find("sb_CH0310_bg_01");

            AssignView(view, entryGroup, modeGroup, entryButton, exitButton, resetButton);
            AssignInputDriver(inputDriver, cameraController);
            AssignCameraController(cameraController);
            AssignController(controller, view, cameraController, inputDriver, momotalk, roomRoot != null ? roomRoot.transform : null);
            AssignDebugToggle(
                GetOrAdd<RuntimeDebugPanelToggleButton>(debugButton.gameObject),
                debugButton,
                debugButton.GetComponent<Image>(),
                debugPanel);

            view.SetModeActive(false);
            inputDriver.SetInputEnabled(false);
            if (debugPanel != null)
                debugPanel.SetVisible(false);

            EditorUtility.SetDirty(canvasRect.gameObject);
            EditorUtility.SetDirty(root.gameObject);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(inputDriver);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(debugButton.gameObject);
            if (cameraController != null)
                EditorUtility.SetDirty(cameraController);
            if (debugPanel != null)
                EditorUtility.SetDirty(debugPanel);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Configured scene camera controls. Canvas={CanvasName}, cameraController={(cameraController == null ? "missing" : "ok")}, momotalk={(momotalk == null ? "missing" : "ok")}.";
        }

        [MenuItem("VirtualPartner/Scene View/Apply Camera Control Setup")]
        private static void ApplyFromMenu()
        {
            Debug.Log(Apply());
        }

        public static void ApplyFromBatch()
        {
            Debug.Log(Apply());
        }

        private static RectTransform EnsureCanvas()
        {
            var canvasGo = GameObject.Find(CanvasName);
            if (canvasGo == null)
                canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var rect = canvasGo.GetComponent<RectTransform>();
            rect.SetParent(null, false);
            Stretch(rect);

            var canvas = GetOrAdd<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = GetOrAdd<CanvasScaler>(canvasGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GetOrAdd<GraphicRaycaster>(canvasGo);
            return rect;
        }

        private static Button CreateRoundButton(
            string name,
            RectTransform parent,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Color color,
            Sprite iconSprite,
            Sprite buttonSprite,
            float iconRotationZ = 0f)
        {
            var rect = FindOrCreateRect(name, parent);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 88f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 88f);

            var image = GetOrAdd<Image>(rect.gameObject);
            image.sprite = buttonSprite;
            image.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = true;

            var button = GetOrAdd<Button>(rect.gameObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            GetOrAdd<MomotalkUIButtonFeedback>(rect.gameObject);

            var iconRect = FindOrCreateRect("Icon", rect);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 48f);
            iconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 48f);
            iconRect.localEulerAngles = new Vector3(0f, 0f, iconRotationZ);

            var icon = GetOrAdd<Image>(iconRect.gameObject);
            icon.sprite = iconSprite;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            return button;
        }

        private static void AssignView(
            SceneCameraModeView view,
            CanvasGroup entryGroup,
            CanvasGroup modeGroup,
            Button entryButton,
            Button exitButton,
            Button resetButton)
        {
            var serializedObject = new SerializedObject(view);
            serializedObject.FindProperty("entryGroup").objectReferenceValue = entryGroup;
            serializedObject.FindProperty("modeGroup").objectReferenceValue = modeGroup;
            serializedObject.FindProperty("entryButton").objectReferenceValue = entryButton;
            serializedObject.FindProperty("exitButton").objectReferenceValue = exitButton;
            serializedObject.FindProperty("resetButton").objectReferenceValue = resetButton;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignInputDriver(VirtualSceneCameraInputDriver inputDriver, VirtualSceneCameraController cameraController)
        {
            var serializedObject = new SerializedObject(inputDriver);
            serializedObject.FindProperty("cameraController").objectReferenceValue = cameraController;
            serializedObject.FindProperty("inputEnabled").boolValue = false;
            serializedObject.FindProperty("wheelZoomStep").floatValue = 20f;
            serializedObject.FindProperty("minZoomRadius").floatValue = 0.8f;
            serializedObject.FindProperty("maxZoomRadius").floatValue = 24f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignCameraController(VirtualSceneCameraController cameraController)
        {
            if (cameraController == null)
                return;

            var serializedObject = new SerializedObject(cameraController);
            serializedObject.FindProperty("minRadius").floatValue = 0.8f;
            serializedObject.FindProperty("maxRadius").floatValue = 24f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignController(
            SceneCameraModeController controller,
            SceneCameraModeView view,
            VirtualSceneCameraController cameraController,
            VirtualSceneCameraInputDriver inputDriver,
            MomotalkUIManager momotalk,
            Transform panBoundsSourceRoot)
        {
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("view").objectReferenceValue = view;
            serializedObject.FindProperty("cameraController").objectReferenceValue = cameraController;
            serializedObject.FindProperty("inputDriver").objectReferenceValue = inputDriver;
            serializedObject.FindProperty("momotalkUIManager").objectReferenceValue = momotalk;
            serializedObject.FindProperty("panBoundsSourceRoot").objectReferenceValue = panBoundsSourceRoot;
            serializedObject.FindProperty("panBoundsPadding").floatValue = 0.8f;
            serializedObject.FindProperty("configurePanBoundsOnAwake").boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignDebugToggle(
            RuntimeDebugPanelToggleButton toggle,
            Button button,
            Image buttonBackground,
            VirtualPartnerRuntimeDebugPanel debugPanel)
        {
            var serializedObject = new SerializedObject(toggle);
            serializedObject.FindProperty("toggleButton").objectReferenceValue = button;
            serializedObject.FindProperty("debugPanel").objectReferenceValue = debugPanel;
            serializedObject.FindProperty("closePanelOnAwake").boolValue = true;
            serializedObject.FindProperty("buttonBackground").objectReferenceValue = buttonBackground;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform FindOrCreateRect(string name, RectTransform parent)
        {
            var child = parent != null ? parent.Find(name) : null;
            if (child != null)
                return (RectTransform)child;

            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null)
                component = target.AddComponent<T>();
            return component;
        }

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static Sprite GetOrCreateIcon(string path, Action<Texture2D> draw)
        {
            if (!File.Exists(path))
            {
                var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                Clear(texture);
                draw(texture);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Clear(Texture2D texture)
        {
            var pixels = new Color32[texture.width * texture.height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);
            texture.SetPixels32(pixels);
        }

        private static void DrawCameraIcon(Texture2D texture)
        {
            var white = new Color32(255, 255, 255, 255);
            FillRoundedRect(texture, 24, 42, 80, 52, 10, white);
            FillRoundedRect(texture, 42, 32, 32, 16, 6, white);
            FillCircle(texture, 64, 68, 18, new Color32(105, 168, 232, 255));
            FillCircle(texture, 64, 68, 11, white);
            FillCircle(texture, 91, 53, 5, white);
        }

        private static void DrawResetIcon(Texture2D texture)
        {
            var white = new Color32(255, 255, 255, 255);
            DrawArc(texture, 64, 64, 34, 28f, 318f, 7, white);
            DrawLine(texture, 91, 84, 101, 86, 7, white);
            DrawLine(texture, 91, 84, 95, 96, 7, white);
            DrawLine(texture, 46, 64, 64, 64, 6, white);
            DrawLine(texture, 64, 64, 64, 81, 6, white);
        }

        private static void DrawExitIcon(Texture2D texture)
        {
            var white = new Color32(255, 255, 255, 255);
            DrawLine(texture, 42, 42, 86, 86, 9, white);
            DrawLine(texture, 86, 42, 42, 86, 9, white);
        }

        private static void DrawDebugIcon(Texture2D texture)
        {
            var white = new Color32(255, 255, 255, 255);
            FillRoundedRect(texture, 30, 30, 68, 68, 12, white);
            FillRoundedRect(texture, 38, 38, 52, 52, 8, new Color32(0x5B, 0x6A, 0x7E, 0xFF));
            DrawLine(texture, 48, 54, 80, 54, 5, white);
            DrawLine(texture, 48, 66, 72, 66, 5, white);
            DrawLine(texture, 48, 78, 84, 78, 5, white);
        }

        private static void FillRoundedRect(Texture2D texture, int x, int y, int width, int height, int radius, Color32 color)
        {
            for (var py = y; py < y + height; py++)
            {
                for (var px = x; px < x + width; px++)
                {
                    var cx = Mathf.Clamp(px, x + radius, x + width - radius - 1);
                    var cy = Mathf.Clamp(py, y + radius, y + height - radius - 1);
                    var dx = px - cx;
                    var dy = py - cy;
                    if (dx * dx + dy * dy <= radius * radius)
                        SetPixel(texture, px, py, color);
                }
            }
        }

        private static void FillCircle(Texture2D texture, int centerX, int centerY, int radius, Color32 color)
        {
            var radiusSq = radius * radius;
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSq)
                        SetPixel(texture, x, y, color);
                }
            }
        }

        private static void DrawArc(Texture2D texture, int centerX, int centerY, int radius, float startDegrees, float endDegrees, int width, Color32 color)
        {
            for (var angle = startDegrees; angle <= endDegrees; angle += 2f)
            {
                var radians = angle * Mathf.Deg2Rad;
                var x = centerX + Mathf.RoundToInt(Mathf.Cos(radians) * radius);
                var y = centerY + Mathf.RoundToInt(Mathf.Sin(radians) * radius);
                FillCircle(texture, x, y, Mathf.Max(1, width / 2), color);
            }
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, int width, Color32 color)
        {
            var dx = Mathf.Abs(x1 - x0);
            var dy = Mathf.Abs(y1 - y0);
            var sx = x0 < x1 ? 1 : -1;
            var sy = y0 < y1 ? 1 : -1;
            var err = dx - dy;

            while (true)
            {
                FillCircle(texture, x0, y0, Mathf.Max(1, width / 2), color);
                if (x0 == x1 && y0 == y1)
                    break;

                var e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private static void SetPixel(Texture2D texture, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                return;

            texture.SetPixel(x, y, color);
        }
    }
}
