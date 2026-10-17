Shader "Custom/Trail/SwordTrail"
{
    // TrailRenderer 用（Texture Mode = Stretch 前提）
    // UV.x: 0 = 剣側(最新) → 1 = 尾(古い) / UV.y: 幅方向
    Properties
    {
        [HDR] _HeadColor ("Head Color (剣側)", Color) = (3, 3, 3.5, 1)
        [HDR] _TailColor ("Tail Color (尾)", Color) = (0.3, 0.6, 2.0, 1)
        _ColorBias   ("Color Bias (尾色への寄り方)", Range(0.1, 4)) = 1.0

        [HDR] _CoreColor ("Core Color (芯)", Color) = (6, 6, 6, 1)
        _CorePosition("Core Position (芯の幅方向位置)", Range(0, 1)) = 0.5
        _CoreWidth   ("Core Width", Range(0.01, 1)) = 0.12
        _CoreLength  ("Core Length (芯が届く長さ)", Range(0.01, 1)) = 0.35

        _EdgeSoftness("Edge Softness (幅方向のぼかし)", Range(0.01, 1)) = 0.5
        _TailWidth   ("Tail Width (尾の細さ)", Range(0, 1)) = 0.3

        _HeadFade    ("Head Fade (剣側のフェード)", Range(0, 0.3)) = 0.03
        _TailFadeStart("Tail Fade Start", Range(0, 1)) = 0.3

        _NoiseScale  ("Streak Scale (x:途切れの細かさ[ワールド] y:筋の本数)", Vector) = (4, 12, 0, 0)
        _NoiseSpeed  ("Streak Speed (ゆらぎの速さ)", Float) = 1.0
        _NoiseAmount ("Streak Amount", Range(0, 1)) = 0.5

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
            Name "SwordTrail"
            Tags { "LightMode"="UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _HeadColor;
                half4  _TailColor;
                half   _ColorBias;
                half4  _CoreColor;
                half   _CorePosition;
                half   _CoreWidth;
                half   _CoreLength;
                half   _EdgeSoftness;
                half   _TailWidth;
                half   _HeadFade;
                half   _TailFadeStart;
                float4 _NoiseScale;
                float  _NoiseSpeed;
                half   _NoiseAmount;
                half   _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;    // TrailRenderer の Color グラデーション
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
                float v = IN.uv.y;

                // ---- 形状: 幅方向は中央が濃く、尾に行くほど細くなる ----
                float halfWidth = lerp(1.0, _TailWidth, t);
                float d = abs(v * 2.0 - 1.0) / max(halfWidth, 1e-3);
                float body = saturate(1.0 - d);
                body = pow(body, 1.0 / max(_EdgeSoftness, 0.01));

                // ---- 芯: 剣の軌道に沿った白い線（剣側だけ） ----
                float coreD = abs(v - _CorePosition) / max(_CoreWidth * halfWidth, 1e-3);
                float core = saturate(1.0 - coreD);
                core *= core;
                core *= 1.0 - smoothstep(0.0, _CoreLength, t);

                // ---- 筋 ----
                // UV.x は Trail の長さが毎フレーム変わるたびに伸び縮みするので使わない
                // 筋の本数は幅方向(v)だけ、途切れはワールド座標で決めて、点が増減しても模様が動かないようにする
                float lines = ValueNoise(float2(v * _NoiseScale.y, 0.5)) * 0.65
                            + ValueNoise(float2(v * _NoiseScale.y * 2.3, 17.5)) * 0.35;
                float breakup = ValueNoise3D(IN.positionWS * _NoiseScale.x + _Time.y * _NoiseSpeed);
                float streak = lines * lerp(0.4, 1.0, breakup);
                // 剣側ほど筋を弱く、尾ほど途切れ途切れに
                float streakMask = lerp(1.0, streak, _NoiseAmount * saturate(t * 1.5 + 0.2));

                // ---- 色 ----
                half3 col = lerp(_HeadColor.rgb, _TailColor.rgb, pow(t, _ColorBias));
                col = lerp(col, _CoreColor.rgb, core);

                // ---- フェード ----
                float fade = smoothstep(0.0, max(_HeadFade, 1e-4), t)
                           * (1.0 - smoothstep(_TailFadeStart, 1.0, t));

                float alpha = saturate(max(body * streakMask, core)) * fade * IN.color.a;
                half3 rgb = col * IN.color.rgb * _Intensity;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
