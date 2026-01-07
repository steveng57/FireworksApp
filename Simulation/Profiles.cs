using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;

namespace FireworksApp.Simulation;

public enum FireworkBurstShape
{
    Peony,
    Chrysanthemum,
    Willow,
    Palm,
    Ring,
    Horsetail,
    DoubleRing,
    Spiral,
    FinaleSalute
}

public sealed record FinaleSaluteParams(
    int SubShellCount,
    float SubShellSpeedMin,
    float SubShellSpeedMax,
    float SubShellUpBias,
    float SubShellGravityScale,
    float SubShellDrag,
    float DetonateDelayMin,
    float DetonateDelayMax,
    float DetonateJitterMax,
    int SparkParticleCount,
    float PopFlashLifetime,
    float PopFlashSize,
    float PopPeakIntensity,
    float PopFadeGamma,
    bool EnableSubShellTrails = true,
    int TrailParticleCount = 6,
    float TrailParticleLifetime = 0.4f,
    float TrailSpeed = 3.0f,
    float TrailSmokeChance = 0.15f)
{
    public static FinaleSaluteParams Defaults { get; } = new(
        SubShellCount: 50,
        SubShellSpeedMin: 18f,
        SubShellSpeedMax: 30f,
        SubShellUpBias: 0.20f,
        SubShellGravityScale: 1.0f,
        SubShellDrag: 0.04f,
        DetonateDelayMin: 2.0f,
        DetonateDelayMax: 3.0f,
        DetonateJitterMax: 0.10f,
        SparkParticleCount: 8000,
        PopFlashLifetime: 0.12f,
        PopFlashSize: 1.2f,
        PopPeakIntensity: 8.0f,
        PopFadeGamma: 2.2f,
        EnableSubShellTrails: true,
        TrailParticleCount: 6,
        TrailParticleLifetime: 0.4f,
        TrailSpeed: 3.0f,
        TrailSmokeChance: 0.15f);
}

public enum GroundEffectType
{
    Fountain,
    Spinner,
    Strobe,
    Mine,

    // New ground effect families
    BengalFlare,
    LanceworkPanel,
    WaterfallCurtain,
    ChaserLine,
    GroundBloom,
    PulsingGlitterFountain
}

public sealed record CanisterType(
    string Id,
    float CaliberInches,
    float InnerDiameterMm,
    float TubeLengthMm,
    float MuzzleVelocityMin,
    float MuzzleVelocityMax,
    float NominalBurstHeightM,
    float ReloadSeconds
);

public sealed record class CanisterProfile(
    string Id,
    string CanisterTypeId,
    Vector2 Position,
    Vector3 LaunchDirection,
    string DefaultShellProfileId);

public sealed record class FireworkShellProfile(
    string Id,
    FireworkBurstShape BurstShape,
    string ColorSchemeId,
    float FuseTimeSeconds,
    float ExplosionRadius,
    int ParticleCount,
    float ParticleLifetimeSeconds,
    // Sparkle/twinkle for burst particles only (visual brightness modulation in shader).
    // Rate is in Hz (sparkles per second). Intensity is roughly 0..1 (can go higher for "glitter bombs").
    float BurstSparkleRateHz = 0.0f,
    float BurstSparkleIntensity = 0.0f,
    Vector3? RingAxis = null,
    float RingAxisRandomTiltDegrees = 0.0f,
    FinaleSaluteParams? FinaleSalute = null)
{
    public FinaleSaluteParams FinaleSaluteParams => FinaleSalute ?? Simulation.FinaleSaluteParams.Defaults;
}

public sealed record class GroundEffectProfile(
    string Id,
    GroundEffectType Type,
    string ColorSchemeId,
    float DurationSeconds,
    float EmissionRate,
    Vector2 ParticleVelocityRange,
    float ParticleLifetimeSeconds,
    float GravityFactor,
    float BrightnessScalar,
    float HeightOffsetMeters = 0.0f,
    float ConeAngleDegrees = 35.0f,
    float FlickerIntensity = 0.08f,
    float AngularVelocityRadiansPerSec = 6.0f,
    float EmissionRadius = 0.15f,
    Vector3? SpinnerAxis = null,
    float FlashIntervalSeconds = 0.22f,
    float FlashDutyCycle = 0.35f,
    float FlashBrightness = 1.8f,
    float ResidualSparkDensity = 0.18f,
    float BurstRate = 2.0f,
    int ParticlesPerBurst = 1200,
    float SmokeAmount = 0.0f,

    // --- Extended parameters used by newer GroundEffectTypes ---
    // Bengal flare / flame pot
    float FlameHeightMeters = 1.2f,
    float FlameNoiseAmplitude = 0.15f,
    float LocalLightRadiusMeters = 5.0f,
    float LocalLightIntensity = 1.0f,
    float OccasionalSparkRate = 0.0f,

    // Lancework / set piece panel
    int GridWidth = 0,
    int GridHeight = 0,
    ulong[]? PatternFrames = null,
    float PatternFrameDurationSeconds = 0.25f,
    float CellFlameHeightMeters = 0.35f,
    float CellFlickerAmount = 0.10f,

    // Waterfall / curtain
    int EmitterCount = 0,
    float CurtainWidthMeters = 8.0f,
    float EmitterHeightMeters = 4.0f,
    float SparkFallSpeed = 4.0f,
    float DensityOverTime = 1.0f,

    // Ground chaser line
    int PointCount = 0,
    float PointSpacingMeters = 0.5f,
    float ChaseSpeed = 4.0f,
    GroundEffectType? EffectPerPoint = null,
    int BurstParticlesPerPoint = 900,
    float BurstVelocity = 8.0f,
    bool ReverseOrBounce = false,

    // Ground bloom flower variant
    float SpinRateOverTime = 0.0f,
    Vector3 GroundDriftVelocity = default,
    Vector4[]? ColorPhases = null,

    // Pulsing glitter fountain
    float PulseFrequencyHz = 6.0f,
    float PulseDepth = 0.7f,
    float GlitterParticleRatio = 0.35f,
    float GlowDecayTimeSeconds = 0.12f);

public sealed class GroundEffectInstance
{
    public GroundEffectProfile Profile { get; }
    public Canister Canister { get; }
    public ColorScheme ColorScheme { get; }

    public float StartTimeSeconds { get; }
    public float DurationSeconds { get; }

    public float ElapsedSeconds { get; private set; }
    public float EmissionAccumulator { get; private set; }
    public int BurstCounter { get; private set; }

    public bool Alive => ElapsedSeconds < DurationSeconds;

    public GroundEffectInstance(GroundEffectProfile profile, Canister canister, ColorScheme colorScheme, float startTimeSeconds)
    {
        Profile = profile;
        Canister = canister;
        ColorScheme = colorScheme;
        StartTimeSeconds = startTimeSeconds;
        DurationSeconds = System.Math.Max(0.0f, profile.DurationSeconds);
    }

    public void Update(float dt)
    {
        if (dt <= 0.0f)
            return;

        ElapsedSeconds += dt;
    }

    public void AddEmission(float particlesToSpawn)
    {
        EmissionAccumulator += particlesToSpawn;
    }

    public int ConsumeWholeParticles()
    {
        int n = (int)EmissionAccumulator;
        EmissionAccumulator -= n;
        return n;
    }

    public int NextBurstIndex() => BurstCounter++;
}

public sealed record class ColorScheme(
    string Id,
    Color[] BaseColors,
    float ColorVariation,
    float FadeOutSeconds)
{
    public ColorScheme(string id, IReadOnlyList<Color> baseColors, float colorVariation, float fadeOutSeconds)
        : this(id, baseColors is Color[] arr ? arr : new List<Color>(baseColors).ToArray(), colorVariation, fadeOutSeconds)
    {
    }
}

public sealed record class FireworksProfileSet(
    IReadOnlyDictionary<string, CanisterProfile> Canisters,
    IReadOnlyDictionary<string, FireworkShellProfile> Shells,
    IReadOnlyDictionary<string, GroundEffectProfile> GroundEffects,
    IReadOnlyDictionary<string, ColorScheme> ColorSchemes);
