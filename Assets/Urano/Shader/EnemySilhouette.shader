// EnemySilhouette.shader  (URP 用)
// 敵船が手前の物(自船など)の陰に隠れたとき、その部分だけ単色のシルエットで見せる。
// URP の Render Objects(Renderer Feature)を2つ、同じマテリアルで使う:
//   1つ目: Pass Index 0 (StencilMark)  敵の見えている部分にステンシルを書く
//   2つ目: Pass Index 1 (Silhouette)   ステンシルが無い(=隠れている)部分だけ単色で描く
// 敵自身の重なり(マストが帆の裏など)でシルエットが出ないよう、ステンシルで除外している
Shader "Custom/EnemySilhouette"
{
    Properties
    {
        _Color ("シルエットの色 (Aで濃さ)", Color) = (1, 0.25, 0.2, 0.55)

        // 帆(SailSway)と同じ名前・同じ計算の揺れ。動く帆とステンシル/シルエットがずれないようにする。
        // 旗ごとの値は SailSwayFitter(useMaterialValues)がレンダラーごとに設定する。
        // 揺れ幅・ふくらみが 0 のままなら、帆以外(船体など)は動かない
        [Header(Sway)]
        _Center    ("揺れの中心 (オブジェクト空間)", Vector) = (0, 0, 0, 0)
        _Radius    ("固定になる距離", Float) = 1
        _SwayDir   ("揺れる向き (オブジェクト空間)", Vector) = (0, 0, 1, 0)
        _Billow    ("ふくらみ (常時、中心の移動量)", Float) = 0
        _SwayAmp   ("揺れ幅", Float) = 0
        _SwaySpeed ("揺れの速さ", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _Center;
            float  _Radius;
            float4 _SwayDir;
            float  _Billow;
            float  _SwayAmp;
            float  _SwaySpeed;
        CBUFFER_END

        // SailSway.shader の SailOffset と同じ計算にすること
        float3 SailOffset(float3 p)
        {
            float d = length(p - _Center.xyz) / max(_Radius, 0.0001);
            float w = 1.0 - smoothstep(0.0, 1.0, d);
            float move = _Billow + _SwayAmp * sin(_Time.y * _SwaySpeed);
            return normalize(_SwayDir.xyz + 1e-5) * move * w;
        }

        float4 vert(float4 positionOS : POSITION) : SV_POSITION
        {
            return TransformObjectToHClip(positionOS.xyz + SailOffset(positionOS.xyz));
        }
        ENDHLSL

        // 0: 敵の見えている部分にステンシル1を書く(色は書かない)
        Pass
        {
            Name "StencilMark"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Offset -1, -1   // 通常描画と深度がわずかにずれても、ステンシルが抜けないように

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // 1: 手前に何かがあって隠れている部分(深度が奥 かつ ステンシルが無い)だけ単色で描く
        Pass
        {
            Name "Silhouette"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 1
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag() : SV_Target
            {
                return half4(_Color);
            }
            ENDHLSL
        }
    }
}
