using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Media;

namespace FireworksApp.Simulation;

public enum FireworkBurstShape
{
    Peony,
    SparklingChrysanthemum,
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

public static class GroundEffectPresets
{
    public static GroundEffectProfile Spinner(
        string id,
        ColorSchemeId colorSchemeId,
        float durationSeconds,
        float emissionRate,
        Vector2 particleVelocityRange,
        float particleLifetimeSeconds,
        float gravityFactor,
        float brightnessScalar,
        float heightOffsetMeters,
        float angularVelocityRadiansPerSec,
        float emissionRadius,
        Vector3 spinnerAxis,
        float smokeAmount) => new(
            Id: id,
            Type: GroundEffectType.Spinner,
            ColorSchemeId: colorSchemeId,
            DurationSeconds: durationSeconds,
            EmissionRate: emissionRate,
            ParticleVelocityRange: particleVelocityRange,
            ParticleLifetimeSeconds: particleLifetimeSeconds,
            GravityFactor: gravityFactor,
            BrightnessScalar: brightnessScalar,
            HeightOffsetMeters: heightOffsetMeters,
            ConeAngleDegrees: 35.0f,
            FlickerIntensity: 0.08f,
            AngularVelocityRadiansPerSec: angularVelocityRadiansPerSec,
            EmissionRadius: emissionRadius,
            SpinnerAxis: spinnerAxis,
            SmokeAmount: smokeAmount);

    public static GroundEffectProfile Fountain(
        string id,
        ColorSchemeId colorSchemeId,
        float durationSeconds,
        float emissionRate,
        Vector2 particleVelocityRange,
        float particleLifetimeSeconds,
        float gravityFactor,
        float brightnessScalar,
        float coneAngleDegrees,
        float flickerIntensity,
        float smokeAmount) => new(
            Id: id,
            Type: GroundEffectType.Fountain,
            ColorSchemeId: colorSchemeId,
            DurationSeconds: durationSeconds,
            EmissionRate: emissionRate,
            ParticleVelocityRange: particleVelocityRange,
            ParticleLifetimeSeconds: particleLifetimeSeconds,
            GravityFactor: gravityFactor,
            BrightnessScalar: brightnessScalar,
            ConeAngleDegrees: coneAngleDegrees,
            FlickerIntensity: flickerIntensity,
            SmokeAmount: smokeAmount);
}

public readonly record struct ShellId
{
    public string Value { get; }

    public ShellId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString() => Value;

    public static implicit operator string(ShellId id) => id.Value;
    public static implicit operator ShellId(string value) => new(value);
}

public readonly record struct SubShellId
{
    public string Value { get; }

    public SubShellId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString() => Value;

    public static implicit operator string(SubShellId id) => id.Value;
    public static implicit operator SubShellId(string value) => new(value);
}

public readonly record struct ColorSchemeId
{
    public string Value { get; }

    public ColorSchemeId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString() => Value;

    public static implicit operator string(ColorSchemeId id) => id.Value;
    public static implicit operator ColorSchemeId(string value) => new(value);
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
    Vector4? TrailColor,
    SubShellId? SubShellProfileId = null,
    float? SubShellDelaySeconds = null)
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
        TrailColor: null,
        SubShellProfileId: null,
        SubShellDelaySeconds: null); // null = use burst color
}

public sealed record SparklerLineTrailParams(
    float SparkRate,
    float SparkLifetimeSeconds,
    float SparkSpeed,
    float SparkDirectionJitter,
    float BrightnessScalar)
{
    public static SparklerLineTrailParams Defaults { get; } = new(
        SparkRate: 180.0f,
        SparkLifetimeSeconds: 0.35f,
        SparkSpeed: 5.5f,
        SparkDirectionJitter: 0.30f,
        BrightnessScalar: 1.05f);
}

public sealed record SparklingChrysanthemumParams(
    int SubShellCount,
    float SubShellSpeedMin,
    float SubShellSpeedMax,
    float SubShellLifetimeMinSeconds,
    float SubShellLifetimeMaxSeconds,
    float SubShellGravityScale,
    float SubShellDrag,
    SparklerLineTrailParams Trail)
{
    public static SparklingChrysanthemumParams Defaults { get; } = new(
        SubShellCount: 100,
        SubShellSpeedMin: 18.0f,
        SubShellSpeedMax: 26.0f,
        SubShellLifetimeMinSeconds: 2.6f,
        SubShellLifetimeMaxSeconds: 4.4f,
        SubShellGravityScale: 0.65f,
        SubShellDrag: 0.05f,
        Trail: SparklerLineTrailParams.Defaults);
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
        PopFlashLifetime: 0.12f,
        PopFlashRadius: 1.2f,
        PopFlashIntensity: 8.0f,
        PopFlashFadeGamma: 2.2f,
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
    ShellId DefaultShellProfileId);

public sealed record class FireworkShellProfile(
    string Id,
    FireworkBurstShape BurstShape,
    ColorSchemeId ColorSchemeId,
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
        ShellTrailProfile? TrailProfile = null,
        Vector3? RingAxis = null,
        float RingAxisRandomTiltDegrees = 0.0f,
        BurstEmissionSettings? Emission = null,
        FinaleSaluteParams? FinaleSalute = null,
        CometParams? Comet = null,
        PeonyToWillowParams? PeonyToWillow = null,
        SubShellSpokeWheelPopParams? SubShellSpokeWheelPop = null,
        SparklingChrysanthemumParams? SparklingChrysanthemum = null)
    {
        public BurstEmissionSettings EmissionSettings => Emission ?? BurstEmissionSettings.Defaults;
        public FinaleSaluteParams FinaleSaluteParams => FinaleSalute ?? Simulation.FinaleSaluteParams.Defaults;
        public CometParams CometParams => Comet ?? Simulation.CometParams.Defaults;
        public PeonyToWillowParams PeonyToWillowParams => PeonyToWillow ?? Simulation.PeonyToWillowParams.Defaults;
        public SubShellSpokeWheelPopParams SubShellSpokeWheelPopParams => SubShellSpokeWheelPop ?? Simulation.SubShellSpokeWheelPopParams.Defaults;
        public SparklingChrysanthemumParams SparklingChrysanthemumParams => SparklingChrysanthemum ?? Simulation.SparklingChrysanthemumParams.Defaults;

        public ShellTrailProfile Trail => TrailProfile ?? ShellTrailPresets.Default;
        public int TrailParticleCount => Trail.ParticleCount;
        public float TrailParticleLifetimeSeconds => Trail.ParticleLifetimeSeconds;
        public float TrailSpeed => Trail.Speed;
        public float TrailSmokeChance => Trail.SmokeChance;
        public Vector4 TrailColor => Trail.Color;
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
    SubShellId WillowSubshellProfileId,
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
        WillowSubshellProfileId: new SubShellId("subshell_basic_pop"),
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
    ColorSchemeId ColorSchemeId,
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

public static class SubShellPresets
{
    public static SubShellProfile Sphere(
        string id,
        ShellId shellProfileId,
        int count,
        float minAltitudeToSpawn,
        float delaySeconds = 0.15f,
        float inheritParentVelocity = 0.2f,
        float addedSpeed = 18.0f,
        float directionJitter = 0.08f,
        float speedJitter = 0.25f,
        float positionJitter = 0.6f,
        float childTimeScale = 1.0f,
        ColorSchemeId? colorSchemeId = null,
        FireworkBurstShape? burstShapeOverride = null,
        int maxSubshellDepth = 1) => new(
            Id: id,
            ShellProfileId: shellProfileId,
            Count: count,
            SpawnMode: SubShellSpawnMode.Sphere,
            DelaySeconds: delaySeconds,
            InheritParentVelocity: inheritParentVelocity,
            AddedSpeed: addedSpeed,
            DirectionJitter: directionJitter,
            SpeedJitter: speedJitter,
            PositionJitter: positionJitter,
            ChildTimeScale: childTimeScale,
            ColorSchemeId: colorSchemeId,
            BurstShapeOverride: burstShapeOverride,
            MinAltitudeToSpawn: minAltitudeToSpawn,
            MaxSubshellDepth: maxSubshellDepth);

    public static SubShellProfile Ring(
        string id,
        ShellId shellProfileId,
        int count,
        float minAltitudeToSpawn,
        float delaySeconds = 0.10f,
        float inheritParentVelocity = 0.1f,
        float addedSpeed = 12.0f,
        float directionJitter = 0.05f,
        float speedJitter = 0.20f,
        float positionJitter = 0.4f,
        float childTimeScale = 1.0f,
        ColorSchemeId? colorSchemeId = null,
        FireworkBurstShape? burstShapeOverride = null,
        int maxSubshellDepth = 1) => new(
            Id: id,
            ShellProfileId: shellProfileId,
            Count: count,
            SpawnMode: SubShellSpawnMode.Ring,
            DelaySeconds: delaySeconds,
            InheritParentVelocity: inheritParentVelocity,
            AddedSpeed: addedSpeed,
            DirectionJitter: directionJitter,
            SpeedJitter: speedJitter,
            PositionJitter: positionJitter,
            ChildTimeScale: childTimeScale,
            ColorSchemeId: colorSchemeId,
            BurstShapeOverride: burstShapeOverride,
            MinAltitudeToSpawn: minAltitudeToSpawn,
            MaxSubshellDepth: maxSubshellDepth);
}

public static class FireworkShellDefaults
{
    public const int TrailParticleCount = 12;
    public const float TrailParticleLifetimeSeconds = 0.6f;
    public const float TrailSpeed = 5.0f;
    public const float TrailSmokeChance = 0.2f;
    public const float BurstSparkleRateHz = 0.0f;
    public const float BurstSparkleIntensity = 0.0f;
}

public readonly record struct ShellTrailParams(int Count, float LifetimeSeconds, float Speed, float SmokeChance);

public sealed record class ShellTrailProfile(
    string Id,
    int ParticleCount,
    float ParticleLifetimeSeconds,
    float Speed,
    float SmokeChance,
    Vector4 Color);

public static class ShellTrailPresets
{
    private static readonly Vector4 DefaultTrailColor = new(1.0f, 0.85f, 0.5f, 1.0f);

    public static ShellTrailProfile Default => new(
        Id: "trail_default",
        ParticleCount: FireworkShellDefaults.TrailParticleCount,
        ParticleLifetimeSeconds: FireworkShellDefaults.TrailParticleLifetimeSeconds,
        Speed: FireworkShellDefaults.TrailSpeed,
        SmokeChance: FireworkShellDefaults.TrailSmokeChance,
        Color: DefaultTrailColor);

    public static ShellTrailProfile ShortBright => new(
        Id: "trail_short_bright",
        ParticleCount: 10,
        ParticleLifetimeSeconds: 0.5f,
        Speed: 4.0f,
        SmokeChance: 0.15f,
        Color: DefaultTrailColor);

    public static ShellTrailProfile WillowLingering => new(
        Id: "trail_willow_lingering",
        ParticleCount: 12,
        ParticleLifetimeSeconds: 0.8f,
        Speed: 5.0f,
        SmokeChance: 0.2f,
        Color: DefaultTrailColor);

    public static ShellTrailProfile CometNeon => new(
        Id: "trail_comet_neon",
        ParticleCount: 12,
        ParticleLifetimeSeconds: 0.5f,
        Speed: 5.0f,
        SmokeChance: 0.18f,
        Color: DefaultTrailColor);
}

public static class ShellPresets
{
    public static FireworkShellProfile Create(
        string id,
        FireworkBurstShape burstShape,
        ColorSchemeId colorSchemeId,
        float fuseTimeSeconds,
        float explosionRadius,
        int particleCount,
        float particleLifetimeSeconds,
        float? burstSpeed = null,
        float? burstSparkleRateHz = null,
        float? burstSparkleIntensity = null,
        bool suppressBurst = false,
        float terminalFadeOutSeconds = 0.0f,
        ShellTrailProfile? trailProfile = null,
        Vector3? ringAxis = null,
        float ringAxisRandomTiltDegrees = 0.0f,
        BurstEmissionSettings? emission = null,
        FinaleSaluteParams? finaleSalute = null,
        CometParams? comet = null,
        PeonyToWillowParams? peonyToWillow = null,
        SubShellSpokeWheelPopParams? subShellSpokeWheelPop = null,
        SparklingChrysanthemumParams? sparklingChrysanthemum = null) => new(
            Id: id,
            BurstShape: burstShape,
            ColorSchemeId: colorSchemeId,
            FuseTimeSeconds: fuseTimeSeconds,
            ExplosionRadius: explosionRadius,
            ParticleCount: particleCount,
            ParticleLifetimeSeconds: particleLifetimeSeconds,
            BurstSpeed: burstSpeed,
            BurstSparkleRateHz: burstSparkleRateHz ?? FireworkShellDefaults.BurstSparkleRateHz,
            BurstSparkleIntensity: burstSparkleIntensity ?? FireworkShellDefaults.BurstSparkleIntensity,
            SuppressBurst: suppressBurst,
            TerminalFadeOutSeconds: terminalFadeOutSeconds,
            TrailProfile: trailProfile ?? ShellTrailPresets.Default,
            RingAxis: ringAxis,
            RingAxisRandomTiltDegrees: ringAxisRandomTiltDegrees,
            Emission: emission,
            FinaleSalute: finaleSalute,
            Comet: comet,
            PeonyToWillow: peonyToWillow,
            SubShellSpokeWheelPop: subShellSpokeWheelPop,
            SparklingChrysanthemum: sparklingChrysanthemum);
}

public static class ProfileValidator
{
    public static void Validate(FireworksProfileSet profileSet)
    {
        ArgumentNullException.ThrowIfNull(profileSet);

        var shells = profileSet.Shells;
        var subshells = profileSet.SubShells;
        var colorSchemes = profileSet.ColorSchemes;
        var trailProfiles = profileSet.TrailProfiles;

        foreach (var canister in profileSet.Canisters.Values)
        {
            EnsureExists(shells, canister.DefaultShellProfileId, $"Canister {canister.Id} references missing shell profile");
        }

        foreach (var shell in shells.Values)
        {
            EnsureExists(colorSchemes, shell.ColorSchemeId, $"Shell {shell.Id} references missing color scheme");

            if (trailProfiles.Count > 0)
            {
                EnsureExists(trailProfiles, shell.Trail.Id, $"Shell {shell.Id} references missing trail profile {shell.Trail.Id}");
            }

            if (shell.PeonyToWillow is { } peonyToWillow)
            {
                EnsureExists(subshells, peonyToWillow.WillowSubshellProfileId, $"Shell {shell.Id} references missing subshell profile {peonyToWillow.WillowSubshellProfileId}");
            }

            if (shell.Comet is { SubShellProfileId: { } cometSubshell })
            {
                EnsureExists(subshells, cometSubshell, $"Shell {shell.Id} comet references missing subshell profile {cometSubshell}");
            }

            if (shell.SubShellSpokeWheelPop is { PopFlashColorSchemeId: { } popFlashScheme })
            {
                EnsureExists(colorSchemes, popFlashScheme, $"Shell {shell.Id} spoke pop references missing color scheme {popFlashScheme}");
            }
        }

        foreach (var subshell in subshells.Values)
        {
            EnsureExists(shells, subshell.ShellProfileId, $"Subshell {subshell.Id} references missing shell profile {subshell.ShellProfileId}");

            if (subshell.ColorSchemeId is { } subshellScheme)
            {
                EnsureExists(colorSchemes, subshellScheme, $"Subshell {subshell.Id} references missing color scheme {subshellScheme}");
            }
        }

        foreach (var groundEffect in profileSet.GroundEffects.Values)
        {
            EnsureExists(colorSchemes, groundEffect.ColorSchemeId, $"Ground effect {groundEffect.Id} references missing color scheme");
        }

        DetectShellSubshellCycles(shells, subshells);

        LogSummary(profileSet);
        LogDetails(profileSet);
    }

    private static void DetectShellSubshellCycles(
        IReadOnlyDictionary<string, FireworkShellProfile> shells,
        IReadOnlyDictionary<string, SubShellProfile> subshells)
    {
        var adjacency = new Dictionary<string, List<string>>();

        static string ShellNode(string id) => $"shell:{id}";
        static string SubshellNode(string id) => $"subshell:{id}";

        void AddEdge(string from, string to)
        {
            if (!adjacency.TryGetValue(from, out var list))
            {
                list = new List<string>();
                adjacency[from] = list;
            }
            list.Add(to);
        }

        foreach (var shell in shells.Values)
        {
            var shellNode = ShellNode(shell.Id);

            if (shell.PeonyToWillow is { } peonyToWillow)
            {
                AddEdge(shellNode, SubshellNode(peonyToWillow.WillowSubshellProfileId));
            }

            if (shell.Comet is { SubShellProfileId: { } cometSubshell })
            {
                AddEdge(shellNode, SubshellNode(cometSubshell));
            }
        }

        foreach (var subshell in subshells.Values)
        {
            AddEdge(SubshellNode(subshell.Id), ShellNode(subshell.ShellProfileId));
        }

        var visited = new HashSet<string>();
        var stack = new Stack<string>();
        var inPath = new HashSet<string>();

        void Dfs(string node)
        {
            if (!inPath.Add(node))
            {
                throw new InvalidOperationException($"Cycle detected in shell/subshell references: {string.Join(" -> ", stack.Reverse().Append(node))}");
            }

            visited.Add(node);
            stack.Push(node);

            if (adjacency.TryGetValue(node, out var next))
            {
                foreach (var neighbor in next)
                {
                    if (!visited.Contains(neighbor))
                        Dfs(neighbor);
                    else if (inPath.Contains(neighbor))
                        throw new InvalidOperationException($"Cycle detected in shell/subshell references: {string.Join(" -> ", stack.Reverse().Append(neighbor))}");
                }
            }

            stack.Pop();
            inPath.Remove(node);
        }

        foreach (var node in adjacency.Keys)
        {
            if (!visited.Contains(node))
            {
                Dfs(node);
            }
        }
    }

    private static void EnsureExists<T>(
        IReadOnlyDictionary<string, T> dictionary,
        string id,
        string message)
    {
        if (!dictionary.ContainsKey(id))
            throw new InvalidOperationException(message);
    }

    [Conditional("DEBUG")]
    public static void LogSummary(FireworksProfileSet profileSet)
    {
        ArgumentNullException.ThrowIfNull(profileSet);
        Debug.WriteLine($"[Profiles] Canisters={profileSet.Canisters.Count}, Shells={profileSet.Shells.Count}, SubShells={profileSet.SubShells.Count}, GroundEffects={profileSet.GroundEffects.Count}, ColorSchemes={profileSet.ColorSchemes.Count}, TrailProfiles={profileSet.TrailProfiles.Count}");
    }

    [Conditional("DEBUG")]
    public static void LogDetails(FireworksProfileSet profileSet)
    {
        ArgumentNullException.ThrowIfNull(profileSet);

        var shapeCounts = new Dictionary<FireworkBurstShape, int>();
        foreach (var shell in profileSet.Shells.Values)
        {
            var shape = shell.BurstShape;
            shapeCounts[shape] = shapeCounts.TryGetValue(shape, out var n) ? n + 1 : 1;
        }

        var groundTypeCounts = new Dictionary<GroundEffectType, int>();
        foreach (var ge in profileSet.GroundEffects.Values)
        {
            var type = ge.Type;
            groundTypeCounts[type] = groundTypeCounts.TryGetValue(type, out var n) ? n + 1 : 1;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("[Profiles] Shapes:");
        foreach (var kvp in shapeCounts)
        {
            sb.Append(' ').Append(kvp.Key).Append('=').Append(kvp.Value).Append(';');
        }

        sb.Append(" GroundTypes:");
        foreach (var kvp in groundTypeCounts)
        {
            sb.Append(' ').Append(kvp.Key).Append('=').Append(kvp.Value).Append(';');
        }

        Debug.WriteLine(sb.ToString());
    }
}

public sealed record class FireworksProfileSet(
    IReadOnlyDictionary<string, CanisterProfile> Canisters,
    IReadOnlyDictionary<string, FireworkShellProfile> Shells,
    IReadOnlyDictionary<string, GroundEffectProfile> GroundEffects,
    IReadOnlyDictionary<string, ColorScheme> ColorSchemes,
    IReadOnlyDictionary<string, SubShellProfile> SubShells,
    IReadOnlyDictionary<string, ShellTrailProfile> TrailProfiles);

public enum SubShellSpawnMode
{
    Sphere,
    Ring,
    Cone
}

public sealed record class SubShellProfile(
    string Id,
    ShellId ShellProfileId,
    int Count,
    SubShellSpawnMode SpawnMode,
    float DelaySeconds,
    float InheritParentVelocity,
    float AddedSpeed,
    float DirectionJitter,
    float SpeedJitter,
    float PositionJitter,
    float ChildTimeScale,
    ColorSchemeId? ColorSchemeId,
    FireworkBurstShape? BurstShapeOverride,
    float MinAltitudeToSpawn,
    int MaxSubshellDepth);

public sealed record class SubShellAttachment(
    SubShellId SubShellProfileId,
    float Probability = 1.0f,
    float Scale = 1.0f,
    int DepthBudget = 1);
