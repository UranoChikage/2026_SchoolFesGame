// GerstnerWaves.hlsl
// Shader Graph の Custom Function ノード（Type: File, Name: GerstnerWaves）で使用
// 入力: PositionWS(Vector3), Time(Float), WaveA/WaveB/WaveC(Vector4)
//   Wave = (方向X, 方向Z, 鋭さ steepness 0-1, 波長 wavelength)
//   ※ 3つの steepness の合計は 1 以下に（超えると波頭がループして破綻する）
// 出力: Offset(Vector3, ワールド空間の変位), Normal(Vector3, ワールド), Tangent(Vector3, ワールド)

#ifndef GERSTNER_WAVES_INCLUDED
#define GERSTNER_WAVES_INCLUDED

void GerstnerWave(float3 p, float4 wave, float time,
                  inout float3 tangent, inout float3 binormal, inout float3 offset)
{
    float steepness  = wave.z;
    float wavelength = max(wave.w, 0.001);
    float k = 6.28318530718 / wavelength;   // 波数
    float c = sqrt(9.8 / k);                 // 位相速度（深水波の分散関係）
    float2 d = normalize(wave.xy + 1e-5);
    float f = k * (dot(d, p.xz) - c * time);
    float a = steepness / k;                 // 振幅

    float s = sin(f);
    float co = cos(f);

    tangent  += float3(-d.x * d.x * (steepness * s),
                        d.x * (steepness * co),
                       -d.x * d.y * (steepness * s));
    binormal += float3(-d.x * d.y * (steepness * s),
                        d.y * (steepness * co),
                       -d.y * d.y * (steepness * s));
    offset   += float3(d.x * (a * co), a * s, d.y * (a * co));
}

void GerstnerWaves_float(float3 PositionWS, float Time,
                         float4 WaveA, float4 WaveB, float4 WaveC,
                         out float3 Offset, out float3 Normal, out float3 Tangent)
{
    float3 tangent  = float3(1, 0, 0);
    float3 binormal = float3(0, 0, 1);
    Offset = float3(0, 0, 0);

    GerstnerWave(PositionWS, WaveA, Time, tangent, binormal, Offset);
    GerstnerWave(PositionWS, WaveB, Time, tangent, binormal, Offset);
    GerstnerWave(PositionWS, WaveC, Time, tangent, binormal, Offset);

    Normal  = normalize(cross(binormal, tangent));
    Tangent = normalize(tangent);
}

// half 精度版（Graph の Precision を Half にした場合用）
void GerstnerWaves_half(half3 PositionWS, half Time,
                        half4 WaveA, half4 WaveB, half4 WaveC,
                        out half3 Offset, out half3 Normal, out half3 Tangent)
{
    float3 o, n, t;
    GerstnerWaves_float(PositionWS, Time, WaveA, WaveB, WaveC, o, n, t);
    Offset = o; Normal = n; Tangent = t;
}

#endif
