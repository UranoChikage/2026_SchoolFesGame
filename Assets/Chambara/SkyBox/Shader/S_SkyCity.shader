Shader "Custom/Skybox/SkyCity"
{
    // 都会の中にいる感じの手続き型スカイボックス
    // 空グラデーション(スモッグ) + 遠景の山 + ビル群3層(奥→手前) + 窓明かり + 航空障害灯
    // 色を変えれば 夕焼け / 昼 / 夜 どれにもできる
    Properties
    {
        [Header(Sky)]
        _ZenithColor ("Zenith Color (天頂)", Color) = (0.22, 0.36, 0.68, 1)
        _HorizonColor("Horizon Color (地平線/スモッグ)", Color) = (1.0, 0.58, 0.32, 1)
        _SkyExponent ("Sky Gradient Exponent", Range(0.1, 4)) = 0.45
        _StarIntensity("Star Intensity (夜用)", Range(0, 2)) = 0
        [HDR] _CityGlowColor("City Glow (街明かりの照り返し)", Color) = (0.6, 0.3, 0.15, 1)
        _CityGlowHeight("City Glow Height", Range(0.01, 0.5)) = 0.12

        [Header(Sun)]
        _SunDirection("Sun Direction", Vector) = (-0.6, 0.06, 0.8, 0)
        [HDR] _SunColor ("Sun Color", Color) = (4, 2.4, 1.2, 1)
        _SunSize     ("Sun Size", Range(0, 0.05)) = 0.006
        _SunGlow     ("Sun Glow (光芒の広がり)", Range(1, 500)) = 40
        _SunGlowIntensity("Sun Glow Intensity", Range(0, 3)) = 0.8

        [Header(Hills)]
        _HillColor ("Hill Color", Color) = (0.35, 0.25, 0.25, 1)
        _HillHeight("Hill Height", Range(0, 0.3)) = 0.07

        [Header(Buildings)]
        _BuildingColor  ("Building Color", Color) = (0.05, 0.05, 0.08, 1)
        _BuildingHeight ("Building Height", Range(0.01, 0.5)) = 0.12
        _BuildingCount  ("Building Count (奥の層の本数)", Range(40, 600)) = 260
        _TowerChance    ("Tower Chance (高層ビル率)", Range(0, 1)) = 0.12
        _HazeColor      ("Haze Color (大気のかすみ)", Color) = (0.95, 0.6, 0.4, 1)
        _FarHaze        ("Far Layer Haze", Range(0, 1)) = 0.75
        _MidHaze        ("Mid Layer Haze", Range(0, 1)) = 0.45
        _NearHaze       ("Near Layer Haze", Range(0, 1)) = 0.15
        _GroundFog      ("Ground Fog (足元のかすみ)", Range(0, 1)) = 0.6
        _GroundColor    ("Ground Color", Color) = (0.12, 0.1, 0.1, 1)

        [Header(Windows)]
        [HDR] _WindowColorA("Window Color A (暖色)", Color) = (1.0, 0.75, 0.4, 1)
        [HDR] _WindowColorB("Window Color B (寒色)", Color) = (0.6, 0.8, 1.0, 1)
        _WindowIntensity   ("Window Intensity", Range(0, 5)) = 0.6
        _WindowLitRatio    ("Window Lit Ratio (点灯率)", Range(0, 1)) = 0.3
        _WindowDensity     ("Window Density (窓の細かさ)", Range(50, 800)) = 300
        [HDR] _BeaconColor ("Beacon Color (航空障害灯)", Color) = (4, 0.2, 0.1, 1)

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
            Name "SkyCity"

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
                half   _StarIntensity;
                half4  _CityGlowColor;
                half   _CityGlowHeight;

                float4 _SunDirection;
                half4  _SunColor;
                half   _SunSize;
                half   _SunGlow;
                half   _SunGlowIntensity;

                half4  _HillColor;
                float  _HillHeight;

                half4  _BuildingColor;
                float  _BuildingHeight;
                float  _BuildingCount;
                half   _TowerChance;
                half4  _HazeColor;
                half   _FarHaze;
                half   _MidHaze;
                half   _NearHaze;
                half   _GroundFog;
                half4  _GroundColor;

                half4  _WindowColorA;
                half4  _WindowColorB;
                half   _WindowIntensity;
                half   _WindowLitRatio;
                float  _WindowDensity;
                half4  _BeaconColor;

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
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    v += amp * ValueNoise(p);
                    p = p * 2.07 + 13.7;
                    amp *= 0.5;
                }
                return v;
            }

            // 範囲 [a,b] の中なら1 (aa 幅でぼかす)
            float Band(float x, float a, float b, float aa)
            {
                return smoothstep(a - aa, a + aa, x) * (1.0 - smoothstep(b - aa, b + aa, x));
            }

            // ビル1層を描いて col に重ねる
            // az01: 方位 0..1 / el: 仰角(rad) / aaEl: 仰角1pxあたりの変化量
            void CityLayer(float az01, float el, float aaEl, float count, float heightScale,
                           float seed, half haze, inout half3 col)
            {
                float u    = az01 * count;
                float cell = fmod(floor(u) + count, count);   // 一周でつながるように
                float fx   = frac(u);

                float r1 = Hash21(float2(cell, seed));
                float r2 = Hash21(float2(cell, seed + 11.3));
                float r3 = Hash21(float2(cell, seed + 23.7));
                float r4 = Hash21(float2(cell, seed + 37.1));
                float r5 = Hash21(float2(cell, seed + 51.9));

                // 幅と位置
                float width = lerp(0.55, 1.0, r2);
                float left  = (1.0 - width) * r3;
                float lx    = (fx - left) / width;            // ビル内 0..1

                // 高さ (たまに高層ビル)
                float tower = step(1.0 - _TowerChance, r4);
                float h = heightScale * (0.25 + 0.75 * r1 * r1) * lerp(1.0, 2.4, tower);
                // セットバック (上が細くなる段々)
                float stepped = step(0.5, r5);
                h *= lerp(1.0, lerp(0.85, 1.15, step(abs(lx - 0.5), 0.25)), stepped);

                float cellAngle = 6.2831853 / count;
                float aaX = aaEl / (cellAngle * width);       // lx 空間での1px
                float inX = Band(lx, 0.0, 1.0, aaX);
                float body = inX * (1.0 - smoothstep(h - aaEl, h + aaEl, el));

                // 高層ビルのアンテナ
                float antennaH = h + heightScale * 0.25;
                float antenna = tower * Band(lx, 0.48, 0.52, aaX)
                              * (1.0 - smoothstep(antennaH - aaEl, antennaH + aaEl, el));

                float mask = saturate(body + antenna);

                // ビルの色: 層ごとのかすみ + 足元ほど霧
                float fog = _GroundFog * exp(-max(el, 0.0) / max(heightScale * 0.6, 1e-3));
                half3 bcol = lerp(_BuildingColor.rgb, _HazeColor.rgb, saturate(haze + (1.0 - haze) * fog));

                // ---- 窓 ----
                float wy = el * _WindowDensity;
                float wx = lx * width * cellAngle * _WindowDensity * 0.8;
                float aaW = aaEl * _WindowDensity;
                float2 gid = floor(float2(wx, wy));
                float2 g = frac(float2(wx, wy));
                float rect = Band(g.x, 0.2, 0.8, aaW) * Band(g.y, 0.3, 0.75, aaW);
                float rw = Hash21(gid + float2(cell * 7.31, seed * 3.17));
                float lit = step(rw, _WindowLitRatio);
                // 窓が1pxより小さい遠景は平均の明るさに置き換えてチラつきを防ぐ
                float detail = saturate(1.5 - aaW * 2.0);
                float win = lerp(_WindowLitRatio * 0.27, rect * lit, detail);
                win *= body * step(0.0, wx) * (1.0 - step(wy, 1.0));   // 地面より上だけ
                half3 wcol = lerp(_WindowColorA.rgb, _WindowColorB.rgb, step(0.7, Hash21(gid + cell)));
                wcol = lerp(wcol, _HazeColor.rgb, haze * 0.6);

                // 航空障害灯 (ゆっくり点滅)
                float2 bp = float2((lx - 0.5) * width * cellAngle, el - antennaH);
                float beaconSize = max(aaEl * 1.5, heightScale * 0.012);
                float beacon = tower * (1.0 - smoothstep(0.0, beaconSize, length(bp)));
                beacon *= 0.5 + 0.5 * sin(_Time.y * 3.0 + cell);

                col = lerp(col, bcol, mask);
                col += wcol * win * _WindowIntensity + _BeaconColor.rgb * beacon;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 dir = normalize(input.viewDirWS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float  sunDot = dot(dir, sunDir);

                float el = asin(clamp(dir.y, -1.0, 1.0));
                float az01 = atan2(dir.x, dir.z) * (0.5 / PI) + 0.5;
                float aaEl = max(fwidth(el), 1e-5);

                // ---- 空 ----
                float up = saturate(dir.y);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, _SkyExponent));

                // 街明かりの照り返し
                col += _CityGlowColor.rgb * exp(-max(el, 0.0) / _CityGlowHeight);

                // 星
                float2 starUV = dir.xz / (dir.y + 1.0) * 400.0;
                float star = step(0.997, Hash21(floor(starUV)));
                star *= (1.0 - smoothstep(0.0, 0.5, length(frac(starUV) - 0.5)));
                col += star * _StarIntensity * smoothstep(0.05, 0.4, dir.y)
                     * (0.6 + 0.4 * sin(_Time.y * 2.0 + Hash21(floor(starUV) + 3.1) * 50.0));

                // 太陽
                float glow = pow(saturate(sunDot), _SunGlow) + pow(saturate(sunDot), 4.0) * 0.1;
                col += _SunColor.rgb * glow * _SunGlowIntensity;
                col += _SunColor.rgb * smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.7, sunDot);

                // 地面 (ビルの隙間から見える部分)
                half3 ground = lerp(_GroundColor.rgb, _HazeColor.rgb, exp(el * 20.0) * _GroundFog);
                col = lerp(col, ground, smoothstep(aaEl, -aaEl, el));

                // ---- 遠景の山 (方位を円周上でサンプルして継ぎ目なし) ----
                float2 ring = normalize(dir.xz + 1e-5);
                float hillH = _HillHeight * (Fbm(ring * 4.0 + 3.0) * 1.4 - 0.2);
                float hill = 1.0 - smoothstep(hillH - aaEl, hillH + aaEl, el);
                half3 hcol = lerp(_HillColor.rgb, _HazeColor.rgb, 0.6 + 0.4 * exp(-max(el, 0.0) / max(_HillHeight * 0.5, 1e-3)) * _GroundFog);
                col = lerp(col, hcol, hill * step(-0.05, el));

                // ---- ビル群 (奥 → 手前) ----
                float n = floor(_BuildingCount);
                CityLayer(az01, el, aaEl, n,                    _BuildingHeight * 0.55, 1.0, _FarHaze,  col);
                CityLayer(az01, el, aaEl, floor(n * 0.45),      _BuildingHeight * 0.85, 2.0, _MidHaze,  col);
                CityLayer(az01, el, aaEl, floor(n * 0.18),      _BuildingHeight * 1.3,  3.0, _NearHaze, col);

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
