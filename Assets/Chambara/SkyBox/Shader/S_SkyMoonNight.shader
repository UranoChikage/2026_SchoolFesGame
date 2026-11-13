Shader "Custom/Skybox/SkyMoonNight"
{
    // 月夜の山並み: 満月 + 星空 + 天の川 + 流れる夜雲 + 何重にも重なる山のシルエット
    // 色を変えると赤い月 (SkyBloodMoon) にもなる
    // Lighting > Environment > Skybox Material に設定して使う
    Properties
    {
        [Header(Sky)]
        _ZenithColor ("Zenith Color (天頂)", Color) = (0.008, 0.015, 0.05, 1)
        _HorizonColor("Horizon Color (地平線)", Color) = (0.05, 0.09, 0.18, 1)
        _SkyExponent ("Sky Gradient Exponent", Range(0.1, 4)) = 0.45

        [Header(Stars)]
        _StarDensity  ("Star Density (細かさ)", Range(50, 600)) = 280
        _StarAmount   ("Star Amount (数)", Range(0, 1)) = 0.35
        _StarIntensity("Star Intensity", Range(0, 5)) = 1.6
        _StarTwinkle  ("Star Twinkle (瞬き)", Range(0, 1)) = 0.5
        _MilkyWayColor("Milky Way Color", Color) = (0.55, 0.6, 0.85, 1)
        _MilkyWayIntensity("Milky Way Intensity", Range(0, 2)) = 0.35
        _MilkyWayAxis ("Milky Way Axis (帯の法線)", Vector) = (0.35, 0.55, -0.75, 0)

        [Header(Moon)]
        _MoonDirection("Moon Direction", Vector) = (-0.35, 0.42, 0.85, 0)
        [HDR] _MoonColor("Moon Color", Color) = (2.4, 2.3, 2.0, 1)
        _MoonSize     ("Moon Size (見かけの半径)", Range(0.005, 0.2)) = 0.045
        _MoonDetail   ("Moon Surface (模様の濃さ)", Range(0, 1)) = 0.45
        [HDR] _MoonHaloColor("Moon Halo Color", Color) = (0.35, 0.45, 0.7, 1)
        _MoonHalo     ("Moon Halo (光暈)", Range(0, 3)) = 1.0

        [Header(Clouds)]
        _CloudColor   ("Cloud Color", Color) = (0.07, 0.09, 0.15, 1)
        _CloudScale   ("Cloud Scale", Range(0.05, 5)) = 0.9
        _CloudCoverage("Cloud Coverage", Range(0, 1)) = 0.4
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.85
        _CloudMoonLit ("Cloud Moon Lit (月明かりの縁)", Range(0, 3)) = 1.0

        [Header(Mountains)]
        _MountainFarColor ("Mountain Far Color", Color) = (0.06, 0.08, 0.14, 1)
        _MountainNearColor("Mountain Near Color", Color) = (0.008, 0.01, 0.018, 1)
        _MountainHeight   ("Mountain Height", Range(0, 0.5)) = 0.14
        _MountainSharpness("Mountain Sharpness (峰の鋭さ)", Range(0, 1)) = 0.5
        _MountainSeed     ("Mountain Seed", Float) = 3.7
        _ValleyMist       ("Valley Mist (谷の霧)", Range(0, 1)) = 0.5
        _RimLight         ("Ridge Rim Light (稜線の月明かり)", Range(0, 2)) = 0.6

        [Header(Common)]
        _WindDirection("Wind Direction (xz)", Vector) = (1, 0, 0.2, 0)
        _WindSpeed    ("Wind Speed", Float) = 0.015
        _HazeColor    ("Horizon Haze Color", Color) = (0.1, 0.13, 0.22, 1)
        _HazeAmount   ("Horizon Haze", Range(0.01, 1)) = 0.3
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
            Name "SkyMoonNight"

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex SkyVert
            #pragma fragment frag

            #include "SkyCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _ZenithColor;
                half4  _HorizonColor;
                half   _SkyExponent;

                float  _StarDensity;
                half   _StarAmount;
                half   _StarIntensity;
                half   _StarTwinkle;
                half4  _MilkyWayColor;
                half   _MilkyWayIntensity;
                float4 _MilkyWayAxis;

                float4 _MoonDirection;
                half4  _MoonColor;
                float  _MoonSize;
                half   _MoonDetail;
                half4  _MoonHaloColor;
                half   _MoonHalo;

                half4  _CloudColor;
                float  _CloudScale;
                half   _CloudCoverage;
                half   _CloudOpacity;
                half   _CloudMoonLit;

                half4  _MountainFarColor;
                half4  _MountainNearColor;
                float  _MountainHeight;
                half   _MountainSharpness;
                float  _MountainSeed;
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
                float3 moonDir = normalize(_MoonDirection.xyz);
                float  moonDot = dot(dir, moonDir);
                float  moonAng = SkyAngle(dir, moonDir);
                float  up = saturate(dir.y);

                // ---- 空のグラデーション ----
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, _SkyExponent));

                // ---- 天の川 ----
                float3 axis = normalize(_MilkyWayAxis.xyz);
                float band = exp(-pow(dot(dir, axis) / 0.2, 2.0));
                float mw = 0.0;
                if (band > 0.01)
                {
                    float n    = SkyFbm3(dir * 3.0, 5);
                    float dust = SkyFbm3(dir * 7.0 + 11.0, 4);
                    mw = band * smoothstep(0.3, 0.75, n) * (1.0 - smoothstep(0.45, 0.7, dust) * 0.8);
                }
                col += _MilkyWayColor.rgb * mw * _MilkyWayIntensity;

                // ---- 星 (月の近くと地平線付近は見えにくい) ----
                float starVis = smoothstep(0.0, 0.25, dir.y) * saturate(moonAng / (_MoonSize * 6.0));
                float2 s1 = SkyStars(dir, _StarDensity, 1.0 - _StarAmount, 0.18, _StarTwinkle);
                float2 s2 = SkyStars(dir, _StarDensity * 0.35, 1.0 - _StarAmount * 0.5, 0.12, _StarTwinkle);
                float2 s3 = SkyStars(dir.zxy, _StarDensity * 1.6, 1.0 - _StarAmount * band, 0.2, _StarTwinkle);
                half3 stars = SkyStarColor(s1.y) * s1.x
                            + SkyStarColor(s2.y) * s2.x * 1.8
                            + SkyStarColor(s3.y) * s3.x * 0.6;
                col += stars * _StarIntensity * starVis;

                // ---- 月の光暈 ----
                col += _MoonHaloColor.rgb * _MoonHalo *
                       (exp(-moonAng / (_MoonSize * 2.5)) * 0.6 + exp(-moonAng / (_MoonSize * 12.0)) * 0.25);

                // ---- 月本体 ----
                float3 mt, mb;
                SkyBasis(moonDir, mt, mb);
                float2 muv = float2(dot(dir, mt), dot(dir, mb)) / _MoonSize;
                float  mr = length(muv);
                float  moonMask = (1.0 - smoothstep(0.96, 1.0, mr)) * step(0.0, moonDot);
                if (moonMask > 0.0)
                {
                    float z = sqrt(saturate(1.0 - mr * mr));
                    float maria  = SkyFbm2(muv * 1.6 + 4.1, 5);
                    float crater = SkyFbm2(muv * 6.0 + 9.3, 4);
                    float surf = 1.0 - _MoonDetail * (smoothstep(0.45, 0.62, maria) * 0.6 + crater * 0.3);
                    float limb = lerp(0.6, 1.0, sqrt(z));
                    col = lerp(col, _MoonColor.rgb * surf * limb, moonMask);
                }

                // ---- 夜雲 (月にかかると縁が光る) ----
                if (dir.y > 0.0)
                {
                    float2 wind = normalize(_WindDirection.xz + 1e-5) * _WindSpeed * _Time.y;
                    float2 uv = dir.xz / (dir.y + 0.12) * _CloudScale;
                    float2 c = SkyCloudLayer(uv, wind, _CloudCoverage, 0.45);
                    float fade = smoothstep(0.0, 0.2, dir.y);

                    float edge = c.x * (1.0 - c.y); // 雲の薄いところ
                    half3 cloudCol = _CloudColor.rgb
                        + _MoonColor.rgb * _CloudMoonLit *
                          (pow(saturate(moonDot), 6.0) * 0.06 + exp(-moonAng / (_MoonSize * 4.0)) * edge * 0.8);
                    col = lerp(col, cloudCol, c.x * _CloudOpacity * fade);
                }

                // ---- 地平線のかすみ ----
                float haze = 1.0 - smoothstep(0.0, _HazeAmount * 0.4 + 0.01, dir.y);
                col = lerp(col, _HazeColor.rgb, haze);

                // ---- 山並み ----
                col = SkyApplyMountains(col, dir, 4, _MountainHeight, _MountainSharpness, _MountainSeed,
                                        _MountainFarColor.rgb, _MountainNearColor.rgb,
                                        _HazeColor.rgb, _ValleyMist,
                                        _MoonColor.rgb * 0.08 * _RimLight, moonDir);

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
