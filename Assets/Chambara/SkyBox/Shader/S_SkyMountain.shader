Shader "Custom/Skybox/SkyMountain"
{
    // 山並みと太陽: 3 色グラデーションの空 + 大きな太陽 + 筋状の雲 + 空気遠近法で霞む山の重なり
    // 夕焼け (SkyMountainDusk) / 朝焼け (SkyMountainDawn) / 昼 (SkyMountainDay) をマテリアルで切り替える
    // Lighting > Environment > Skybox Material に設定して使う
    Properties
    {
        [Header(Sky)]
        _ZenithColor ("Zenith Color (天頂)", Color) = (0.12, 0.13, 0.32, 1)
        _MidColor    ("Mid Color (中間)", Color) = (0.8, 0.4, 0.38, 1)
        _HorizonColor("Horizon Color (地平線)", Color) = (1.0, 0.6, 0.3, 1)
        _MidHeight   ("Mid Height (中間色の高さ)", Range(0.01, 0.8)) = 0.18
        _SkyExponent ("Sky Gradient Exponent", Range(0.1, 4)) = 0.8

        [Header(Sun)]
        _SunDirection("Sun Direction", Vector) = (0, 0.04, 1, 0)
        [HDR] _SunColor("Sun Color", Color) = (4, 2.2, 0.9, 1)
        _SunSize     ("Sun Size (見かけの半径)", Range(0.002, 0.15)) = 0.03
        [HDR] _SunGlowColor("Sun Glow Color", Color) = (1, 0.45, 0.15, 1)
        _SunGlow     ("Sun Glow", Range(0, 3)) = 1.0
        _HorizonGlow ("Horizon Glow (太陽側の地平線)", Range(0, 3)) = 0.8

        [Header(Clouds)]
        [HDR] _CloudLitColor("Cloud Lit Color (日向)", Color) = (1.4, 0.7, 0.4, 1)
        _CloudShadowColor("Cloud Shadow Color (陰)", Color) = (0.35, 0.2, 0.3, 1)
        _CloudScale   ("Cloud Scale", Range(0.05, 5)) = 0.5
        _CloudCoverage("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudStretch ("Cloud Stretch (筋雲の伸び)", Range(1, 8)) = 4
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.9

        [Header(Mountains)]
        _MountainFarColor ("Mountain Far Color", Color) = (0.45, 0.3, 0.4, 1)
        _MountainNearColor("Mountain Near Color", Color) = (0.05, 0.03, 0.06, 1)
        _MountainHeight   ("Mountain Height", Range(0, 0.5)) = 0.12
        _MountainSharpness("Mountain Sharpness (峰の鋭さ)", Range(0, 1)) = 0.35
        _MountainSeed     ("Mountain Seed", Float) = 1.3
        _AtmosphericPerspective("Atmospheric Perspective (遠くの山の霞み)", Range(0, 1)) = 0.7
        _ValleyMist       ("Valley Mist (谷の霧)", Range(0, 1)) = 0.6
        _RimLight         ("Ridge Rim Light", Range(0, 2)) = 0.5

        [Header(Common)]
        _WindDirection("Wind Direction (xz)", Vector) = (1, 0, 0.15, 0)
        _WindSpeed    ("Wind Speed", Float) = 0.01
        _HazeColor    ("Horizon Haze Color", Color) = (1, 0.65, 0.45, 1)
        _HazeAmount   ("Horizon Haze", Range(0.01, 1)) = 0.2
        _Exposure     ("Exposure", Range(0, 4)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Background"
            "Queue"="Background"
            "PreviewType"="Skybox"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "SkyMountain"

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex SkyVert
            #pragma fragment frag

            #include "SkyCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _ZenithColor;
                half4  _MidColor;
                half4  _HorizonColor;
                half   _MidHeight;
                half   _SkyExponent;

                float4 _SunDirection;
                half4  _SunColor;
                float  _SunSize;
                half4  _SunGlowColor;
                half   _SunGlow;
                half   _HorizonGlow;

                half4  _CloudLitColor;
                half4  _CloudShadowColor;
                float  _CloudScale;
                half   _CloudCoverage;
                float  _CloudStretch;
                half   _CloudOpacity;

                half4  _MountainFarColor;
                half4  _MountainNearColor;
                float  _MountainHeight;
                half   _MountainSharpness;
                float  _MountainSeed;
                half   _AtmosphericPerspective;
                half   _ValleyMist;
                half   _RimLight;

                float4 _WindDirection;
                float  _WindSpeed;
                half4  _HazeColor;
                half   _HazeAmount;
                half   _Exposure;
            CBUFFER_END

            half4 frag(SkyVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 dir = normalize(input.viewDirWS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float  sunDot = dot(dir, sunDir);
                float  sunAng = SkyAngle(dir, sunDir);
                float  up = saturate(dir.y);

                // ---- 3 色グラデーション ----
                half3 col = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0, _MidHeight, up));
                col = lerp(col, _ZenithColor.rgb, pow(smoothstep(_MidHeight * 0.5, 1.0, up), _SkyExponent));

                // 太陽側の地平線が明るく焼ける
                float3 flatDir = normalize(float3(dir.x, 0, dir.z) + float3(1e-5, 0, 0));
                float3 flatSun = normalize(float3(sunDir.x, 0, sunDir.z) + float3(1e-5, 0, 0));
                float  side = pow(dot(flatDir, flatSun) * 0.5 + 0.5, 3.0);
                half3  horizonGlow = _SunGlowColor.rgb * side * _HorizonGlow;
                col += horizonGlow * (1.0 - smoothstep(0.0, 0.5, up));

                // 太陽の光芒
                col += _SunGlowColor.rgb * _SunGlow *
                       (exp(-sunAng / (_SunSize * 2.5)) * 0.8 + exp(-sunAng / (_SunSize * 10.0)) * 0.3);

                // ---- 太陽 ----
                float disk = 1.0 - smoothstep(_SunSize * 0.96, _SunSize, sunAng);
                col = lerp(col, _SunColor.rgb, disk);

                // ---- 筋状の雲 ----
                if (dir.y > 0.0)
                {
                    float2 wdir = normalize(_WindDirection.xz + 1e-5);
                    float2 uv = dir.xz / (dir.y + 0.06) * _CloudScale;
                    float2 ruv = float2(dot(uv, wdir), dot(uv, float2(-wdir.y, wdir.x)));
                    ruv.x /= _CloudStretch;
                    float2 wind = float2(_WindSpeed * _Time.y, 0);
                    float2 c = SkyCloudLayer(ruv, wind, _CloudCoverage, 0.4);
                    float fade = smoothstep(0.0, 0.12, dir.y);

                    float lit = pow(sunDot * 0.5 + 0.5, 3.0);
                    half3 cloudCol = lerp(_CloudShadowColor.rgb, _CloudLitColor.rgb, saturate(lit * (1.2 - c.y * 0.7)));
                    // 太陽の近くの薄い縁は強く光る
                    cloudCol += _SunGlowColor.rgb * exp(-sunAng / (_SunSize * 8.0)) * (1.0 - c.y) * 2.0;
                    col = lerp(col, cloudCol, c.x * _CloudOpacity * fade);
                }

                // ---- 地平線のかすみ ----
                float haze = 1.0 - smoothstep(0.0, _HazeAmount * 0.4 + 0.01, dir.y);
                col = lerp(col, _HazeColor.rgb + horizonGlow * 0.5, haze);

                // ---- 山並み (遠くの山ほど空の色に溶ける) ----
                half3 horizonSky = _HorizonColor.rgb + horizonGlow;
                half3 farCol = lerp(_MountainFarColor.rgb, horizonSky, _AtmosphericPerspective * 0.6);
                col = SkyApplyMountains(col, dir, 4, _MountainHeight, _MountainSharpness, _MountainSeed,
                                        farCol, _MountainNearColor.rgb,
                                        lerp(_HazeColor.rgb, horizonSky, 0.5), _ValleyMist,
                                        _SunColor.rgb * 0.05 * _RimLight, sunDir);

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
