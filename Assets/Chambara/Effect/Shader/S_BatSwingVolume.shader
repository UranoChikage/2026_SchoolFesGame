Shader "Custom/Trail/BatSwingVolume"
{
    // SwordTrailMesh（Shape = Tube）用。バットの振りの「かたまり」をトゥーン陰影つきの不透明な立体で描く
    // UV.x: 0 = 剣側(最新) → 1 = 尾(古い) / UV.y: 円周 0→1
    // 頂点カラーの a に振りの速さ（0〜1）。尾・遅いところほど筋に割れて消える（透けない）
    // 法線は SwordTrailMesh が断面の外向きで入れる
    Properties
    {
        [Header(Color Start to End)]
        [HDR] _StartColor ("Start Color (出始めの色)", Color) = (1.6, 1.55, 1.45, 1)
        [HDR] _MidColor   ("Mid Color (途中の色)", Color) = (1.2, 0.95, 0.6, 1)
        [HDR] _EndColor   ("End Color (終わりの色)", Color) = (1.0, 0.45, 0.1, 1)
        _StartLength ("Start Length (出始めの色が続く長さ)", Range(0, 1)) = 0.3
        _MidEnd      ("Mid End (途中の色が終わる位置)", Range(0, 1)) = 0.6
        _ColorBlend  ("Color Blend (色の境目のぼかし。0 でパキッと)", Range(0, 0.5)) = 0.05

        [Header(Toon)]
        _HighlightAdd ("Highlight Add (明るい面に足す白)", Range(0, 2)) = 0.6
        _ShadeMul     ("Shade Mul (影の面の暗さ)", Range(0, 1)) = 0.6
        [HDR] _RimColor      ("Rim Color (輪郭)", Color) = (0.3, 0.1, 0.04, 1)
        _HighlightThreshold  ("Highlight Threshold", Range(0, 1)) = 0.85
        _ShadeThreshold      ("Shade Threshold", Range(0, 1)) = 0.5
        _RimThreshold        ("Rim Threshold (輪郭の太さ。小さいほど太い)", Range(0, 1)) = 0.75
        _LightDir            ("Light Dir (xyz。w=1 でこの向き / 0 でメインライト)", Vector) = (0.3, 1, -0.5, 0)

        [Header(Speed Lines)]
        _LineCount   ("Line Count (円周の筋の本数)", Float) = 9
        _LineBreak   ("Line Break (筋の途切れの細かさ[ワールド])", Float) = 1.5
        _LineSpeed   ("Line Speed", Float) = 1
        _LineCut     ("Line Cut (尾ほど筋に割る量)", Range(0, 1)) = 0.3

        [Header(Fade)]
        _HeadFade    ("Head Fade", Range(0, 0.3)) = 0.02
        _TailFadeStart("Tail Fade Start", Range(0, 1)) = 0.25
        _MinSpeed    ("Min Speed", Range(0, 1)) = 0.1

        _Intensity   ("Intensity", Range(0, 10)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4  _StartColor;
            half4  _MidColor;
            half4  _EndColor;
            half   _StartLength;
            half   _MidEnd;
            half   _ColorBlend;
            half   _HighlightAdd;
            half   _ShadeMul;
            half4  _RimColor;
            half   _HighlightThreshold;
            half   _ShadeThreshold;
            half   _RimThreshold;
            float4 _LightDir;
            float  _LineCount;
            float  _LineBreak;
            float  _LineSpeed;
            half   _LineCut;
            half   _HeadFade;
            half   _TailFadeStart;
            half   _MinSpeed;
            half   _Intensity;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv         : TEXCOORD0;
            half4  color      : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
            half4  color      : COLOR;
            float3 positionWS : TEXCOORD1;
            float3 normalWS   : TEXCOORD2;
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

        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
            OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
            OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
            OUT.uv    = IN.uv;
            OUT.color = IN.color;
            return OUT;
        }

        // 出始め(t=0) → 途中 → 終わり(t=1) の3色。境目は blend の幅でなじませる（0 でパキッと切り替え）
        half3 TimeColor(float t, half3 c0, half3 c1, half3 c2, half len0, half end1, half blend)
        {
            half b = max(blend, 1e-4);
            half3 col = lerp(c0, c1, smoothstep(len0 - b, len0 + b, t));
            return lerp(col, c2, smoothstep(end1 - b, end1 + b, t));
        }

        // 尾・遅いところを筋に割って切り抜く
        void Erode(Varyings IN)
        {
            float t = saturate(IN.uv.x);
            float v = frac(IN.uv.y);
            // 円周でつながるように v を円に乗せてノイズを引く
            float a = v * 6.2831853;
            float2 ring = float2(cos(a), sin(a)) * _LineCount * 0.3;
            float lines = ValueNoise(ring + 0.5) * 0.65 + ValueNoise(ring * 2.3 + 17.5) * 0.35;
            float breakup = ValueNoise3D(IN.positionWS * _LineBreak + _Time.y * _LineSpeed);
            float n = lines * lerp(0.6, 1.0, breakup);

            float head  = smoothstep(0.0, max(_HeadFade, 1e-4), t);
            float tail  = 1.0 - smoothstep(_TailFadeStart, 1.0, t);
            float speed = lerp(_MinSpeed, 1.0, IN.color.a);
            float erode = saturate(1.0 - head * tail * speed + _LineCut * t);
            clip(n - erode);
        }
        ENDHLSL

        Pass
        {
            Name "BatSwingVolume"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(Varyings IN) : SV_Target
            {
                Erode(IN);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                if (dot(N, V) < 0) N = -N;   // 裏側（内側）が見えても表として陰影をつける

                float3 L = _LightDir.w > 0.5 ? normalize(_LightDir.xyz) : GetMainLight().direction;
                float ndl = dot(N, L) * 0.5 + 0.5;

                // ---- 色は出始め → 終わりで変え、その上にトゥーンの明暗(3段) + 輪郭をのせる ----
                half3 col = TimeColor(saturate(IN.uv.x), _StartColor.rgb, _MidColor.rgb, _EndColor.rgb, _StartLength, _MidEnd, _ColorBlend);
                col = ndl > _HighlightThreshold ? col + _HighlightAdd
                    : ndl > _ShadeThreshold     ? col
                    :                             col * _ShadeMul;
                if (dot(N, V) < 1.0 - _RimThreshold) col = _RimColor.rgb;

                return half4(col * IN.color.rgb * _Intensity, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth

            half fragDepth(Varyings IN) : SV_Target
            {
                Erode(IN);
                return IN.positionCS.z;
            }
            ENDHLSL
        }
    }
}
