// Shaders/Pad.hlsl
// World-space launch pad (20m x 20m quad on the XZ plane at Y=0).

cbuffer SceneCB : register(b0)
{
    float4x4 WorldViewProjection;
};

struct VSIn
{
    float3 Position : POSITION;
};

struct VSOut
{
    float4 Position : SV_Position;
};

VSOut VSMain(VSIn input)
{
    VSOut o;
    o.Position = mul(float4(input.Position, 1.0f), WorldViewProjection);
    return o;
}

float4 PSMain(VSOut input) : SV_Target
{
    // Pad material (unlit for now; the ground lighting provides context).
    return float4(0.10f, 0.30f, 0.90f, 1.0f);
}

