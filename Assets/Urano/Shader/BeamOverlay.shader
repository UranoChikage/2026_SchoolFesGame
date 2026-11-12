// ビームの上に重ねる用のシェーダ。
// 黒い背景に白い模様のテクスチャを、明るさをそのまま透明度にして黒抜きし、
// ToonParticleRiver のように UV をスクロールさせて流す。
//
// ■ 使い方
//   ・ビーム本体の上に、同じ形のメッシュ／LineRenderer（もう1本）を重ね、このマテリアルを付ける。
//   ・Base Map に黒地に白い模様のテクスチャ（Wrap Mode は Repeat）を入れる。
//   ・LineRenderer は Texture Mode を Tile にすると、長さに合わせて模様が繰り返される。
//   ・スクロール方向は Scroll の (x, y)。長さ方向が U か V かはメッシュ次第なので、Scroll で合わせる。
//
// ■ 加算合成なので、黒いところは何も足されない（黒抜き）。重ねるほど明るくなる。
Shader "Custom/BeamOverlay"
{
    Properties
    {
        _BaseMap   ("模様テクスチャ（黒地に白）", 2D) = "white" {}
        [HDR] _ColorBright ("明るい部分の色", Color) = (1.0, 0.95, 0.75, 1)
        [HDR] _ColorBase   ("暗い部分の色",   Color) = (1.0, 0.45, 0.1, 1)
        _Scroll    ("スクロール速度 (x, y)", Vector) = (-1.5, 0, 0, 0)
        _Cutoff    ("黒抜きの境目 (大きいほど模様が細くなる)", Range(0, 1)) = 0.15
        _Softness  ("境目のぼかし (0でくっきり)", Range(0.001, 1)) = 0.2
        _Intensity ("全体の強さ", Range(0, 4)) = 1
        _Layer2    ("2枚目の重ね具合 (0で1枚だけ)", Range(0, 1)) = 0.6
        _Layer2Scale ("2枚目の大きさ (倍率)", Float) = 1.37
        _Layer2Speed ("2枚目の速さ (倍率。負で逆向き)", Float) = -0.7
        _ToonSteps ("色の段数 (0で滑らか、2〜4でくっきり)", Range(0, 4)) = 0
        _EdgeFade  ("幅方向の端のフェード (0で無し)", Range(0, 0.5)) = 0
        [Enum(U, 0, V, 1)] _WidthAxis ("幅方向のUV軸", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"   // 海(3000)・ビーム本体より後に描く
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One      // 加算：黒は足されないので黒抜きになる
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _ColorBright;
                float4 _ColorBase;
                float4 _Scroll;
                float  _Cutoff;
                float  _Softness;
                float  _Intensity;
                float  _Layer2;
                float  _Layer2Scale;
                float  _Layer2Speed;
                float  _ToonSteps;
                float  _EdgeFade;
                float  _WidthAxis;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;   // LineRenderer のグラデーション／パーティクルの色
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            // 模様の明るさ(0〜1)。黒抜きの境目を通した値
            float Pattern(float2 uv)
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap));
                float lum = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                return smoothstep(_Cutoff, _Cutoff + _Softness, lum);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // 1枚目：Scroll の向きに流す
                float p1 = Pattern(IN.uv + _Scroll.xy * t);

                // 2枚目：大きさと速さを変えて重ね、模様の繰り返しを目立たなくする
                float2 uv2 = IN.uv * _Layer2Scale + _Scroll.xy * (t * _Layer2Speed);
                float p2 = Pattern(uv2) * _Layer2;

                float p = saturate(max(p1, p2));

                // トゥーン：明るさを段階に丸める
                if (_ToonSteps > 0.5) p = floor(p * _ToonSteps + 0.5) / _ToonSteps;

                // 幅方向の端を薄くする（ビームの縁に模様がはみ出ないように）
                float across = _WidthAxis > 0.5 ? IN.uv.y : IN.uv.x;
                float edge = _EdgeFade > 0.0001 ? smoothstep(0.0, _EdgeFade, across) * smoothstep(0.0, _EdgeFade, 1.0 - across) : 1.0;

                // 明るいほど「明るい色」、暗いほど「暗い色」
                float3 col = lerp(_ColorBase.rgb, _ColorBright.rgb, p) * p;
                col *= IN.color.rgb * IN.color.a * edge * _Intensity;

                // 加算合成なのでフォグは掛けない（掛けるとフォグの色が足されてしまう）
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
