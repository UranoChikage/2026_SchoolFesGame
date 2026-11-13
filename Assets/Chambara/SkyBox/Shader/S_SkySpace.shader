Shader "Custom/Skybox/SkySpace"
{
    // 宇宙空間: 星雲 + 何層もの星 + 輪のある巨大惑星 + 光条を放つ恒星
    // 全方向が宇宙なので上下の区別なし
    // Lighting > Environment > Skybox Material に設定して使う
    Properties
    {
        [Header(Space)]
        _BackgroundColor("Background Color", Color) = (0.004, 0.004, 0.012, 1)

        [Header(Nebula)]
        [HDR] _NebulaColorA("Nebula Color A", Color) = (0.15, 0.25, 0.9, 1)
        [HDR] _NebulaColorB("Nebula Color B", Color) = (0.9, 0.2, 0.6, 1)
        _NebulaScale    ("Nebula Scale", Range(0.2, 6)) = 1.6
        _NebulaDensity  ("Nebula Density", Range(0, 1)) = 0.55
        _NebulaIntensity("Nebula Intensity", Range(0, 3)) = 0.8
        _DustAmount     ("Dark Dust (暗黒星雲)", Range(0, 1)) = 0.6
        _NebulaAxis     ("Nebula Band Axis (帯の法線)", Vector) = (0.2, 1, 0.4, 0)

        [Header(Stars)]
        _StarDensity  ("Star Density (細かさ)", Range(50, 800)) = 350
        _StarAmount   ("Star Amount (数)", Range(0, 1)) = 0.45
        _StarIntensity("Star Intensity", Range(0, 5)) = 2.0
        _StarTwinkle  ("Star Twinkle (瞬き)", Range(0, 1)) = 0.2

        [Header(Sun)]
        _SunDirection("Sun Direction (恒星の方向)", Vector) = (0.6, 0.3, -0.7, 0)
        [HDR] _SunColor("Sun Color", Color) = (6, 5, 4, 1)
        _SunGlow     ("Sun Glow", Range(0, 3)) = 1.0
        _SunSpikes   ("Sun Spikes (光条)", Range(0, 3)) = 0.6

        [Header(Planet)]
        _PlanetDirection("Planet Direction", Vector) = (-0.3, 0.15, 0.9, 0)
        _PlanetSize     ("Planet Size (見かけの大きさ)", Range(0.02, 0.9)) = 0.25
        _PlanetColorA   ("Planet Color A", Color) = (0.85, 0.65, 0.45, 1)
        _PlanetColorB   ("Planet Color B", Color) = (0.45, 0.28, 0.22, 1)
        _PlanetBands    ("Planet Bands (縞の数)", Range(1, 30)) = 9
        _PlanetTurbulence("Planet Turbulence (縞の乱れ)", Range(0, 8)) = 3
        _PlanetAxis     ("Planet Axis (自転軸)", Vector) = (0.25, 1, 0.15, 0)
        _RotationSpeed  ("Rotation Speed", Range(0, 0.2)) = 0.01
        [HDR] _AtmosphereColor("Atmosphere Color", Color) = (0.5, 0.7, 1.5, 1)
        _AtmosphereAmount("Atmosphere Amount", Range(0, 3)) = 1.0

        [Header(Ring)]
        _RingColor  ("Ring Color", Color) = (0.9, 0.8, 0.65, 1)
        _RingInner  ("Ring Inner (惑星半径比)", Range(1, 4)) = 1.4
        _RingOuter  ("Ring Outer (惑星半径比)", Range(1, 5)) = 2.3
        _RingOpacity("Ring Opacity", Range(0, 1)) = 0.8

        [Header(Common)]
        _Exposure ("Exposure", Range(0, 4)) = 1.0
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
            Name "SkySpace"

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex SkyVert
            #pragma fragment frag

            #include "SkyCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _BackgroundColor;

                half4  _NebulaColorA;
                half4  _NebulaColorB;
                float  _NebulaScale;
                half   _NebulaDensity;
                half   _NebulaIntensity;
                half   _DustAmount;
                float4 _NebulaAxis;

                float  _StarDensity;
                half   _StarAmount;
                half   _StarIntensity;
                half   _StarTwinkle;

                float4 _SunDirection;
                half4  _SunColor;
                half   _SunGlow;
                half   _SunSpikes;

                float4 _PlanetDirection;
                float  _PlanetSize;
                half4  _PlanetColorA;
                half4  _PlanetColorB;
                float  _PlanetBands;
                float  _PlanetTurbulence;
                float4 _PlanetAxis;
                float  _RotationSpeed;
                half4  _AtmosphereColor;
                half   _AtmosphereAmount;

                half4  _RingColor;
                float  _RingInner;
                float  _RingOuter;
                half   _RingOpacity;

                half   _Exposure;
            CBUFFER_END

            #define PLANET_DISTANCE 100.0

            half4 frag(SkyVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 dir = normalize(input.viewDirWS);
                float3 sunDir = normalize(_SunDirection.xyz);

                // ---- 星雲 (ドメインワープした 3D ノイズ) ----
                float3 p = dir * _NebulaScale;
                float3 q = float3(SkyFbm3(p, 4), SkyFbm3(p + 5.2, 4), SkyFbm3(p + 9.7, 4));
                float n = SkyFbm3(p * 1.5 + q * 2.5, 5);
                float dens = saturate((n - (0.7 - _NebulaDensity * 0.4)) * 3.0);
                dens *= dens;
                half3 neb = lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, smoothstep(0.35, 0.65, q.x)) * dens;
                float dust = smoothstep(0.45, 0.7, SkyFbm3(p * 3.0 + 3.1, 4)) * _DustAmount;
                neb *= 1.0 - dust;
                // 天球を横切る大きな帯にまとめる
                float band = exp(-pow(dot(dir, normalize(_NebulaAxis.xyz)) / 0.45, 2.0));
                neb *= lerp(0.2, 1.0, band);

                half3 col = _BackgroundColor.rgb + neb * _NebulaIntensity;

                // ---- 星 (星雲の帯の中ほど多い) ----
                float2 s1 = SkyStars(dir, _StarDensity, 1.0 - _StarAmount, 0.18, _StarTwinkle);
                float2 s2 = SkyStars(dir, _StarDensity * 0.3, 1.0 - _StarAmount * 0.5, 0.1, _StarTwinkle);
                float2 s3 = SkyStars(dir.yzx, _StarDensity * 1.8, 1.0 - _StarAmount * band, 0.2, _StarTwinkle);
                half3 stars = SkyStarColor(s1.y) * s1.x
                            + SkyStarColor(s2.y) * s2.x * 2.5
                            + SkyStarColor(s3.y) * s3.x * 0.6;
                col += stars * _StarIntensity * (1.0 - dust * 0.7);

                // ---- 恒星 (光芒 + 十字の光条) ----
                float sunAng = SkyAngle(dir, sunDir);
                col += _SunColor.rgb * (exp(-sunAng / 0.004) + exp(-sunAng / 0.05) * 0.12 * _SunGlow + exp(-sunAng / 0.3) * 0.02 * _SunGlow);
                float3 st, sb;
                SkyBasis(sunDir, st, sb);
                float2 suv = float2(dot(dir, st), dot(dir, sb));
                float spikes = exp(-abs(suv.x) / 0.0015) * exp(-abs(suv.y) / 0.12)
                             + exp(-abs(suv.y) / 0.0015) * exp(-abs(suv.x) / 0.12);
                col += _SunColor.rgb * spikes * _SunSpikes * 0.3 * step(0.0, dot(dir, sunDir));

                // ---- 惑星 (レイと球の交差) ----
                float3 center = normalize(_PlanetDirection.xyz) * PLANET_DISTANCE;
                float  radius = PLANET_DISTANCE * _PlanetSize;
                float3 axis = normalize(_PlanetAxis.xyz);
                float  b = dot(dir, center);
                float  disc = b * b - (dot(center, center) - radius * radius);
                float  tPlanet = 1e9;

                if (b > 0.0)
                {
                    // 大気の外側の光
                    float3 closest = dir * b - center;
                    float perp = length(closest);
                    if (perp > radius)
                    {
                        float facing = dot(closest / perp, sunDir);
                        col += _AtmosphereColor.rgb * _AtmosphereAmount *
                               exp(-(perp - radius) / (radius * 0.04)) * smoothstep(-0.4, 0.6, facing) * 0.6;
                    }
                }

                if (disc > 0.0 && b > 0.0)
                {
                    tPlanet = b - sqrt(disc);
                    float3 nrm = normalize(dir * tPlanet - center);
                    float ndl = dot(nrm, sunDir);

                    // 自転する惑星の座標系
                    float3 pt, pb;
                    SkyBasis(axis, pt, pb);
                    float rot = _Time.y * _RotationSpeed;
                    float cr = cos(rot), sr = sin(rot);
                    float3 lp = float3(dot(nrm, pt) * cr - dot(nrm, pb) * sr,
                                          dot(nrm, axis),
                                          dot(nrm, pt) * sr + dot(nrm, pb) * cr);

                    float turb = SkyFbm3(float3(lp.x * 3.0, lp.y * 12.0, lp.z * 3.0), 5);
                    float stripes = sin(lp.y * _PlanetBands * SKY_PI + turb * _PlanetTurbulence) * 0.5 + 0.5;
                    half3 surf = lerp(_PlanetColorB.rgb, _PlanetColorA.rgb, stripes);
                    surf *= 0.8 + 0.4 * SkyFbm3(lp * 6.0 + 2.0, 3);

                    float lit = smoothstep(-0.08, 0.5, ndl);
                    half3 pc = surf * (lit * 1.1 + 0.015);

                    // 大気のふち
                    float rim = pow(1.0 - saturate(dot(nrm, -dir)), 3.0);
                    pc += _AtmosphereColor.rgb * rim * _AtmosphereAmount * smoothstep(-0.3, 0.5, ndl);

                    float mask = saturate(disc / (radius * radius * 0.008));
                    col = lerp(col, pc, mask);
                }

                // ---- リング ----
                float denom = dot(dir, axis);
                if (abs(denom) > 1e-4)
                {
                    float tRing = dot(center, axis) / denom;
                    if (tRing > 0.0 && tRing < tPlanet)
                    {
                        float3 hit = dir * tRing;
                        float r = length(hit - center) / radius;
                        if (r > _RingInner && r < _RingOuter)
                        {
                            float rr = (r - _RingInner) / (_RingOuter - _RingInner);
                            float ringDens = SkyNoise2(float2(r * 40.0, 0.5)) * 0.6 + SkyNoise2(float2(r * 150.0, 3.3)) * 0.4;
                            ringDens *= smoothstep(0.0, 0.05, rr) * (1.0 - smoothstep(0.9, 1.0, rr));
                            ringDens *= 1.0 - 0.85 * exp(-pow((rr - 0.62) / 0.03, 2.0)); // 隙間

                            // 惑星の影
                            float3 oc = hit - center;
                            float sb2 = dot(oc, sunDir);
                            float sh = sb2 * sb2 - (dot(oc, oc) - radius * radius);
                            float shadow = (sh > 0.0 && sb2 < 0.0) ? 0.08 : 1.0;

                            float ringLit = shadow * (0.35 + 0.65 * abs(dot(axis, sunDir)));
                            col = lerp(col, _RingColor.rgb * ringLit, saturate(ringDens * _RingOpacity));
                        }
                    }
                }

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
