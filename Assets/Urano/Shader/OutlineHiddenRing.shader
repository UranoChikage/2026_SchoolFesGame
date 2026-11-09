// OutlineHidden.shader の Outline 側。Mark(ステンシル2)の外側にある、隠れた部分にだけ輪郭を描く
Shader "Urano/OutlineHiddenRing"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Width ("Width (pixels)", Float) = 3
        _HideMargin ("Hide margin (m)", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+100" }
        Pass
        {
            Name "Ring"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest Greater
            Stencil { Ref 2 Comp NotEqual }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
                float _HideMargin;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 殻をカメラ側へ _HideMargin(m) 寄せてから深度比較させる。
                // 海面すれすれ・自身の厚み程度の「ほぼ同じ深さ」では隠れた扱いにならず、手前に _HideMargin 以上の物があるときだけ出る
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                posWS += normalize(_WorldSpaceCameraPos - posWS) * _HideMargin;
                float4 clip = TransformWorldToHClip(posWS);
                float3 nWS = TransformObjectToWorldNormal(IN.normalOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                nCS = normalize(nCS + 1e-6);
                clip.xy += nCS * _Width * 2.0 / _ScreenParams.xy * clip.w;
                OUT.positionCS = clip;
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
