Shader "Custom/Skybox/SkyStorm"
{
    // 雷雲: 低く垂れこめる厚い雲 + 雲の中で光る稲光 + 時々落ちる稲妻 + 雨のカーテン + 荒れた海
    // 決戦っぽい雰囲気用
    // Lighting > Environment > Skybox Material に設定して使う
    Properties
    {
        [Header(Sky)]
        _SkyColor ("Sky Color (雲の切れ間)", Color) = (0.12, 0.13, 0.16, 1)

        [Header(Clouds)]
        _CloudDarkColor ("Cloud Dark Color", Color) = (0.025, 0.03, 0.04, 1)
        _CloudLightColor("Cloud Light Color", Color) = (0.2, 0.22, 0.26, 1)
        _CloudScale     ("Cloud Scale", Range(0.05, 5)) = 0.6
        _CloudCoverage  ("Cloud Coverage", Range(0, 1)) = 0.85
        _ScudAmount     ("Scud (流れる低い雲)", Range(0, 1)) = 0.7

        [Header(Lightning)]
        [HDR] _FlashColor("Flash Color", Color) = (2.5, 2.6, 3.2, 1)
        _FlashInterval  ("Flash Interval (秒)", Range(0.5, 20)) = 4
        _FlashChance    ("Flash Chance", Range(0, 1)) = 0.75
        _FlashIntensity ("Flash Intensity", Range(0, 5)) = 1.5
        _BoltChance     ("Bolt Chance (稲妻が見える確率)", Range(0, 1)) = 0.6
        _BoltWidth      ("Bolt Width", Range(0.0003, 0.01)) = 0.0015

        [Header(Rain)]
        _RainColor ("Rain Color", Color) = (0.13, 0.15, 0.18, 1)
        _RainShafts("Rain Shafts (雨のカーテン)", Range(0, 1)) = 0.6

        [Header(Sea)]
        _SeaColor  ("Sea Color", Color) = (0.012, 0.016, 0.02, 1)
        _WaveScale ("Wave Scale", Range(0.05, 5)) = 0.8

        [Header(Common)]
        _WindDirection("Wind Direction (xz)", Vector) = (1, 0, 0.4, 0)
        _WindSpeed    ("Wind Speed", Float) = 0.04
        _HazeColor    ("Horizon Haze Color", Color) = (0.09, 0.1, 0.12, 1)
        _HazeAmount   ("Horizon Haze", Range(0.01, 1)) = 0.35
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
            Name "SkyStorm"

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex SkyVert
            #pragma fragment frag

            #include "SkyCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _SkyColor;

                half4  _CloudDarkColor;
                half4  _CloudLightColor;
                float  _CloudScale;
                half   _CloudCoverage;
                half   _ScudAmount;

                half4  _FlashColor;
                float  _FlashInterval;
                half   _FlashChance;
                half   _FlashIntensity;
                half   _BoltChance;
                float  _BoltWidth;

                half4  _RainColor;
                half   _RainShafts;

                half4  _SeaColor;
                float  _WaveScale;

                float4 _WindDirection;
                float  _WindSpeed;
                half4  _HazeColor;
                half   _HazeAmount;
                half   _Exposure;
            CBUFFER_END

            // 1 回の落雷で 2〜3 回明滅する
            float FlashPulse(float t)
            {
                if (t < 0.0) return 0.0;
                float f = exp(-t * 18.0);
                f += step(0.12, t) * exp(-(t - 0.12) * 25.0) * 0.8;
                f += step(0.3, t)  * exp(-(t - 0.3)  * 12.0) * 0.6;
                return f;
            }

            half4 frag(SkyVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 dir = normalize(input.viewDirWS);
                float  az = SkyAzimuth(dir);
                float2 wind = normalize(_WindDirection.xz + 1e-5) * _WindSpeed * _Time.y;

                // ---- 雷のタイミングと位置 (周期ごとに乱数で決める) ----
                float cycleT = _Time.y / _FlashInterval;
                float cycle = floor(cycleT);
                float localT = frac(cycleT) * _FlashInterval - SkyHash21(float2(cycle, 3.1)) * _FlashInterval * 0.5;
                float active = step(SkyHash21(float2(cycle, 1.7)), _FlashChance);
                float pulse = FlashPulse(localT) * active;
                float flash = pulse * _FlashIntensity;

                float flashAz = SkyHash21(float2(cycle, 5.3)) * SKY_TAU - SKY_PI;
                float flashEl = lerp(0.06, 0.2, SkyHash21(float2(cycle, 8.9)));
                float3 flashDir = normalize(float3(sin(flashAz), flashEl, cos(flashAz)));
                float  flashAng = SkyAngle(dir, flashDir);

                half3 horizonCol = _HazeColor.rgb + _FlashColor.rgb * flash * 0.06;
                half3 col;

                if (dir.y >= 0.0)
                {
                    // ---- 厚い雷雲 ----
                    float2 uv = dir.xz / (dir.y + 0.06) * _CloudScale;
                    float2 c = SkyCloudLayer(uv, wind, _CloudCoverage, 0.6);
                    // 速く流れる低いちぎれ雲
                    float2 c2 = SkyCloudLayer(uv * 1.8 + 30.0, wind * 2.5, _CloudCoverage * 0.6, 0.5);

                    // 雲の内側から照らされる稲光
                    float flashLight = flash * (exp(-flashAng / 0.25) * (0.4 + 0.6 * (1.0 - c.y)) + 0.06);

                    half3 cloud = lerp(_CloudLightColor.rgb, _CloudDarkColor.rgb, c.y);
                    cloud += _FlashColor.rgb * flashLight;
                    half3 sky = _SkyColor.rgb + _FlashColor.rgb * flash * 0.05;
                    col = lerp(sky, cloud, c.x);
                    col = lerp(col, _CloudDarkColor.rgb * 0.8 + _FlashColor.rgb * flashLight * 0.5, c2.x * _ScudAmount);

                    // ---- 地平線のかすみ ----
                    float haze = 1.0 - smoothstep(0.0, _HazeAmount * 0.4 + 0.01, dir.y);
                    col = lerp(col, horizonCol, haze);

                    // ---- 稲妻 ----
                    float boltOn = step(SkyHash21(float2(cycle, 6.6)), _BoltChance);
                    float dAz = az - flashAz;
                    dAz -= SKY_TAU * round(dAz / SKY_TAU);
                    if (boltOn * pulse > 0.001 && abs(dAz) < 0.3 && dir.y < flashEl)
                    {
                        float yy = dir.y / flashEl; // 0 = 地面, 1 = 雲底
                        float jag = (SkyNoise2(float2(yy * 12.0, cycle)) - 0.5) * 0.05
                                  + (SkyNoise2(float2(yy * 45.0, cycle + 3.0)) - 0.5) * 0.015;
                        float d = abs(dAz - jag);
                        // 枝分かれ
                        float branchStart = 0.45 + SkyHash21(float2(cycle, 9.2)) * 0.3;
                        float bt = saturate((yy - branchStart) / (1.0 - branchStart));
                        float side = SkyHash21(float2(cycle, 4.4)) > 0.5 ? 1.0 : -1.0;
                        float branchX = jag + side * (1.0 - bt) * 0.04
                                      + (SkyNoise2(float2(yy * 60.0, cycle + 7.0)) - 0.5) * 0.01;
                        float dBranch = abs(dAz - branchX) + step(yy, branchStart);

                        float bolt = exp(-d / _BoltWidth) + exp(-d / (_BoltWidth * 15.0)) * 0.15
                                   + exp(-dBranch / (_BoltWidth * 0.7)) * 0.6;
                        bolt *= 1.0 - smoothstep(0.85, 1.0, yy);
                        col += _FlashColor.rgb * bolt * pulse * 2.0;
                    }
                }
                else
                {
                    // ---- 荒れた海 ----
                    float dist = 1.0 / max(-dir.y, 1e-3);
                    float2 gp = dir.xz * dist * _WaveScale;
                    float waves = SkyFbm2(gp + wind * 8.0, 4);
                    float crest = smoothstep(0.62, 0.75, SkyFbm2(gp * 2.3 - wind * 5.0, 4)); // 白波
                    half3 sea = _SeaColor.rgb * (0.6 + 0.8 * waves) + _CloudLightColor.rgb * crest * 0.25;

                    // 稲光の照り返し
                    float3 rdir = normalize(float3(dir.x + (waves - 0.5) * 0.1, -dir.y, dir.z + (waves - 0.5) * 0.1));
                    float reflAng = SkyAngle(rdir, flashDir);
                    sea += _FlashColor.rgb * flash * (exp(-reflAng / 0.3) * 0.15 + 0.03) * (0.5 + waves);

                    float haze = 1.0 - exp(-dist * _HazeAmount * 0.08);
                    haze = max(haze, 1.0 - smoothstep(0.0, 0.03, -dir.y));
                    col = lerp(sea, horizonCol, saturate(haze));
                }

                // ---- 雨のカーテン (地平線付近) ----
                float shaft = SkyFbm2(float2(cos(az), sin(az)) * 6.0 + wind * 0.5, 4);
                float streak = SkyNoise3(float3(cos(az) * 60.0, sin(az) * 60.0, dir.y * 4.0 + _Time.y * 4.0));
                float veil = smoothstep(0.45, 0.7, shaft) * (1.0 - smoothstep(0.0, 0.25, dir.y)) * smoothstep(-0.05, 0.0, dir.y);
                half3 rainCol = _RainColor.rgb + _FlashColor.rgb * flash * 0.08;
                col = lerp(col, rainCol, veil * _RainShafts * (0.7 + 0.3 * streak));

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
