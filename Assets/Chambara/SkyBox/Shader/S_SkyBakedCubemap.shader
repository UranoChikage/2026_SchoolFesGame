Shader "Custom/Skybox/SkyBakedCubemap"
{
    // StageThemeApplier が手続き型スカイボックスを焼き付けたキューブマップを表示するだけの軽量シェーダー
    // (マテリアルは実行時に自動で作られるので、手動で設定する必要はない)
    Properties
    {
        [NoScaleOffset] _Cubemap("Cubemap", Cube) = "grey" {}
        _Exposure("Exposure", Range(0, 4)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Background"
            "Queue"="Background"
            "PreviewType"="Skybox"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "SkyBakedCubemap"

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex SkyVert
            #pragma fragment frag

            #include "SkyCommon.hlsl"

            TEXTURECUBE(_Cubemap);
            SAMPLER(sampler_Cubemap);

            CBUFFER_START(UnityPerMaterial)
                half _Exposure;
            CBUFFER_END

            half4 frag(SkyVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 dir = normalize(input.viewDirWS);
                half3 col = SAMPLE_TEXTURECUBE_LOD(_Cubemap, sampler_Cubemap, dir, 0).rgb;
                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
