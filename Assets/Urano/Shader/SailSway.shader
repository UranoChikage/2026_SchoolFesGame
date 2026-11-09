// SailSway.shader  (URP 用)
// 帆の中心ほど大きく、全頂点を同じ向きに動かすだけのシンプルな揺れ。端は動かない。
// 厚みのある帆でも表裏が割れないよう、法線ではなく共通の向き(_SwayDir)に動かす。
// _Center / _Radius / _SwayDir は SailSwayFitter が自動設定する
Shader "Custom/SailSway"
{
    Properties
    {
        [Header(Color)]
        _BaseMap   ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Ambient   ("影の明るさ", Range(0, 1)) = 0.45

        [Header(Sway)]
        _Center    ("揺れの中心 (オブジェクト空間)", Vector) = (0, 0, 0, 0)
        _Radius    ("固定になる距離", Float) = 1
        _SwayDir   ("揺れる向き (オブジェクト空間)", Vector) = (0, 0, 1, 0)
        _Billow    ("ふくらみ (常時、中心の移動量)", Float) = 0.1
        _SwayAmp   ("揺れ幅", Float) = 0.1
        _SwaySpeed ("揺れの速さ", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float  _Ambient;
            float4 _Center;
            float  _Radius;
            float4 _SwayDir;
            float  _Billow;
            float  _SwayAmp;
            float  _SwaySpeed;
        CBUFFER_END

        // 中心 = 1, 端 = 0 のなめらかな重みで、全頂点を同じ向きに動かす
        float3 SailOffset(float3 p)
        {
            float d = length(p - _Center.xyz) / max(_Radius, 0.0001);
            float w = 1.0 - smoothstep(0.0, 1.0, d);
            float move = _Billow + _SwayAmp * sin(_Time.y * _SwaySpeed);
            return normalize(_SwayDir.xyz + 1e-5) * move * w;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posOS = IN.positionOS.xyz + SailOffset(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(posOS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                float3 N = normalize(IN.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float lit = saturate(dot(N, mainLight.direction)) * mainLight.shadowAttenuation;

                float3 col = albedo.rgb * (mainLight.color * lit + SampleSH(N) * _Ambient);
                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertShadow(Attributes IN)
            {
                Varyings OUT;
                float3 posOS = IN.positionOS.xyz + SailOffset(IN.positionOS.xyz);
                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return OUT;
            }

            half4 fragShadow(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
