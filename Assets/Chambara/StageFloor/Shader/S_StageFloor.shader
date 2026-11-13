Shader "Custom/Stage/StageFloor"
{
    // ステージの足場 (Cube を伸ばした床) 用シェーダー (URP Lit ベース)
    // 背景のスカイボックスに埋もれないように
    //   ・上面の外周に発光ライン → 足場の輪郭が常にはっきり見える
    //   ・上面 (タイル) と側面 (帯 / 警告ストライプ) で模様を変えて立体感を出す
    //   ・Self Illumination で暗い背景でも床が沈まない
    //   ・リムライトでシルエットを立たせる
    // タイルはワールド座標 (m) 基準なので Cube のスケールを変えても伸びない
    Properties
    {
        [Header(Top Surface)]
        _BaseColor    ("Base Color", Color) = (0.25, 0.25, 0.28, 1)
        _BaseColor2   ("Base Color 2 (タイルごとの色ムラ)", Color) = (0.2, 0.2, 0.23, 1)
        _Smoothness   ("Smoothness", Range(0, 1)) = 0.5
        _Metallic     ("Metallic", Range(0, 1)) = 0.1
        _TileSize     ("Tile Size (xz m)", Vector) = (1, 1, 0, 0)
        [Toggle] _BrickOffset("Brick Offset (互い違い)", Float) = 0
        _GroutWidth   ("Grout Width (目地の太さ)", Range(0.001, 0.2)) = 0.03
        _GroutColor   ("Grout Color", Color) = (0.05, 0.05, 0.06, 1)
        _Wear         ("Wear (汚れ・かすれ)", Range(0, 1)) = 0.3

        [Header(Side)]
        _SideColor    ("Side Color", Color) = (0.1, 0.1, 0.12, 1)
        _SideSeam     ("Side Seam Interval (m)", Range(0.1, 10)) = 2
        [HDR] _StripeColor("Stripe Color (警告ストライプ)", Color) = (1, 0.7, 0.05, 1)
        _StripeAmount ("Stripe Amount (0 = なし)", Range(0, 1)) = 0
        _StripeBand   ("Stripe Band Height (上端からの幅 m)", Range(0.02, 2)) = 0.25
        _StripePeriod ("Stripe Period (m)", Range(0.05, 2)) = 0.4

        [Header(Edge Light)]
        [HDR] _EdgeColor("Edge Color (上面外周の光)", Color) = (0.3, 1.5, 3, 1)
        _EdgeWidth    ("Edge Width (m)", Range(0.005, 0.3)) = 0.04
        _EdgeInset    ("Edge Inset (端からの距離 m)", Range(0, 0.5)) = 0.06
        _EdgeFlow     ("Edge Flow (流れる光)", Range(0, 1)) = 0.5
        _EdgeFlowSpeed("Edge Flow Speed (m/s)", Range(-20, 20)) = 3
        _BevelHighlight("Bevel Highlight (角の明るさ)", Range(0, 1)) = 0.4

        [Header(Grout Glow)]
        [HDR] _LineColor("Line Color (目地の光)", Color) = (0, 0, 0, 1)

        [Header(Wet)]
        _Wetness      ("Wetness (濡れ・水たまり)", Range(0, 1)) = 0

        [Header(Visibility)]
        [HDR] _RimColor ("Rim Color", Color) = (0.4, 0.6, 1, 1)
        _RimPower     ("Rim Power", Range(0.5, 10)) = 4
        _RimIntensity ("Rim Intensity", Range(0, 3)) = 0.5
        _SelfIllum    ("Self Illumination (暗くても沈まない)", Range(0, 1)) = 0.15
        _EmissionIntensity("Emission Intensity", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
            "UniversalMaterialType"="Lit"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4  _BaseColor;
            half4  _BaseColor2;
            half   _Smoothness;
            half   _Metallic;
            float4 _TileSize;
            float  _BrickOffset;
            half   _GroutWidth;
            half4  _GroutColor;
            half   _Wear;

            half4  _SideColor;
            float  _SideSeam;
            half4  _StripeColor;
            half   _StripeAmount;
            float  _StripeBand;
            float  _StripePeriod;

            half4  _EdgeColor;
            float  _EdgeWidth;
            float  _EdgeInset;
            half   _EdgeFlow;
            float  _EdgeFlowSpeed;
            half   _BevelHighlight;

            half4  _LineColor;
            half   _Wetness;

            half4  _RimColor;
            half   _RimPower;
            half   _RimIntensity;
            half   _SelfIllum;
            half   _EmissionIntensity;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS   : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS   = input.normalOS;
                output.fogFactor  = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * Noise(p);
                    p = p * 2.07 + 13.1;
                    a *= 0.5;
                }
                return v;
            }

            // 1 次元の線 (アンチエイリアス付き). d = 線からの距離, w = 太さ
            float LineAA(float d, float w)
            {
                float aa = max(fwidth(d), 1e-5);
                return 1.0 - smoothstep(w * 0.5 - aa, w * 0.5 + aa, d);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 pw = input.positionWS;
                float3 po = input.positionOS;
                float3 no = normalize(input.normalOS);

                // オブジェクトのスケール (m) → 端までの距離を m で測る
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20),
                                      length(UNITY_MATRIX_M._m01_m11_m21),
                                      length(UNITY_MATRIX_M._m02_m12_m22));
                float dx = (0.5 - abs(po.x)) * scale.x;
                float dy = (0.5 - po.y) * scale.y;   // 上端までの距離
                float dz = (0.5 - abs(po.z)) * scale.z;

                bool isTop = no.y > 0.5;
                bool isBottom = no.y < -0.5;
                // 上面の外周までの距離
                float topEdgeDist = isTop ? min(dx, dz) : dy;

                half3 albedo;
                half  smooth = _Smoothness;
                half3 emission = 0;

                if (isTop || isBottom)
                {
                    // ---- 上面: タイル ----
                    float2 uv = pw.xz / max(_TileSize.xy, 1e-3);
                    float row = floor(uv.y);
                    uv.x += _BrickOffset * 0.5 * fmod(abs(row), 2.0);
                    float2 id = floor(uv);
                    float2 g = abs(frac(uv) - 0.5);           // タイル中心から
                    float2 groutD = (0.5 - g) * _TileSize.xy; // 目地までの距離 (m)
                    float grout = max(LineAA(groutD.x, _GroutWidth * 2.0), LineAA(groutD.y, _GroutWidth * 2.0));

                    float h = Hash21(id + 3.7);
                    half3 tile = lerp(_BaseColor.rgb, _BaseColor2.rgb, h);
                    float wear = Fbm(pw.xz * 1.3) * Fbm(pw.xz * 5.1 + 9.0);
                    tile *= 1.0 - _Wear * smoothstep(0.1, 0.35, wear) * 0.5;

                    albedo = lerp(tile, _GroutColor.rgb, grout);
                    smooth *= 1.0 - grout * 0.5;
                    emission += _LineColor.rgb * grout;

                    // 濡れ・水たまり
                    float puddle = smoothstep(0.5, 0.58, Fbm(pw.xz * 0.35 + 4.0)) * _Wetness;
                    float wet = saturate(_Wetness * 0.6 + puddle);
                    albedo *= lerp(1.0, 0.55, wet);
                    smooth = lerp(smooth, 0.95, wet);
                }
                else
                {
                    // ---- 側面: 帯 + 継ぎ目 + 警告ストライプ ----
                    float along = abs(no.x) > 0.5 ? pw.z : pw.x;
                    float seam = LineAA(abs(frac(along / _SideSeam + 0.5) - 0.5) * _SideSeam, 0.02);
                    albedo = _SideColor.rgb * (1.0 - seam * 0.6);
                    albedo *= 0.85 + 0.3 * Fbm(float2(along, pw.y) * 2.0) * (1.0 - _Wear * 0.5);

                    float stripe = step(0.5, frac((along + pw.y) / _StripePeriod));
                    float band = 1.0 - smoothstep(_StripeBand - 0.01, _StripeBand, dy);
                    float st = stripe * band * _StripeAmount;
                    albedo = lerp(albedo, saturate(_StripeColor.rgb), st);
                    emission += _StripeColor.rgb * st * 0.15;
                }

                // ---- 角の面取りハイライト ----
                // 各軸の面までの距離のうち、2 番目に小さいもの = 最寄りの辺までの距離
                float3 d3 = float3(dx, (0.5 - abs(po.y)) * scale.y, dz);
                float edgeAll = d3.x + d3.y + d3.z - min(d3.x, min(d3.y, d3.z)) - max(d3.x, max(d3.y, d3.z));
                float bevel = 1.0 - smoothstep(0.0, 0.025, edgeAll);
                albedo = lerp(albedo, albedo * 1.6 + 0.08, bevel * _BevelHighlight);

                // ---- 上面外周の発光ライン ----
                float edgeLine = LineAA(abs(topEdgeDist - _EdgeInset - _EdgeWidth * 0.5), _EdgeWidth);
                float flowPos = (abs(normalWS.x) > 0.5 ? pw.z : pw.x) - _Time.y * _EdgeFlowSpeed;
                float flow = pow(frac(flowPos / 6.0), 8.0);
                half3 edgeCol = _EdgeColor.rgb * (1.0 + flow * _EdgeFlow * 3.0);
                emission += edgeCol * edgeLine;
                // ラインの周りのにじみ
                emission += _EdgeColor.rgb * 0.15 * exp(-abs(topEdgeDist - _EdgeInset) / 0.08) * (isTop ? 1.0 : 0.5);

                // ---- リム & 自己発光 ----
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                emission += _RimColor.rgb * rim * _RimIntensity;
                emission += albedo * _SelfIllum;

                // ---- ライティング ----
                SurfaceData surface = (SurfaceData)0;
                surface.albedo     = albedo;
                surface.metallic   = _Metallic;
                surface.smoothness = smooth;
                surface.occlusion  = 1.0;
                surface.alpha      = 1.0;
                surface.normalTS   = half3(0, 0, 1);
                surface.emission   = emission * _EmissionIntensity;

                InputData inputData = (InputData)0;
                inputData.positionWS = pw;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(pw);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    Fallback Off
}
