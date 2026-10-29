Shader "Custom/Skybox/SkyFloatingCity"
{
    // 街の上空に少し浮いている感じの手続き型スカイボックス
    // ビルを格子状の箱としてレイトレース (2D DDA) するので、オブジェクトを置かなくても
    // 見下ろすと屋上・道路・車のライト、水平方向には摩天楼が見える
    // カメラ位置は無視して常に「街の上空」に固定 (= 背景としてだけ使う)
    Properties
    {
        [Header(Camera)]
        _CameraHeight ("Camera Height (浮いている高さ m)", Range(5, 400)) = 70
        _DriftSpeed   ("Drift Speed (ゆっくり前進 m/s)", Range(0, 30)) = 2

        [Header(City Layout)]
        _CellSize     ("Block Size (区画の大きさ m)", Range(10, 100)) = 32
        _StreetWidth  ("Street Width (道幅 m)", Range(2, 40)) = 12
        _MinHeight    ("Min Building Height", Range(1, 100)) = 10
        _BuildingHeight("Building Height", Range(5, 300)) = 55
        _TowerChance  ("Tower Chance (高層ビル率)", Range(0, 1)) = 0.12
        _TowerMultiplier("Tower Multiplier", Range(1, 6)) = 3
        _EmptyChance  ("Empty Lot Chance (空き地率)", Range(0, 1)) = 0.08
        _DowntownPos  ("Downtown Position (xz 中心街の位置)", Vector) = (0, 0, 600, 0)
        _DowntownRadius("Downtown Radius", Range(50, 3000)) = 700
        _MaxDistance  ("Max Distance (描画距離 m)", Range(200, 6000)) = 3000

        [Header(Buildings)]
        _BuildingColorA("Building Color A", Color) = (0.32, 0.33, 0.36, 1)
        _BuildingColorB("Building Color B", Color) = (0.45, 0.4, 0.35, 1)
        _RoofColor     ("Roof Color", Color) = (0.2, 0.2, 0.22, 1)
        _GlassColor    ("Glass Color (消灯窓)", Color) = (0.08, 0.1, 0.14, 1)
        _FloorHeight   ("Floor Height (階高 m)", Range(2, 8)) = 3.6
        _WindowSpacing ("Window Spacing (窓の間隔 m)", Range(1, 8)) = 3

        [Header(Lights)]
        [HDR] _WindowColorA("Window Color A (暖色)", Color) = (1.6, 1.1, 0.55, 1)
        [HDR] _WindowColorB("Window Color B (寒色)", Color) = (0.8, 1.1, 1.6, 1)
        _WindowLitRatio    ("Window Lit Ratio (点灯率)", Range(0, 1)) = 0.4
        _WindowIntensity   ("Window Intensity", Range(0, 5)) = 1.5
        [HDR] _CrownColor  ("Tower Crown Light (高層ビルの頂部照明)", Color) = (1.5, 1.8, 2.5, 1)
        [HDR] _ShopColor   ("Shop Light (1階の店明かり)", Color) = (2, 1.4, 0.8, 1)
        [HDR] _BeaconColor ("Beacon Color (航空障害灯)", Color) = (6, 0.3, 0.15, 1)
        [HDR] _HeadlightColor("Headlight Color", Color) = (3, 2.8, 2.4, 1)
        [HDR] _TaillightColor("Taillight Color", Color) = (3, 0.25, 0.15, 1)
        [HDR] _LampColor   ("Street Lamp Color", Color) = (3, 1.8, 0.7, 1)
        _TrafficDensity    ("Traffic Density", Range(0, 1)) = 0.5
        _TrafficSpeed      ("Traffic Speed (m/s)", Range(0, 40)) = 14

        [Header(Ground)]
        _RoadColor    ("Road Color", Color) = (0.06, 0.06, 0.07, 1)
        _SidewalkColor("Sidewalk Color", Color) = (0.2, 0.2, 0.21, 1)

        [Header(Lighting)]
        _SunDirection ("Sun or Moon Direction", Vector) = (-0.4, 0.5, 0.7, 0)
        [HDR] _SunColor("Sun or Moon Color", Color) = (0.12, 0.15, 0.25, 1)
        _AmbientColor ("Ambient Color", Color) = (0.05, 0.05, 0.09, 1)

        [Header(Sky)]
        _ZenithColor  ("Zenith Color (天頂)", Color) = (0.01, 0.015, 0.045, 1)
        _HorizonColor ("Horizon Color (地平線)", Color) = (0.2, 0.12, 0.15, 1)
        _SkyExponent  ("Sky Gradient Exponent", Range(0.1, 4)) = 0.4
        _StarIntensity("Star Intensity", Range(0, 2)) = 0.6
        _SunDiskSize  ("Sun or Moon Disk Size", Range(0, 0.05)) = 0.0015
        _SunGlow      ("Sun Glow Intensity", Range(0, 3)) = 0.3

        [Header(Fog)]
        _FogColor     ("Fog Color (街明かりで光るもや)", Color) = (0.28, 0.17, 0.16, 1)
        _FogDensity   ("Fog Density", Range(0, 0.01)) = 0.0012

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
            Name "SkyFloatingCity"

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_STEPS 256

            CBUFFER_START(UnityPerMaterial)
                float  _CameraHeight;
                float  _DriftSpeed;

                float  _CellSize;
                float  _StreetWidth;
                float  _MinHeight;
                float  _BuildingHeight;
                half   _TowerChance;
                float  _TowerMultiplier;
                half   _EmptyChance;
                float4 _DowntownPos;
                float  _DowntownRadius;
                float  _MaxDistance;

                half4  _BuildingColorA;
                half4  _BuildingColorB;
                half4  _RoofColor;
                half4  _GlassColor;
                float  _FloorHeight;
                float  _WindowSpacing;

                half4  _WindowColorA;
                half4  _WindowColorB;
                half   _WindowLitRatio;
                half   _WindowIntensity;
                half4  _CrownColor;
                half4  _ShopColor;
                half4  _BeaconColor;
                half4  _HeadlightColor;
                half4  _TaillightColor;
                half4  _LampColor;
                half   _TrafficDensity;
                float  _TrafficSpeed;

                half4  _RoadColor;
                half4  _SidewalkColor;

                float4 _SunDirection;
                half4  _SunColor;
                half4  _AmbientColor;

                half4  _ZenithColor;
                half4  _HorizonColor;
                half   _SkyExponent;
                half   _StarIntensity;
                half   _SunDiskSize;
                half   _SunGlow;

                half4  _FogColor;
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

            float Band(float x, float a, float b, float aa)
            {
                return smoothstep(a - aa, a + aa, x) * (1.0 - smoothstep(b - aa, b + aa, x));
            }

            // 区画 cell に建つビルの箱 (高さ0なら空き地)
            void GetBuilding(float2 cell, out float3 bmin, out float3 bmax, out float4 rnd, out float tower)
            {
                rnd = float4(Hash21(cell), Hash21(cell + 17.3), Hash21(cell + 41.1), Hash21(cell + 73.7));

                float S = _CellSize;
                float w = _StreetWidth * 0.5;
                float2 c0 = cell * S;
                float2 insetA = float2(rnd.y, rnd.z) * S * 0.1;
                float2 insetB = float2(rnd.z, rnd.w) * S * 0.1;
                bmin.xz = c0 + w + insetA;
                bmax.xz = c0 + S - w - insetB;

                // 中心街ほど高い
                float d = length(c0 + S * 0.5 - _DowntownPos.xz);
                float downtown = lerp(0.35, 1.6, exp(-d / _DowntownRadius));
                tower = step(1.0 - _TowerChance * downtown, rnd.w);

                float h = lerp(_MinHeight, _BuildingHeight, rnd.x * rnd.x) * downtown;
                h *= lerp(1.0, _TowerMultiplier, tower);
                h *= step(_EmptyChance, Hash21(cell + 91.7));

                bmin.y = 0.0;
                bmax.y = h;
            }

            half3 Sky(float3 rd, float3 sunDir)
            {
                float up = saturate(rd.y);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, _SkyExponent));

                float2 starUV = rd.xz / (rd.y + 1.0) * 400.0;
                float star = step(0.997, Hash21(floor(starUV)));
                star *= 1.0 - smoothstep(0.0, 0.5, length(frac(starUV) - 0.5));
                col += star * _StarIntensity * smoothstep(0.1, 0.5, rd.y)
                     * (0.6 + 0.4 * sin(_Time.y * 2.0 + Hash21(floor(starUV) + 3.1) * 50.0));

                float sunDot = dot(rd, sunDir);
                col += _SunColor.rgb * (pow(saturate(sunDot), 60.0) + pow(saturate(sunDot), 6.0) * 0.1) * _SunGlow * 4.0;
                col += _SunColor.rgb * 8.0 * smoothstep(1.0 - _SunDiskSize, 1.0 - _SunDiskSize * 0.7, sunDot);

                // 地平線付近は街のもやに溶かす
                col = lerp(col, _FogColor.rgb, exp(-max(rd.y, 0.0) * 12.0));
                return col;
            }

            // 道路 (車のライト・街灯)
            half3 ShadeGround(float3 p, float footprint, half3 light)
            {
                float S = _CellSize;
                float w = _StreetWidth * 0.5;
                float2 q  = p.xz - floor(p.xz / S) * S;          // 0..S
                float2 sd = q - S * step(S * 0.5, q);            // 最寄りの道路中心線からの符号付き距離
                float2 ad = abs(sd);
                float2 streetId = floor(p.xz / S + 0.5);

                float street = saturate(step(ad.x, w) + step(ad.y, w));
                half3 col = lerp(_SidewalkColor.rgb, _RoadColor.rgb, street) * light;
                col += _LampColor.rgb * 0.015 * street;

                float detail = saturate(2.0 - footprint);
                half3 emit = 0;

                // 車: z方向の道 (ad.x<w) と x方向の道 (ad.y<w)
                [unroll]
                for (int axis = 0; axis < 2; axis++)
                {
                    float across = axis == 0 ? sd.x : sd.y;
                    float along  = axis == 0 ? p.z : p.x;
                    float sid    = axis == 0 ? streetId.x : streetId.y;
                    float inStreet = step(abs(across), w);

                    [unroll]
                    for (int l = 0; l < 2; l++)
                    {
                        float lane = l == 0 ? 1.0 : -1.0;
                        float a = along - lane * _TrafficSpeed * _Time.y;
                        float spacing = 9.0;
                        float seg = floor(a / spacing);
                        float f = frac(a / spacing) * spacing - spacing * 0.5;
                        float present = step(Hash21(float2(seg, sid * 3.7 + lane + axis * 51.0)), _TrafficDensity);
                        float dc = length(float2((across - lane * w * 0.45) / 0.9, f / 2.0));
                        float car = (1.0 - smoothstep(0.5, 1.0, dc)) * present * inStreet;
                        emit += (lane > 0 ? _HeadlightColor.rgb : _TaillightColor.rgb) * car;
                    }

                    // 街灯 (歩道側に一定間隔)
                    float lampAlong = frac(along / 18.0) * 18.0 - 9.0;
                    float dl = length(float2(abs(across) - (w + 1.0), lampAlong));
                    emit += _LampColor.rgb * (1.0 - smoothstep(0.3, 1.2, dl));
                    // 街灯の照らす光だまり
                    emit += _LampColor.rgb * 0.04 * (1.0 - smoothstep(0.0, 7.0, dl)) * inStreet;
                }

                // 遠方は平均の明るさに (チラつき防止)
                half3 avg = ((_HeadlightColor.rgb + _TaillightColor.rgb) * _TrafficDensity * 0.08 + _LampColor.rgb * 0.06)
                          * (2.0 * _StreetWidth / S);
                return col + lerp(avg, emit, detail);
            }

            half3 ShadeBuilding(float3 p, float3 n, float3 bmin, float3 bmax, float4 rnd, float tower,
                                float footprint, half3 light, float3 sunDir)
            {
                float h = bmax.y;
                float isTop = step(0.5, n.y);

                half3 albedo = lerp(_BuildingColorA.rgb, _BuildingColorB.rgb, rnd.z);
                albedo = lerp(albedo, _RoofColor.rgb, isTop);

                float ao = lerp(0.35, 1.0, saturate(p.y / 15.0));
                half3 col = albedo * light * lerp(ao, 1.0, isTop);
                half3 emit = 0;

                if (isTop < 0.5)
                {
                    // ---- 壁面: 窓 ----
                    float a = abs(n.x) > 0.5 ? p.z : p.x;
                    float2 wc = float2(a / _WindowSpacing, p.y / _FloorHeight);
                    float2 gid = floor(wc);
                    float2 g = frac(wc);
                    float aa = footprint / _WindowSpacing;
                    float rect = Band(g.x, 0.15, 0.85, aa) * Band(g.y, 0.25, 0.8, aa);
                    float ratio = saturate(_WindowLitRatio * lerp(0.4, 1.6, rnd.y));
                    float lit = step(Hash21(gid + rnd.xy * 97.0 + n.xz * 13.0), ratio);
                    half3 wcol = lerp(_WindowColorA.rgb, _WindowColorB.rgb, step(0.65, Hash21(gid.yy + rnd.zw * 31.0)));

                    float detail = saturate(1.5 - aa * 1.5);
                    float onFacade = step(_FloorHeight, p.y) * step(p.y, h - 1.0);
                    float winMask = lerp(0.45 * ratio, rect * lit, detail) * onFacade;
                    float glassMask = lerp(0.45, rect, detail) * onFacade;

                    col = lerp(col, _GlassColor.rgb * (light + _AmbientColor.rgb * 2.0), glassMask * 0.8);
                    emit += wcol * winMask * _WindowIntensity;

                    // 1階の店明かり
                    emit += _ShopColor.rgb * step(p.y, _FloorHeight) * step(0.5, p.y) * step(0.3, rnd.x) * 0.6;

                    // 高層ビルの頂部照明
                    emit += _CrownColor.rgb * tower * step(h - 2.5, p.y);
                }
                else
                {
                    // ---- 屋上: 縁と航空障害灯 ----
                    float2 toEdge = min(p.xz - bmin.xz, bmax.xz - p.xz);
                    float edge = 1.0 - smoothstep(0.4, 0.6 + footprint, min(toEdge.x, toEdge.y));
                    col = lerp(col, albedo * 1.4 * light, edge);

                    float corner = length(toEdge);
                    float blink = 0.5 + 0.5 * sin(_Time.y * 3.0 + rnd.x * 20.0);
                    float beaconSize = max(1.2, footprint * 1.5);
                    emit += _BeaconColor.rgb * step(25.0, h)
                          * (1.0 - smoothstep(0.0, beaconSize, corner)) * blink;
                    emit += _CrownColor.rgb * tower * edge * 0.6;
                }

                return col + emit;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 rd = normalize(input.viewDirWS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float pixelAngle = max(length(fwidth(rd)), 1e-5);

                // 道路の交差点 (x=0 の通り) の上空を前進する
                float3 ro = float3(0.0, _CameraHeight, _Time.y * _DriftSpeed);

                float S = _CellSize;
                float3 rdSafe = rd + (step(abs(rd), 1e-6) * 1e-6);
                float3 invRd = 1.0 / rdSafe;

                // 判定を打ち切る距離 (地面 / ビル最大高さ / 描画距離)
                float maxH = _BuildingHeight * 1.6 * _TowerMultiplier;
                float tGround = rd.y < 0.0 ? ro.y / -rd.y : 1e9;
                float tTop = rd.y > 0.0 ? max((maxH - ro.y) / rd.y, 0.0) : 1e9;
                float tEnd = min(min(tGround, tTop), _MaxDistance);

                // ---- 2D DDA で区画をたどる ----
                float2 cell = floor(ro.xz / S);
                float2 stepDir = sign(rdSafe.xz);
                float2 tMax = ((cell + step(0.0, stepDir)) * S - ro.xz) * invRd.xz;
                float2 tDelta = abs(S * invRd.xz);
                float tCell = 0.0;

                float hitT = -1.0;
                float3 hitN = 0;
                float3 hitMin = 0, hitMax = 0;
                float4 hitRnd = 0;
                float hitTower = 0;

                [loop]
                for (int i = 0; i < MAX_STEPS; i++)
                {
                    if (tCell > tEnd) break;

                    float3 bmin, bmax; float4 rnd; float tower;
                    GetBuilding(cell, bmin, bmax, rnd, tower);

                    if (bmax.y > 0.0)
                    {
                        float3 t1 = (bmin - ro) * invRd;
                        float3 t2 = (bmax - ro) * invRd;
                        float3 tn = min(t1, t2);
                        float3 tf = max(t1, t2);
                        float tNear = max(max(tn.x, tn.y), tn.z);
                        float tFar  = min(min(tf.x, tf.y), tf.z);
                        if (tNear <= tFar && tNear > 0.0)
                        {
                            hitT = tNear;
                            hitN = -sign(rdSafe) * step(tn.yzx, tn) * step(tn.zxy, tn);
                            hitMin = bmin; hitMax = bmax; hitRnd = rnd; hitTower = tower;
                            break;
                        }
                    }

                    if (tMax.x < tMax.y) { tCell = tMax.x; tMax.x += tDelta.x; cell.x += stepDir.x; }
                    else                 { tCell = tMax.y; tMax.y += tDelta.y; cell.y += stepDir.y; }
                }

                half3 col;
                float t;

                if (hitT > 0.0)
                {
                    t = hitT;
                    float3 p = ro + rd * t;
                    half3 l = _SunColor.rgb * saturate(dot(hitN, sunDir)) + _AmbientColor.rgb * lerp(0.7, 1.0, hitN.y);
                    col = ShadeBuilding(p, hitN, hitMin, hitMax, hitRnd, hitTower, t * pixelAngle, l, sunDir);
                }
                else if (rd.y < 0.0 && tGround < _MaxDistance)
                {
                    t = tGround;
                    float3 p = ro + rd * t;
                    half3 l = _SunColor.rgb * saturate(sunDir.y) + _AmbientColor.rgb;
                    col = ShadeGround(p, t * pixelAngle, l);
                }
                else if (rd.y < 0.0)
                {
                    t = _MaxDistance;
                    col = _FogColor.rgb;
                }
                else
                {
                    return half4(Sky(rd, sunDir) * _Exposure, 1.0);
                }

                // 距離フォグ
                float fog = 1.0 - exp(-t * _FogDensity);
                col = lerp(col, _FogColor.rgb, fog);

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
