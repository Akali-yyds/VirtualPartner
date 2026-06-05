Shader "VirtualPartner/Scene/Campus Background Blur"
{
    Properties
    {
        [MainTexture] _MainTex ("Background Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        _BlurRadius ("Blur Radius (Texture Pixels)", Range(0,32)) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "CampusBackgroundBlur"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _BlurRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 SampleBackground(float2 uv)
            {
                return tex2D(_MainTex, saturate(uv));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 step1 = _MainTex_TexelSize.xy * max(_BlurRadius, 0.0);
                float2 step2 = step1 * 2.0;

                fixed4 color = SampleBackground(i.uv) * 0.20;

                color += SampleBackground(i.uv + float2( step1.x, 0.0)) * 0.10;
                color += SampleBackground(i.uv + float2(-step1.x, 0.0)) * 0.10;
                color += SampleBackground(i.uv + float2(0.0,  step1.y)) * 0.10;
                color += SampleBackground(i.uv + float2(0.0, -step1.y)) * 0.10;

                color += SampleBackground(i.uv + float2( step1.x,  step1.y)) * 0.05;
                color += SampleBackground(i.uv + float2(-step1.x,  step1.y)) * 0.05;
                color += SampleBackground(i.uv + float2( step1.x, -step1.y)) * 0.05;
                color += SampleBackground(i.uv + float2(-step1.x, -step1.y)) * 0.05;

                color += SampleBackground(i.uv + float2( step2.x, 0.0)) * 0.035;
                color += SampleBackground(i.uv + float2(-step2.x, 0.0)) * 0.035;
                color += SampleBackground(i.uv + float2(0.0,  step2.y)) * 0.035;
                color += SampleBackground(i.uv + float2(0.0, -step2.y)) * 0.035;

                color += SampleBackground(i.uv + float2( step2.x,  step2.y)) * 0.015;
                color += SampleBackground(i.uv + float2(-step2.x,  step2.y)) * 0.015;
                color += SampleBackground(i.uv + float2( step2.x, -step2.y)) * 0.015;
                color += SampleBackground(i.uv + float2(-step2.x, -step2.y)) * 0.015;

                return color * _Color;
            }
            ENDCG
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
