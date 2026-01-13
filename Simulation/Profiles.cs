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
    PeonyToWillow,
    FinaleSalute,
    Comet,
    SubShellSpokeWheelPop
}

public sealed record CometParams(
    int CometCount,
    float CometSpeedMin,
    float CometSpeedMax,
    float CometUpBias,
    float CometGravityScale,
    float CometDrag,
    float CometLifetimeSeconds,
    int TrailParticleCount,
    float TrailParticleLifetime,
    float TrailSpeed,
    float TrailSmokeChance,
    Vector4? TrailColor)
{
    public static CometParams Defaults { get; } = new(
        CometCount: 30,
        CometSpeedMin: 15f,
        CometSpeedMax: 25f,
        CometUpBias: 0.30f,
        CometGravityScale: 0.8f,
        CometDrag: 0.06f,
        CometLifetimeSeconds: 4.0f,
        TrailParticleCount: 8,
        TrailParticleLifetime: 0.5f,
        TrailSpeed: 4.0f,
        TrailSmokeChance: 0.20f,
        TrailColor: null); // null = use burst color
}

public sealed record SubShellSpokeWheelPopParams(
    int SubShellCount,
    float RingStartAngleDegrees,
    float RingEndAngleDegrees,
    float RingRadius,
    float SubShellSpeed,
    float SubShellFuseMinSeconds,
    float SubShellFuseMaxSeconds,
    int PopFlashParticleCount,
    float PopFlashLifetime,
    float PopFlashRadius,
    float PopFlashIntensity,
    float PopFlashFadeGamma,
    string? PopFlashColorSchemeId = null,
    Color[]? PopFlashColors = null,
    float SubShellGravityScale = 1.0f,
    float SubShellDrag = 0.05f,
    float AngleJitterDegrees = 0.0f,
    float TangentialSpeed = 0.0f,
    Vector3? RingAxis = null,
    float RingAxisRandomTiltDegrees = 0.0f,
    bool EnableSubShellTrails = true,
    int TrailParticleCount = 6,
    float TrailParticleLifetimeSeconds = 0.4f,
    float TrailSpeed = 3.0f,
    float TrailSmokeChance = 0.15f)
{
    public static SubShellSpokeWheelPopParams Defaults { get; } = new(
        SubShellCount: 12,
        RingStartAngleDegrees: 0.0f,
        RingEndAngleDegrees: 360.0f,
        RingRadius: 6.0f,
        SubShellSpeed: 16.0f,
        SubShellFuseMinSeconds: 0.25f,
        SubShellFuseMaxSeconds: 0.85f,
        PopFlashParticleCount: 2200,
        PopFlashLifetime: 0.35f,
        PopFlashRadius: 5.5f,
        PopFlashIntensity: 2.0f,
        PopFlashFadeGamma: 2.1f,
        PopFlashColorSchemeId: null,
        PopFlashColors: null,
        SubShellGravityScale: 1.0f,
        SubShellDrag: 0.08f,
        AngleJitterDegrees: 4.0f,
        TangentialSpeed: 2.5f,
        RingAxis: Vector3.UnitY,
        RingAxisRandomTiltDegrees: 18.0f,
        EnableSubShellTrails: true,
        TrailParticleCount: 6,
        TrailParticleLifetimeSeconds: 0.4f,
        TrailSpeed: 3.0f,
        TrailSmokeChance: 0.15f);
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
    float TrailSmokeChance = 0.15f,
    string? PopFlashColorSchemeId = null)
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
        TrailSmokeChance: 0.15f,
        PopFlashColorSchemeId: null);
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
    float? BurstSpeed = null,
    // Sparkle/twinkle for burst particles only (visual brightness modulation in shader).
    // Rate is in Hz (sparkles per second). Intensity is roughly 0..1 (can go higher for "glitter bombs").
        float BurstSparkleRateHz = 0.0f,
        float BurstSparkleIntensity = 0.0f,
        // Terminal behavior: allow shells to end without an explosion by fading out their trail.
        // If SuppressBurst is true, no burst particles are spawned when FuseTimeSeconds elapses.
        // If TerminalFadeOutSeconds > 0, the shell remains alive for that duration and its trail emission fades to zero.
        bool SuppressBurst = false,
        float TerminalFadeOutSeconds = 0.0f,
        // Shell trail emission parameters (used during flight/fall, not the burst).
        int TrailParticleCount = 12,
        float TrailParticleLifetimeSeconds = 0.6f,
        float TrailSpeed = 5.0f,
        float TrailSmokeChance = 0.2f,
        Vector3? RingAxis = null,
        float RingAxisRandomTiltDegrees = 0.0f,
        BurstEmissionSettings? Emission = null,
        FinaleSaluteParams? FinaleSalute = null,
        CometParams? Comet = null,
        PeonyToWillowParams? PeonyToWillow = null,
        SubShellSpokeWheelPopParams? SubShellSpokeWheelPop = null)
    {
        public BurstEmissionSettings EmissionSettings => Emission ?? BurstEmissionSettings.Defaults;
        public FinaleSaluteParams FinaleSaluteParams => FinaleSalute ?? Simulation.FinaleSaluteParams.Defaults;
        public CometParams CometParams => Comet ?? Simulation.CometParams.Defaults;
        public PeonyToWillowParams PeonyToWillowParams => PeonyToWillow ?? Simulation.PeonyToWillowParams.Defaults;
        public SubShellSpokeWheelPopParams SubShellSpokeWheelPopParams => SubShellSpokeWheelPop ?? Simulation.SubShellSpokeWheelPopParams.Defaults;
    }

public sealed record BurstEmissionSettings(
    int ChrysanthemumSpokeCount,
    float ChrysanthemumSpokeJitter,
    float WillowDownwardBlend,
    int PalmFrondCount,
    float PalmFrondConeAngleRadians,
    float PalmFrondJitterAngleRadians,
    float HorsetailDownwardBlend,
    float HorsetailMinDownDot,
    float HorsetailJitterAngleRadians)
{
    public static BurstEmissionSettings Defaults { get; } = new(
        ChrysanthemumSpokeCount: 24,
        ChrysanthemumSpokeJitter: 0.12f,
        WillowDownwardBlend: 0.35f,
        PalmFrondCount: 7,
        PalmFrondConeAngleRadians: 0.65f,
        PalmFrondJitterAngleRadians: 0.08f,
        HorsetailDownwardBlend: 0.75f,
        HorsetailMinDownDot: -0.25f,
        HorsetailJitterAngleRadians: 0.15f);
}

public sealed record PeonyToWillowParams(
    int PeonySparkCount,
    float PeonySpeedMin,
    float PeonySpeedMax,
    float PeonyLifetimeSeconds,
    float HandoffDelaySeconds,
    float HandoffFraction,
    float HandoffRandomness,
    string WillowSubshellProfileId,
    float WillowVelocityScale,
    float WillowGravityMultiplier,
    float WillowDragMultiplier,
    float WillowLifetimeMultiplier,
    float WillowBrightnessBoost,
    float WillowTrailSpawnRate,
    float WillowTrailSpeed)
{
    public static PeonyToWillowParams Defaults { get; } = new(
        PeonySparkCount: 50,
        PeonySpeedMin: 18f,
        PeonySpeedMax: 26f,
        PeonyLifetimeSeconds: 2.0f,
        HandoffDelaySeconds: 0.45f,
        HandoffFraction: 0.55f,
        HandoffRandomness: 0.15f,
        WillowSubshellProfileId: "subshell_basic_pop",
        WillowVelocityScale: 0.45f,
        WillowGravityMultiplier: 2.2f,
        WillowDragMultiplier: 2.4f,
        WillowLifetimeMultiplier: 2.8f,
        WillowBrightnessBoost: 1.1f,
        WillowTrailSpawnRate: 10f,
        WillowTrailSpeed: 2.5f);
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
    IReadOnlyDictionary<string, ColorScheme> ColorSchemes,
    IReadOnlyDictionary<string, SubShellProfile> SubShells);

public enum SubShellSpawnMode
{
    Sphere,
    Ring,
    Cone
}

public sealed record class SubShellProfile(
    string Id,
    string ShellProfileId,
    int Count,
    SubShellSpawnMode SpawnMode,
    float DelaySeconds,
    float InheritParentVelocity,
    float AddedSpeed,
    float DirectionJitter,
    float SpeedJitter,
    float PositionJitter,
    float ChildTimeScale,
    string? ColorSchemeId,
    FireworkBurstShape? BurstShapeOverride,
    float MinAltitudeToSpawn,
    int MaxSubshellDepth);

public sealed record class SubShellAttachment(
    string SubShellProfileId,
    float Probability = 1.0f,
    float Scale = 1.0f,
    int DepthBudget = 1);
