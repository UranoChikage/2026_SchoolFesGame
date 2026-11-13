#ifndef CHAMBARA_SKY_COMMON_INCLUDED
#define CHAMBARA_SKY_COMMON_INCLUDED

// 手続き型スカイボックス共通処理 (頂点シェーダ / ノイズ / 星 / 雲 / 山並み)
// 各 S_Sky*.shader から #include "SkyCommon.hlsl" で使う

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#define SKY_PI  3.14159265
#define SKY_TAU 6.28318531

struct SkyAttributes
{
    float4 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SkyVaryings
{
    float4 positionCS : SV_POSITION;
    float3 viewDirWS  : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

SkyVaryings SkyVert(SkyAttributes input)
{
    SkyVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.viewDirWS  = input.positionOS.xyz;
    return output;
}

// ---------------------------------------------------------------- Hash / Noise

float SkyHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float SkyHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float3 SkyHash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float SkyNoise2(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = SkyHash21(i);
    float b = SkyHash21(i + float2(1, 0));
    float c = SkyHash21(i + float2(0, 1));
    float d = SkyHash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float SkyNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = SkyHash31(i);
    float n100 = SkyHash31(i + float3(1, 0, 0));
    float n010 = SkyHash31(i + float3(0, 1, 0));
    float n110 = SkyHash31(i + float3(1, 1, 0));
    float n001 = SkyHash31(i + float3(0, 0, 1));
    float n101 = SkyHash31(i + float3(1, 0, 1));
    float n011 = SkyHash31(i + float3(0, 1, 1));
    float n111 = SkyHash31(i + float3(1, 1, 1));
    float x00 = lerp(n000, n100, u.x);
    float x10 = lerp(n010, n110, u.x);
    float x01 = lerp(n001, n101, u.x);
    float x11 = lerp(n011, n111, u.x);
    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
}

float SkyFbm2(float2 p, int octaves)
{
    float v = 0.0;
    float amp = 0.5;
    // 回転を挟んで格子感を消す
    const float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
    for (int k = 0; k < octaves; k++)
    {
        v += amp * SkyNoise2(p);
        p = mul(rot, p) * 2.03 + 17.1;
        amp *= 0.5;
    }
    return v;
}

float SkyFbm3(float3 p, int octaves)
{
    float v = 0.0;
    float amp = 0.5;
    for (int k = 0; k < octaves; k++)
    {
        v += amp * SkyNoise3(p);
        p = p.yzx * 2.03 + 7.3;
        amp *= 0.5;
    }
    return v;
}

// ---------------------------------------------------------------- Utility

// n に垂直な 2 軸を作る
void SkyBasis(float3 n, out float3 t, out float3 b)
{
    float3 refUp = abs(n.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
    t = normalize(cross(refUp, n));
    b = cross(n, t);
}

// 2 方向のなす角 (ラジアンの近似。小さい角度で正確)
float SkyAngle(float3 a, float3 b)
{
    return sqrt(max(0.0, 2.0 * (1.0 - dot(a, b))));
}

float SkyAzimuth(float3 dir)
{
    return atan2(dir.x, dir.z);
}

// ---------------------------------------------------------------- Stars

// x = 明るさ, y = 色用の乱数
float2 SkyStars(float3 dir, float density, float threshold, float size, float twinkle)
{
    float3 p = dir * density;
    float3 cell = floor(p);
    float3 h = SkyHash33(cell);
    float exist = step(threshold, SkyHash31(cell + 17.31));
    float3 starPos = cell + 0.25 + h * 0.5;
    float d = length(p - starPos);
    float b = exp(-d * d / (size * size));
    float tw = lerp(1.0, 0.45 + 0.55 * sin(_Time.y * (1.5 + h.x * 5.0) + h.y * 60.0), twinkle);
    return float2(b * exist * tw * (0.25 + 0.75 * h.z * h.z), h.x);
}

half3 SkyStarColor(float h)
{
    // 青白い星 〜 オレンジの星
    half3 cool = half3(0.7, 0.82, 1.0);
    half3 warm = half3(1.0, 0.82, 0.6);
    return lerp(cool, warm, h * h);
}

// ---------------------------------------------------------------- Clouds

// 平面投影の雲. x = 濃度 (0..1), y = 厚み (陰影用)
float2 SkyCloudLayer(float2 uv, float2 wind, half coverage, half softness)
{
    float n  = SkyFbm2(uv + wind, 6);
    float n2 = SkyFbm2(uv * 2.7 - wind * 1.6 + 5.2, 4);
    float shape = n * 0.75 + n2 * 0.25;
    float threshold = 1.0 - coverage;
    float density = smoothstep(threshold - softness * 0.5, threshold + softness * 0.5, shape);
    return float2(density, saturate((shape - threshold) * 2.0));
}

// ---------------------------------------------------------------- Mountains

// 方位角に対して継ぎ目の無い稜線の高さ (0..1 程度)
float SkyRidge(float azimuth, float freq, float seed, float sharpness)
{
    float2 p = float2(cos(azimuth), sin(azimuth)) * freq + seed;
    float n = SkyFbm2(p, 5);
    float ridged = 1.0 - abs(n * 2.0 - 1.0);
    return lerp(n, ridged * ridged, sharpness);
}

// 奥 (far) から手前 (near) へ何重にも重なる山のシルエットを描く
// mistCol / mist : 谷間にたまる霧
// rimCol / lightDir : 光源側の稜線をふちどる光
half3 SkyApplyMountains(half3 col, float3 dir, int layers, float height, float sharpness, float seed,
                        half3 farCol, half3 nearCol, half3 mistCol, half mist, half3 rimCol, float3 lightDir)
{
    float az = SkyAzimuth(dir);
    float3 flatDir   = normalize(float3(dir.x, 0, dir.z) + float3(1e-5, 0, 0));
    float3 flatLight = normalize(float3(lightDir.x, 0, lightDir.z) + float3(1e-5, 0, 0));
    float facing = pow(dot(flatDir, flatLight) * 0.5 + 0.5, 4.0);

    for (int i = 0; i < layers; i++)
    {
        float t = layers > 1 ? (float)i / (float)(layers - 1) : 1.0;
        float base = lerp(0.015, -0.03, t);
        float amp  = height * lerp(1.0, 0.45, t);
        float freq = lerp(3.2, 1.4, t);
        float h = base + amp * SkyRidge(az, freq, seed + i * 7.13, sharpness);

        float cover = 1.0 - smoothstep(h - 0.0015, h + 0.0015, dir.y);

        half3 layerCol = lerp(farCol, nearCol, t);
        // 谷にたまる霧 (奥の層ほど濃い)
        float m = (1.0 - smoothstep(base - 0.02, h, dir.y)) * mist * (1.0 - t * 0.5);
        layerCol = lerp(layerCol, mistCol, m);
        // 稜線のふち
        float rim = exp(-max(h - dir.y, 0.0) / 0.004);
        layerCol += rimCol * rim * facing;

        col = lerp(col, layerCol, cover);
    }
    return col;
}

#endif
