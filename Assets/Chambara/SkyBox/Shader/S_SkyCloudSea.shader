Shader "Custom/Skybox/SkyCloudSea"
{
    // 空の上にいる感じの手続き型スカイボックス
    // 上: 青空グラデーション + 太陽 + 薄い上層雲 / 下: 一面の雲海
    // Lighting > Environment > Skybox Material に設定して使う
    Properties
    {
        [Header(Sky)]
        _ZenithColor ("Zenith Color (天頂)", Color) = (0.12, 0.35, 0.85, 1)
        _HorizonColor("Horizon Color (地平線)", Color) = (0.72, 0.85, 1.0, 1)
        _SkyExponent ("Sky Gradient Exponent", Range(0.1, 4)) = 0.6

        [Header(Sun)]
        _SunDirection("Sun Direction", Vector) = (0.3, 0.35, 0.8, 0)
        [HDR] _SunColor ("Sun Color", Color) = (3, 2.8, 2.4, 1)
        _SunSize     ("Sun Size", Range(0.0001, 0.05)) = 0.004
        _SunGlow     ("Sun Glow (光芒の広がり)", Range(1, 2000)) = 300
        _SunGlowIntensity("Sun Glow Intensity", Range(0, 3)) = 0.6

        [Header(Cloud Sea)]
        _CloudTopColor   ("Cloud Top Color (日向)", Color) = (1, 1, 1, 1)
        _CloudShadowColor("Cloud Shadow Color (陰)", Color) = (0.55, 0.65, 0.82, 1)
        _CloudSeaHeight  ("Cloud Sea Depth (雲海までの距離)", Range(0.05, 5)) = 1.0
        _CloudSeaScale   ("Cloud Sea Scale (模様の大きさ)", Range(0.05, 5)) = 0.6
        _CloudSeaCoverage("Cloud Sea Coverage (密度)", Range(0, 1)) = 0.75
        _CloudSeaSoftness("Cloud Sea Softness", Range(0.01, 1)) = 0.35

        [Header(Upper Clouds)]
        _UpperCloudColor   ("Upper Cloud Color", Color) = (1, 1, 1, 1)
        _UpperCloudScale   ("Upper Cloud Scale", Range(0.05, 5)) = 0.8
        _UpperCloudCoverage("Upper Cloud Coverage", Range(0, 1)) = 0.35
        _UpperCloudOpacity ("Upper Cloud Opacity", Range(0, 1)) = 0.7

        [Header(Common)]
        _WindDirection("Wind Direction (xz)", Vector) = (1, 0, 0.3, 0)
        _WindSpeed    ("Wind Speed", Float) = 0.02
        _HazeColor    ("Horizon Haze Color", Color) = (0.85, 0.92, 1.0, 1)
        _HazeAmount   ("Horizon Haze (地平線のかすみ)", Range(0.01, 1)) = 0.25
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
            Name "SkyCloudSea"

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _ZenithColor;
                half4  _HorizonColor;
                half   _SkyExponent;

                float4 _SunDirection;
                half4  _SunColor;
                half   _SunSize;
                half   _SunGlow;
                half   _SunGlowIntensity;

                half4  _CloudTopColor;
                half4  _CloudShadowColor;
                float  _CloudSeaHeight;
                float  _CloudSeaScale;
                half   _CloudSeaCoverage;
                half   _CloudSeaSoftness;

                half4  _UpperCloudColor;
                float  _UpperCloudScale;
                half   _UpperCloudCoverage;
                half   _UpperCloudOpacity;

                float4 _WindDirection;
                float  _WindSpeed;
                half4  _HazeColor;
                half   _HazeAmount;
                half   _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDirWS  = input.positionOS.xyz;
                return output;
            }

            // ---- Noise ----
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                // 回転を挟んで格子感を消す
                const float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
                [unroll]
                for (int k = 0; k < 6; k++)
                {
                    v += amp * ValueNoise(p);
                    p = mul(rot, p) * 2.03 + 17.1;
                    amp *= 0.5;
                }
                return v;
            }

            // 平面に投影した雲の濃度 (0..1) と厚み (陰影用)
            float2 CloudLayer(float2 uv, half coverage, half softness)
            {
                float2 wind = normalize(_WindDirection.xz + 1e-5) * _WindSpeed * _Time.y;
                float n  = Fbm(uv + wind);
                float n2 = Fbm(uv * 2.7 - wind * 1.6 + 5.2); // ディテール
                float shape = n * 0.75 + n2 * 0.25;
                float threshold = 1.0 - coverage;
                float density = smoothstep(threshold - softness * 0.5, threshold + softness * 0.5, shape);
                return float2(density, saturate((shape - threshold) * 2.0));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 dir = normalize(input.viewDirWS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float  sunDot = dot(dir, sunDir);

                // ---- 空のグラデーション ----
                float up = saturate(dir.y);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, _SkyExponent));

                // 太陽の光芒
                float glow = pow(saturate(sunDot), _SunGlow) + pow(saturate(sunDot), 8.0) * 0.15;
                sky += _SunColor.rgb * glow * _SunGlowIntensity;

                half3 col = sky;

                // ---- 上層雲 (薄い雲) ----
                if (dir.y > 0.0)
                {
                    float2 uv = dir.xz / (dir.y + 0.15) * _UpperCloudScale;
                    float2 c = CloudLayer(uv + 100.0, _UpperCloudCoverage, 0.4);
                    // 地平線付近はかすみに溶かす
                    float fade = smoothstep(0.0, 0.25, dir.y);
                    half3 cloudCol = _UpperCloudColor.rgb + _SunColor.rgb * pow(saturate(sunDot), 6.0) * 0.3;
                    col = lerp(col, cloudCol, c.x * _UpperCloudOpacity * fade);
                }

                // ---- 太陽本体 (上層雲の後ろに隠れすぎないよう後で加算) ----
                float sunDisk = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, sunDot);
                col += _SunColor.rgb * sunDisk;

                // ---- 雲海 ----
                if (dir.y < 0.0)
                {
                    float t = _CloudSeaHeight / max(-dir.y, 1e-4);   // 雲海平面までの距離
                    float2 uv = dir.xz * t * _CloudSeaScale;
                    float2 c = CloudLayer(uv, _CloudSeaCoverage, _CloudSeaSoftness);

                    // 隙間は雲の下にある深い色、雲は上面を太陽で照らす
                    float sunUp = saturate(sunDir.y);
                    half3 below = _CloudShadowColor.rgb * 0.7;
                    half3 lit = lerp(_CloudShadowColor.rgb, _CloudTopColor.rgb, saturate(c.y * 0.8 + 0.3 + sunUp * 0.3));
                    // 太陽側の照り返し
                    float3 flatDir = normalize(float3(dir.x, 0, dir.z));
                    float3 flatSun = normalize(float3(sunDir.x, 0, sunDir.z) + 1e-5);
                    lit += _SunColor.rgb * pow(saturate(dot(flatDir, flatSun)), 4.0) * 0.08;

                    half3 sea = lerp(below, lit, c.x);

                    // 遠くほどかすむ (遠方のノイズのちらつきも消える)
                    float haze = 1.0 - exp(-t * _HazeAmount * 0.15);
                    haze = max(haze, 1.0 - smoothstep(0.0, 0.08, -dir.y));
                    col = lerp(sea, _HazeColor.rgb, saturate(haze));
                }
                else
                {
                    // 地平線のかすみ (空側)
                    float haze = 1.0 - smoothstep(0.0, _HazeAmount * 0.4 + 0.01, dir.y);
                    col = lerp(col, _HazeColor.rgb, haze);
                }

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
