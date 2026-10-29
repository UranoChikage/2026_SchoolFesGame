Shader "Custom/Skybox/SkyMetaverse"
{
    // メタバース / 電脳空間っぽい手続き型スカイボックス
    // 下: どこまでも続くネオングリッドの床 (光のパルスが走る・波紋が広がる・空を映り込む)
    //     + 床から立ち上るデータの光柱
    // 上: 紫のグラデーション + ホログラムの六角形バリア + 浮遊するデータ粒子
    //     + シンセウェーブ風の太陽 + 回転する巨大なポータルリング
    // Lighting > Environment > Skybox Material に設定して使う
    Properties
    {
        [Header(Camera)]
        _CameraHeight ("Camera Height (床からの高さ m)", Range(1, 200)) = 20
        _ScrollSpeed  ("Scroll Speed (xz 床の流れる速さ m/s)", Vector) = (0, 6, 0, 0)

        [Header(Sky)]
        _ZenithColor  ("Zenith Color (天頂)", Color) = (0.01, 0.005, 0.045, 1)
        _HorizonColor ("Horizon Color (地平線)", Color) = (0.22, 0.04, 0.32, 1)
        _SkyExponent  ("Sky Gradient Exponent", Range(0.1, 4)) = 0.5
        [HDR] _HorizonGlowColor("Horizon Glow Color", Color) = (1.6, 0.25, 1.3, 1)
        _HorizonGlowWidth("Horizon Glow Width", Range(0.002, 0.3)) = 0.04

        [Header(Hex Barrier)]
        [HDR] _HexColor ("Hex Color", Color) = (0.2, 0.8, 1.6, 1)
        _HexScale       ("Hex Scale (六角形の細かさ)", Range(1, 40)) = 9
        _HexLineWidth   ("Hex Line Width", Range(0.005, 0.2)) = 0.03
        _HexIntensity   ("Hex Intensity", Range(0, 2)) = 0.25
        _HexScanSpeed   ("Hex Scan Speed (走査波の速さ)", Range(0, 2)) = 0.25
        _HexFlicker     ("Hex Flicker (点灯するセルの割合)", Range(0, 0.2)) = 0.03

        [Header(Data Particles)]
        [HDR] _ParticleColor("Particle Color", Color) = (0.6, 1.4, 2.0, 1)
        _ParticleScale  ("Particle Scale (粒の数)", Range(10, 300)) = 90
        _ParticleSize   ("Particle Size", Range(0.01, 0.3)) = 0.06
        _ParticleDensity("Particle Density", Range(0, 1)) = 0.35
        _ParticleRise   ("Particle Rise Speed (上昇速度)", Range(0, 3)) = 0.4

        [Header(Sun)]
        _SunDirection ("Sun Direction", Vector) = (0, 0.12, 1, 0)
        [HDR] _SunColorTop   ("Sun Color Top", Color) = (3, 2.4, 0.6, 1)
        [HDR] _SunColorBottom("Sun Color Bottom", Color) = (3, 0.3, 1.4, 1)
        _SunSize      ("Sun Size (角度 rad)", Range(0.02, 0.6)) = 0.22
        _SunStripes   ("Sun Stripes (横縞の本数)", Range(0, 30)) = 10
        _SunGlow      ("Sun Glow Intensity", Range(0, 3)) = 0.6

        [Header(Portal Ring)]
        _RingDirection("Ring Direction", Vector) = (0, 0.42, 1, 0)
        [HDR] _RingColor ("Ring Color", Color) = (0.3, 1.8, 3.0, 1)
        [HDR] _RingColor2("Ring Color 2 (外周の破線)", Color) = (2.5, 0.4, 2.2, 1)
        _RingRadius   ("Ring Radius (角度 rad)", Range(0.05, 1.4)) = 0.55
        _RingWidth    ("Ring Width", Range(0.001, 0.05)) = 0.006
        [IntRange] _RingSegments ("Ring Segments (破線の数)", Range(2, 128)) = 48
        _RingSpeed    ("Ring Rotate Speed", Range(-2, 2)) = 0.05
        _PortalIntensity("Portal Inner Swirl", Range(0, 1)) = 0.15

        [Header(Grid Floor)]
        _FloorColor   ("Floor Color", Color) = (0.005, 0.0, 0.02, 1)
        _GridSize     ("Grid Size (マス目の大きさ m)", Range(0.5, 50)) = 6
        [IntRange] _MajorEvery   ("Major Line Every (太線の間隔 マス)", Range(1, 16)) = 5
        _GridLineWidth("Grid Line Width", Range(0.001, 0.3)) = 0.04
        [HDR] _GridColor     ("Grid Color", Color) = (0.1, 0.6, 1.6, 1)
        [HDR] _GridMajorColor("Grid Major Color", Color) = (0.6, 0.3, 2.4, 1)
        _FloorReflection("Floor Reflection (空の映り込み)", Range(0, 1)) = 0.5

        [Header(Grid Pulses)]
        [HDR] _PulseColor ("Pulse Color (線を走る光)", Color) = (2.5, 3, 4, 1)
        _PulseDensity ("Pulse Density", Range(0, 1)) = 0.35
        _PulseSpeed   ("Pulse Speed", Range(0, 5)) = 1.2
        _WaveSpacing  ("Ripple Spacing (波紋の間隔 m)", Range(10, 1000)) = 160
        _WaveSpeed    ("Ripple Speed", Range(0, 2)) = 0.25
        _WaveIntensity("Ripple Intensity", Range(0, 5)) = 1.5

        [Header(Data Beams)]
        [HDR] _BeamColorA ("Beam Color A", Color) = (0.2, 1.5, 3.0, 1)
        [HDR] _BeamColorB ("Beam Color B", Color) = (2.5, 0.3, 2.0, 1)
        [IntRange] _BeamCells    ("Beam Block (光柱の区画 マス)", Range(2, 32)) = 10
        _BeamChance   ("Beam Chance (光柱が立つ確率)", Range(0, 1)) = 0.12
        _BeamHeight   ("Beam Height (m)", Range(10, 1000)) = 260
        _BeamWidth    ("Beam Width (m)", Range(0.05, 5)) = 0.6
        _MaxDistance  ("Max Distance (描画距離 m)", Range(100, 6000)) = 2500

        [Header(Fog)]
        _FogDensity   ("Fog Density", Range(0, 0.01)) = 0.0015

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
            Name "SkyMetaverse"

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_STEPS 96
            #define TAU 6.28318530718

            CBUFFER_START(UnityPerMaterial)
                float  _CameraHeight;
                float4 _ScrollSpeed;

                half4  _ZenithColor;
                half4  _HorizonColor;
                half   _SkyExponent;
                half4  _HorizonGlowColor;
                half   _HorizonGlowWidth;

                half4  _HexColor;
                float  _HexScale;
                half   _HexLineWidth;
                half   _HexIntensity;
                half   _HexScanSpeed;
                half   _HexFlicker;

                half4  _ParticleColor;
                float  _ParticleScale;
                half   _ParticleSize;
                half   _ParticleDensity;
                half   _ParticleRise;

                float4 _SunDirection;
                half4  _SunColorTop;
                half4  _SunColorBottom;
                float  _SunSize;
                half   _SunStripes;
                half   _SunGlow;

                float4 _RingDirection;
                half4  _RingColor;
                half4  _RingColor2;
                float  _RingRadius;
                float  _RingWidth;
                float  _RingSegments;
                half   _RingSpeed;
                half   _PortalIntensity;

                half4  _FloorColor;
                float  _GridSize;
                float  _MajorEvery;
                half   _GridLineWidth;
                half4  _GridColor;
                half4  _GridMajorColor;
                half   _FloorReflection;

                half4  _PulseColor;
                half   _PulseDensity;
                half   _PulseSpeed;
                float  _WaveSpacing;
                half   _WaveSpeed;
                half   _WaveIntensity;

                half4  _BeamColorA;
                half4  _BeamColorB;
                float  _BeamCells;
                half   _BeamChance;
                float  _BeamHeight;
                float  _BeamWidth;
                float  _MaxDistance;

                float  _FogDensity;

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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Hash31(float3 p)
            {
                p = frac(p * float3(123.34, 456.21, 789.53));
                p += dot(p, p.yzx + 45.32);
                return frac((p.x + p.y) * p.z);
            }

            float2 ModPos(float2 x, float2 m)
            {
                return x - m * floor(x / m);
            }

            // 遠くでもチラつかないグリッド線 (uv はマス単位)
            float PristineGrid(float2 uv, float lineWidth)
            {
                float2 uvDeriv = max(fwidth(uv), 1e-5);
                float2 drawWidth = clamp(lineWidth, uvDeriv, 0.5);
                float2 lineAA = uvDeriv * 1.5;
                float2 gridUV = 1.0 - abs(frac(uv) * 2.0 - 1.0);
                float2 grid2 = smoothstep(drawWidth + lineAA, drawWidth - lineAA, gridUV);
                grid2 *= saturate(lineWidth / drawWidth);
                grid2 = lerp(grid2, lineWidth, saturate(uvDeriv * 2.0 - 1.0));
                return lerp(grid2.x, 1.0, grid2.y);
            }

            // 方向 dir を中心にした接平面の基底
            void Basis(float3 dir, out float3 right, out float3 up)
            {
                float3 ref = abs(dir.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0);
                right = normalize(cross(ref, dir));
                up = cross(dir, right);
            }

            // 空のグラデーション + 太陽のにじみ (床の映り込みでも使う軽い版)
            half3 SkyBase(float3 rd, float3 sunDir)
            {
                float up = saturate(rd.y);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, _SkyExponent));
                float sunDot = saturate(dot(rd, sunDir));
                col += lerp(_SunColorBottom.rgb, _SunColorTop.rgb, 0.3) * pow(sunDot, 8.0) * _SunGlow * 0.3;
                return col;
            }

            // ホログラムの六角形バリア
            half3 HexBarrier(float3 rd)
            {
                float2 uv = rd.xz / (rd.y + 0.35) * _HexScale;

                const float2 r = float2(1.0, 1.7320508);
                float2 h = r * 0.5;
                float2 a = ModPos(uv, r) - h;
                float2 b = ModPos(uv - h, r) - h;
                float2 gv = dot(a, a) < dot(b, b) ? a : b;
                float2 id = uv - gv;

                float2 ap = abs(gv);
                float hexDist = max(dot(ap, float2(0.5, 0.8660254)), ap.x);
                float edge = 0.5 - hexDist;
                float aa = fwidth(edge) + 1e-4;
                float hexLine = 1.0 - smoothstep(_HexLineWidth, _HexLineWidth + aa * 1.5, edge);
                // 密になりすぎる所は薄く
                hexLine *= saturate(1.0 - aa * 8.0);

                // 中心から広がる走査波
                float scan = frac(length(id) * 0.04 - _Time.y * _HexScanSpeed);
                scan = pow(saturate(1.0 - abs(scan * 2.0 - 1.0)), 12.0);

                // ランダムに点灯するセル
                float flick = step(1.0 - _HexFlicker, Hash21(id + floor(_Time.y * 2.0 + Hash21(id) * 4.0)));

                float scanLine = 0.85 + 0.15 * sin(rd.y * 900.0 - _Time.y * 6.0);
                float mask = smoothstep(0.02, 0.3, rd.y);
                return _HexColor.rgb * _HexIntensity * mask * scanLine
                     * (hexLine * (0.35 + scan * 2.5) + flick * 0.25 + scan * 0.08);
            }

            // 浮遊するデータ粒子 (ゆっくり上昇)
            half3 Particles(float3 rd, float pixelAngle)
            {
                half3 col = 0;
                [unroll]
                for (int layer = 0; layer < 2; layer++)
                {
                    float scale = _ParticleScale * (layer == 0 ? 1.0 : 0.55);
                    float3 q = rd * scale + float3(layer * 31.7, -_Time.y * _ParticleRise * (layer == 0 ? 1.0 : 0.6), layer * 17.3);
                    float3 id = floor(q);
                    float3 f = frac(q) - 0.5;
                    float h = Hash31(id);
                    float3 off = (float3(Hash31(id + 1.7), Hash31(id + 3.3), Hash31(id + 5.9)) - 0.5) * 0.6;
                    float d = length(f - off);

                    float size = _ParticleSize * (0.4 + h);
                    float pix = pixelAngle * scale;
                    float drawSize = max(size, pix);
                    float p = (1.0 - smoothstep(drawSize * 0.5, drawSize, d)) * (size / drawSize);
                    p += exp(-d / (drawSize * 1.5)) * 0.15;
                    p *= step(1.0 - _ParticleDensity, h);
                    p *= 0.5 + 0.5 * sin(_Time.y * (1.5 + h * 3.0) + h * 60.0);
                    col += _ParticleColor.rgb * p * (layer == 0 ? 1.0 : 0.6);
                }
                return col * smoothstep(-0.02, 0.15, rd.y);
            }

            // シンセウェーブ風の太陽
            half3 Sun(float3 rd, float3 sunDir, float pixelAngle)
            {
                float3 right, up;
                Basis(sunDir, right, up);
                float front = step(0.0, dot(rd, sunDir));
                float2 s = float2(dot(rd, right), dot(rd, up)) / _SunSize;
                float r = length(s);

                float aa = pixelAngle / _SunSize * 1.5;
                float disk = 1.0 - smoothstep(1.0 - aa, 1.0, r);

                // 下半分ほど太くなる横縞の切れ込み
                float stripe = frac(s.y * _SunStripes * 0.5 + _Time.y * 0.15);
                float gap = saturate(-s.y * 0.7 + 0.1) * step(s.y, 0.2);
                float sAA = aa * _SunStripes * 0.5;
                disk *= lerp(1.0, smoothstep(gap - sAA, gap + sAA, stripe), saturate(gap * 20.0) * step(0.5, _SunStripes));

                half3 sunCol = lerp(_SunColorBottom.rgb, _SunColorTop.rgb, saturate(s.y * 0.5 + 0.5));
                half3 glow = lerp(_SunColorBottom.rgb, _SunColorTop.rgb, 0.4) * exp(-max(r - 1.0, 0.0) * 2.5) * 0.25 * _SunGlow;
                return (sunCol * disk + glow * (1.0 - disk)) * front;
            }

            // 回転する巨大なポータルリング
            half3 PortalRing(float3 rd, float pixelAngle)
            {
                float3 dir = normalize(_RingDirection.xyz);
                float3 right, up;
                Basis(dir, right, up);

                float ang = acos(clamp(dot(rd, dir), -1.0, 1.0));
                float theta = atan2(dot(rd, up), dot(rd, right)) / TAU;   // -0.5..0.5
                float aa = pixelAngle * 1.5;
                float segN = floor(_RingSegments);   // 整数にしないと atan2 の継ぎ目でズレる
                float segAA = aa / max(sin(ang), 1e-3) * segN / TAU + 1e-4;

                half3 col = 0;

                // メインリング (細い芯 + 光のにじみ)
                float d0 = abs(ang - _RingRadius);
                float w0 = max(_RingWidth, aa);
                col += _RingColor.rgb * ((1.0 - smoothstep(w0 * 0.5, w0, d0)) * (_RingWidth / w0) + exp(-d0 / (_RingWidth * 6.0)) * 0.2);

                // 外周の回転する破線リング
                float d1 = abs(ang - _RingRadius * 1.07);
                float w1 = max(_RingWidth * 1.8, aa);
                float seg = frac(theta * segN + _Time.y * _RingSpeed * segN);
                float segMask = smoothstep(0.0, segAA, seg) * (1.0 - smoothstep(0.6 - segAA, 0.6, seg));
                col += _RingColor2.rgb * (1.0 - smoothstep(w1 * 0.5, w1, d1)) * segMask * (_RingWidth * 1.8 / w1);

                // 内側の逆回転する目盛りリング
                float d2 = abs(ang - _RingRadius * 0.93);
                float w2 = max(_RingWidth * 0.7, aa);
                float tick = frac(theta * segN * 2.0 - _Time.y * _RingSpeed * segN * 3.0);
                float tickMask = 1.0 - smoothstep(0.15, 0.15 + segAA * 2.0, tick);
                col += _RingColor.rgb * (1.0 - smoothstep(w2 * 0.5, w2, d2)) * (0.3 + tickMask) * 0.6 * (_RingWidth * 0.7 / w2);

                // ポータル内部の渦
                float inside = 1.0 - smoothstep(_RingRadius * 0.85, _RingRadius * 0.93, ang);
                float swirl = 0.5 + 0.5 * sin(theta * TAU * 5.0 + ang * 40.0 - _Time.y * 1.5);
                swirl *= 0.5 + 0.5 * sin(theta * TAU * 3.0 - ang * 25.0 + _Time.y * 0.9);
                col += lerp(_RingColor.rgb, _RingColor2.rgb, swirl) * swirl * inside * _PortalIntensity
                     * saturate(ang / _RingRadius) * 0.3;

                return col;
            }

            // 床を走るパルス (太線上を移動する光)
            float GridPulses(float2 uvMajor, float pixelInMajor)
            {
                float detail = saturate(1.0 - pixelInMajor * 4.0);
                float result = 0;
                [unroll]
                for (int axis = 0; axis < 2; axis++)
                {
                    float across = axis == 0 ? uvMajor.y : uvMajor.x;
                    float along  = axis == 0 ? uvMajor.x : uvMajor.y;
                    float lineId = floor(across + 0.5);
                    float dAcross = abs(across - lineId);

                    float h = Hash21(float2(lineId, axis * 37.0));
                    float dir = h > 0.5 ? 1.0 : -1.0;
                    float a = along * 0.125 - dir * _Time.y * _PulseSpeed * (0.5 + h) * 0.25 + h * 13.0;
                    float seg = floor(a);
                    float f = frac(a);
                    float present = step(Hash21(float2(seg, lineId + axis * 91.0)), _PulseDensity);
                    // 進行方向に尾を引く
                    float head = dir > 0.0 ? f : 1.0 - f;
                    float trail = pow(head, 6.0) * (1.0 - smoothstep(0.97, 1.0, head));

                    float w = max(0.03, pixelInMajor);
                    float lineMask = (1.0 - smoothstep(w * 0.5, w, dAcross)) * (0.03 / w);
                    result += trail * present * lineMask;
                }
                return result * detail;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 rd = normalize(input.viewDirWS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float pixelAngle = max(length(fwidth(rd)), 1e-5);

                float3 ro = float3(_ScrollSpeed.x * _Time.y, _CameraHeight, _ScrollSpeed.y * _Time.y);

                // ================= 床 =================
                // 微分を使うので分岐の外で常に計算する
                float tGround = ro.y / max(-rd.y, 1e-4);
                float3 p = ro + rd * tGround;
                float2 uv = p.xz / _GridSize;
                float2 uvMajor = uv / _MajorEvery;

                float minor = PristineGrid(uv, _GridLineWidth);
                float major = PristineGrid(uvMajor, _GridLineWidth * 1.5 / _MajorEvery);
                float pixelInMajor = tGround * pixelAngle / (_GridSize * _MajorEvery);

                // カメラ位置から広がる波紋
                float rDist = length(p.xz - ro.xz);
                float wave = frac(rDist / _WaveSpacing - _Time.y * _WaveSpeed);
                wave = pow(saturate(1.0 - abs(wave * 2.0 - 1.0)), 10.0) * _WaveIntensity;
                wave *= saturate(1.0 - tGround * pixelAngle / (_WaveSpacing * 0.05));   // 遠方のチラつき防止

                half3 floorCol = _FloorColor.rgb;
                floorCol += _GridColor.rgb * minor * 0.5 * (1.0 + wave);
                floorCol += _GridMajorColor.rgb * major * (1.0 + wave * 0.5);
                floorCol += _PulseColor.rgb * GridPulses(uvMajor, pixelInMajor);

                // 光沢のある床に空が映り込む
                float3 rr = float3(rd.x, -rd.y, rd.z);
                float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(-rd.y), 5.0);
                floorCol += SkyBase(rr, sunDir) * _FloorReflection * fresnel;

                float fog = 1.0 - exp(-tGround * _FogDensity);
                floorCol = lerp(floorCol, _HorizonColor.rgb, fog);

                // ================= 空 =================
                half3 skyCol = SkyBase(rd, sunDir);
                skyCol += HexBarrier(rd);
                skyCol += Particles(rd, pixelAngle);
                skyCol += Sun(rd, sunDir, pixelAngle);
                skyCol += PortalRing(rd, pixelAngle);

                float isSky = smoothstep(-pixelAngle, pixelAngle, rd.y);
                half3 col = lerp(floorCol, skyCol, isSky);

                // 地平線の発光
                col += _HorizonGlowColor.rgb * exp(-abs(rd.y) / _HorizonGlowWidth);

                // ================= データの光柱 (2D DDA) =================
                float S = _GridSize * _BeamCells;
                float tTop = rd.y > 0.0 ? max((_BeamHeight - ro.y) / rd.y, 0.0) : 1e9;
                float tEnd = min(min(rd.y < 0.0 ? tGround : 1e9, tTop), _MaxDistance);

                float3 rdSafe = rd + (step(abs(rd), 1e-6) * 1e-6);
                float3 invRd = 1.0 / rdSafe;
                float2 cell = floor(ro.xz / S);
                float2 stepDir = sign(rdSafe.xz);
                float2 tMax = ((cell + step(0.0, stepDir)) * S - ro.xz) * invRd.xz;
                float2 tDelta = abs(S * invRd.xz);
                float tCell = 0.0;
                float rd2 = max(dot(rd.xz, rd.xz), 1e-8);

                half3 beams = 0;
                [loop]
                for (int i = 0; i < MAX_STEPS; i++)
                {
                    if (tCell > tEnd) break;

                    float h0 = Hash21(cell + 7.7);
                    if (h0 < _BeamChance)
                    {
                        float2 c = (cell + 0.5 + (float2(Hash21(cell + 1.3), Hash21(cell + 2.9)) - 0.5) * 0.4) * S;
                        float H = _BeamHeight * lerp(0.3, 1.0, Hash21(cell + 5.1));

                        float tc = clamp(dot(c - ro.xz, rd.xz) / rd2, 0.0, tEnd);
                        float d = length(ro.xz + rd.xz * tc - c);
                        float y = ro.y + rd.y * tc;
                        float inH = step(0.0, y) * (1.0 - smoothstep(H * 0.4, H, y));

                        float rEff = max(_BeamWidth, tc * pixelAngle * 1.5);
                        float k = _BeamWidth / rEff;
                        float core = exp(-d * d / (rEff * rEff)) * k;
                        float halo = exp(-d / (rEff * 4.0)) * 0.25 * k;
                        float flow = 0.6 + 0.4 * sin(y * 0.12 - _Time.y * 5.0 + h0 * 40.0);

                        half3 bc = lerp(_BeamColorA.rgb, _BeamColorB.rgb, step(0.5, Hash21(cell + 9.2)));
                        beams += bc * (core + halo) * inH * flow * exp(-tc * _FogDensity);
                    }

                    if (tMax.x < tMax.y) { tCell = tMax.x; tMax.x += tDelta.x; cell.x += stepDir.x; }
                    else                 { tCell = tMax.y; tMax.y += tDelta.y; cell.y += stepDir.y; }
                }
                col += beams;

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
