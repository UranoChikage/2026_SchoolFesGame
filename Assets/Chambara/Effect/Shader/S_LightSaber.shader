Shader "Custom/Effect/LightSaber"
{
    // ライトセーバーの刀身用（円柱やカプセルなど、法線がなめらかなメッシュ前提）
    // Pass1: 芯（白く光る本体） / Pass2: 法線方向に膨らませた外側の光（加算）
    // Bloom が有効なカメラで HDR カラーが光って見える
    Properties
    {
        [HDR] _CoreColor  ("Core Color (芯)", Color) = (8, 8, 8, 1)
        [HDR] _BladeColor ("Blade Color (刀身のふち)", Color) = (0.5, 1.5, 6, 1)
        _CoreWidth   ("Core Width (白い芯の太さ)", Range(0, 1)) = 0.55
        _EdgeSoftness("Edge Softness (芯→ふちのぼかし)", Range(0.01, 1)) = 0.35

        [HDR] _GlowColor ("Glow Color (外側の光)", Color) = (0.3, 0.8, 4, 1)
        _GlowWidth   ("Glow Width (外側の光の厚み[ワールド])", Range(0, 0.2)) = 0.03
        _GlowFalloff ("Glow Falloff (外側の光の減衰)", Range(0.5, 8)) = 2.5

        _FlickerSpeed ("Flicker Speed (ちらつきの速さ)", Float) = 18
        _FlickerAmount("Flicker Amount", Range(0, 0.5)) = 0.08
        _RippleScale  ("Ripple Scale (刀身に沿ったゆらぎ[ワールド])", Float) = 6
        _RippleSpeed  ("Ripple Speed", Float) = 3
        _RippleAmount ("Ripple Amount", Range(0, 1)) = 0.25

        _Intensity   ("Intensity", Range(0, 10)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _CoreColor;
            half4 _BladeColor;
            half  _CoreWidth;
            half  _EdgeSoftness;
            half4 _GlowColor;
            float _GlowWidth;
            half  _GlowFalloff;
            float _FlickerSpeed;
            half  _FlickerAmount;
            float _RippleScale;
            float _RippleSpeed;
            half  _RippleAmount;
            half  _Intensity;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS   : TEXCOORD1;
        };

        float Hash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = Hash(i);
            float b = Hash(i + float2(1, 0));
            float c = Hash(i + float2(0, 1));
            float d = Hash(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // 全体のちらつき + 刀身に沿って流れるゆらぎ
        half Flicker(float3 positionWS)
        {
            float t = _Time.y;
            half flicker = 1.0 - _FlickerAmount * ValueNoise(float2(t * _FlickerSpeed, 3.7));
            float r = ValueNoise(float2(dot(positionWS, 1.0) * _RippleScale - t * _RippleSpeed, 11.3));
            half ripple = lerp(1.0, lerp(0.75, 1.15, r), _RippleAmount);
            return flicker * ripple;
        }

        Varyings VertBase(Attributes IN, float extrude)
        {
            Varyings OUT;
            float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
            float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz) + normalWS * extrude;
            OUT.positionWS = positionWS;
            OUT.normalWS   = normalWS;
            OUT.positionCS = TransformWorldToHClip(positionWS);
            return OUT;
        }

        half NdotV(Varyings IN)
        {
            float3 n = normalize(IN.normalWS);
            float3 v = normalize(GetWorldSpaceViewDir(IN.positionWS));
            return saturate(abs(dot(n, v)));
        }
        ENDHLSL

        // ---- 芯: 正面ほど白く、輪郭ほど刀身色 ----
        Pass
        {
            Name "LightSaberCore"
            Tags { "LightMode"="UniversalForward" }

            Blend Off
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes IN) { return VertBase(IN, 0.0); }

            half4 frag(Varyings IN) : SV_Target
            {
                half rim = 1.0 - NdotV(IN);
                half edge = smoothstep(_CoreWidth, _CoreWidth + _EdgeSoftness, rim);
                half3 col = lerp(_CoreColor.rgb, _BladeColor.rgb, edge);
                return half4(col * Flicker(IN.positionWS) * _Intensity, 1.0);
            }
            ENDHLSL
        }

        // ---- 外側の光: 法線方向に膨らませた殻を加算、輪郭に向かって消える ----
        Pass
        {
            Name "LightSaberGlow"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes IN) { return VertBase(IN, _GlowWidth); }

            half4 frag(Varyings IN) : SV_Target
            {
                half glow = pow(NdotV(IN), _GlowFalloff);
                half3 col = _GlowColor.rgb * glow * Flicker(IN.positionWS) * _Intensity;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
