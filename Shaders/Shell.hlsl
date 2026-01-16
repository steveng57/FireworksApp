// Shaders/Shell.hlsl
// Bright unlit shell for visibility (debug / flare-like).

cbuffer SceneCB : register(b0)
{
    // View-projection matrix (world assumed identity; instance data provides translation).
    float4x4 WorldViewProjection;
};

struct VSIn
{
    float3 Position : POSITION;
    float3 Normal   : NORMAL;
    float3 InstancePos : INSTANCEPOS;
};

struct VSOut
{
    float4 Position : SV_Position;
};

VSOut VSMain(VSIn input)
{
    VSOut o;
    float3 worldPos = input.Position + input.InstancePos;
    o.Position = mul(float4(worldPos, 1.0f), WorldViewProjection);
    return o;
}

float4 PSMain(VSOut input) : SV_Target
{
    // Bright yellow
    return float4(1.0f, 0.95f, 0.1f, 1.0f);
}
