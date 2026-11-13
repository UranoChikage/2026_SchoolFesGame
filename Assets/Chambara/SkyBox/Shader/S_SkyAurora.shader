Shader "Custom/Skybox/SkyAurora"
{
    // オーロラの夜: 揺らめくオーロラのカーテン + 星空 + 雪山 + オーロラを映す凍った湖
    // Lighting > Environment > Skybox Material に設定して使う
    Properties
    {
        [Header(Sky)]
        _ZenithColor ("Zenith Color (天頂)", Color) = (0.004, 0.008, 0.025, 1)
        _HorizonColor("Horizon Color (地平線)", Color) = (0.02, 0.05, 0.08, 1)
        _SkyExponent ("Sky Gradient Exponent", Range(0.1, 4)) = 0.5

        [Header(Stars)]
        _StarDensity  ("Star Density (細かさ)", Range(50, 600)) = 300
        _StarAmount   ("Star Amount (数)", Range(0, 1)) = 0.4
        _StarIntensity("Star Intensity", Range(0, 5)) = 1.4
        _StarTwinkle  ("Star Twinkle (瞬き)", Range(0, 1)) = 0.5

        [Header(Aurora)]
        [HDR] _AuroraColorA("Aurora Bottom Color (下端)", Color) = (0.1, 1.6, 0.6, 1)
        [HDR] _AuroraColorB("Aurora Top Color (上端)", Color) = (0.7, 0.2, 1.2, 1)
        _AuroraIntensity("Aurora Intensity", Range(0, 5)) = 1.3
        _AuroraScale    ("Aurora Scale", Range(0.1, 5)) = 1.2
        _AuroraBands    ("Aurora Band Frequency (帯の本数)", Range(0.02, 1)) = 0.15
        _AuroraWidth    ("Aurora Band Width (帯の太さ)", Range(0.01, 0.4)) = 0.07
        _AuroraWaviness ("Aurora Waviness (うねり)", Range(0, 10)) = 4
        _AuroraHeight   ("Aurora Height (カーテンの高さ)", Range(0.01, 0.2)) = 0.06
        _AuroraSpeed    ("Aurora Speed", Range(0, 1)) = 0.05

        [Header(Mountains)]
        _MountainFarColor ("Mountain Far Color", Color) = (0.04, 0.06, 0.09, 1)
        _MountainNearColor("Mountain Near Color", Color) = (0.01, 0.015, 0.025, 1)
        _MountainHeight   ("Mountain Height", Range(0, 0.5)) = 0.09
        _MountainSharpness("Mountain Sharpness (峰の鋭さ)", Range(0, 1)) = 0.7
        _MountainSeed     ("Mountain Seed", Float) = 8.1
        _RimLight         ("Snow Rim Light (オーロラに照らされる稜線)", Range(0, 2)) = 0.8

        [Header(Frozen Lake)]
        _IceColor        ("Ice Color", Color) = (0.02, 0.035, 0.05, 1)
        _SnowColor       ("Snow Color", Color) = (0.12, 0.15, 0.2, 1)
        _SnowAmount      ("Snow Amount (氷上の雪)", Range(0, 1)) = 0.5
        _IceReflection   ("Ice Reflection (映り込み)", Range(0, 1)) = 0.6
        _IceRoughness    ("Ice Roughness (映り込みの揺らぎ)", Range(0, 0.3)) = 0.04

        [Header(Common)]
        _HazeColor    ("Horizon Haze Color", Color) = (0.03, 0.07, 0.09, 1)
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
            Name "SkyAurora"

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

                half4  _AuroraColorA;
                half4  _AuroraColorB;
                half   _AuroraIntensity;
                float  _AuroraScale;
                float  _AuroraBands;
                float  _AuroraWidth;
                float  _AuroraWaviness;
                float  _AuroraHeight;
                float  _AuroraSpeed;

                half4  _MountainFarColor;
                half4  _MountainNearColor;
                float  _MountainHeight;
                half   _MountainSharpness;
                float  _MountainSeed;
                half   _RimLight;

                half4  _IceColor;
                half4  _SnowColor;
                half   _SnowAmount;
                half   _IceReflection;
                float  _IceRoughness;

                half4  _HazeColor;
                half   _HazeAmount;
                half   _Exposure;
            CBUFFER_END

            #define AURORA_LAYERS 20

            // 高さの違う平面を何枚も重ね、同じ模様を投影して縦に伸びるカーテンを作る
            half3 Aurora(float3 dir)
            {
                half3 acc = 0;
                if (dir.y <= 0.0) return acc;

                float t = _Time.y * _AuroraSpeed;
                const float2 bandDir = float2(0.8, 0.6);
                const float2 bandPerp = float2(-0.6, 0.8);

                for (int i = 0; i < AURORA_LAYERS; i++)
                {
                    float fi = i / (float)(AURORA_LAYERS - 1);
                    float h = 1.0 + i * _AuroraHeight;
                    float2 p = dir.xz * (h / (dir.y + 0.04)) * _AuroraScale;

                    float along  = dot(p, bandDir);
                    float across = dot(p, bandPerp);

                    float warp = SkyFbm2(p * 0.3 + float2(t, -t * 0.7), 3);
                    float d = across + (warp - 0.5) * _AuroraWaviness + t * 3.0;
                    float f = frac(d * _AuroraBands) - 0.5;
                    float curtain = exp(-(f * f) / (_AuroraWidth * _AuroraWidth));

                    // カーテンの縦すじ (全層で同じ位置 → 縦に通る光の筋)
                    float rays = 0.35 + 0.65 * SkyNoise2(float2(along * 5.0 + t * 4.0, 1.7));

                    // 下端がくっきり明るく、上にいくほど薄れる
                    float fall = smoothstep(0.0, 0.08, fi + 0.02) * pow(1.0 - fi, 1.8);

                    half3 c = lerp(_AuroraColorA.rgb, _AuroraColorB.rgb, pow(fi, 0.7));
                    acc += c * curtain * rays * fall;
                }

                acc *= _AuroraIntensity * (3.0 / AURORA_LAYERS);
                acc *= smoothstep(0.0, 0.15, dir.y);
                return acc;
            }

            // 地平線より上の景色 (湖の映り込みにも使う)
            half3 UpperScene(float3 dir, half3 aurora, float starAmount)
            {
                float up = saturate(dir.y);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, _SkyExponent));

                float starVis = smoothstep(0.0, 0.2, dir.y) * starAmount;
                float2 s1 = SkyStars(dir, _StarDensity, 1.0 - _StarAmount, 0.18, _StarTwinkle);
                float2 s2 = SkyStars(dir, _StarDensity * 0.35, 1.0 - _StarAmount * 0.5, 0.12, _StarTwinkle);
                col += (SkyStarColor(s1.y) * s1.x + SkyStarColor(s2.y) * s2.x * 1.8) * _StarIntensity * starVis;

                col += aurora;

                // 地平線のかすみ (オーロラの光で少し色づく)
                float haze = 1.0 - smoothstep(0.0, _HazeAmount * 0.4 + 0.01, dir.y);
                col = lerp(col, _HazeColor.rgb + _AuroraColorA.rgb * 0.02 * _AuroraIntensity, haze);

                // 雪山 (オーロラ色に照らされる稜線)
                col = SkyApplyMountains(col, dir, 3, _MountainHeight, _MountainSharpness, _MountainSeed,
                                        _MountainFarColor.rgb, _MountainNearColor.rgb,
                                        _HazeColor.rgb, 0.4,
                                        _AuroraColorA.rgb * 0.05 * _RimLight, float3(0.8, 0, 0.6));
                return col;
            }

            half4 frag(SkyVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 dir = normalize(input.viewDirWS);
                half3 col;

                if (dir.y >= 0.0)
                {
                    col = UpperScene(dir, Aurora(dir), 1.0);
                }
                else
                {
                    // ---- 凍った湖 ----
                    float dist = 1.0 / max(-dir.y, 1e-3);
                    float2 gp = dir.xz * dist;

                    // 氷の凹凸で映り込みを揺らす
                    float3 rdir = float3(dir.x, -dir.y, dir.z);
                    float2 ripple = float2(SkyNoise2(gp * 2.0), SkyNoise2(gp * 2.0 + 7.7)) - 0.5;
                    rdir.xz += ripple * _IceRoughness;
                    rdir = normalize(rdir);
                    rdir.y = max(rdir.y, 0.0);

                    half3 aurora = Aurora(rdir);
                    half3 refl = UpperScene(rdir, aurora, 0.4);

                    float fres = lerp(0.35, 1.0, pow(1.0 - (-dir.y), 5.0));
                    half3 ground = lerp(_IceColor.rgb, refl, _IceReflection * fres);

                    // 氷の上に積もった雪 (オーロラの光で色づく)
                    float snow = smoothstep(0.5, 0.68, SkyFbm2(gp * 0.35, 5));
                    half3 snowCol = _SnowColor.rgb + aurora * 0.08 + _AuroraColorA.rgb * 0.01 * _AuroraIntensity;
                    ground = lerp(ground, snowCol, snow * _SnowAmount);

                    // 遠くほどかすむ
                    float haze = 1.0 - exp(-dist * _HazeAmount * 0.05);
                    haze = max(haze, 1.0 - smoothstep(0.0, 0.02, -dir.y));
                    col = lerp(ground, _HazeColor.rgb, saturate(haze));
                }

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
