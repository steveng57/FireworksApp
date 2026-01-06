// Shaders/Particles.hlsl
// GPU particle system: shells + sparks (burst) in a single structured buffer.

cbuffer FrameCB : register(b0)
{
    float4x4 ViewProjection;
    float3 CameraRightWS;
    float DeltaTime;
    float3 CameraUpWS;
    float Time;

    float3 CrackleBaseColor;
    float CrackleBaseSize;
    float3 CracklePeakColor;
    float CrackleFlashSizeMul;
    float3 CrackleFadeColor;
    float CrackleTau;

    float3 SchemeTint;
    float _stpad0;

    uint ParticlePass; // 0=additive, 1=alpha
    uint3 _ppad;
};

struct Particle
{
    float3 Position;
    float3 Velocity;
    float Age;
    float Lifetime;
    float4 Color;
    uint Kind; // 0=Dead, 1=Shell, 2=Spark, 3=Smoke (reserved), 4=Crackle
    uint3 _pad;
};

RWStructuredBuffer<Particle> Particles : register(u0);

static const float3 Gravity = float3(0.0f, -9.81f, 0.0f);
static const float SmokeIntensity = 0.28f;

// Quadratic drag strength for sparks/crackles (tune visually).
static const float SparkDragK = 0.015f;

// Simple integer hash → [0,1)
float Hash01(uint x)
{
    // Wang hash
    x = (x ^ 61u) ^ (x >> 16);
    x *= 9u;
    x = x ^ (x >> 4);
    x *= 0x27d4eb2du;
    x = x ^ (x >> 15);

    // Convert to [0,1)
    return (x & 0x00FFFFFFu) / 16777216.0f;
}

float4 ColorRampSpark(float t, float4 baseColor)
{
    t = saturate(t);

    // Time shaping: keep sparks visually "younger" for longer
    float tColor = pow(t, 0.7f); // 0.7 < 1 => stretches out early/mid phases

    float3 scheme = max(SchemeTint, 0.001f);
    float3 whiteHot = baseColor.rgb * scheme * 4.0f + 1.5f;
    float3 mid = baseColor.rgb * scheme;
    float3 ember = float3(1.0f, 0.35f, 0.08f);

    float early = smoothstep(0.00f, 0.20f, tColor);
    float midW = smoothstep(0.20f, 0.70f, tColor);

    float3 c = lerp(whiteHot, mid, early);
    c = lerp(c, ember, midW);

    float a = 1.0f - smoothstep(0.70f, 1.00f, tColor);

    return float4(c, a);
}

// Burst-sparkle (twinkle) for Kind==2 sparks only.
// Rate is in Hz (sparkles/sec) and intensity is roughly 0..1 (can go higher for "glitter").
// The CPU packs rate/intensity into Particle._pad0/_pad1 as float bits.
float SparkleMul(float time, float rateHz, float intensity, uint seed)
{
    if (rateHz <= 0.0f || intensity <= 0.0f)
        return 1.0f;

    // Per-particle phase and flavor.
    float phase = Hash01(seed ^ 0x9e3779b9u);
    float flavor = Hash01(seed * 1664525u + 1013904223u);

    // Continuous flicker (sin) pushed toward "spiky" peaks.
    float s = 0.5f + 0.5f * sin((time * rateHz) * 6.2831853f + phase * 6.2831853f);
    s = pow(s, 3.5f);

    // Add short "flash" pulses: narrow bright windows each cycle.
    float x = frac(time * rateHz + phase);
    float pulse = smoothstep(0.00f, 0.06f, x) * (1.0f - smoothstep(0.14f, 0.34f, x));

    // Blend between soft glitter and harder twinkle.
    float tw = lerp(s, max(s, pulse), saturate(flavor * 1.25f));

    // Map to brightness multiplier.
    // Keep baseline closer to 1.0 but push peaks higher so sparkles read clearly.
    // Clamp to keep HDR under control.
    float mul = 1.0f + intensity * (0.10f + 4.90f * tw);
    return min(mul, 1.0f + 7.0f * intensity);
}

// Simulates "crackling" by generating short, irregular on/off pulses in the particle lifetime.
// Returns (brightnessMultiplier, pulse01)
float2 CrackleFlicker(float age, float lifetime, uint seed)
{
    float t = (lifetime > 1e-5f) ? saturate(age / lifetime) : 1.0f;

    // 2–5 pulses total.
    float rP = Hash01(seed ^ 0xA2C79u);
    int pulses = 2 + (int) floor(rP * 4.0f); // 2..5

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
    float active = step(pulseId, (float) (pulses - 1));

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

    // Integrate motion.
    // Sparks & crackles: gravity + quadratic drag (like shells, but on GPU).
    // Smoke / others: keep existing simple linear drag so they stay floaty.
    if (p.Kind == 2 || p.Kind == 4)
    {
        // Spark or crackle
        float3 v = p.Velocity;

        // Gravity
        float3 accel = Gravity;

        // Quadratic drag opposite velocity
        float speed = length(v);
        if (speed > 1e-4f)
        {
            float3 dir = v / speed;
            float3 dragAccel = -dir * (SparkDragK * speed * speed);
            accel += dragAccel;
        }

        v += accel * dt;
        p.Velocity = v;
        p.Position += v * dt;
    }
    else
    {
        // Shell/smoke/other: old scheme (gravity + simple linear drag)
        float drag = 0.02f; // very small (almost vacuum)
        if (p.Kind == 3)
        {
            // Smoke has much higher drag so it slows quickly.
            drag = 2.2f;
        }
        p.Velocity += Gravity * dt;
        p.Velocity *= (1.0f / (1.0f + drag * dt));
        p.Position += p.Velocity * dt;
    }

    // Kill particles that fall below ground (y=0).
    // Prevents sparks/smoke rendering through the ground plane.
    if (p.Position.y < 0.0f)
    {
        p.Kind = 0;
        p.Color = float4(0, 0, 0, 0);
        Particles[i] = p;
        return;
    }

    if (p.Kind == 2)
    {
        p.Color = ColorRampSpark(t, p.Color);

        // Sparkle/twinkle: brightness-only modulation of burst particles.
        // Stored as float bits in pads.
        float sparkleRateHz = asfloat(p._pad.x);
        float sparkleIntensity = asfloat(p._pad.y);
        uint seed = p._pad.z ^ (i * 747796405u);
        float mul = SparkleMul(Time, sparkleRateHz, sparkleIntensity, seed);
        p.Color.rgb *= mul;
    }
    else if (p.Kind == 3)
    {
        float life01 = saturate(t);

        float3 startC = float3(0.35f, 0.33f, 0.30f);
        float3 endC = float3(0.24f, 0.25f, 0.27f);
        float3 c = lerp(startC, endC, life01);

        float a;
        if (life01 < 0.2f)
            a = smoothstep(0.0f, 0.2f, life01);
        else if (life01 > 0.8f)
            a = smoothstep(1.0f, 0.8f, life01);
        else
            a = 1.0f;

        a *= SmokeIntensity;
        p.Color = float4(c, a);
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
    float4 Color : COLOR0;
    float2 UV : TEXCOORD0;
    uint Kind : TEXCOORD1;
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
        o.UV = float2(0, 0);
        o.Kind = 0;
        return o;
    }

    // Smoke must be alpha blended only: drop it from additive pass.
    if (p.Kind == 3 && ParticlePass == 0)
    {
        o.Position = float4(0, 0, 0, 0);
        o.Color = float4(0, 0, 0, 0);
        o.UV = float2(0, 0);
        o.Kind = 0;
        return o;
    }

    float2 offsets[6] =
    {
        float2(-1, -1), float2(1, -1), float2(-1, 1),
        float2(1, -1), float2(1, 1), float2(-1, 1)
    };

    float2 uv = offsets[corner];

    float size = (p.Kind == 2) ? 0.10f : 0.20f;
    if (p.Kind == 3)
    {
        float life01 = (p.Lifetime > 1e-5f) ? saturate(p.Age / p.Lifetime) : 1.0f;
        float baseRadius = 0.4f;
        float maxRadius = 4.0f;
        float g = pow(life01, 0.7f);
        size = lerp(baseRadius, maxRadius, g);
    }
    if (p.Kind == 4)
    {
        // Use alpha as pulse indicator (set in CS). Flash size during peaks.
        float flash = step(0.75f, p.Color.a);
        size = CrackleBaseSize * lerp(1.0f, CrackleFlashSizeMul, flash);
    }

    float3 worldPos = p.Position + (CameraRightWS * (uv.x * size)) + (CameraUpWS * (uv.y * size));
    o.Position = mul(float4(worldPos, 1.0f), ViewProjection);
    o.Color = p.Color;
    o.UV = uv;
    o.Kind = p.Kind;
    return o;
}

float4 PSParticle(VSOut input) : SV_Target
{
    float4 c = input.Color;

    // Soft radial falloff for smoke only (cheap billboard).
    // Applying this to sparks/crackle made the burst effectively vanish.
    if (input.Kind == 3)
    {
        float2 p = input.UV;
        float r2 = dot(p, p);
        // Lower exponent => softer / more diffuse smoke edge
        float soft = exp(-r2 * 1.35f);
        // Make the corners die off faster so the quad boundary doesn't build up when many overlap.
        soft = soft * soft;
        // Fade both alpha and RGB so the quad doesn't look like a tinted rectangle
        // when multiple smoke particles overlap.
        c.rgb *= soft;
        c.a *= soft;

        // Hard cut extremely low contribution so quads don't stack into visible rectangles.
        // (Keeps the interior soft while zeroing the corners.)
        if (c.a < 0.01f)
            discard;

        // Alpha pass uses premultiplied blending; make sure smoke output is actually premultiplied.
        if (ParticlePass == 1)
        {
            c.rgb *= c.a;
        }
    }

    return c;
}
