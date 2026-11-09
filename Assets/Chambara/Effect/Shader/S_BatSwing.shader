Shader "Custom/Trail/BatSwing"
{
    // SwordTrailMesh 用（バットの振り。半透明にせず、ベタ塗りのアニメ調で描く）
    // UV.x: 0 = 剣側(最新) → 1 = 尾(古い) / UV.y: 0 = 根元 → 1 = 先端
    // 頂点カラーの a に振りの速さ（0〜1）が入る。遅いところ・尾ほど「筋に割れて」消える（透けない）
    // 不透明 + アルファテスト（clip）なので、後ろの物は透けて見えない
    Properties
    {
        [Header(Color)]
        [HDR] _CoreColor ("Start Color (出始めの色)", Color) = (1.6, 1.55, 1.45, 1)
        [HDR] _MidColor  ("Mid Color (途中の色)", Color) = (1.0, 0.85, 0.55, 1)
        [HDR] _OuterColor("End Color (終わりの色)", Color) = (1.0, 0.45, 0.1, 1)
        [HDR] _EdgeColor ("Edge Color (内側の縁取り)", Color) = (0.35, 0.12, 0.05, 1)
        _CoreSize    ("Start Length (出始めの色が続く長さ)", Range(0, 1)) = 0.35
        _MidPosition ("Mid End (途中の色が終わる位置)", Range(0, 1)) = 0.65
        _EdgeWidth   ("Edge Width (縁取りの幅)", Range(0, 0.3)) = 0.06
        _TailShift   ("Color Speed (1 = 寿命いっぱいで変わる。大きいほど早く変わる)", Range(0.1, 4)) = 1
        _ColorBlend  ("Color Blend (色の境目のぼかし。0 でパキッと)", Range(0, 0.5)) = 0.05

        [Header(Shape)]
        _TailShrink  ("Tail Shrink (尾ほど先端側へ細る)", Range(0, 1)) = 0.45
        _JagAmount   ("Jag Amount (内側の縁のギザギザ)", Range(0, 0.6)) = 0.08
        _JagScale    ("Jag Scale", Float) = 6
        _JagSpeed    ("Jag Speed", Float) = 3

        [Header(Speed Lines)]
        _LineCount   ("Line Count (筋の本数)", Float) = 7
        _LineBreak   ("Line Break (筋の途切れの細かさ[ワールド])", Float) = 1.5
        _LineSpeed   ("Line Speed (ゆらぎの速さ)", Float) = 1
        _LineCut     ("Line Cut (振りの途中でも筋に割る量)", Range(0, 1)) = 0.25
        _LineBase    ("Line Base (最初から筋に割る量。上げると筋だけ残る)", Range(0, 1)) = 0

        [Header(Fade)]
        _HeadFade    ("Head Fade (剣側で欠け始める幅)", Range(0, 0.3)) = 0.02
        _TailFadeStart("Tail Fade Start (尾で割れ始める位置)", Range(0, 1)) = 0.3
        _MinSpeed    ("Min Speed (遅いところの残り具合)", Range(0, 1)) = 0.1

        _Intensity   ("Intensity", Range(0, 10)) = 1.0
        _ZOffset     ("Z Offset (同じ面に重ねるとき、負にすると手前に出る)", Float) = 0

        [Header(Transparency)]
        _Alpha        ("Alpha (濃さ。半透明のとき使う)", Range(0, 1)) = 1
        _AlphaTailFade("Alpha Tail Fade (尾ほど薄くする量)", Range(0, 1)) = 0
        _EdgeSoftness ("Edge Softness (筋の縁のぼかし。0 でパキッと切り抜き)", Range(0, 0.5)) = 0
        // 不透明: Src One / Dst Zero / ZWrite On / Render Queue 2450
        // 半透明: Src SrcAlpha / Dst OneMinusSrcAlpha / ZWrite Off / Render Queue 3000
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
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

        CBUFFER_START(UnityPerMaterial)
            half4  _CoreColor;
            half4  _MidColor;
            half4  _OuterColor;
            half4  _EdgeColor;
            half   _CoreSize;
            half   _MidPosition;
            half   _EdgeWidth;
            half   _TailShift;
            half   _ColorBlend;
            half   _TailShrink;
            half   _JagAmount;
            float  _JagScale;
            float  _JagSpeed;
            float  _LineCount;
            float  _LineBreak;
            float  _LineSpeed;
            half   _LineCut;
            half   _LineBase;
            half   _Alpha;
            half   _AlphaTailFade;
            half   _EdgeSoftness;
            half   _HeadFade;
            half   _TailFadeStart;
            half   _MinSpeed;
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

        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
            OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
            OUT.uv    = IN.uv;
            OUT.color = IN.color;
            return OUT;
        }

        // 形を決めて、外なら clip する。戻り値は色決めに使う値
        // inner: 内側の縁の位置 / g: 0 = 出始め → 1 = 終わり / coverage: 筋の縁のぼかし（半透明のときの濃さ）
        void Shape(Varyings IN, out float inner, out float g, out float coverage)
        {
            float t = saturate(IN.uv.x);   // 0 = 剣側 → 1 = 尾
            float v = saturate(IN.uv.y);   // 0 = 根元 → 1 = 先端

            // ---- 内側の縁: 尾ほど先端側へ寄る ----
            float jag = ValueNoise(float2(t * _JagScale - _Time.y * _JagSpeed, 3.7)) * 0.7
                      + ValueNoise(float2(t * _JagScale * 2.7 - _Time.y * _JagSpeed * 1.6, 9.1)) * 0.3;
            inner = min(_TailShrink * t + _JagAmount * jag * lerp(0.3, 1.0, t), 0.98);
            clip(v - inner);

            // ---- 筋（スピード線）: 透けさせる代わりに、筋の隙間を切り抜いて消していく ----
            float lines = ValueNoise(float2(v * _LineCount, 0.5)) * 0.65
                        + ValueNoise(float2(v * _LineCount * 2.3, 17.5)) * 0.35;
            float breakup = ValueNoise3D(IN.positionWS * _LineBreak + _Time.y * _LineSpeed);
            float n = lines * lerp(0.6, 1.0, breakup);   // 0〜1

            float head  = smoothstep(0.0, max(_HeadFade, 1e-4), t);
            float tail  = 1.0 - smoothstep(_TailFadeStart, 1.0, t);
            float speed = lerp(_MinSpeed, 1.0, IN.color.a);
            float keep  = head * tail * speed;                    // 1 = 全部残る → 0 = 全部消える
            float erode = saturate(1.0 - keep + _LineCut * t + _LineBase);     // 尾へ行くほど少し筋に割る
            // _EdgeSoftness = 0 ならパキッと切り抜き。上げると筋の縁がぼけて薄くなる（半透明用）
            coverage = _EdgeSoftness > 0 ? smoothstep(erode, erode + _EdgeSoftness, n) : 1.0;
            clip(n - erode - 1e-4);

            g = saturate(t * _TailShift);   // 色は出始め → 終わりで変える
        }

        // 出始め(t=0) → 途中 → 終わり(t=1) の3色。境目は blend の幅でなじませる（0 でパキッと切り替え）
        half3 TimeColor(float t, half3 c0, half3 c1, half3 c2, half len0, half end1, half blend)
        {
            half b = max(blend, 1e-4);
            half3 col = lerp(c0, c1, smoothstep(len0 - b, len0 + b, t));
            return lerp(col, c2, smoothstep(end1 - b, end1 + b, t));
        }
        ENDHLSL

        Pass
        {
            Name "BatSwing"
            Tags { "LightMode"="UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Offset [_ZOffset], [_ZOffset]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(Varyings IN) : SV_Target
            {
                float inner, g, coverage;
                Shape(IN, inner, g, coverage);

                // ---- 色: 出始め → 途中 → 終わり。内側の縁だけ縁取り ----
                float v = saturate(IN.uv.y);
                half3 col = TimeColor(g, _CoreColor.rgb, _MidColor.rgb, _OuterColor.rgb, _CoreSize, _MidPosition, _ColorBlend);
                if (v - inner < _EdgeWidth) col = _EdgeColor.rgb;

                // 不透明（Blend One Zero）のときは alpha は使われない
                half alpha = coverage * _Alpha * (1.0 - _AlphaTailFade * saturate(IN.uv.x));
                return half4(col * IN.color.rgb * _Intensity, alpha);
            }
            ENDHLSL
        }

        // 深度プリパス / 深度テクスチャ用（切り抜きを揃える）
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            Offset [_ZOffset], [_ZOffset]
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth

            half fragDepth(Varyings IN) : SV_Target
            {
                float inner, g, coverage;
                Shape(IN, inner, g, coverage);
                return IN.positionCS.z;
            }
            ENDHLSL
        }
    }
}
