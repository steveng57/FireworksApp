// Shaders/Particles.hlsl
// GPU particle system: shells + sparks (burst) in a single structured buffer.

cbuffer FrameCB : register(b0)
{
    float4x4 ViewProjection;
    float3   CameraRightWS;
    float    DeltaTime;
    float3   CameraUpWS;
    float    Time;

    float3   CrackleBaseColor;
    float    CrackleBaseSize;
    float3   CracklePeakColor;
    float    CrackleFlashSizeMul;
    float3   CrackleFadeColor;
    float    CrackleTau;
};

struct Particle
{
    float3 Position;
    float3 Velocity;
    float  Age;
    float  Lifetime;
    float4 Color;
    uint   Kind;   // 0=Dead, 1=Shell, 2=Spark, 3=Smoke (reserved), 4=Crackle
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

float Hash01(uint x)
{
    // Simple integer hashing -> [0,1)
    x ^= x >> 16;
    x *= 0x7feb352d;
    x ^= x >> 15;
    x *= 0x846ca68b;
    x ^= x >> 16;
    return (x & 0x00ffffffu) / 16777216.0f;
}

// Simulates "crackling" by generating short, irregular on/off pulses in the particle lifetime.
// Returns (brightnessMultiplier, pulse01)
float2 CrackleFlicker(float age, float lifetime, uint seed)
{
    float t = (lifetime > 1e-5f) ? saturate(age / lifetime) : 1.0f;

    // 2–5 pulses total.
    float rP = Hash01(seed ^ 0xA2C79u);
    int pulses = 2 + (int)floor(rP * 4.0f); // 2..5

    // Pulse timing domain in seconds.
    // On: 5–15ms, Off: 5–12ms, approximate by a per-pulse frequency.
    float baseHz = 18.0f + 22.0f * Hash01(seed ^ 0x19F3Bu); // ~18..40Hz (fits 30-90ms life)
    float phase = Hash01(seed ^ 0xC0FFEEu);

    // Create a harsh square-like wave, then gate it so only a few pulses exist.
    float wave = frac((age * baseHz) + phase);
    float on = step(wave, 0.42f + 0.18f * Hash01(seed ^ 0x51u)); // ~40-60% duty

    // Limit number of pulses by time; each pulse occupies 1/pulses of normalized lifetime.
    float pulseWindow = (pulses > 0) ? (1.0f / pulses) : 1.0f;
    float pulseId = floor(t / pulseWindow);
    float active = step(pulseId, (float)(pulses - 1));

    float pulse01 = on * active;

    // Brightness envelope: peak * exp(-t/tau) + random spikes
    float tau = max(0.001f, CrackleTau);
    float env = exp(-age / tau);

    float spike = 0.6f + 1.2f * Hash01(seed ^ (asuint(pulseId) * 0x9E3779B9u));
    float brightness = lerp(0.15f, 1.0f, pulse01) * (0.8f + 2.6f * env) * spike;
    return float2(brightness, pulse01);
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
    else if (p.Kind == 4)
    {
        uint seed = p._pad.x ^ p._pad.y ^ p._pad.z ^ (i * 747796405u);
        float2 flick = CrackleFlicker(p.Age, p.Lifetime, seed);
        float brightness = flick.x;
        float pulse01 = flick.y;

        float3 baseC = CrackleBaseColor;
        float3 peakC = CracklePeakColor;
        float3 fadeC = CrackleFadeColor;

        // Early: peak flashes, then drift toward fade.
        float fadeT = saturate(t * 1.25f);
        float3 c = lerp(peakC, baseC, 1.0f - exp(-p.Age / max(0.001f, CrackleTau)));
        c = lerp(c, fadeC, fadeT);

        // Abrupt appearance/disappearance (no smooth fade); keep minimal alpha for second pass.
        float a = saturate(0.35f + 0.65f * pulse01);
        p.Color = float4(c * brightness, a);
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
    if (p.Kind == 4)
    {
        // Use alpha as pulse indicator (set in CS). Flash size during peaks.
        float flash = step(0.75f, p.Color.a);
        size = CrackleBaseSize * lerp(1.0f, CrackleFlashSizeMul, flash);
    }

    float3 worldPos = p.Position + (CameraRightWS * (uv.x * size)) + (CameraUpWS * (uv.y * size));
    o.Position = mul(float4(worldPos, 1.0f), ViewProjection);
    o.Color = p.Color;
    return o;
}

float4 PSParticle(VSOut input) : SV_Target
{
    return input.Color;
}
