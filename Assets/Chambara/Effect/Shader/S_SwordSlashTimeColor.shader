Shader "Custom/Trail/SwordSlashTimeColor"
{
    // SwordTrailMesh 用（根元〜刃先は同じ色。時間経過＝尾へ流れるにつれて色とアルファが変わる）
    // UV.x: 0 = 剣側(出たばかり) → 1 = 尾(寿命の終わり) / UV.y: 0 = 根元 → 1 = 刃先
    // 頂点カラーの a に振りの速さ（0〜1）が入る。遅いところほど薄くなる
    // 色キー 4 つで Gradient の代わりにする。各キーの a がその時点のアルファ
    Properties
    {
        [Header(Color Over Lifetime  a is Alpha)]
        [HDR] _Color0 ("Color 0 (出た瞬間)", Color) = (8, 8, 8, 1)
        [HDR] _Color1 ("Color 1", Color) = (4, 1.64, 0.21, 1)
        [HDR] _Color2 ("Color 2", Color) = (3, 0, 0.04, 0.7)
        [HDR] _Color3 ("Color 3 (消える瞬間)", Color) = (3, 0, 0.04, 0)
        _Time1 ("Color 1 Time", Range(0, 1)) = 0.2
        _Time2 ("Color 2 Time", Range(0, 1)) = 0.55

        [Header(Shape)]
        _TailShrink  ("Tail Shrink (尾ほど刃先側へ細る)", Range(0, 1)) = 0.7
        _InnerSoftness("Inner Softness (内側の縁のぼかし)", Range(0.001, 0.5)) = 0.04

        [Header(Flame Edge)]
        _JagAmount   ("Jag Amount (ギザギザの深さ)", Range(0, 0.6)) = 0.25
        _JagScale    ("Jag Scale (ギザギザの細かさ)", Float) = 14
        _JagSpeed    ("Jag Speed (揺らぎの速さ)", Float) = 6

        [Header(Streaks)]
        _StreakScale ("Streak Scale (x:途切れの細かさ[ワールド] y:筋の本数 z:2層目の本数)", Vector) = (3, 10, 28, 0)
        _StreakSpeed ("Streak Speed (ゆらぎの速さ)", Float) = 2.0
        _StreakAmount("Streak Amount (0 で根元〜刃先が完全に均一)", Range(0, 1)) = 0.0

        [Header(Ragged End)]
        _ZigDepth    ("Zig Depth (ギザギザの深さ。寿命に対する割合)", Range(0, 1)) = 0.35
        _ZigCount    ("Zig Count (円周/幅あたりのトゲの数。整数)", Range(1, 32)) = 9
        _ZigIrregular("Zig Irregular (0 = 整ったノコギリ → 1 = バラバラ)", Range(0, 1)) = 0.85
        _ZigSoftness ("Zig Softness (切り口のぼかし)", Range(0.001, 0.2)) = 0.01
        _ZigSpeed    ("Zig Speed (形が切り替わる回数/秒。0 で固定)", Float) = 0

        [Header(Fade)]
        _HeadFade    ("Head Fade (剣側のフェード)", Range(0, 0.3)) = 0.015
        _MinSpeedAlpha("Min Speed Alpha (遅いところの濃さ)", Range(0, 1)) = 0.15

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
            Name "SwordSlashTimeColor"
            Tags { "LightMode"="UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color0;
                half4  _Color1;
                half4  _Color2;
                half4  _Color3;
                half   _Time1;
                half   _Time2;
                half   _TailShrink;
                half   _InnerSoftness;
                half   _JagAmount;
                float  _JagScale;
                float  _JagSpeed;
                float4 _StreakScale;
                float  _StreakSpeed;
                half   _StreakAmount;
                half   _ZigDepth;
                float  _ZigCount;
                half   _ZigIrregular;
                half   _ZigSoftness;
                float  _ZigSpeed;
                half   _HeadFade;
                half   _MinSpeedAlpha;
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
                float z = floor(p.z);
                float f = frac(p.z);
                f = f * f * (3.0 - 2.0 * f);
                float a = ValueNoise(p.xy + z * float2(37.0, 17.0));
                float b = ValueNoise(p.xy + (z + 1.0) * float2(37.0, 17.0));
                return lerp(a, b, f);
            }

            float Lines(float v, float count, float seed)
            {
                return ValueNoise(float2(v * count, seed)) * 0.65
                     + ValueNoise(float2(v * count * 2.3, seed + 17.0)) * 0.35;
            }

            // 4 キーのグラデーション（rgb: 色 / a: アルファ）
            half4 EvalGradient(float t)
            {
                float t1 = saturate(_Time1);
                float t2 = max(saturate(_Time2), t1);
                if (t < t1) return lerp(_Color0, _Color1, saturate(t / max(t1, 1e-4)));
                if (t < t2) return lerp(_Color1, _Color2, saturate((t - t1) / max(t2 - t1, 1e-4)));
                return lerp(_Color2, _Color3, saturate((t - t2) / max(1.0 - t2, 1e-4)));
            }

            // 不揃いなジグザグ（0〜1）。トゲごとに谷の高さ・山の高さ・山の位置をランダムにする
            // トゲの番号を _ZigCount で回すので、v = 0 と 1 でつながる（Tube の継ぎ目が出ない）
            float RaggedZigZag(float v)
            {
                float count = max(round(_ZigCount), 1.0);
                float x = v * count;
                float i = floor(x);
                float f = x - i;
                float seed = floor(_Time.y * _ZigSpeed) * 17.0;

                float i0 = fmod(i, count);
                float i1 = fmod(i + 1.0, count);
                float irr = _ZigIrregular;
                float valley0 = Hash(float2(i0, seed + 1.3)) * 0.45 * irr;
                float valley1 = Hash(float2(i1, seed + 1.3)) * 0.45 * irr;
                float peakH   = lerp(1.0, lerp(0.35, 1.0, Hash(float2(i0, seed + 5.9))), irr);
                float peakX   = lerp(0.5, lerp(0.12, 0.88, Hash(float2(i0, seed + 9.2))), irr);

                return f < peakX
                    ? lerp(valley0, peakH, f / peakX)
                    : lerp(peakH, valley1, (f - peakX) / (1.0 - peakX));
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
                float t = saturate(IN.uv.x);   // 0 = 出たばかり → 1 = 寿命の終わり
                float v = saturate(IN.uv.y);   // 0 = 根元 → 1 = 刃先

                // ---- 内側の縁: 尾ほど刃先側へ寄り、炎のようにギザギザ揺れる ----
                float jag = ValueNoise(float2(t * _JagScale - _Time.y * _JagSpeed, 3.7)) * 0.7
                          + ValueNoise(float2(t * _JagScale * 2.7 - _Time.y * _JagSpeed * 1.6, 9.1)) * 0.3;
                float inner = _TailShrink * t + _JagAmount * jag * lerp(0.3, 1.0, t);
                inner = min(inner, 0.98);
                float mask = smoothstep(inner, inner + _InnerSoftness, v);

                // ---- 色とアルファ: 時間(t)だけで決まる。根元〜刃先は同じ ----
                half4 grad = EvalGradient(t);

                // ---- 筋（任意）: アルファだけを揺らす。色は変えない ----
                float breakup1 = ValueNoise3D(IN.positionWS * _StreakScale.x + _Time.y * _StreakSpeed);
                float breakup2 = ValueNoise3D(IN.positionWS * _StreakScale.x * 2.1 - _Time.y * _StreakSpeed * 1.3 + 11.0);
                float streak = Lines(v, _StreakScale.y, 0.5) * lerp(0.4, 1.0, breakup1) * 0.6
                             + Lines(v, _StreakScale.z, 41.5) * lerp(0.3, 1.0, breakup2) * 0.4;
                float streakMask = lerp(1.0, saturate(streak * 1.4), _StreakAmount);

                // ---- 濃さ ----
                float headFade = smoothstep(0.0, max(_HeadFade, 1e-4), t);
                float speed = lerp(_MinSpeedAlpha, 1.0, IN.color.a);

                // ---- 終わり際: 尾の端をギザギザに切り取る ----
                float cutoff = 1.0 - _ZigDepth * RaggedZigZag(v);
                float ragged = 1.0 - smoothstep(cutoff - _ZigSoftness, cutoff, t);

                float alpha = saturate(grad.a) * streakMask * mask * headFade * speed * ragged;
                half3 rgb = grad.rgb * IN.color.rgb * _Intensity;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
