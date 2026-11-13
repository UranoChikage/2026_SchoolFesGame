Shader "Custom/Effect/GuardBarrier"
{
    // ガード方向を示すバリア用（Quad または球の一部などの曲面メッシュ想定。UV 0-1 前提）
    // ・六角形の格子 + ふちが光る + 中心から外へ広がる波紋
    // ・UV の外周に向かってフェードするので、Quad に貼っても四角く見えない
    // ・_Hit を 0→1 にするとガード成立時のフラッシュ（スクリプトから SetFloat で制御）
    // ・_Alpha でバリア全体の出現/消滅をフェードできる
    // Bloom が有効なカメラで HDR カラーが光って見える
    Properties
    {
        [HDR] _BaseColor ("Base Color (面の色)", Color) = (0.2, 0.6, 2.0, 1)
        [HDR] _HexColor  ("Hex Color (六角形の線)", Color) = (0.6, 1.8, 6.0, 1)
        [HDR] _RimColor  ("Rim Color (外周/輪郭の光)", Color) = (1.0, 2.5, 8.0, 1)
        [HDR] _HitColor  ("Hit Color (ガード成立時のフラッシュ)", Color) = (8, 8, 8, 1)

        _BaseAlpha   ("Base Alpha (面の濃さ)", Range(0, 1)) = 0.15

        // 白っぽい背景でも見えるように、光の下に暗い色を敷く
        _DarkColor   ("Dark Color (下に敷く暗い色)", Color) = (0.02, 0.05, 0.2, 1)
        _DarkBase    ("Dark Base (面全体を暗くする量)", Range(0, 1)) = 0.35
        _DarkLine    ("Dark Line (線・縁を暗く縁取る量)", Range(0, 1)) = 0.6

        _HexScale    ("Hex Scale (六角形の細かさ)", Float) = 8
        _HexWidth    ("Hex Line Width (線の太さ)", Range(0.01, 0.5)) = 0.08
        _HexScroll   ("Hex Scroll Speed (格子の流れる速さ)", Vector) = (0, 0.1, 0, 0)

        _EdgeFade    ("Edge Fade (外周ぼかし幅)", Range(0.01, 0.5)) = 0.2
        _Roundness   ("Roundness (0:四角 1:円形)", Range(0, 1)) = 1
        _RimWidth    ("Rim Width (外周の光の太さ)", Range(0.0, 0.3)) = 0.06

        _FresnelPower("Fresnel Power (曲面の輪郭光)", Range(0.5, 8)) = 3
        _FresnelAmount("Fresnel Amount", Range(0, 2)) = 0.5

        _WaveSpeed   ("Wave Speed (波紋の速さ)", Float) = 1.2
        _WaveWidth   ("Wave Width (波紋の幅)", Range(0.01, 0.5)) = 0.12
        _WaveAmount  ("Wave Amount (波紋の強さ)", Range(0, 3)) = 1

        _FlickerSpeed ("Flicker Speed (ちらつき)", Float) = 20
        _FlickerAmount("Flicker Amount", Range(0, 0.5)) = 0.08

        _Hit         ("Hit (0-1: ガード成立フラッシュ)", Range(0, 1)) = 0
        _Alpha       ("Alpha (全体フェード)", Range(0, 1)) = 1
        _Intensity   ("Intensity", Range(0, 10)) = 1
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
            Name "GuardBarrier"
            Tags { "LightMode"="UniversalForward" }

            // 乗算済みアルファ: 結果 = 光(rgb) + 背景 × (1 - 暗さ(a))
            // a=0 なら加算と同じ、a を上げると背景を暗い色で覆う
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half4  _HexColor;
                half4  _RimColor;
                half4  _HitColor;
                half   _BaseAlpha;
                half4  _DarkColor;
                half   _DarkBase;
                half   _DarkLine;
                float  _HexScale;
                half   _HexWidth;
                float4 _HexScroll;
                half   _EdgeFade;
                half   _Roundness;
                half   _RimWidth;
                half   _FresnelPower;
                half   _FresnelAmount;
                float  _WaveSpeed;
                half   _WaveWidth;
                half   _WaveAmount;
                float  _FlickerSpeed;
                half   _FlickerAmount;
                half   _Hit;
                half   _Alpha;
                half   _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv         = IN.uv;
                return OUT;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // 六角形格子: 戻り値 x=セル境界までの距離(0:中心付近 0.5:境界) yz=セルID
            float3 HexGrid(float2 p)
            {
                const float2 s = float2(1.0, 1.7320508);
                float4 hc = floor(float4(p, p - float2(0.5, 1.0)) / s.xyxy) + 0.5;
                float4 h  = float4(p - hc.xy * s, p - (hc.zw + 0.5) * s);
                float4 g  = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? float4(h.xy, hc.xy) : float4(h.zw, hc.zw + 0.5);
                float2 a  = abs(g.xy);
                float  d  = max(dot(a, s * 0.5), a.x);   // 中心0 → 境界0.5
                return float3(d, g.zw);
            }

            half4 frag(Varyings IN, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float t = _Time.y;
                float2 c = IN.uv * 2.0 - 1.0;   // 中心0, 外周1

                // ---- 外周の形（円形 / 四角）と距離 ----
                float distRound  = length(c);
                float distSquare = max(abs(c.x), abs(c.y));
                float dist = lerp(distSquare, distRound, _Roundness);

                half shape = 1.0 - smoothstep(1.0 - _EdgeFade, 1.0, dist);   // 内側1 → 外周0
                if (shape <= 0.0) discard;

                // 外周の光る縁
                half rim = 0.0;
                if (_RimWidth > 0.0)
                    rim = smoothstep(1.0 - _EdgeFade - _RimWidth, 1.0 - _EdgeFade, dist) * shape;

                // ---- 六角形格子 ----
                float2 hp = (IN.uv + _HexScroll.xy * t) * _HexScale;
                float3 hex = HexGrid(hp);
                half hexLine = smoothstep(0.5 - _HexWidth, 0.5, hex.x);
                // セルごとにランダムにまたたく
                half cellBlink = 0.6 + 0.4 * sin(t * 3.0 + Hash(hex.yz) * 6.2831);
                hexLine *= cellBlink;

                // ---- 中心から外へ広がる波紋 ----
                float wavePos = frac(t * _WaveSpeed);
                half wave = 1.0 - saturate(abs(dist - wavePos) / _WaveWidth);
                wave = wave * wave * (1.0 - wavePos) * _WaveAmount;

                // ---- フレネル（曲面メッシュのとき輪郭が光る。Quad では正面でほぼ0） ----
                float3 n = normalize(IN.normalWS) * (isFrontFace ? 1.0 : -1.0);
                float3 v = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half fresnel = pow(1.0 - saturate(dot(n, v)), _FresnelPower) * _FresnelAmount;

                // ---- ちらつき ----
                half flicker = 1.0 - _FlickerAmount * (0.5 + 0.5 * sin(t * _FlickerSpeed + sin(t * 7.3) * 2.0));

                // ---- 合成 ----
                half3 col = _BaseColor.rgb * _BaseAlpha;
                col += _HexColor.rgb * hexLine * (0.35 + wave);
                col += _HexColor.rgb * wave * 0.3;
                col += _RimColor.rgb * (rim + fresnel);

                // ガード成立フラッシュ: 全体を白く光らせ、格子を強調
                col = lerp(col, _HitColor.rgb * (0.4 + hexLine), _Hit * 0.7);

                col *= flicker * _Intensity;

                // ---- 暗い下地: 面全体 + 線・縁に沿って濃く（白背景で光が埋もれないように） ----
                half dark = saturate(_DarkBase + _DarkLine * saturate(hexLine + rim + fresnel));
                dark *= 1.0 - _Hit * 0.7;   // フラッシュ中は明るさを優先
                col += _DarkColor.rgb * dark;

                half fade = shape * _Alpha;
                return half4(col * fade, dark * fade);
            }
            ENDHLSL
        }
    }
}
