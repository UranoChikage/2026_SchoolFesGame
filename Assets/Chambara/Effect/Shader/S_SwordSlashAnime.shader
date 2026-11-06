Shader "Custom/Trail/SwordSlashAnime"
{
    // SwordTrailMesh 用（アニメ調の斬撃。刃先が白く、内側へ シアン → コバルト と変わり、内側の縁が炎のようにギザギザ揺れる）
    // UV.x: 0 = 剣側(最新) → 1 = 尾(古い) / UV.y: 0 = 根元 → 1 = 刃先
    // 頂点カラーの a に振りの速さ（0〜1）が入る。遅いところほど薄くなる
    // 加算で描くので、Bloom を強めにかけると光がにじむ
    Properties
    {
        [Header(Color)]
        [HDR] _CoreColor ("Core Color (刃先の白)", Color) = (8, 8, 8, 1)
        [HDR] _MidColor  ("Mid Color (シアン)", Color) = (0.2, 2.5, 4.0, 1)
        [HDR] _OuterColor("Outer Color (コバルト)", Color) = (0.0, 0.25, 3.0, 1)
        _CoreSize    ("Core Size (白の幅)", Range(0.01, 0.5)) = 0.12
        _MidPosition ("Mid Position (シアンの位置)", Range(0.05, 0.95)) = 0.4
        _TailShift   ("Tail Shift (尾ほど外側の色に寄る)", Range(0, 2)) = 0.8

        [Header(Shape)]
        _TailShrink  ("Tail Shrink (尾ほど刃先側へ細る)", Range(0, 1)) = 0.7
        _InnerSoftness("Inner Softness (内側の縁のぼかし)", Range(0.001, 0.5)) = 0.04
        _OuterAlpha  ("Outer Alpha (内側の縁付近の濃さ)", Range(0, 1)) = 0.55

        [Header(Flame Edge)]
        _JagAmount   ("Jag Amount (ギザギザの深さ)", Range(0, 0.6)) = 0.25
        _JagScale    ("Jag Scale (ギザギザの細かさ)", Float) = 14
        _JagSpeed    ("Jag Speed (揺らぎの速さ)", Float) = 6

        [Header(Streaks)]
        _StreakScale ("Streak Scale (x:途切れの細かさ[ワールド] y:筋の本数 z:2層目の本数)", Vector) = (3, 10, 28, 0)
        _StreakSpeed ("Streak Speed (ゆらぎの速さ)", Float) = 2.0
        _StreakAmount("Streak Amount", Range(0, 1)) = 0.6

        [Header(Fade)]
        _HeadFade    ("Head Fade (剣側のフェード)", Range(0, 0.3)) = 0.015
        _TailFadeStart("Tail Fade Start", Range(0, 1)) = 0.45
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
            Name "SwordSlashAnime"
            Tags { "LightMode"="UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _CoreColor;
                half4  _MidColor;
                half4  _OuterColor;
                half   _CoreSize;
                half   _MidPosition;
                half   _TailShift;
                half   _TailShrink;
                half   _InnerSoftness;
                half   _OuterAlpha;
                half   _JagAmount;
                float  _JagScale;
                float  _JagSpeed;
                float4 _StreakScale;
                float  _StreakSpeed;
                half   _StreakAmount;
                half   _HeadFade;
                half   _TailFadeStart;
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
                // z を 2D ノイズのずらし量にして重ねる簡易 3D ノイズ
                float z = floor(p.z);
                float f = frac(p.z);
                f = f * f * (3.0 - 2.0 * f);
                float a = ValueNoise(p.xy + z * float2(37.0, 17.0));
                float b = ValueNoise(p.xy + (z + 1.0) * float2(37.0, 17.0));
                return lerp(a, b, f);
            }

            // 弧に沿った筋（幅方向 v だけで決める）
            float Lines(float v, float count, float seed)
            {
                return ValueNoise(float2(v * count, seed)) * 0.65
                     + ValueNoise(float2(v * count * 2.3, seed + 17.0)) * 0.35;
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

                // ---- 内側の縁: 尾ほど刃先側へ寄り、炎のようにギザギザ揺れる ----
                // 尾へ流れる向きにノイズを動かして、舌のような形が後ろへ流れて見えるようにする
                float jag = ValueNoise(float2(t * _JagScale - _Time.y * _JagSpeed, 3.7)) * 0.7
                          + ValueNoise(float2(t * _JagScale * 2.7 - _Time.y * _JagSpeed * 1.6, 9.1)) * 0.3;
                float inner = _TailShrink * t + _JagAmount * jag * lerp(0.3, 1.0, t);
                inner = min(inner, 0.98);
                float mask = smoothstep(inner, inner + _InnerSoftness, v);

                // ---- 色: 刃先(白) → シアン → コバルト。尾ほど外側の色に寄る ----
                float d = saturate((1.0 - v) / max(1.0 - inner, 1e-3));   // 0 = 刃先 → 1 = 内側の縁
                float g = saturate(d + t * _TailShift);
                float core = 1.0 - smoothstep(0.0, _CoreSize, g);
                half3 col = g < _MidPosition
                    ? lerp(_CoreColor.rgb, _MidColor.rgb, smoothstep(0.0, _MidPosition, g))
                    : lerp(_MidColor.rgb, _OuterColor.rgb, smoothstep(_MidPosition, 1.0, g));
                col = lerp(col, _CoreColor.rgb, core);

                // ---- 筋: 2層重ねる。途切れはワールド座標で決めて、点が増減しても模様が動かないようにする ----
                float breakup1 = ValueNoise3D(IN.positionWS * _StreakScale.x + _Time.y * _StreakSpeed);
                float breakup2 = ValueNoise3D(IN.positionWS * _StreakScale.x * 2.1 - _Time.y * _StreakSpeed * 1.3 + 11.0);
                float streak = Lines(v, _StreakScale.y, 0.5) * lerp(0.4, 1.0, breakup1) * 0.6
                             + Lines(v, _StreakScale.z, 41.5) * lerp(0.3, 1.0, breakup2) * 0.4;
                // 白い芯には筋を入れず、外側ほど筋を強く
                float streakMask = lerp(1.0, saturate(streak * 1.4), _StreakAmount * saturate(g * 1.5));

                // ---- 濃さ ----
                float body = lerp(1.0, _OuterAlpha, g) * streakMask;
                float fade = smoothstep(0.0, max(_HeadFade, 1e-4), t)
                           * (1.0 - smoothstep(_TailFadeStart, 1.0, t));
                float speed = lerp(_MinSpeedAlpha, 1.0, IN.color.a);

                float alpha = saturate(max(body, core)) * mask * fade * speed;
                half3 rgb = col * IN.color.rgb * _Intensity;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
