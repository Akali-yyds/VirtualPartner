using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace VirtualPartner.Runtime
{
    public sealed class SceneOutlineRendererFeature : ScriptableRendererFeature
    {
        private const string DefaultMaskShaderName = "VirtualPartner/SceneOutlineMask";
        private const string DefaultCompositeShaderName = "VirtualPartner/SceneOutlineComposite";

        [SerializeField] private SceneOutlineSettings settings = new();
        [SerializeField] private Material maskMaterial;
        [SerializeField] private Material compositeMaterial;
        [SerializeField] private RenderPassEvent maskPassEvent = RenderPassEvent.AfterRenderingTransparents;
        [SerializeField] private RenderPassEvent compositePassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        private MaskPass maskPass;
        private CompositePass compositePass;

        public SceneOutlineSettings Settings => settings;

        public override void Create()
        {
            settings ??= new SceneOutlineSettings();
            maskPass = new MaskPass(settings)
            {
                renderPassEvent = maskPassEvent
            };
            compositePass = new CompositePass(settings)
            {
                renderPassEvent = compositePassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection ||
                UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;

            EnsureMaterials();
            if (maskMaterial == null || compositeMaterial == null || settings.proxyLayer.value == 0)
                return;

            maskPass.Setup(maskMaterial);
            compositePass.Setup(compositeMaterial, maskPass);
            renderer.EnqueuePass(maskPass);
            renderer.EnqueuePass(compositePass);
        }

        protected override void Dispose(bool disposing)
        {
            maskPass?.Dispose();
            compositePass?.Dispose();
        }

        private void EnsureMaterials()
        {
            if (maskMaterial == null)
            {
                var shader = Shader.Find(DefaultMaskShaderName);
                if (shader != null)
                {
                    maskMaterial = CoreUtils.CreateEngineMaterial(shader);
                    maskMaterial.name = "Scene Outline Mask Material";
                }
            }

            if (compositeMaterial == null)
            {
                var shader = Shader.Find(DefaultCompositeShaderName);
                if (shader != null)
                {
                    compositeMaterial = CoreUtils.CreateEngineMaterial(shader);
                    compositeMaterial.name = "Scene Outline Composite Material";
                }
            }
        }

        private sealed class MaskPass : ScriptableRenderPass
        {
            private readonly SceneOutlineSettings settings;
            private readonly List<ShaderTagId> shaderTags = new()
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            private Material material;
            private TextureHandle maskTexture;

            public MaskPass(SceneOutlineSettings settings)
            {
                this.settings = settings;
                profilingSampler = new ProfilingSampler("Scene Boundary Outline Mask");
            }

            public TextureHandle MaskTexture => maskTexture;

            public void Setup(Material overrideMaterial)
            {
                material = overrideMaterial;
            }

            public void Dispose()
            {
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                maskTexture = TextureHandle.nullHandle;
                if (material == null || settings.proxyLayer.value == 0)
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();

                var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
                desc.name = "_SceneBoundaryOutlineMask";
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                desc.format = GraphicsFormat.R8_UNorm;
                desc.msaaSamples = MSAASamples.None;
                desc.bindTextureMS = false;
                maskTexture = renderGraph.CreateTexture(desc);

                using var builder = renderGraph.AddRasterRenderPass<PassData>("Scene Boundary Outline Mask", out var passData, profilingSampler);
                passData.rendererList = CreateRendererList(renderGraph, renderingData, cameraData, lightData);

                if (!passData.rendererList.IsValid())
                    return;

                builder.UseRendererList(passData.rendererList);
                builder.SetRenderAttachment(maskTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }

            private RendererListHandle CreateRendererList(
                RenderGraph renderGraph,
                UniversalRenderingData renderingData,
                UniversalCameraData cameraData,
                UniversalLightData lightData)
            {
                var sorting = cameraData.defaultOpaqueSortFlags;
                var drawingSettings = RenderingUtils.CreateDrawingSettings(
                    shaderTags,
                    renderingData,
                    cameraData,
                    lightData,
                    sorting);
                drawingSettings.overrideMaterial = material;
                drawingSettings.overrideMaterialPassIndex = 0;

                var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.proxyLayer);
                var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
                return renderGraph.CreateRendererList(rendererListParams);
            }

            private sealed class PassData
            {
                public RendererListHandle rendererList;
            }
        }

        private sealed class CompositePass : ScriptableRenderPass
        {
            private static readonly int MaskTextureId = Shader.PropertyToID("_SceneBoundaryOutlineMask");
            private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int OutlineWidthPxId = Shader.PropertyToID("_OutlineWidthPx");
            private static readonly int OutlineSoftnessPxId = Shader.PropertyToID("_OutlineSoftnessPx");
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly MaterialPropertyBlock SharedPropertyBlock = new();

            private readonly SceneOutlineSettings settings;
            private MaskPass maskPass;
            private Material material;

            public CompositePass(SceneOutlineSettings settings)
            {
                this.settings = settings;
                profilingSampler = new ProfilingSampler("Scene Boundary Outline Composite");
            }

            public void Setup(Material compositeMaterial, MaskPass sourceMaskPass)
            {
                material = compositeMaterial;
                maskPass = sourceMaskPass;
                requiresIntermediateTexture = true;
            }

            public void Dispose()
            {
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || maskPass == null || !maskPass.MaskTexture.IsValid())
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    Debug.LogError("SceneOutlineRendererFeature requires an intermediate color texture.");
                    return;
                }

                var source = resourceData.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_SceneBoundaryOutlineComposite";
                desc.clearBuffer = false;
                var destination = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Scene Boundary Outline Composite", out var passData, profilingSampler))
                {
                    passData.source = source;
                    passData.mask = maskPass.MaskTexture;
                    passData.material = material;
                    passData.outlineColor = settings.EffectiveColor;
                    passData.widthPx = settings.ClampedWidthPx;
                    passData.softnessPx = settings.ClampedSoftnessPx;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.UseTexture(passData.mask, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        ExecuteComposite(context.cmd, data.source, data.mask, data.material, data.outlineColor, data.widthPx, data.softnessPx);
                    });
                }

                resourceData.cameraColor = destination;
            }

            private static void ExecuteComposite(
                RasterCommandBuffer commandBuffer,
                RTHandle source,
                RTHandle mask,
                Material material,
                Color outlineColor,
                float widthPx,
                float softnessPx)
            {
                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetTexture(BlitTextureId, source);
                SharedPropertyBlock.SetTexture(MaskTextureId, mask);
                SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                SharedPropertyBlock.SetColor(OutlineColorId, outlineColor);
                SharedPropertyBlock.SetFloat(OutlineWidthPxId, widthPx);
                SharedPropertyBlock.SetFloat(OutlineSoftnessPxId, softnessPx);
                commandBuffer.DrawProcedural(
                    Matrix4x4.identity,
                    material,
                    0,
                    MeshTopology.Triangles,
                    3,
                    1,
                    SharedPropertyBlock);
            }

            private sealed class PassData
            {
                public TextureHandle source;
                public TextureHandle mask;
                public Material material;
                public Color outlineColor;
                public float widthPx;
                public float softnessPx;
            }
        }
    }
}
