Shader "Custom/Skybox/SkyHighAltitude"
{
    // はるか上空に浮かぶステージ用: 足元のずっと下に地上が見える手続き型スカイボックス
    // 下: 畑のパッチワーク + 森 + 川・湖 + 町 + 幹線道路 (夜は町明かり)
    //     + カメラと地上の間に浮かぶ雲 (地上に影を落とす)
    //     + 遠くほど空気で霞む (高さを感じさせる)
    // 上: 空のグラデーション + 太陽 (夜は月) + 星
    // Lighting > Environment > Skybox Material に設定して使う
    Properties
    {
        [Header(Camera)]
        _Altitude     ("Altitude (地上からの高さ m)", Range(100, 10000)) = 1500
        _DriftSpeed   ("Drift Speed (xy = 地上の流れる速さ m/s)", Vector) = (0, 4, 0, 0)
        _MaxDistance  ("Max Distance (描画距離 m)", Range(5000, 200000)) = 50000

        [Header(Sky)]
        _ZenithColor  ("Zenith Color (天頂)", Color) = (0.1, 0.3, 0.8, 1)
        _HorizonColor ("Horizon Color (地平線)", Color) = (0.65, 0.8, 0.95, 1)
        _SkyExponent  ("Sky Gradient Exponent", Range(0.1, 4)) = 0.5
        _SunDirection ("Sun Direction (夜は月)", Vector) = (0.4, 0.6, 0.5, 0)
        [HDR] _SunColor("Sun Color", Color) = (5, 4.8, 4.4, 1)
        _SunSize      ("Sun Size (見かけの半径)", Range(0.002, 0.1)) = 0.01
        [HDR] _SunGlowColor("Sun Glow Color", Color) = (1, 0.9, 0.75, 1)
        _SunGlow      ("Sun Glow", Range(0, 3)) = 0.5
        _StarIntensity("Star Intensity (夜だけ)", Range(0, 5)) = 0

        [Header(Ground)]
        [HDR] _GroundLight("Ground Light (地上の明るさ・色)", Color) = (1.1, 1.05, 1, 1)
        _FieldSize    ("Field Size (畑の大きさ m)", Range(20, 500)) = 120
        _FieldColorA  ("Field Color A", Color) = (0.4, 0.5, 0.2, 1)
        _FieldColorB  ("Field Color B", Color) = (0.6, 0.52, 0.3, 1)
        _FieldColorC  ("Field Color C", Color) = (0.3, 0.4, 0.17, 1)
        _ForestColor  ("Forest Color", Color) = (0.08, 0.16, 0.07, 1)
        _ForestAmount ("Forest Amount", Range(0, 1)) = 0.4
        _TownColor    ("Town Color", Color) = (0.5, 0.48, 0.46, 1)
        _TownAmount   ("Town Amount", Range(0, 1)) = 0.25
        _RoadColor    ("Road Color", Color) = (0.22, 0.22, 0.23, 1)
        _WaterColor   ("Water Color", Color) = (0.04, 0.1, 0.16, 1)
        _RiverWidth   ("River Width (m)", Range(0, 200)) = 40
        _LakeAmount   ("Lake Amount", Range(0, 1)) = 0.25

        [Header(Night Lights)]
        [HDR] _TownLightColor("Town Light Color", Color) = (3, 1.8, 0.8, 1)
        [HDR] _RoadLightColor("Road Light Color", Color) = (3, 1.4, 0.4, 1)
        _NightLights  ("Night Lights (0 = 昼)", Range(0, 3)) = 0

        [Header(Clouds)]
        _CloudColor      ("Cloud Color (日向)", Color) = (1.2, 1.2, 1.2, 1)
        _CloudShadowColor("Cloud Shadow Color (陰)", Color) = (0.6, 0.66, 0.78, 1)
        _CloudAltitude   ("Cloud Altitude (地上からの高さ m)", Range(50, 10000)) = 700
        _CloudScale      ("Cloud Scale (雲の大きさ m)", Range(50, 5000)) = 900
        _CloudCoverage   ("Cloud Coverage", Range(0, 1)) = 0.35
        _CloudOpacity    ("Cloud Opacity", Range(0, 1)) = 0.95
        _CloudShadow     ("Cloud Shadow (地上の影の濃さ)", Range(0, 1)) = 0.55
        _WindDirection   ("Wind Direction (xz)", Vector) = (1, 0, 0.3, 0)
        _WindSpeed       ("Wind Speed", Float) = 0.01

        [Header(Atmosphere)]
        _HazeColor    ("Haze Color (空気の色)", Color) = (0.7, 0.8, 0.92, 1)
        _HazeDensity  ("Haze Density (霞み)", Range(0, 0.0005)) = 0.00007
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
            Name "SkyHighAltitude"

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex SkyVert
            #pragma fragment frag

            #include "SkyCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _Altitude;
                float4 _DriftSpeed;
                float  _MaxDistance;

                half4  _ZenithColor;
                half4  _HorizonColor;
                half   _SkyExponent;
                float4 _SunDirection;
                half4  _SunColor;
                float  _SunSize;
                half4  _SunGlowColor;
                half   _SunGlow;
                half   _StarIntensity;

                half4  _GroundLight;
                float  _FieldSize;
                half4  _FieldColorA;
                half4  _FieldColorB;
                half4  _FieldColorC;
                half4  _ForestColor;
                half   _ForestAmount;
                half4  _TownColor;
                half   _TownAmount;
                half4  _RoadColor;
                half4  _WaterColor;
                float  _RiverWidth;
                half   _LakeAmount;

                half4  _TownLightColor;
                half4  _RoadLightColor;
                half   _NightLights;

                half4  _CloudColor;
                half4  _CloudShadowColor;
                float  _CloudAltitude;
                float  _CloudScale;
                half   _CloudCoverage;
                half   _CloudOpacity;
                half   _CloudShadow;
                float4 _WindDirection;
                float  _WindSpeed;

                half4  _HazeColor;
                float  _HazeDensity;
                half   _Exposure;
            CBUFFER_END

            // 空の色 (水面の映り込みにも使う)
            half3 SkyColor(float3 dir, float3 sunDir, bool drawSun)
            {
                float up = saturate(dir.y);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, _SkyExponent));
                float ang = SkyAngle(dir, sunDir);
                col += _SunGlowColor.rgb * _SunGlow * (exp(-ang / (_SunSize * 3.0)) * 0.7 + exp(-ang / 0.35) * 0.25);
                if (drawSun)
                {
                    float disk = 1.0 - smoothstep(_SunSize * 0.96, _SunSize, ang);
                    col = lerp(col, _SunColor.rgb, disk);
                }
                return col;
            }

            // 空気の霞み (太陽の方向は明るく)
            half3 HazeColor(float3 dir, float3 sunDir)
            {
                return _HazeColor.rgb + _SunGlowColor.rgb * _SunGlow * pow(saturate(dot(dir, sunDir)), 6.0) * 0.4;
            }

            // 地上の模様. p = 地上の位置 (m), fw = 1 ピクセルの大きさ (m)
            // 戻り値 rgb = 色, water = 水面, light = 夜の明かり
            half3 GroundAlbedo(float2 p, float fw, out float water, out half3 light)
            {
                const float2x2 rot = float2x2(0.955, -0.296, 0.296, 0.955);
                float2 rp = mul(rot, p);

                // ---- 畑のパッチワーク ----
                float2 warp = float2(SkyFbm2(rp / (_FieldSize * 6.0), 3), SkyFbm2(rp / (_FieldSize * 6.0) + 5.3, 3)) - 0.5;
                float2 q = rp / _FieldSize + warp * 2.0;
                float row = floor(q.y);
                float hx = SkyHash21(float2(row, 1.3));
                float widthMul = 0.6 + hx * 0.8;
                float qx = q.x * widthMul + hx * 7.0;
                float2 id = float2(floor(qx), row);
                float h = SkyHash21(id);
                half3 field = h < 0.33 ? _FieldColorA.rgb : (h < 0.66 ? _FieldColorB.rgb : _FieldColorC.rgb);
                field *= 0.8 + 0.4 * SkyHash21(id + 3.1);

                // 作物の筋
                float stripeCoord = SkyHash21(id + 9.1) > 0.5 ? frac(qx) : frac(q.y);
                float stripes = 0.88 + 0.12 * sin(stripeCoord * SKY_TAU * 10.0);
                field *= lerp(stripes, 1.0, smoothstep(_FieldSize * 0.02, _FieldSize * 0.06, fw));

                // あぜ道・生け垣
                float bx = min(frac(qx), 1.0 - frac(qx)) * _FieldSize / widthMul;
                float by = min(frac(q.y), 1.0 - frac(q.y)) * _FieldSize;
                float border = 1.0 - smoothstep(1.5, 1.5 + fw, min(bx, by));
                border *= saturate(3.0 / max(fw, 1e-3));
                field = lerp(field, _ForestColor.rgb * 1.2, border * 0.7);

                half3 col = field;

                // ---- 森 ----
                float fn = SkyFbm2(p / (_FieldSize * 6.0) + 3.1, 5);
                float fth = lerp(0.72, 0.32, _ForestAmount);
                float forest = smoothstep(fth, fth + 0.03, fn);
                float trees = lerp(SkyNoise2(p / 6.0), 0.5, smoothstep(2.0, 8.0, fw));
                half3 forestCol = _ForestColor.rgb * (0.65 + 0.7 * trees);
                col = lerp(col, forestCol, forest);

                // ---- 町 ----
                float tn = SkyFbm2(p / (_FieldSize * 18.0) + 7.7, 4);
                float tth = lerp(0.72, 0.38, _TownAmount);
                float town = smoothstep(tth, tth + 0.02, tn);
                float2 bf = frac(p / 40.0);
                float streetD = min(min(bf.x, 1.0 - bf.x), min(bf.y, 1.0 - bf.y)) * 40.0;
                float street = (1.0 - smoothstep(3.0, 3.0 + fw, streetD)) * saturate(6.0 / max(fw, 1e-3));
                float lr = 1.5;
                float avgLight = 0.5 * 3.14 * lr * lr / 144.0;
                float lights = avgLight;
                half3 roof = _TownColor.rgb;
                // 屋根と明かりの細かい計算は、町の中で近いところだけ
                [branch]
                if (town > 0.0 && fw < 12.0)
                {
                    float2 cell = floor(p / 10.0);
                    float rh = SkyHash21(cell);
                    half3 roofNear = _TownColor.rgb * (0.55 + 0.9 * rh);
                    roofNear = lerp(roofNear, half3(0.55, 0.28, 0.22), step(0.85, SkyHash21(cell + 4.4)) * 0.8); // 赤い屋根
                    roof = lerp(roofNear, _TownColor.rgb, smoothstep(3.0, 12.0, fw));

                    // 夜の町明かり (遠くでは平均値に)
                    float2 lc = floor(p / 12.0);
                    float3 lh = SkyHash33(float3(lc, 2.0));
                    float2 lpos = (lc + 0.2 + lh.xy * 0.6) * 12.0;
                    float ld = length(p - lpos);
                    float pointLight = step(0.5, lh.z) * exp(-ld * ld / (lr * lr + fw * fw)) * (lr * lr) / (lr * lr + fw * fw);
                    lights = lerp(pointLight, avgLight, smoothstep(4.0, 12.0, fw));
                }
                half3 townCol = lerp(roof, _RoadColor.rgb, street);
                col = lerp(col, townCol, town);
                light = _TownLightColor.rgb * lights * 3.0 * town + _RoadLightColor.rgb * street * town * 0.25;

                // ---- 幹線道路 ----
                float ra = abs(frac((rp.x + (SkyFbm2(rp / 3000.0, 3) - 0.5) * 900.0) / 1700.0) - 0.5) * 1700.0;
                float rb = abs(frac((rp.y + (SkyFbm2(rp / 3000.0 + 4.0, 3) - 0.5) * 900.0) / 2300.0) - 0.5) * 2300.0;
                float road = (1.0 - smoothstep(4.0, 4.0 + fw, min(ra, rb))) * saturate(8.0 / max(fw, 1e-3) + 0.15);
                col = lerp(col, _RoadColor.rgb, road);
                light += _RoadLightColor.rgb * road * 0.6;

                // ---- 川と湖 ----
                float rn = SkyFbm2(p / (_FieldSize * 25.0) + 11.0, 5);
                // m に換算した川の中心からの距離 (ノイズの平均的な傾きで近似。分岐内で使えるよう微分を使わない)
                float rd = abs(rn - 0.5) * _FieldSize * 25.0 * 1.5;
                float river = (1.0 - smoothstep(_RiverWidth * 0.5, _RiverWidth * 0.5 + fw, rd)) * saturate(_RiverWidth / max(fw, 1e-3) + 0.2);
                float ln = SkyFbm2(p / (_FieldSize * 10.0) + 21.0, 5);
                float lth = lerp(0.8, 0.55, _LakeAmount);
                float lake = smoothstep(lth, lth + 0.01, ln);
                water = saturate(max(river * step(0.001, _RiverWidth), lake));
                light *= 1.0 - water;

                return col;
            }

            half4 frag(SkyVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 dir = normalize(input.viewDirWS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float2 camXZ = _DriftSpeed.xy * _Time.y;
                float2 wind = normalize(_WindDirection.xz + 1e-5) * _WindSpeed * _Time.y;
                half3 haze = HazeColor(dir, sunDir);
                // 1 ピクセルの角度 (微分は分岐の外で取る)
                float pixelAngle = max(length(fwidth(dir)), 1e-5);

                half3 col;

                [branch]
                if (dir.y >= 0.0)
                {
                    // ---- 空 ----
                    col = SkyColor(dir, sunDir, true);
                    [branch]
                    if (_StarIntensity > 0.0)
                    {
                        float2 s1 = SkyStars(dir, 280.0, 0.65, 0.18, 0.5);
                        float2 s2 = SkyStars(dir, 100.0, 0.8, 0.12, 0.5);
                        col += (SkyStarColor(s1.y) * s1.x + SkyStarColor(s2.y) * s2.x * 1.8) * _StarIntensity * smoothstep(0.0, 0.2, dir.y);
                    }
                    col = lerp(col, haze, 1.0 - smoothstep(0.0, 0.06, dir.y));
                }
                else
                {
                    // ---- 地上 ----
                    float down = max(-dir.y, 1e-4);
                    float tGround = min(_Altitude / down, _MaxDistance);
                    // 斜めに見るほど 1 ピクセルが地面で長く伸びる
                    float fw = max(tGround * pixelAngle / max(down, 0.02), 1e-3);

                    float groundHaze = 1.0 - exp(-tGround * _HazeDensity);
                    groundHaze = max(groundHaze, step(_MaxDistance * 0.999, tGround));

                    [branch]
                    if (groundHaze > 0.995)
                    {
                        // ほぼ霞で見えない遠方は模様を計算しない
                        col = haze;
                    }
                    else
                    {
                        float2 gp = camXZ + dir.xz * tGround;

                        float water;
                        half3 nightLight;
                        half3 albedo = GroundAlbedo(gp, fw, water, nightLight);

                        // 雲の影 (雲本体より軽い近似)
                        float cloudShadow = 0.0;
                        [branch]
                        if (_CloudShadow > 0.0 && sunDir.y > 0.0)
                        {
                            float2 shadowUV = (gp + sunDir.xz * (_CloudAltitude / max(sunDir.y, 0.05))) / _CloudScale;
                            float n = SkyFbm2(shadowUV + wind, 4);
                            float th = 1.0 - _CloudCoverage;
                            cloudShadow = smoothstep(th - 0.2, th + 0.2, n) * _CloudShadow;
                        }

                        half3 ground = albedo * _GroundLight.rgb * (1.0 - cloudShadow);

                        // 水面: 空の映り込み + 太陽 (月) のきらめき
                        [branch]
                        if (water > 0.0)
                        {
                            float3 rdir = normalize(float3(dir.x, abs(dir.y), dir.z));
                            float fres = lerp(0.1, 1.0, pow(1.0 - down, 5.0));
                            half3 waterCol = lerp(_WaterColor.rgb * _GroundLight.rgb, SkyColor(rdir, sunDir, false), fres);
                            float glint = pow(saturate(dot(rdir, sunDir)), 400.0) * 3.0 + pow(saturate(dot(rdir, sunDir)), 40.0) * 0.15;
                            waterCol += _SunColor.rgb * glint * (1.0 - cloudShadow);
                            ground = lerp(ground, waterCol, water);
                        }

                        ground += nightLight * _NightLights;

                        // 空気遠近法
                        col = lerp(ground, haze, groundHaze);
                    }
                }
                // 地平線のつなぎ目をなじませる
                col = lerp(col, haze, (1.0 - smoothstep(0.0, 0.01, abs(dir.y))) * 0.8);

                // ---- 雲の層 (カメラより下なら見下ろす、上なら見上げる) ----
                float dh = _CloudAltitude - _Altitude;
                float tCloud = dh / (abs(dir.y) > 1e-4 ? dir.y : 1e-4);
                if (tCloud > 0.0 && tCloud < _MaxDistance)
                {
                    float2 cuv = (camXZ + dir.xz * tCloud) / _CloudScale;
                    float2 c = SkyCloudLayer(cuv, wind, _CloudCoverage, 0.4);

                    float sunUp = saturate(sunDir.y * 2.0);
                    float fromAbove = step(dh, 0.0);
                    float lit = saturate(0.35 + c.y * 0.6 * lerp(0.5, 1.0, fromAbove) + sunUp * 0.2);
                    half3 cloudCol = lerp(_CloudShadowColor.rgb, _CloudColor.rgb, lit);
                    // 太陽側の縁が明るい
                    cloudCol += _SunGlowColor.rgb * pow(saturate(dot(dir, sunDir)), 4.0) * (1.0 - c.y) * 0.3;

                    float cloudHaze = 1.0 - exp(-tCloud * _HazeDensity);
                    cloudCol = lerp(cloudCol, haze, cloudHaze);
                    col = lerp(col, cloudCol, c.x * _CloudOpacity);
                }

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
