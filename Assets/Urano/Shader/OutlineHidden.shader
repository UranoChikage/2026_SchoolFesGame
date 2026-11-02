// 手前の物に隠れた部分にだけ出るアウトライン（URP）。OutlineHull コンポーネントが2枚の殻を作って使う。
//   Mark    : 物体の全体にステンシル2を書く（色は出さない。深度は無視）…物体自身の面にアウトラインが重ならないようにする
//   Outline : 法線方向に画面上の一定ピクセル幅だけ膨らませた殻を、「奥にある(=隠れている)」かつ「ステンシルが無い」ところにだけ描く
// → 隠れているときだけ、物体の外周に輪郭線が出る。見えている間は何も出ない
Shader "Urano/OutlineHidden"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Width ("Width (pixels)", Float) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Transparent+50" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Width;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Mark"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest Always
            ColorMask 0
            Stencil { Ref 2 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
