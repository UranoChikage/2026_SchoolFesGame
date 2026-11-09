Shader "Custom/Trail/SwordSlash"
{
    // SwordTrailMesh 用（刃が通った面を三日月状に光らせる）
    // UV.x: 0 = 剣側(最新) → 1 = 尾(古い) / UV.y: 0 = 根元 → 1 = 刃先
    // 頂点カラーの a に振りの速さ（0〜1）が入る。遅いところほど薄くなる
    // 剣ごとに見た目を変えるときは、このマテリアルを複製して色やテクスチャを変える
    Properties
    {
        [HDR] _EdgeColor ("Edge Color (刃先の線)", Color) = (6, 6, 6, 1)
        [HDR] _BodyColor ("Body Color (面・剣側)", Color) = (1.5, 2.0, 4.0, 1)
        [HDR] _TailColor ("Tail Color (面・尾)", Color) = (0.2, 0.4, 2.0, 1)
        _ColorBias   ("Color Bias (尾色への寄り方)", Range(0.1, 4)) = 1.0

        _EdgeWidth   ("Edge Width (刃先の線の太さ)", Range(0.01, 0.5)) = 0.08
        _EdgeSharpness("Edge Sharpness (線のくっきり具合)", Range(0.5, 8)) = 3.0
        _EdgeLength  ("Edge Length (線が届く長さ)", Range(0.05, 1)) = 0.7

        _BodyAlpha   ("Body Alpha (面の濃さ)", Range(0, 1)) = 0.5
        _RootFalloff ("Root Falloff (根元へ向かう消え方)", Range(0.1, 8)) = 2.0
        _TailShrink  ("Tail Shrink (尾ほど刃先側へ細る)", Range(0, 1)) = 0.6

        _HeadFade    ("Head Fade (剣側のフェード)", Range(0, 0.3)) = 0.02
        _TailFadeStart("Tail Fade Start", Range(0, 1)) = 0.4

        _MinSpeedAlpha("Min Speed Alpha (遅いところの濃さ)", Range(0, 1)) = 0.2

        _StreakScale ("Streak Scale (x:途切れの細かさ[ワールド] y:筋の本数)", Vector) = (4, 16, 0, 0)
        _StreakSpeed ("Streak Speed (ゆらぎの速さ)", Float) = 1.0
        _StreakAmount("Streak Amount", Range(0, 1)) = 0.5

        _MainTex     ("Pattern (任意。UV は上と同じ)", 2D) = "white" {}

        _Intensity   ("Intensity", Range(0, 10)) = 1.5

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5   // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1   // One = 加算 / OneMinusSrcAlpha(10) = 半透明
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "SwordSlash"
            Tags { "LightMode"="UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _EdgeColor;
                half4  _BodyColor;
                half4  _TailColor;
                half   _ColorBias;
                half   _EdgeWidth;
                half   _EdgeSharpness;
                half   _EdgeLength;
                half   _BodyAlpha;
                half   _RootFalloff;
                half   _TailShrink;
                half   _HeadFade;
                half   _TailFadeStart;
                half   _MinSpeedAlpha;
                float4 _StreakScale;
                float  _StreakSpeed;
                half   _StreakAmount;
                float4 _MainTex_ST;
                half   _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;    // rgb: 色 / a: 振りの速さ
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float ValueNoise3D(float3 p)
            {
                // z を 2D ノイズのずらし量にして重ねる簡易 3D ノイズ
                float z = floor(p.z);
                float f = frac(p.z);
                f = f * f * (3.0 - 2.0 * f);
                float a = ValueNoise(p.xy + z * float2(37.0, 17.0));
                float b = ValueNoise(p.xy + (z + 1.0) * float2(37.0, 17.0));
                return lerp(a, b, f);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv    = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = saturate(IN.uv.x);   // 0 = 剣側 → 1 = 尾
                float v = saturate(IN.uv.y);   // 0 = 根元 → 1 = 刃先

                // ---- 形状: 尾ほど根元側が欠けて、刃先側だけ残る（三日月） ----
                float cut = _TailShrink * t;
                float vv = saturate((v - cut) / max(1.0 - cut, 1e-3));   // 残っている範囲で 0〜1

                // ---- 面: 刃先ほど濃く、根元へ消える ----
                float body = pow(vv, _RootFalloff) * _BodyAlpha;

                // ---- 刃先の線: 刃先の弧に沿った明るい線（尾に行くほど細く短く） ----
                float edgeW = _EdgeWidth * lerp(1.0, 0.4, t);
                float edge = pow(saturate(1.0 - (1.0 - v) / max(edgeW, 1e-3)), _EdgeSharpness);
                edge *= 1.0 - smoothstep(0.0, _EdgeLength, t);

                // ---- 筋: 弧に沿った筋。途切れはワールド座標で決めて、点が増減しても模様が動かないようにする ----
                float lines = ValueNoise(float2(v * _StreakScale.y, 0.5)) * 0.65
                            + ValueNoise(float2(v * _StreakScale.y * 2.3, 17.5)) * 0.35;
                float breakup = ValueNoise3D(IN.positionWS * _StreakScale.x + _Time.y * _StreakSpeed);
                float streak = lines * lerp(0.4, 1.0, breakup);
                // 剣側ほど筋を弱く、尾ほど途切れ途切れに
                body *= lerp(1.0, streak, _StreakAmount * saturate(t * 1.5 + 0.2));

                // ---- 色 ----
                half3 col = lerp(_BodyColor.rgb, _TailColor.rgb, pow(t, _ColorBias));
                col = lerp(col, _EdgeColor.rgb, edge);

                // ---- フェード ----
                float fade = smoothstep(0.0, max(_HeadFade, 1e-4), t)
                           * (1.0 - smoothstep(_TailFadeStart, 1.0, t));
                float speed = lerp(_MinSpeedAlpha, 1.0, IN.color.a);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(IN.uv, _MainTex));

                float alpha = saturate(max(body, edge)) * fade * speed * tex.a;
                half3 rgb = col * IN.color.rgb * tex.rgb * _Intensity;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
