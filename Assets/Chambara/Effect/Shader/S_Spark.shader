Shader "Custom/Particles/SwordSpark"
{
    Properties
    {
        [HDR] _CoreColor ("Core Color (白熱)", Color) = (4, 3.5, 2.5, 1)
        [HDR] _MidColor  ("Mid Color (黄〜橙)", Color) = (3, 1.2, 0.2, 1)
        [HDR] _EndColor  ("End Color (赤)", Color) = (1.2, 0.1, 0.0, 1)

        _CoreWidth   ("Core Width", Range(0.01, 1)) = 0.18
        _EdgeSoftness("Edge Softness", Range(0.01, 1)) = 0.6
        _TipSharpness("Tip Sharpness", Range(0.5, 6)) = 2.0
        _Intensity   ("Intensity", Range(0, 10)) = 2.5

        _FlickerSpeed("Flicker Speed", Range(0, 80)) = 40
        _FlickerAmount("Flicker Amount", Range(0, 1)) = 0.35
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
            Name "SwordSpark"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha One      // 加算合成
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _MidColor;
                half4 _EndColor;
                half  _CoreWidth;
                half  _EdgeSoftness;
                half  _TipSharpness;
                half  _Intensity;
                half  _FlickerSpeed;
                half  _FlickerAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;        // Particle System の Start Color / Color over Lifetime
                float4 custom     : TEXCOORD1;    // Custom Vertex Streams: x=Age%(0-1), y=StableRandom
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float2 custom     : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv     = IN.uv;
                OUT.color  = IN.color;
                OUT.custom = IN.custom.xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float age  = saturate(IN.custom.x);   // 0 → 1
                float rand = IN.custom.y;

                // UVを -1〜1 に変換。x: 長さ方向 / y: 幅方向
                float2 p = IN.uv * 2.0 - 1.0;

                // ---- 形状: 幅方向は細く、長さ方向の端は尖らせる ----
                float lenMask = pow(saturate(1.0 - abs(p.x)), _TipSharpness);
                float widthFalloff = abs(p.y) / max(lenMask, 1e-3);   // 端ほど細くなる
                float body = saturate(1.0 - widthFalloff);
                body = pow(body, 1.0 / max(_EdgeSoftness, 0.01));

                // 白熱した芯
                float core = saturate(1.0 - widthFalloff / max(_CoreWidth, 0.01));
                core = core * core;

                // ---- 色: 寿命に沿って 白 → 橙 → 赤 ----
                half3 col = lerp(_CoreColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.45, age));
                col = lerp(col, _EndColor.rgb, smoothstep(0.35, 1.0, age));

                // 芯は常に少し白く
                col = lerp(col, _CoreColor.rgb, core * (1.0 - age));

                // ---- ちらつき(個体ごとに位相をずらす) ----
                float flicker = 1.0 - _FlickerAmount *
                    (0.5 + 0.5 * sin(_Time.y * _FlickerSpeed + rand * 100.0));

                // ---- フェードアウト ----
                float fade = 1.0 - smoothstep(0.55, 1.0, age);

                float alpha = body * fade * IN.color.a;
                half3 rgb = col * IN.color.rgb * _Intensity * flicker;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}