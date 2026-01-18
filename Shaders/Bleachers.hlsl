// Shaders/Bleachers.hlsl
// Lit bleacher geometry sharing Scene and Lighting constant buffers.

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
    uint   FigureId : TEXCOORD0;
};

struct VSOut
{
    float4 Position : SV_Position;
    float3 NormalWS : TEXCOORD0;
    nointerpolation float3 FigureColor : COLOR0;
};

static float Hash31(float3 p)
{
    // Fast, low-cost hash for pseudo-random selection.
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

static float Hash11(uint n)
{
    n ^= 2747636419u;
    n *= 2654435769u;
    n ^= n >> 16;
    return frac((float)n * (1.0 / 4294967296.0));
}

static float3 SelectFigureColor(uint figureId)
{
    // Keep non-figure geometry dark grey.
    const float3 darkGrey = float3(0.18f, 0.18f, 0.20f);
    if (figureId == 0)
    {
        return darkGrey;
    }

    float h = Hash11(figureId);
    if (h < 0.5f)
    {
        return darkGrey;
    }

    float t = (h - 0.5f) * 2.0f; // 0..1 across color band
    if (t < 0.2f) return float3(0.85f, 0.12f, 0.12f);      // red
    if (t < 0.4f) return float3(0.12f, 0.75f, 0.18f);      // green
    if (t < 0.6f) return float3(0.18f, 0.18f, 0.85f);      // blue
    if (t < 0.8f) return float3(0.58f, 0.32f, 0.78f);      // purple
    return float3(0.95f, 0.88f, 0.20f);                    // yellow
}

VSOut VSMain(VSIn input)
{
    VSOut o;
    o.Position = mul(float4(input.Position, 1.0f), WorldViewProjection);
    o.NormalWS = mul(float4(input.Normal, 0.0f), World).xyz;
    o.FigureColor = SelectFigureColor(input.FigureId);
    return o;
}

float4 PSMain(VSOut input) : SV_Target
{
    float3 N;
    float ndotl;

    bool isSilhouette = dot(input.NormalWS, input.NormalWS) < 1e-6;

    if (isSilhouette)
    {
        // Uniform darker silhouette color for clearer readability.
        float3 baseColor = input.FigureColor;

        // Keep shading minimal; use a small fixed ndotl so silhouettes stay visible.
        ndotl = 0.25f;
        float3 lit = baseColor * (AmbientColor + LightColor * ndotl);
        return float4(lit, 1.0f);
    }
    else
    {
        N = normalize(input.NormalWS);
        float3 L = normalize(-LightDirectionWS);
        ndotl = saturate(dot(N, L));

        // Slightly darker base to keep structure readable at night without tinting.
        float3 baseColor = input.FigureColor;
        float3 lit = baseColor * (AmbientColor + LightColor * ndotl);
        return float4(lit, 1.0f);
    }
}
