// Ocean.shader  (URP 用)
// 必要: URP Asset で Depth Texture / Opaque Texture を ON
// 波・深さによる色・屈折・岸辺の泡・空の反射・ハイライトを1つで処理する
Shader "Custom/Ocean"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("浅い所の色 (水中に掛ける色)", Color) = (0.55, 0.9, 0.95, 1)
        _DeepColor    ("深い所の色", Color) = (0.02, 0.12, 0.25, 1)
        _DepthRange   ("深海色になる深さ (m)", Float) = 6

        // Wave = (方向X, 方向Z, 鋭さ, 波長)。鋭さの合計は 1 以下に
        [Header(Waves)]
        _WaveHeight ("波の高さ (全体の倍率)", Range(0, 1)) = 0.3
        _WaveA ("Wave A (方向X 方向Z 鋭さ 波長)", Vector) = (1, 0, 0.25, 60)
        _WaveB ("Wave B (方向X 方向Z 鋭さ 波長)", Vector) = (0, 1, 0.25, 31)
        _WaveC ("Wave C (方向X 方向Z 鋭さ 波長)", Vector) = (1, 1, 0.15, 18)

        [Header(Detail Normal)]
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalSize     ("ノーマルマップ1枚の大きさ (m)", Float) = 8
        _NormalStrength ("ノーマルの強さ", Range(0, 2)) = 0.6
        _NormalSpeed    ("ノーマルの流れる速さ", Float) = 0.4

        [Header(Refraction and Reflection)]
        _RefractionStrength ("屈折の強さ", Range(0, 0.2)) = 0.04
        _ReflectionStrength ("反射の強さ", Range(0, 1)) = 0.8
        _FresnelPower       ("フレネルの鋭さ", Range(1, 10)) = 5

        [Header(Specular)]
        _SpecularPower     ("ハイライトの鋭さ", Range(8, 1024)) = 256
        _SpecularIntensity ("ハイライトの強さ", Range(0, 5)) = 1.5

        [Header(Foam)]
        _FoamColor    ("泡の色 (Aで濃さ)", Color) = (1, 1, 1, 1)
        _FoamDistance ("泡が出る深さ (m)", Float) = 0.6
        _FoamScale    ("泡の細かさ", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "OceanForward"
            Tags { "LightMode" = "UniversalForward" }

            // 透明感は Opaque Texture(背景の色) を自前で混ぜて出すので、ブレンドは不要
            Blend Off
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthRange;
                float  _WaveHeight;
                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
                float4 _NormalMap_ST;
                float  _NormalSize;
                float  _NormalStrength;
                float  _NormalSpeed;
                float  _RefractionStrength;
                float  _ReflectionStrength;
                float  _FresnelPower;
                float  _SpecularPower;
                float  _SpecularIntensity;
                float4 _FoamColor;
                float  _FoamDistance;
                float  _FoamScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float4 screenPos   : TEXCOORD4;
                float  fogFactor   : TEXCOORD5;
            };

            // ---------- ゲルストナー波 ----------
            // wave = (方向X, 方向Z, 鋭さ, 波長)。3つの鋭さの合計は 1 以下に
            void GerstnerWave(float3 p, float4 wave, float time,
                              inout float3 tangent, inout float3 binormal, inout float3 offset)
            {
                float steepness  = wave.z;
                float wavelength = max(wave.w, 0.001);
                float k = TWO_PI / wavelength;
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy + 1e-5);
                float f = k * (dot(d, p.xz) - c * time);
                float a = steepness / k;
                float s = sin(f);
                float co = cos(f);

                tangent  += float3(-d.x * d.x * steepness * s,  d.x * steepness * co, -d.x * d.y * steepness * s);
                binormal += float3(-d.x * d.y * steepness * s,  d.y * steepness * co, -d.y * d.y * steepness * s);
                offset   += float3(d.x * a * co, a * s, d.y * a * co);
            }

            // ---------- 泡用の簡易ノイズ ----------
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // ---------- 頂点 ----------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // ワールド座標で波を計算し、そのままクリップ座標へ（オブジェクト空間に戻さない）
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                // 各波の変位と傾きを合計し、最後に _WaveHeight で一括して縮める
                float3 dT     = float3(0, 0, 0);
                float3 dB     = float3(0, 0, 0);
                float3 offset = float3(0, 0, 0);
                float t = _Time.y;

                GerstnerWave(positionWS, _WaveA, t, dT, dB, offset);
                GerstnerWave(positionWS, _WaveB, t, dT, dB, offset);
                GerstnerWave(positionWS, _WaveC, t, dT, dB, offset);

                float3 tangent  = float3(1, 0, 0) + dT * _WaveHeight;
                float3 binormal = float3(0, 0, 1) + dB * _WaveHeight;
                positionWS += offset * _WaveHeight;

                OUT.positionWS  = positionWS;
                OUT.tangentWS   = normalize(tangent);
                OUT.bitangentWS = normalize(binormal);
                OUT.normalWS    = normalize(cross(binormal, tangent));
                OUT.positionCS  = TransformWorldToHClip(positionWS);
                OUT.screenPos   = ComputeScreenPos(OUT.positionCS);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            // ---------- ピクセル ----------
            half4 frag(Varyings IN) : SV_Target
            {
                // --- ノーマル: 波のノーマル + 2方向に流れる細かい波 ---
                float3 T  = normalize(IN.tangentWS);
                float3 B  = normalize(IN.bitangentWS);
                float3 N0 = normalize(IN.normalWS);

                float2 baseUV = IN.positionWS.xz / max(_NormalSize, 0.01);
                float2 uv1 = baseUV + _Time.y * _NormalSpeed * float2(0.10, 0.03);
                float2 uv2 = baseUV * 0.63 + _Time.y * _NormalSpeed * float2(-0.04, 0.08);
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1), _NormalStrength);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2), _NormalStrength);
                half3 nTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                float3 N = normalize(mul(nTS, float3x3(T, B, N0)));

                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // --- 深さ: 水面から海底までの距離 ---
                float2 screenUV   = IN.screenPos.xy / IN.screenPos.w;
                float surfaceDepth = IN.screenPos.w;   // カメラ→水面
                float sceneDepth   = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams); // カメラ→海底
                float depthDiff    = sceneDepth - surfaceDepth;

                // --- 屈折: 背景をノーマルでずらす（手前の物体が映り込む場合はずらさない） ---
                float2 refrUV = screenUV + N.xz * _RefractionStrength;
                float refrDepth = LinearEyeDepth(SampleSceneDepth(refrUV), _ZBufferParams);
                if (refrDepth < surfaceDepth)
                {
                    refrUV = screenUV;
                    refrDepth = sceneDepth;
                }
                float refrDiff = max(refrDepth - surfaceDepth, 0.0);
                float3 sceneColor = SampleSceneColor(refrUV);

                // --- 水の色: 浅いほど背景が透け、深いほど DeepColor ---
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(N, mainLight.direction));
                float depth01 = saturate(refrDiff / max(_DepthRange, 0.01));
                float3 deep = _DeepColor.rgb * mainLight.color * (NdotL * 0.5 + 0.5);
                float3 col = lerp(sceneColor * _ShallowColor.rgb, deep, depth01);

                // --- 反射: 浅い角度ほど空（リフレクションプローブ）が映る ---
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float3 reflection = GlossyEnvironmentReflection(reflect(-V, N), 0.05, 1.0);
                col = lerp(col, reflection, fresnel * _ReflectionStrength);

                // --- 太陽のハイライト ---
                float3 H = normalize(mainLight.direction + V);
                float spec = pow(saturate(dot(N, H)), _SpecularPower) * _SpecularIntensity;
                col += mainLight.color * spec;

                // --- 岸辺の泡 ---
                float shore = 1.0 - saturate(depthDiff / max(_FoamDistance, 0.01));
                float noise = ValueNoise(IN.positionWS.xz * _FoamScale + _Time.y * 0.3);
                float foam = smoothstep(noise - 0.05, noise + 0.05, shore);
                col = lerp(col, _FoamColor.rgb, foam * _FoamColor.a);

                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
