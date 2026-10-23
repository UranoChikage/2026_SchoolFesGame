// ToonParticle.shader  (URP 用)
// パーティクル(爆発・水しぶきなど)用のトゥーン調シェーダ。
// ・明暗を 3 色(明るい / 中間 / 暗い)にはっきり分ける。光は固定方向
// ・半透明にせず、ディザ(網点)で抜く。Color over Lifetime のアルファを下げると網点が粗くなって消える
// ・メッシュのパーティクルは本物の法線で、板のパーティクルは球の法線をUVから作って陰影を付ける
Shader "Custom/ToonParticle"
{
    Properties
    {
        _LightColor ("明るい色", Color) = (1.0, 0.95, 0.75, 1)
        _MidColor   ("中間の色", Color) = (1.0, 0.6, 0.2, 1)
        _DarkColor  ("暗い色", Color) = (0.55, 0.15, 0.1, 1)
        _LightDir   ("光が来る向き", Vector) = (0.4, 0.8, 0.3, 0)
        _Threshold1 ("明るい色の範囲 (大きいほど狭い)", Range(-1, 1)) = 0.55
        _Threshold2 ("暗い色の範囲 (大きいほど広い)", Range(-1, 1)) = 0.0
        _DitherScale ("網点の大きさ", Range(1, 8)) = 1
        _BaseMap    ("Texture (任意。板のパーティクル用)", 2D) = "white" {}
        [Toggle(_SPHERE_NORMAL)] _SphereNormal ("板のパーティクルを球のように陰影する", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        // 既定(LEqual / ZWrite On)なら従来どおり。メッシュの背後に回したいときは Always / Off にして Render Queue を小さくする
        // 名前は既存マテリアルに残っている古い _ZWrite と被らないようにしてある
        [Enum(UnityEngine.Rendering.CompareFunction)] _ToonZTest ("ZTest (背後に回すなら Always)", Float) = 4
        [Enum(Off, 0, On, 1)] _ToonZWrite ("ZWrite (背後に回すなら Off)", Float) = 1
        // Stencil: 既定(Ref 0 / Always / Keep)なら何もしない。
        // メッシュの後ろに回したい: 隠したい側(宝箱)は Ref=1, Pass=Replace。後光側は Ref=1, Comp=NotEqual。Queue は 宝箱 < 後光
        _ToonStencilRef ("Stencil Ref", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ToonStencilComp ("Stencil Comp (後光は NotEqual)", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _ToonStencilPass ("Stencil Pass (宝箱は Replace)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend Off
            Stencil
            {
                Ref [_ToonStencilRef]
                Comp [_ToonStencilComp]
                Pass [_ToonStencilPass]
            }
            ZWrite [_ToonZWrite]
            ZTest [_ToonZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _SPHERE_NORMAL
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _LightColor;
                float4 _MidColor;
                float4 _DarkColor;
                float4 _LightDir;
                float  _Threshold1;
                float  _Threshold2;
                float  _DitherScale;
            CBUFFER_END

            // ノイズ状のしきい値（0〜1）。Interleaved Gradient Noise。画面のピクセル位置で決まり、格子模様が出にくい
            float DitherThreshold(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(floor(pixel), float2(0.06711056, 0.00583715))));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(IN.uv, _BaseMap));

                // アルファ(テクスチャ × パーティクルの色)が低いほど、網点で多く抜く
                float alpha = tex.a * IN.color.a;
                clip(alpha - DitherThreshold(IN.positionCS.xy / _DitherScale));

            #if _SPHERE_NORMAL
                // 板のパーティクル: UV から球の表面の法線(カメラ基準)を作り、ワールドへ戻す
                float2 p = IN.uv * 2.0 - 1.0;
                float3 nVS = float3(p.x, p.y, sqrt(saturate(1.0 - dot(p, p))));
                float3 N = normalize(mul((float3x3)UNITY_MATRIX_I_V, nVS));
            #else
                float3 N = normalize(IN.normalWS);
            #endif

                float ndl = dot(N, normalize(_LightDir.xyz + 1e-5));

                // 3 色に分ける（境目はくっきり）
                float3 shade = ndl > _Threshold1 ? _LightColor.rgb
                             : ndl > _Threshold2 ? _MidColor.rgb
                             : _DarkColor.rgb;

                float3 col = shade * tex.rgb * IN.color.rgb;
                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
