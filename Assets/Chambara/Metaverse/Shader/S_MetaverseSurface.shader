Shader "Custom/Metaverse/MetaverseSurface"
{
    // SkyMetaverse と同じ雰囲気の 3D オブジェクト用シェーダー (URP Lit ベース)
    // 背景に溶け込まないように
    //   ・ライティング / 影 / 空の映り込みをちゃんと受けて「実体」に見せる
    //   ・ベースを背景より明るく (or 暗く) して明度差をつける
    //   ・輪郭にリムライトを入れてシルエットを立たせる
    // 模様: トライプラナーのパネル溝 (光るライン) + ランダムに光るパネル + 下から上へ走るスキャン
    Properties
    {
        [Header(Surface)]
        _BaseColor    ("Base Color", Color) = (0.78, 0.78, 0.86, 1)
        _Smoothness   ("Smoothness", Range(0, 1)) = 0.75
        _Metallic     ("Metallic", Range(0, 1)) = 0.1

        [Header(Panel Lines)]
        [Toggle] _ObjectSpace("Object Space (動くオブジェクトはON)", Float) = 0
        _PanelSize    ("Panel Size (パネルの大きさ m)", Range(0.05, 20)) = 1
        _LineWidth    ("Line Width (光る線の太さ)", Range(0.001, 0.2)) = 0.02
        _GrooveWidth  ("Groove Width (溝の太さ)", Range(0.001, 0.3)) = 0.06
        _GrooveDarken ("Groove Darken (溝の暗さ)", Range(0, 1)) = 0.8
        [HDR] _LineColor ("Line Color", Color) = (0.2, 1.6, 3.0, 1)
        _TriplanarSharpness("Triplanar Sharpness", Range(1, 32)) = 8

        [Header(Panel Glow)]
        [HDR] _PanelGlowColor("Panel Glow Color (ランダムに光るパネル)", Color) = (0.15, 0.5, 1.2, 1)
        _PanelGlowChance("Panel Glow Chance", Range(0, 1)) = 0.06
        _PanelGlowSpeed ("Panel Glow Speed", Range(0, 5)) = 0.7

        [Header(Rim)]
        [HDR] _RimColor ("Rim Color (輪郭の光)", Color) = (0.6, 1.4, 2.4, 1)
        _RimPower     ("Rim Power", Range(0.5, 10)) = 3.5
        _RimIntensity ("Rim Intensity", Range(0, 3)) = 1

        [Header(Scan)]
        [HDR] _ScanColor ("Scan Color (下から上へ走る光)", Color) = (1.5, 0.4, 2.2, 1)
        _ScanSpeed    ("Scan Speed (m/s)", Range(0, 20)) = 2
        _ScanInterval ("Scan Interval (m)", Range(1, 100)) = 12
        _ScanWidth    ("Scan Width", Range(0.01, 0.5)) = 0.08

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
            half   _Smoothness;
            half   _Metallic;

            float  _ObjectSpace;
            float  _PanelSize;
            half   _LineWidth;
            half   _GrooveWidth;
            half   _GrooveDarken;
            half4  _LineColor;
            half   _TriplanarSharpness;

            half4  _PanelGlowColor;
            half   _PanelGlowChance;
            half   _PanelGlowSpeed;

            half4  _RimColor;
            half   _RimPower;
            half   _RimIntensity;

            half4  _ScanColor;
            float  _ScanSpeed;
            float  _ScanInterval;
            half   _ScanWidth;

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

            // 1平面ぶんのパネル模様 (uv はパネル単位)
            // x: 光る線, y: 溝, z: パネルの点灯
            float3 PanelPlane(float2 uv, float seed)
            {
                float2 deriv = max(fwidth(uv), 1e-5);
                float2 g = abs(frac(uv + 0.5) - 0.5);   // 最寄りの溝までの距離

                float2 lw = max(_LineWidth * 0.5, deriv);
                float2 lineM = (1.0 - smoothstep(lw - deriv, lw + deriv, g)) * saturate(_LineWidth * 0.5 / lw);
                float2 gw = max(_GrooveWidth * 0.5, deriv);
                float2 grooveM = 1.0 - smoothstep(gw - deriv, gw + deriv, g);

                // 遠くて細かすぎる所は平均値に (チラつき防止)
                float far = saturate(max(deriv.x, deriv.y) * 2.0 - 0.5);
                float lineMask   = lerp(max(lineM.x, lineM.y), _LineWidth * 2.0, far);
                float grooveMask = lerp(max(grooveM.x, grooveM.y), _GrooveWidth * 2.0, far);

                float2 id = floor(uv) + seed;
                float h = Hash21(id);
                float phase = _Time.y * _PanelGlowSpeed + h * 10.0;
                float on = step(Hash21(id + floor(phase) * 7.31), _PanelGlowChance);
                float glow = on * sin(frac(phase) * PI) * (1.0 - grooveMask);

                return float3(lineMask, grooveMask, glow);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // ---- トライプラナーでパネル模様 ----
                float3 p = lerp(input.positionWS, input.positionOS, _ObjectSpace) / _PanelSize;
                float3 n = lerp(normalWS, normalize(input.normalOS), _ObjectSpace);
                float3 w = pow(abs(n), _TriplanarSharpness);
                w /= max(w.x + w.y + w.z, 1e-5);

                float3 pattern = PanelPlane(p.zy, 0.0) * w.x
                               + PanelPlane(p.xz, 31.7) * w.y
                               + PanelPlane(p.xy, 63.1) * w.z;
                float lineMask = pattern.x;
                float grooveMask = pattern.y;
                float panelGlow = pattern.z;

                // ---- 表面 ----
                SurfaceData surface = (SurfaceData)0;
                surface.albedo     = _BaseColor.rgb * (1.0 - grooveMask * _GrooveDarken);
                surface.metallic   = _Metallic;
                surface.smoothness = _Smoothness * (1.0 - grooveMask * 0.6);
                surface.occlusion  = 1.0;
                surface.alpha      = 1.0;
                surface.normalTS   = half3(0, 0, 1);

                // ---- 発光 ----
                half3 emission = _LineColor.rgb * lineMask;
                emission += _PanelGlowColor.rgb * panelGlow;

                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                emission += _RimColor.rgb * rim * _RimIntensity;

                float scan = frac((input.positionWS.y - _Time.y * _ScanSpeed) / _ScanInterval);
                scan = smoothstep(1.0 - _ScanWidth, 1.0, scan) * (1.0 - smoothstep(0.995, 1.0, scan));
                emission += _ScanColor.rgb * scan * (0.25 + lineMask * 3.0);

                surface.emission = emission * _EmissionIntensity;

                // ---- ライティング ----
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
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
