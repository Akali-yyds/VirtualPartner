Shader "VirtualPartner/SceneOutlineComposite"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.78, 0.92, 1, 0.75)
        _OutlineWidthPx ("Width Px", Float) = 4
        _OutlineSoftnessPx ("Softness Px", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "SceneOutlineComposite"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_SceneBoundaryOutlineMask);

            float4 _OutlineColor;
            float _OutlineWidthPx;
            float _OutlineSoftnessPx;

            half SampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_SceneBoundaryOutlineMask, sampler_LinearClamp, uv).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord.xy;
                half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half center = SampleMask(uv);

                float2 texel = 1.0 / _ScreenParams.xy;
                float width = clamp(_OutlineWidthPx, 2.0, 8.0);
                float softness = clamp(_OutlineSoftnessPx, 0.0, 8.0);
                int sampleRadius = clamp((int)ceil(width + softness), 2, 8);
                half maxNeighbor = 0;

                [loop]
                for (int y = -8; y <= 8; y++)
                {
                    [loop]
                    for (int x = -8; x <= 8; x++)
                    {
                        float dist = length(float2(x, y));
                        if (dist > sampleRadius || dist <= 0.01)
                            continue;

                        float falloff = softness <= 0.001
                            ? (dist <= width ? 1.0 : 0.0)
                            : saturate((width + softness - dist) / softness);
                        float2 offset = float2(x, y) * texel;
                        maxNeighbor = max(maxNeighbor, SampleMask(uv + offset) * falloff);
                    }
                }

                half edge = saturate(maxNeighbor - center);
                half alpha = edge * _OutlineColor.a;
                return lerp(sourceColor, half4(_OutlineColor.rgb, 1), alpha);
            }
            ENDHLSL
        }
    }
}
