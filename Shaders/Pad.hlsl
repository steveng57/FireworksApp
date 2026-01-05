// Shaders/Pad.hlsl
// World-space launch pad (20m x 20m quad on the XZ plane at Y=0).

cbuffer SceneCB : register(b0)
{
    float4x4 WorldViewProjection;
};

struct VSIn
{
    float3 Position : POSITION;
    float4 Color    : COLOR;
};

struct VSOut
{
    float4 Position : SV_Position;
    float4 Color    : COLOR;
};

VSOut VSMain(VSIn input)
{
    VSOut o;
    o.Position = mul(float4(input.Position, 1.0f), WorldViewProjection);
    o.Color = input.Color;
    return o;
}

float4 PSMain(VSOut input) : SV_Target
{
    // Unlit vertex color.
    return input.Color;
}

