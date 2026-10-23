Shader "Custom/NoShadowLit"
{
    // 影を受けない 3D オブジェクト用シェーダー (URP)
    //   ・ライトの色 / 明るさは反映するが、向きによる明暗 (陰影) は付けない = 全面均一
    //   ・他オブジェクトの落とす影・セルフシャドウ・SSAO も受けない
    //   ・Unlit を ON にするとライティングも無視してテクスチャ色そのままで表示
    //   ・自分が影を落とすかは MeshRenderer の Cast Shadows で切り替え (ShadowCaster パスあり)
    Properties
    {
        [Header(Surface)]
        _BaseMap      ("Base Map", 2D) = "white" {}
        _BaseColor    ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_UNLIT)] _Unlit("Unlit (ライティングも無視)", Float) = 0

        [Header(Lighting)]
        _LightScale   ("Light Scale (ライトの強さ)", Range(0, 2)) = 1
        _AmbientScale ("Ambient Scale (環境光の強さ)", Range(0, 2)) = 1

        [Header(Rim)]
        [HDR] _RimColor ("Rim Color", Color) = (0, 0, 0, 1)
        _RimPower     ("Rim Power", Range(0.5, 10)) = 4

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _LightScale;
            half   _AmbientScale;
            half4  _RimColor;
            half   _RimPower;
            half4  _EmissionColor;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
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

            // シャドウ系のキーワードはあえて入れない (= 影をサンプリングしない)
            #pragma shader_feature_local _UNLIT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            // 向き (N・L) も影も使わず、ライトの色と距離減衰だけ
            half3 Shade(Light light)
            {
                return light.color * light.distanceAttenuation * _LightScale;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

            #if defined(_UNLIT)
                half3 color = albedo.rgb + _EmissionColor.rgb;
            #else
                half3 normalWS  = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // 環境光: 法線を 0 にして方向成分を消し、全面同じ明るさにする
                half3 lighting = SampleSH(half3(0, 0, 0)) * _AmbientScale;

                // メインライト: shadowCoord を渡さないので影なし
                lighting += Shade(GetMainLight());

            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();

            #if USE_CLUSTER_LIGHT_LOOP
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    lighting += Shade(GetAdditionalLight(lightIndex, input.positionWS));
                }
            #endif

                // 2 引数版 GetAdditionalLight は影を計算しない
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    lighting += Shade(GetAdditionalLight(lightIndex, input.positionWS));
                LIGHT_LOOP_END
            #endif

                half rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);

                half3 color = albedo.rgb * lighting + _RimColor.rgb * rim + _EmissionColor.rgb;
            #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // 自分が影を落とす用 (不要なら MeshRenderer の Cast Shadows を Off)
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
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return 0; }
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
