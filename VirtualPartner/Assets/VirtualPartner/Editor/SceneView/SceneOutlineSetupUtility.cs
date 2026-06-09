using System;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VirtualPartner.Runtime;

namespace VirtualPartner.EditorTools
{
    public static class SceneOutlineSetupUtility
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string MaterialFolder = "Assets/VirtualPartner/Materials";

        public static string Apply()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);

            var backgroundLayer = EnsureLayer("SceneBackground", 8);
            var proxyLayer = EnsureLayer("SceneBoundaryProxy", 9);
            EnsureFolder(MaterialFolder);

            var maskMaterial = GetOrCreateMaterial(
                MaterialFolder + "/M_SceneOutlineMask.mat",
                "VirtualPartner/SceneOutlineMask");
            var compositeMaterial = GetOrCreateMaterial(
                MaterialFolder + "/M_SceneOutlineComposite.mat",
                "VirtualPartner/SceneOutlineComposite");
            var hiddenProxyMaterial = GetOrCreateMaterial(
                MaterialFolder + "/M_SceneOutlineProxyHidden.mat",
                "VirtualPartner/SceneOutlineProxyHidden");

            var background = GameObject.Find("SceneBackground_CampusCourtyard");
            if (background != null)
                SetLayerRecursively(background, backgroundLayer);

            var roomRoot = GameObject.Find("sb_CH0310_bg_01");
            var roomBounds = GetRendererBounds(roomRoot);

            var sceneCameraGo = GameObject.Find("Main Camera") ?? GameObject.Find("SceneCamera");
            if (sceneCameraGo == null)
                throw new InvalidOperationException("No Main Camera or SceneCamera found.");

            sceneCameraGo.name = "SceneCamera";
            sceneCameraGo.tag = "MainCamera";
            var sceneCamera = GetOrAdd<Camera>(sceneCameraGo);
            var sceneCameraTransform = sceneCamera.transform;

            sceneCamera.clearFlags = CameraClearFlags.Nothing;
            sceneCamera.depth = 0f;
            sceneCamera.cullingMask = ~(1 << backgroundLayer);

            var sceneData = sceneCamera.GetUniversalAdditionalCameraData();
            SetCameraRenderType(sceneData, CameraRenderType.Overlay);
            sceneData.renderPostProcessing = true;
            sceneData.requiresDepthTexture = true;
            sceneData.requiresColorTexture = true;
            sceneData.SetRenderer(0);
            SetCameraClearDepth(sceneData, true);
            GetOrAdd<CinemachineBrain>(sceneCameraGo);

            var backgroundCameraGo = FindOrCreate("BackgroundCamera");
            backgroundCameraGo.tag = "Untagged";
            backgroundCameraGo.transform.SetPositionAndRotation(sceneCameraTransform.position, sceneCameraTransform.rotation);
            backgroundCameraGo.transform.localScale = sceneCameraTransform.localScale;

            var backgroundCamera = GetOrAdd<Camera>(backgroundCameraGo);
            CopyCameraSettings(sceneCamera, backgroundCamera);
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.depth = -20f;
            backgroundCamera.cullingMask = 1 << backgroundLayer;

            var backgroundData = backgroundCamera.GetUniversalAdditionalCameraData();
            SetCameraRenderType(backgroundData, CameraRenderType.Base);
            backgroundData.renderPostProcessing = true;
            backgroundData.requiresDepthTexture = true;
            backgroundData.requiresColorTexture = true;
            backgroundData.SetRenderer(0);
            SetBaseCameraStack(backgroundData, sceneCamera);

            var focusGo = FindOrCreate("SceneCameraFocusTarget");
            var focusPosition = roomBounds.center;
            if (roomRoot != null)
                focusPosition.y = Mathf.Max(roomBounds.min.y + 0.9f, roomBounds.center.y);
            focusGo.transform.position = focusPosition;

            var rigGo = FindOrCreate("VirtualSceneCameraRig");
            rigGo.transform.SetPositionAndRotation(sceneCameraTransform.position, sceneCameraTransform.rotation);

            var cmCamera = GetOrAdd<CinemachineCamera>(rigGo);
            cmCamera.Target.TrackingTarget = focusGo.transform;
            cmCamera.Target.CustomLookAtTarget = true;
            cmCamera.Target.LookAtTarget = focusGo.transform;
            cmCamera.Lens = LensSettings.FromCamera(sceneCamera);
            cmCamera.Priority = 10;

            var orbital = GetOrAdd<CinemachineOrbitalFollow>(rigGo);
            ConfigureOrbitalFollow(orbital, sceneCameraTransform.position, focusGo.transform.position);

            var composer = GetOrAdd<CinemachineRotationComposer>(rigGo);
            composer.Damping = Vector2.zero;
            composer.TargetOffset = Vector3.zero;

            var cameraController = GetOrAdd<VirtualSceneCameraController>(rigGo);
            var controllerSo = new SerializedObject(cameraController);
            controllerSo.FindProperty("sceneCamera").objectReferenceValue = cmCamera;
            controllerSo.FindProperty("orbitalFollow").objectReferenceValue = orbital;
            controllerSo.FindProperty("focusTarget").objectReferenceValue = focusGo.transform;
            controllerSo.FindProperty("outputCamera").objectReferenceValue = sceneCamera;
            controllerSo.FindProperty("enableDebugInput").boolValue = false;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            var proxyGo = FindOrCreate("SceneBoundaryProxy");
            SetLayerRecursively(proxyGo, proxyLayer);
            var proxy = GetOrAdd<SceneBoundaryProxy>(proxyGo);
            proxy.SourceRendererRoot = roomRoot != null ? roomRoot.transform : null;
            proxy.Shape = SceneBoundaryProxyShape.ClosedBoundsShell;
            proxy.Height = 0.18f;
            proxy.VerticalOffset = 0.01f;
            proxy.Outset = 0.005f;
            proxy.UseProjectedSilhouette = false;
            proxy.SilhouetteCamera = sceneCamera;
            proxy.SilhouetteHeight = Mathf.Max(roomBounds.size.y, 2.4f);
            proxy.TopOffset = 0.015f;
            proxy.IncludeSideWalls = false;
            proxy.IncludeRightSideWall = true;
            proxy.IncludeOpenEdgeRightCap = false;
            proxy.OpenEdgeRightCapStart = 0.5f;
            proxy.EdgeRibbonThickness = 0.02f;
            proxy.RightCapTopLift = 0.035f;
            proxy.OpenEdgeReference = null;
            var openEdgeIndex = ResolveClosestBoundsEdge(roomBounds, sceneCameraTransform, 0.005f);
            proxy.OpenEdgeIndex = openEdgeIndex;
            proxy.RightSideWallEdgeIndex = ResolveScreenRightSideBoundsEdge(roomBounds, sceneCamera, openEdgeIndex, 0.005f);

            var proxyRenderer = GetOrAdd<MeshRenderer>(proxyGo);
            proxyRenderer.sharedMaterial = hiddenProxyMaterial;
            proxyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            proxyRenderer.receiveShadows = false;
            proxy.Rebuild();

            ConfigureRendererFeature(proxyLayer, maskMaterial, compositeMaterial);

            EditorUtility.SetDirty(sceneCameraGo);
            EditorUtility.SetDirty(sceneCamera);
            EditorUtility.SetDirty(sceneData);
            EditorUtility.SetDirty(backgroundCameraGo);
            EditorUtility.SetDirty(backgroundCamera);
            EditorUtility.SetDirty(backgroundData);
            EditorUtility.SetDirty(rigGo);
            EditorUtility.SetDirty(focusGo);
            EditorUtility.SetDirty(proxyGo);
            EditorUtility.SetDirty(proxy);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var proxyMesh = proxy.GetComponent<MeshFilter>().sharedMesh;
            var stackCount = backgroundData.cameraStack == null ? -1 : backgroundData.cameraStack.Count;
            return $"Configured scene outline. Layers: SceneBackground={backgroundLayer}, SceneBoundaryProxy={proxyLayer}. Background stack={stackCount}. Proxy vertices={(proxyMesh == null ? 0 : proxyMesh.vertexCount)}.";
        }

        [MenuItem("VirtualPartner/Scene View/Apply Boundary Outline Setup")]
        private static void ApplyFromMenu()
        {
            Debug.Log(Apply());
        }

        private static int EnsureLayer(string layerName, int preferredIndex)
        {
            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            var tagManager = new SerializedObject(tagManagerAssets[0]);
            var layers = tagManager.FindProperty("layers");

            for (var i = 0; i < layers.arraySize; i++)
            {
                var prop = layers.GetArrayElementAtIndex(i);
                if (prop.stringValue == layerName)
                    return i;
            }

            if (preferredIndex >= 8 &&
                preferredIndex < layers.arraySize &&
                string.IsNullOrEmpty(layers.GetArrayElementAtIndex(preferredIndex).stringValue))
            {
                layers.GetArrayElementAtIndex(preferredIndex).stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                EditorUtility.SetDirty(tagManagerAssets[0]);
                return preferredIndex;
            }

            for (var i = 8; i < layers.arraySize; i++)
            {
                var prop = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(prop.stringValue))
                    continue;

                prop.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                EditorUtility.SetDirty(tagManagerAssets[0]);
                return i;
            }

            throw new InvalidOperationException($"No free user layer for {layerName}.");
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static Material GetOrCreateMaterial(string path, string shaderName)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Shader not found: {shaderName}");

            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ConfigureRendererFeature(int proxyLayer, Material maskMaterial, Material compositeMaterial)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (rendererData == null)
                throw new InvalidOperationException($"Renderer data not found: {RendererPath}");

            SceneOutlineRendererFeature feature = null;
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(RendererPath);
            foreach (var asset in allAssets)
            {
                if (asset is SceneOutlineRendererFeature existing)
                {
                    feature = existing;
                    break;
                }
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<SceneOutlineRendererFeature>();
                feature.name = "SceneBoundaryOutline";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
            }

            var featureSo = new SerializedObject(feature);
            featureSo.FindProperty("settings.outlineColor").colorValue = new Color(0.78f, 0.92f, 1f, 0.75f);
            featureSo.FindProperty("settings.widthPx").floatValue = 4f;
            featureSo.FindProperty("settings.softnessPx").floatValue = 1.5f;
            featureSo.FindProperty("settings.alpha").floatValue = 0.75f;
            featureSo.FindProperty("settings.proxyLayer.m_Bits").intValue = 1 << proxyLayer;
            featureSo.FindProperty("maskMaterial").objectReferenceValue = maskMaterial;
            featureSo.FindProperty("compositeMaterial").objectReferenceValue = compositeMaterial;
            featureSo.FindProperty("maskPassEvent").intValue = (int)RenderPassEvent.AfterRenderingTransparents;
            featureSo.FindProperty("compositePassEvent").intValue = (int)RenderPassEvent.BeforeRenderingPostProcessing;
            featureSo.ApplyModifiedPropertiesWithoutUndo();
            feature.Create();

            var rendererSo = new SerializedObject(rendererData);
            var features = rendererSo.FindProperty("m_RendererFeatures");
            var featureMap = rendererSo.FindProperty("m_RendererFeatureMap");
            var alreadyListed = false;
            for (var i = 0; i < features.arraySize; i++)
            {
                if (features.GetArrayElementAtIndex(i).objectReferenceValue == feature)
                {
                    alreadyListed = true;
                    break;
                }
            }

            if (!alreadyListed)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                featureMap.arraySize++;
                featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            }

            rendererSo.ApplyModifiedPropertiesWithoutUndo();
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(feature);
        }

        private static void ConfigureOrbitalFollow(CinemachineOrbitalFollow orbital, Vector3 cameraPosition, Vector3 focusPosition)
        {
            var toCamera = cameraPosition - focusPosition;
            var radius = Mathf.Clamp(toCamera.magnitude, 2.5f, 12f);
            var horizontalDegrees = Mathf.Atan2(-toCamera.x, -toCamera.z) * Mathf.Rad2Deg;
            var verticalDegrees = radius > 0.001f
                ? Mathf.Asin(Mathf.Clamp(toCamera.y / radius, -1f, 1f)) * Mathf.Rad2Deg
                : 17.5f;

            orbital.Radius = radius;

            var horizontalAxis = orbital.HorizontalAxis;
            horizontalAxis.Range = new Vector2(-180f, 180f);
            horizontalAxis.Wrap = true;
            horizontalAxis.Value = horizontalAxis.ClampValue(horizontalDegrees);
            orbital.HorizontalAxis = horizontalAxis;

            var verticalAxis = orbital.VerticalAxis;
            verticalAxis.Range = new Vector2(-8f, 42f);
            verticalAxis.Wrap = false;
            verticalAxis.Value = verticalAxis.ClampValue(verticalDegrees);
            orbital.VerticalAxis = verticalAxis;

            var radialAxis = orbital.RadialAxis;
            radialAxis.Range = new Vector2(1f, 1f);
            radialAxis.Value = 1f;
            orbital.RadialAxis = radialAxis;
        }

        private static void CopyCameraSettings(Camera source, Camera target)
        {
            target.clearFlags = source.clearFlags;
            target.backgroundColor = source.backgroundColor;
            target.orthographic = source.orthographic;
            target.orthographicSize = source.orthographicSize;
            target.fieldOfView = source.fieldOfView;
            target.nearClipPlane = source.nearClipPlane;
            target.farClipPlane = source.farClipPlane;
            target.depth = source.depth;
            target.allowHDR = source.allowHDR;
            target.allowMSAA = source.allowMSAA;
            target.usePhysicalProperties = source.usePhysicalProperties;
            target.sensorSize = source.sensorSize;
            target.lensShift = source.lensShift;
            target.gateFit = source.gateFit;
            target.focalLength = source.focalLength;
        }

        private static void SetCameraClearDepth(UniversalAdditionalCameraData cameraData, bool value)
        {
            var serializedObject = new SerializedObject(cameraData);
            var clearDepth = serializedObject.FindProperty("m_ClearDepth");
            if (clearDepth != null)
                clearDepth.boolValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetCameraRenderType(UniversalAdditionalCameraData cameraData, CameraRenderType type)
        {
            cameraData.renderType = type;

            var serializedObject = new SerializedObject(cameraData);
            var cameraType = serializedObject.FindProperty("m_CameraType");
            if (cameraType != null)
                cameraType.intValue = (int)type;
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetBaseCameraStack(UniversalAdditionalCameraData baseCameraData, Camera overlayCamera)
        {
            var serializedObject = new SerializedObject(baseCameraData);
            var cameraType = serializedObject.FindProperty("m_CameraType");
            if (cameraType != null)
                cameraType.intValue = (int)CameraRenderType.Base;

            var cameras = serializedObject.FindProperty("m_Cameras");
            if (cameras != null)
            {
                cameras.arraySize = 1;
                cameras.GetArrayElementAtIndex(0).objectReferenceValue = overlayCamera;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static GameObject FindOrCreate(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go : new GameObject(name);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static Bounds GetRendererBounds(GameObject root)
        {
            var renderers = root != null
                ? root.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
            var hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds ? bounds : new Bounds(Vector3.zero, new Vector3(4f, 2f, 4f));
        }

        private static int ResolveClosestBoundsEdge(Bounds bounds, Transform reference, float outset)
        {
            if (reference == null)
                return -1;

            var min = bounds.min;
            var max = bounds.max;
            var center = bounds.center;
            var y = min.y;
            var points = new[]
            {
                OutsetPoint(new Vector3(min.x, y, min.z), center, outset),
                OutsetPoint(new Vector3(max.x, y, min.z), center, outset),
                OutsetPoint(new Vector3(max.x, y, max.z), center, outset),
                OutsetPoint(new Vector3(min.x, y, max.z), center, outset)
            };

            var referencePosition = reference.position;
            referencePosition.y = 0f;
            var centerToReference = referencePosition - center;
            centerToReference.y = 0f;
            if (centerToReference.sqrMagnitude < 0.0001f)
                return -1;

            var openingEdge = 0;
            var openingScore = float.NegativeInfinity;
            for (var i = 0; i < points.Length; i++)
            {
                var next = (i + 1) % points.Length;
                var midpoint = (points[i] + points[next]) * 0.5f;
                midpoint.y = 0f;
                var centerToMidpoint = midpoint - center;
                centerToMidpoint.y = 0f;
                var score = Vector3.Dot(centerToMidpoint, centerToReference);
                if (score <= openingScore)
                    continue;

                openingScore = score;
                openingEdge = i;
            }

            return openingEdge;
        }

        private static int ResolveScreenRightSideBoundsEdge(Bounds bounds, Camera camera, int openEdge, float outset)
        {
            if (camera == null || openEdge < 0)
                return -1;

            var backEdge = (openEdge + 2) % 4;
            var min = bounds.min;
            var max = bounds.max;
            var center = bounds.center;
            var y = max.y;
            var points = new[]
            {
                OutsetPoint(new Vector3(min.x, y, min.z), center, outset),
                OutsetPoint(new Vector3(max.x, y, min.z), center, outset),
                OutsetPoint(new Vector3(max.x, y, max.z), center, outset),
                OutsetPoint(new Vector3(min.x, y, max.z), center, outset)
            };

            var bestEdge = -1;
            var bestScreenX = float.NegativeInfinity;
            for (var i = 0; i < points.Length; i++)
            {
                if (i == openEdge || i == backEdge)
                    continue;

                var next = (i + 1) % points.Length;
                var midpoint = (points[i] + points[next]) * 0.5f;
                var screen = camera.WorldToScreenPoint(midpoint);
                if (screen.z <= 0f || screen.x <= bestScreenX)
                    continue;

                bestScreenX = screen.x;
                bestEdge = i;
            }

            return bestEdge;
        }

        private static Vector3 OutsetPoint(Vector3 point, Vector3 center, float outset)
        {
            var direction = point - center;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return point;

            return point + direction.normalized * Mathf.Max(0f, outset);
        }
    }
}
