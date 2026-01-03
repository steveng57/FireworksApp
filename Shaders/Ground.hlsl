// Shaders/Ground.hlsl
// Simple lit ground plane (world-space) with directional lighting.

cbuffer SceneCB : register(b0)
{
    float4x4 WorldViewProjection;
    float4x4 World;
};

cbuffer LightingCB : register(b1)
{
    float3   LightDirectionWS;
    float    _pad0;
    float3   LightColor;
    float    _pad1;
    float3   AmbientColor;
    float    _pad2;
};

struct VSIn
{
    float3 Position : POSITION;
    float3 Normal   : NORMAL;
};

struct VSOut
{
    float4 Position : SV_Position;
    float3 NormalWS : TEXCOORD0;
};

VSOut VSMain(VSIn input)
{
    VSOut o;
    o.Position = mul(float4(input.Position, 1.0f), WorldViewProjection);
    o.NormalWS = mul(float4(input.Normal, 0.0f), World).xyz;
    return o;
}

float4 PSMain(VSOut input) : SV_Target
{
    float3 N = normalize(input.NormalWS);
    float3 L = normalize(-LightDirectionWS);
    float ndotl = saturate(dot(N, L));

    float3 baseColor = float3(0.05f, 0.06f, 0.08f);
    float3 lit = baseColor * (AmbientColor + LightColor * ndotl);
    return float4(lit, 1.0f);
}
