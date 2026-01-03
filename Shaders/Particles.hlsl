// Shaders/Particles.hlsl
// GPU particle system: shells + sparks (burst) in a single structured buffer.

cbuffer FrameCB : register(b0)
{
    float4x4 ViewProjection;
    float3   CameraRightWS;
    float    DeltaTime;
    float3   CameraUpWS;
    float    Time;
};

struct Particle
{
    float3 Position;
    float3 Velocity;
    float  Age;
    float  Lifetime;
    float4 Color;
    uint   Kind;   // 0=Dead, 1=Shell, 2=Spark, 3=Smoke (reserved)
    uint3  _pad;
};

RWStructuredBuffer<Particle> Particles : register(u0);

static const float3 Gravity = float3(0.0f, -9.81f, 0.0f);

float4 ColorRampSpark(float t, float4 baseColor)
{
    t = saturate(t);

    float3 whiteHot = baseColor.rgb * 4.0f + 1.5f;
    float3 mid = baseColor.rgb;
    float3 ember = float3(1.0f, 0.35f, 0.08f);

    float early = smoothstep(0.00f, 0.20f, t);
    float midW = smoothstep(0.20f, 0.70f, t);

    float3 c = lerp(whiteHot, mid, early);
    c = lerp(c, ember, midW);

    float a = 1.0f - smoothstep(0.70f, 1.00f, t);
    return float4(c, a);
}

[numthreads(256, 1, 1)]
void CSUpdate(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x;

    Particle p = Particles[i];
    if (p.Kind == 0)
        return;

    float dt = DeltaTime;

    p.Age += dt;
    float t = (p.Lifetime > 0.0001f) ? (p.Age / p.Lifetime) : 1.0f;

    if (p.Age >= p.Lifetime)
    {
        p.Kind = 0;
        p.Color = float4(0, 0, 0, 0);
        Particles[i] = p;
        return;
    }

    // Integrate motion (semi-implicit Euler)
    float drag = 0.02f; // very small (almost vacuum)
    p.Velocity += Gravity * dt;
    p.Velocity *= (1.0f / (1.0f + drag * dt));
    p.Position += p.Velocity * dt;

    if (p.Kind == 2)
    {
        p.Color = ColorRampSpark(t, p.Color);
    }

    Particles[i] = p;
}

StructuredBuffer<Particle> ParticlesRO : register(t0);

struct VSOut
{
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
};

VSOut VSParticle(uint vid : SV_VertexID)
{
    // 6 verts per particle (two triangles)
    uint particleIndex = vid / 6;
    uint corner = vid % 6;

    Particle p = ParticlesRO[particleIndex];

    VSOut o;

    if (p.Kind == 0 || p.Color.a <= 0.0f)
    {
        o.Position = float4(0, 0, 0, 0);
        o.Color = float4(0, 0, 0, 0);
        return o;
    }

    float2 offsets[6] = {
        float2(-1,-1), float2( 1,-1), float2(-1, 1),
        float2( 1,-1), float2( 1, 1), float2(-1, 1)
    };

    float2 uv = offsets[corner];

    float size = (p.Kind == 2) ? 0.10f : 0.20f;

    float3 worldPos = p.Position + (CameraRightWS * (uv.x * size)) + (CameraUpWS * (uv.y * size));
    o.Position = mul(float4(worldPos, 1.0f), ViewProjection);
    o.Color = p.Color;
    return o;
}

float4 PSParticle(VSOut input) : SV_Target
{
    return input.Color;
}
