Shader "ModelRepair/Toki/CH0187/MX_C-Weapon"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _MaskTex ("Mask Tex", 2D) = "black" {}
        _HairSpecTex ("Hair Spec Tex", 2D) = "black" {}
        _SourceTex ("Source Tex", 2D) = "black" {}
        _TempTex ("Temp Tex", 2D) = "white" {}
        _MouthTileTex ("Mouth Tile Tex", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Color ("Color", Color) = (1,1,1,1)
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _TwoSideTint ("Two Side Tint", Color) = (1,1,1,1)
        _EyeTint ("Eye Tint", Color) = (1,1,1,1)
        _MouthTint ("Mouth Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.5,0.5,0.5,1)
        _ShadowLightDir ("Shadow Light Dir", Vector) = (0,-0.94,0.342,0)
        _MxCharLightDir ("Mx Char Light Dir", Vector) = (0.1,0.65,0,0)
        _MxCharLightTone ("Mx Char Light Tone", Color) = (1,1,1,1)
        _MxCharShadowTone ("Mx Char Shadow Tone", Color) = (1,1,1,1)
        _MxCharLightData ("Mx Char Light Data", Vector) = (1,1,0,0)
        _ShadowTintR4 ("Shadow Tint R", Vector) = (0.5,0.5,0.5,1)
        _ShadowTintG4 ("Shadow Tint G", Vector) = (0.5,0.5,0.5,1)
        _ShadowTintB4 ("Shadow Tint B", Vector) = (0.5,0.5,0.5,1)
        _ShadowThreshold4 ("Shadow Threshold 4", Vector) = (0.5,0.5,0.5,0.5)
        _BaseBrightness4 ("Base Brightness 4", Vector) = (0,0,0,0)
        _ViewOffset4 ("View Offset 4", Vector) = (0,0,0,0)
        _ViewPower4 ("View Power 4", Vector) = (1,1,1,1)
        _ViewStrength4 ("View Strength 4", Vector) = (0,0,0,0)
        _InvViewPower4 ("Inv View Power 4", Vector) = (1,1,1,1)
        _InvViewStrength4 ("Inv View Strength 4", Vector) = (0,0,0,0)
        _ViewLightEdge4 ("View Light Edge 4", Vector) = (0,0,0,0)
        _RimAreaMultiplier4 ("Rim Area Multiplier 4", Vector) = (3,3,3,3)
        _RimStrength4 ("Rim Strength 4", Vector) = (1,1,1,1)
        _SpecDirMultiplier ("Spec Dir Transform", Vector) = (0,0,0,0)
        _InlineTint ("Inline Tint", Color) = (0.5,0.5,0.5,1)
        _FakeLightDir ("Fake Light Dir", Vector) = (0.1,0.65,0,0)
        _OutlineTint ("Outline Tint", Color) = (0.5,0.5,0.5,1)
        _OutlineSolidColorTint ("Solid Color Tint", Color) = (0.7,0,0,1)
        _SpecColor ("Spec Color", Color) = (0.2,0.2,0.2,1)
        _GlowTint ("Glow Tint", Color) = (0,0,0,1)
        _GlowTint0 ("Glow Tint 0", Color) = (0,0,0,1)
        _GlowMaskColor0 ("Glow Mask Color 0", Color) = (1,1,1,1)
        _CodeAddColor ("_CodeAddColor", Color) = (0,0,0,0)
        _CodeMultiplyColor ("_CodeMultiplyColor", Color) = (1,1,1,1)
        _CodeAddRimColor ("_CodeAddRimColor", Color) = (0,0,0,0)
        _Cutoff ("Cutoff", Range(0,1)) = 0.5
        _MouthTileEnabled ("Mouth Tile Enabled", Float) = 1
        _MouthTileRows ("Mouth Tile Rows", Float) = 8
        _MouthTileCols ("Mouth Tile Cols", Float) = 8
        _MaskGSensitivity ("Mask G Sensitivity", Range(0,3)) = 1
        _ShadowThreshold ("Shadow Threshold", Range(-5,5)) = 0.5
        _BaseBrightness ("Base Brightness", Range(-2,5)) = 0
        _ViewOffset ("View Light Angle Offset", Range(-8,8)) = 0
        _ViewPower ("View Light Sharpness", Range(1,128)) = 1
        _ViewStrength ("View Light Strength", Range(-10,10)) = 0
        _InvViewPower ("Inv.View Light Sharpness", Range(1,128)) = 1
        _InvViewStrength ("Inv.View Light Strength", Range(-10,10)) = 0
        _ViewLightEdge ("View Light Edge", Range(0,1)) = 0
        _ShadowStrong ("Shadow Strong", Float) = 4
        _LightValue ("Light Value", Range(0,1)) = 0.5
        _LightStrong ("Light Strong", Float) = 10
        _SpecStrong ("Spec Strong", Range(0,10)) = 1
        _SpecStrength ("Spec Strength", Range(0,10)) = 1
        _SpecTopMultiplier ("Spec Top Multiplier", Range(1,20)) = 3
        _SpecTopLeveler ("Spec Top Leveler", Float) = 2
        _SpecBotArea ("Spec Bottom Area", Range(0,1)) = 0.3
        _SpecBotMultiplier ("Spec Bottom Multiplier", Float) = 11
        _RimAreaMultiplier ("Rim Area Multiplier", Range(1,7)) = 3
        _RimAreaLeveler ("Rim Area Leveler", Float) = 2
        _RimStrength ("Rim Strength", Range(0,10)) = 1
        _InlineAmount ("Inline Amount", Range(0,1)) = 1
        _AdditionalLightStrength ("Additional Light Strength", Range(0,2)) = 1
        _AdditionalLightSharpness ("Additional Light Sharpness", Range(0,100)) = 5
        _GlowStrength ("Glow Strength", Range(0,10)) = 0
        _GlowStrength0 ("Glow Strength 0", Range(0,10)) = 0
        _GlowStrictness0 ("Glow Strictness 0", Range(0.34,30)) = 3
        _AdjustiveFaceShadow ("Apply Mask R", Range(0,1)) = 0
        _AdjustiveHairShadow ("Apply Mask R", Range(0,1)) = 0
        _OutlineZCorrection ("Outline Correction (world z)", Range(-0.0005,0.0005)) = 0
        _ZCorrection ("Z Correction", Range(0,0.1)) = 0
        _UseGlow ("Use Glow", Float) = 0
        _IsDither ("_DITHER_HORIZONTAL_LINES", Float) = 0
        _DitherThreshold ("_DitherThreshold", Range(0,1)) = 0
        _GrayBrightness ("_GrayBrightness", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendAlpha ("Src Blend Alpha", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendAlpha ("Dst Blend Alpha", Float) = 0
        [Enum(Off,0, On,1)] _ZWrite ("Z Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300
        Pass
        {
            Tags { "LightMode" = "ModelRepairOutline" }
            Cull Front
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineTint;
            float _OutlineZCorrection;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz + worldNormal * 0.0015;
                worldPos.z += _OutlineZCorrection;
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineTint;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            
            
            
            
            
            
            
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MaskTex;
            sampler2D _HairSpecTex;
            sampler2D _SourceTex;
            sampler2D _MouthTileTex;
            float4 _MouthTileTex_ST;
            fixed4 _Tint;
            fixed4 _Color;
            fixed4 _BaseColor;
            fixed4 _TwoSideTint;
            fixed4 _EyeTint;
            fixed4 _MouthTint;
            fixed4 _ShadowTint;
            fixed4 _ShadowTintR4;
            fixed4 _ShadowTintG4;
            fixed4 _ShadowTintB4;
            fixed4 _ShadowThreshold4;
            fixed4 _BaseBrightness4;
            fixed4 _ViewOffset4;
            fixed4 _ViewPower4;
            fixed4 _ViewStrength4;
            fixed4 _InvViewPower4;
            fixed4 _InvViewStrength4;
            fixed4 _ViewLightEdge4;
            fixed4 _RimAreaMultiplier4;
            fixed4 _RimStrength4;
            fixed4 _OutlineTint;
            fixed4 _SpecColor;
            fixed4 _GlowTint;
            fixed4 _GlowTint0;
            fixed4 _GlowMaskColor0;
            fixed4 _CodeAddColor;
            fixed4 _CodeMultiplyColor;
            fixed4 _CodeAddRimColor;
            float4 _ShadowLightDir;
            float4 _MxCharLightDir;
            fixed4 _MxCharLightTone;
            fixed4 _MxCharShadowTone;
            float4 _MxCharLightData;
            float4 _FakeLightDir;
            float4 _SpecDirMultiplier;
            fixed4 _InlineTint;
            float _Cutoff;
            float _MouthTileEnabled;
            float _MaskGSensitivity;
            float _ShadowThreshold;
            float _BaseBrightness;
            float _ViewOffset;
            float _ViewPower;
            float _ViewStrength;
            float _InvViewPower;
            float _InvViewStrength;
            float _ViewLightEdge;
            float _InlineAmount;
            float _ShadowStrong;
            float _LightValue;
            float _LightStrong;
            float _SpecStrong;
            float _SpecStrength;
            float _SpecTopMultiplier;
            float _SpecTopLeveler;
            float _SpecBotArea;
            float _SpecBotMultiplier;
            float _RimAreaMultiplier;
            float _RimAreaLeveler;
            float _RimStrength;
            float _AdditionalLightStrength;
            float _AdditionalLightSharpness;
            float _GlowStrength;
            float _GlowStrength0;
            float _GlowStrictness0;
            float _AdjustiveFaceShadow;
            float _AdjustiveHairShadow;
            float _DitherThreshold;
            float _GrayBrightness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 mouthUv : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                float2 rawUv : TEXCOORD4;
                fixed3 ambient : TEXCOORD5;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.mouthUv = TRANSFORM_TEX(v.uv, _MouthTileTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.rawUv = v.uv;
                fixed3 ambient = max(ShadeSH9(fixed4(normalize(o.worldNormal), 1.0)), fixed3(0,0,0));
                fixed ambientSum = max(dot(ambient, fixed3(1,1,1)), 0.3);
                o.ambient = ambient * (0.3 / ambientSum);
                o.color = v.color;
                return o;
            }

            fixed3 ResolveColor(fixed3 value, fixed3 fallback)
            {
                return lerp(fallback, value, step(0.001, dot(abs(value), fixed3(1,1,1))));
            }

            float4 ResolveLayer4Weights(float layer)
            {
                float3 shifted = layer.xxx + float3(-0.25, -0.5, -0.75);
                float3 floors = floor(shifted);
                float3 ceils = ceil(shifted);
                return saturate(float4(-floors.x, -floors.y * ceils.x, -floors.z * ceils.y, ceils.z));
            }

            fixed3 ResolveMxLightDir()
            {
                fixed3 mxLightDir = _MxCharLightDir.xyz;
                mxLightDir = lerp(_FakeLightDir.xyz, mxLightDir, step(0.001, dot(abs(mxLightDir), fixed3(1,1,1))));
                return normalize(mxLightDir);
            }

            fixed3 ApplyLayer4Lighting(fixed3 rgb, float2 uv, float3 worldNormal, float3 worldPos, fixed facing, fixed3 ambient, fixed vertexAlpha)
            {
                float4 mask = tex2D(_MaskTex, uv);
                float4 layer = ResolveLayer4Weights(mask.a);
                float maskShadow = (1.0 - mask.g * 2.0) * _MaskGSensitivity;

                float3 normalDir = normalize(worldNormal);
                float isFront = step(0.0, facing);
                normalDir *= lerp(-1.0, 1.0, isFront);

                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                float3 mxLightDir = ResolveMxLightDir();

                float viewDot = dot(normalDir, viewDir);
                float lightDot = dot(normalDir, mxLightDir);
                float viewOffset = dot(_ViewOffset4, layer);
                float3 viewLightDir = normalize(mxLightDir * viewOffset + viewDir);

                float viewPower = max(dot(_ViewPower4, layer), 0.001);
                float viewLight = pow(max(saturate(dot(normalDir, viewLightDir)), 0.0001), viewPower);
                viewLight = saturate(maskShadow + viewLight);
                float edge = saturate(dot(_ViewLightEdge4, layer));
                viewLight = lerp(viewLight, saturate((viewLight - 0.5) * 10.0), edge);

                float invView = 1.0 - viewDot;
                float invPower = max(dot(_InvViewPower4, layer), 0.001);
                float invLight = pow(max(saturate(invView), 0.0001), invPower);
                invLight = saturate(maskShadow + invLight);
                invLight = lerp(invLight, min(invLight * 4000.0, 1.0), edge);

                float additive = dot(_BaseBrightness4, layer);
                additive += viewLight * dot(_ViewStrength4, layer);
                additive += invLight * dot(_InvViewStrength4, layer);

                fixed3 lightTone = ResolveColor(_MxCharLightTone.rgb, fixed3(1,1,1));
                fixed3 shadowTone = ResolveColor(_MxCharShadowTone.rgb, fixed3(1,1,1));
                fixed3 layerShadowTint = fixed3(dot(_ShadowTintR4, layer), dot(_ShadowTintG4, layer), dot(_ShadowTintB4, layer));
                fixed3 shadowColor = layerShadowTint * shadowTone;

                float shadowBase = viewDot * 0.77 + lightDot + maskShadow;
                float shadowThreshold = dot(_ShadowThreshold4, layer) + (1.0 - _MxCharLightData.y) * 0.15;
                float shadowMix = saturate((shadowThreshold - shadowBase) * 10.0);
                fixed3 tone = lerp(lightTone, shadowColor, shadowMix);
                tone = tone * _MxCharLightData.x + ambient;

                float rimArea = dot(_RimAreaMultiplier4, layer);
                float rimMask = saturate(invView * rimArea - max(rimArea - 1.0, 0.0));
                float rimStrength = dot(_RimStrength4, layer) * rimMask * vertexAlpha * _MxCharLightData.x;
                fixed3 rimTone = (saturate(lightDot * 4.0 + 0.25) * 0.4 + 0.3) * lightTone;
                tone += rimStrength * rimTone;
                tone += additive * lightTone;

                fixed3 glow = mask.b * _GlowStrength * _GlowTint.rgb;
                fixed3 litColor = tone * rgb + glow;
                fixed backLight = saturate(lightDot * 0.33 + 0.5);
                fixed3 backColor = rgb * _TwoSideTint.rgb * shadowColor * backLight;
                fixed3 color = lerp(backColor, litColor, isFront);
                return saturate(color + invView * invView * _CodeAddRimColor.rgb);
            }

            fixed3 ApplySingleInlineLighting(fixed3 rgb, float2 uv, float3 worldNormal, float3 worldPos, fixed facing, fixed3 ambient, fixed vertexAlpha)
            {
                fixed3 lightTone = ResolveColor(_MxCharLightTone.rgb, fixed3(1,1,1));
                fixed3 shadowTone = ResolveColor(_MxCharShadowTone.rgb, fixed3(1,1,1));
                fixed3 shadowColor = _ShadowTint.rgb * shadowTone;
                float4 mask = tex2D(_MaskTex, uv);

                float3 normalDir = normalize(worldNormal);
                float isFront = step(0.0, facing);
                normalDir *= lerp(-1.0, 1.0, isFront);

                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                float3 mxLightDir = ResolveMxLightDir();
                float3 viewLightDir = normalize(mxLightDir * _ViewOffset + viewDir);
                float viewLight = pow(max(saturate(dot(normalDir, viewLightDir)), 0.0001), max(_ViewPower, 0.001));
                float maskShadow = (1.0 - mask.g * 2.0) * _MaskGSensitivity;
                viewLight = saturate(maskShadow + viewLight);
                viewLight = lerp(viewLight, saturate((viewLight - 0.5) * 10.0), saturate(_ViewLightEdge));

                float lightDot = dot(normalDir, mxLightDir);
                float viewDot = dot(normalDir, viewDir);
                float invView = 1.0 - viewDot;
                float shadowBase = viewDot * 0.77 + lightDot + maskShadow;
                float shadowThreshold = _ShadowThreshold + (1.0 - _MxCharLightData.y) * 0.15;
                float shadowMix = saturate((shadowThreshold - shadowBase) * 10.0);

                float rimMask = saturate(invView * _RimAreaMultiplier - _RimAreaLeveler) * vertexAlpha;
                float rimStrength = rimMask * _RimStrength * _MxCharLightData.x;
                float rimBlock = saturate(1.0 - rimMask * _RimStrength);

                float invLight = pow(max(saturate(invView), 0.0001), max(_InvViewPower, 0.001));
                invLight = saturate(maskShadow + invLight);
                invLight = lerp(invLight, min(invLight * 4000.0, 1.0), saturate(_ViewLightEdge));

                float additive = _BaseBrightness + viewLight * _ViewStrength + invLight * _InvViewStrength;
                float inlineMask = mask.r * shadowMix * rimBlock * saturate(1.0 - viewLight * _ViewStrength) * saturate(1.0 - invLight * _InvViewStrength) * _InlineAmount;
                fixed3 inlineTone = lerp(fixed3(1,1,1), ResolveColor(_InlineTint.rgb, fixed3(0.5,0.5,0.5)), inlineMask);

                fixed3 tone = lerp(lightTone, shadowColor, shadowMix);
                tone = tone * _MxCharLightData.x + ambient;
                fixed3 rimTone = (saturate(lightDot * 4.0 + 0.25) * 0.4 + 0.3) * lightTone;
                tone += rimStrength * rimTone;
                tone += additive * lightTone;
                tone *= inlineTone;

                fixed3 glow = mask.b * _GlowStrength * _GlowTint.rgb;
                fixed3 litColor = tone * rgb + glow;
                fixed backLight = saturate(lightDot * 0.33 + 0.5);
                fixed3 backColor = rgb * _TwoSideTint.rgb * shadowColor * backLight;
                fixed3 color = lerp(backColor, litColor, isFront);
                return saturate(color + invView * invView * _CodeAddRimColor.rgb);
            }

            fixed3 ApplyFaceLighting(fixed3 rgb, float2 uv, float3 worldNormal, float3 worldPos, fixed3 ambient, fixed vertexAlpha)
            {
                fixed3 lightTone = ResolveColor(_MxCharLightTone.rgb, fixed3(1,1,1));
                fixed3 shadowTone = ResolveColor(_MxCharShadowTone.rgb, fixed3(1,1,1));
                fixed3 shadowColor = _ShadowTint.rgb * shadowTone;
                fixed2 mask = tex2D(_MaskTex, uv).rg;

                float3 normalDir = normalize(worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                float3 mxLightDir = ResolveMxLightDir();
                float3 shadowLightDir = mul((float3x3)unity_ObjectToWorld, _ShadowLightDir.xyz);
                shadowLightDir = lerp(mxLightDir, shadowLightDir, step(0.001, dot(abs(shadowLightDir), fixed3(1,1,1))));
                shadowLightDir = normalize(shadowLightDir);

                float viewDot = dot(normalDir, viewDir);
                float lightDot = dot(normalDir, shadowLightDir);
                float maskShadow = (1.0 - mask.g * 2.0 + mask.r * _AdjustiveFaceShadow) * _MaskGSensitivity;
                float shadowBase = viewDot * 0.77 + lightDot + maskShadow;
                float shadowThreshold = _ShadowThreshold + (1.0 - _MxCharLightData.y) * 0.15;
                float shadowMix = saturate((shadowThreshold - shadowBase) * 2.0);

                fixed3 tone = lerp(lightTone, shadowColor, shadowMix);
                tone = tone * _MxCharLightData.x + ambient;

                float invView = 1.0 - viewDot;
                float rimMask = saturate(invView * _RimAreaMultiplier - _RimAreaLeveler) * vertexAlpha;
                float rimStrength = rimMask * _RimStrength * _MxCharLightData.x;
                fixed3 rimTone = (saturate(dot(normalDir, mxLightDir) * 4.0 + 0.25) * 0.4 + 0.3) * lightTone;
                tone += rimStrength * rimTone;

                return saturate(tone * rgb + invView * invView * _CodeAddRimColor.rgb);
            }

            fixed3 ApplyHairLighting(fixed3 rgb, float2 uv, float3 worldNormal, float3 worldPos, fixed facing, fixed3 ambient, fixed vertexAlpha)
            {
                fixed3 lightTone = ResolveColor(_MxCharLightTone.rgb, fixed3(1,1,1));
                fixed3 shadowTone = ResolveColor(_MxCharShadowTone.rgb, fixed3(1,1,1));
                fixed3 shadowColor = _ShadowTint.rgb * shadowTone;
                fixed4 hairSpec = tex2D(_HairSpecTex, uv);
                fixed4 mask = tex2D(_MaskTex, uv);

                float3 normalDir = normalize(worldNormal);
                float isFront = step(0.0, facing);
                normalDir *= lerp(-1.0, 1.0, isFront);

                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                float3 mxLightDir = ResolveMxLightDir();
                float3 halfDir = normalize(viewDir + mxLightDir);

                float3 localSpecNormal = hairSpec.rgb * 2.0 - 1.0;
                float3 specNormal = normalize(mul((float3x3)unity_ObjectToWorld, localSpecNormal));
                float specDot = dot(specNormal, halfDir) + dot(localSpecNormal, _SpecDirMultiplier.xyz);
                float topSpec = (1.0 - specDot) * _SpecTopMultiplier - _SpecTopLeveler;
                float botSpec = pow(max(specDot, -_SpecBotArea) + _SpecBotArea, 2.0) * _SpecBotMultiplier;
                float spec = lerp(botSpec, topSpec, step(0.0, specDot));
                spec = max(spec - (1.0 - hairSpec.a), 0.0) * _SpecStrength;

                float viewDot = dot(normalDir, viewDir);
                float lightDot = dot(normalDir, mxLightDir);
                float maskShadow = (1.0 - mask.g * 2.0 + mask.r * _AdjustiveHairShadow) * _MaskGSensitivity;
                float shadowThreshold = _ShadowThreshold + (1.0 - _MxCharLightData.y) * 0.15;
                float shadowDelta = viewDot * 0.77 + lightDot + maskShadow - shadowThreshold;
                float specGate = saturate(shadowDelta * 5.0 - 0.5);
                float shadowMix = saturate(shadowDelta * -20.0);
                spec *= specGate;

                float invView = 1.0 - viewDot;
                float rimMask = saturate(invView * _RimAreaMultiplier - _RimAreaLeveler) * vertexAlpha;
                float rim = rimMask * _RimStrength;
                float rimSpec = (spec + rim) * _MxCharLightData.x;

                fixed3 tone = lerp(lightTone, shadowColor, shadowMix);
                tone = tone * _MxCharLightData.x + ambient;
                fixed3 rimTone = (saturate(lightDot * 4.0 + 0.25) * 0.4 + 0.3) * lightTone;
                tone += rimSpec * rimTone;

                fixed3 glow = mask.b * _GlowStrength * _GlowTint.rgb;
                fixed3 litColor = tone * rgb + glow;
                fixed backLight = saturate(lightDot * 0.33 + 0.5);
                fixed3 backColor = rgb * _TwoSideTint.rgb * shadowColor * backLight;
                fixed3 color = lerp(backColor, litColor, isFront);
                return saturate(color + invView * invView * _CodeAddRimColor.rgb);
            }

            fixed3 ApplyFallbackLighting(fixed3 rgb, float2 uv, float3 worldNormal, float3 worldPos)
            {
                fixed3 normalDir = normalize(worldNormal);
                fixed3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                fixed3 mxLightDir = ResolveMxLightDir();

                fixed3 lightTone = ResolveColor(_MxCharLightTone.rgb, fixed3(1,1,1));
                fixed cleanLight = smoothstep(-0.28, 0.92, dot(normalDir, mxLightDir));
                fixed3 liftedRgb = lerp(rgb, sqrt(saturate(rgb)), 0.14);
                fixed3 lighting = (0.87 + cleanLight * 0.13) * lightTone;
                fixed3 color = liftedRgb * lighting;
                fixed luma = dot(rgb, fixed3(0.299, 0.587, 0.114));
                fixed shadowDetail = saturate((0.48 - luma) / 0.48) * (1.0 - cleanLight);
                color += shadowDetail * fixed3(0.035, 0.035, 0.035);

                fixed3 halfDir = normalize(viewDir + mxLightDir);
                fixed specMask = max(tex2D(_HairSpecTex, uv).r, tex2D(_SourceTex, uv).r);
                fixed specPower = max(_SpecTopMultiplier * _SpecTopLeveler, 1.0);
                fixed spec = pow(saturate(dot(normalDir, halfDir)), specPower) * specMask * max(_SpecStrong, _SpecStrength);
                spec += pow(saturate(1.0 - dot(normalDir, mxLightDir)), max(_SpecBotMultiplier, 1.0)) * specMask * _SpecBotArea * _SpecStrength;
                color += spec * _SpecColor.rgb * 0.032;

                fixed rimBase = saturate(1.0 - dot(viewDir, normalDir));
                fixed rim = rimBase * rimBase * _RimStrength;
                fixed3 rimTone = (saturate(dot(normalDir, mxLightDir) * 4.0 + 0.25) * 0.4 + 0.3) * lightTone;
                color += rim * (rimTone + _CodeAddRimColor.rgb) * 0.042;
                return saturate(color);
            }

            fixed3 ApplyCharacterLighting(fixed3 rgb, float2 uv, float3 worldNormal, float3 worldPos, fixed facing, fixed3 ambient, fixed vertexAlpha)
            {
#if MODEL_LAYER4
                return ApplyLayer4Lighting(rgb, uv, worldNormal, worldPos, facing, ambient, vertexAlpha);
#elif MODEL_HAIR
                return ApplyHairLighting(rgb, uv, worldNormal, worldPos, facing, ambient, vertexAlpha);
#elif MODEL_FACE
                return ApplyFaceLighting(rgb, uv, worldNormal, worldPos, ambient, vertexAlpha);
#elif MODEL_SINGLE_INLINE
                return ApplySingleInlineLighting(rgb, uv, worldNormal, worldPos, facing, ambient, vertexAlpha);
#else
                return ApplyFallbackLighting(rgb, uv, worldNormal, worldPos);
#endif
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, i.uv);
#if MODEL_MOUTH_TILE
                fixed mouthRegion = step(i.rawUv.x, 0.25) * step(i.rawUv.y, 0.25);
                fixed4 eye = source * _EyeTint;
                fixed4 mouth = tex2D(_MouthTileTex, i.mouthUv) * _MouthTint;
                fixed4 c = lerp(eye, mouth, mouthRegion * saturate(_MouthTileEnabled));
                c.rgb = c.rgb * _CodeMultiplyColor.rgb + _CodeAddColor.rgb;
                return c;
#else
                fixed4 tint = _Color;
                fixed4 c = fixed4(source.rgb * tint.rgb, source.a * saturate(tint.a));
#ifndef MODEL_UNLIT
                c.rgb = ApplyCharacterLighting(c.rgb, i.uv, i.worldNormal, i.worldPos, facing, i.ambient, i.color.a);
#endif
#ifdef MODEL_HALO
                fixed3 glowDelta = _GlowMaskColor0.rgb - source.rgb;
                fixed haloGlow = (1.0 - saturate(dot(glowDelta, glowDelta) * _GlowStrictness0)) * _GlowStrength0;
                c.rgb += haloGlow * _GlowTint0.rgb;
#endif
                c.rgb *= _GrayBrightness;
                c.rgb = c.rgb * _CodeMultiplyColor.rgb + _CodeAddColor.rgb;
                clip(c.a - _Cutoff * step(0.001, _Cutoff));
                return c;
#endif
            }
            ENDCG
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]
            ColorMask 0

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed alpha = tex2D(_MainTex, i.uv).a;
                clip(alpha - _Cutoff * step(0.001, _Cutoff));
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }

    }
    Fallback Off
}
