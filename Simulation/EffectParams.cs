using System;
using System.Numerics;
using System.Windows.Media;

namespace FireworksApp.Simulation;

public sealed record CometParams(
    int CometCount,
    float CometSpeedMin,
    float CometSpeedMax,
    float CometUpBias,
    float CometGravityScale,
    float CometDrag,
    float CometLifetimeSeconds,
    float CometLifetimeJitterSeconds,
    int TrailParticleCount,
    float TrailParticleLifetime,
    float TrailSpeed,
    float TrailSmokeChance,
    Vector4? TrailColor,
    SubShellId? SubShellProfileId = null,
    float? SubShellDelaySeconds = null,
    float SubShellDelayJitterSeconds = 0.0f)
{
    public static CometParams Defaults { get; } = new(
        CometCount: 30,
        CometSpeedMin: 15f,
        CometSpeedMax: 25f,
        CometUpBias: 0.30f,
        CometGravityScale: 0.8f,
        CometDrag: 0.06f,
        CometLifetimeSeconds: 4.0f,
        CometLifetimeJitterSeconds: 0.0f,
        TrailParticleCount: 8,
        TrailParticleLifetime: 0.5f,
        TrailSpeed: 4.0f,
        TrailSmokeChance: 0.20f,
        TrailColor: null,
        SubShellProfileId: null,
        SubShellDelaySeconds: null,
        SubShellDelayJitterSeconds: 0.0f);
}

public sealed record SilverDragonParams(
    int DragonCount,
    float SpeedMin,
    float SpeedMax,
    float UpBias,
    float LifetimeSeconds,
    float LifetimeJitterSeconds,
    float GravityScale,
    float Drag,
    float SpiralRadiusMeters,
    float SpiralRadiusGrowth,
    float AngularSpeedRadPerSec,
    float AngularSpeedJitterFraction,
    float TrailSpawnRate,
    int TrailParticleCount,
    float TrailParticleLifetimeSeconds,
    float TrailSpeed,
    float TrailSmokeChance,
    Vector4? TrailColor,
    bool EndExplosionEnabled,
    FireworkBurstShape EndExplosionBurstShape,
    int EndExplosionCount,
    float EndExplosionSpeed,
    float EndExplosionLifetimeSeconds)
{
    public static SilverDragonParams Defaults { get; } = new(
        DragonCount: 10,
        SpeedMin: 14.0f,
        SpeedMax: 22.0f,
        UpBias: 0.20f,
        LifetimeSeconds: 3.2f,
        LifetimeJitterSeconds: 0.6f,
        GravityScale: 0.85f,
        Drag: 0.05f,
        SpiralRadiusMeters: 0.24f,
        SpiralRadiusGrowth: 0.05f,
        AngularSpeedRadPerSec: 18.0f,
        AngularSpeedJitterFraction: 0.18f,
        TrailSpawnRate: 22.0f,
        TrailParticleCount: 20,
        TrailParticleLifetimeSeconds: 1.20f,
        TrailSpeed: 4.5f,
        TrailSmokeChance: 0.08f,
        TrailColor: null,
        EndExplosionEnabled: false,
        EndExplosionBurstShape: FireworkBurstShape.Peony,
        EndExplosionCount: 32,
        EndExplosionSpeed: 6.0f,
        EndExplosionLifetimeSeconds: 1.0f);
}

public sealed record SparklerLineTrailParams(
    float SparkRate,
    float SparkLifetimeSeconds,
    float SparkSpeed,
    float SparkDirectionJitter,
    float BrightnessScalar,
    int MinSpawnPerTick = 0)
{
    public static SparklerLineTrailParams Defaults { get; } = new(
        SparkRate: 180.0f,
        SparkLifetimeSeconds: 0.35f,
        SparkSpeed: 5.5f,
        SparkDirectionJitter: 0.30f,
        BrightnessScalar: 1.05f,
        MinSpawnPerTick: 0);
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

public sealed record FishParams(
    int SubShellCount,
    float SubShellSpeedMin,
    float SubShellSpeedMax,
    float SubShellLifetimeMinSeconds,
    float SubShellLifetimeMaxSeconds,
    float SubShellGravityScale,
    float SubShellDrag,

    int JerkCountMin,
    int JerkCountMax,
    float JerkIntervalMinSeconds,
    float JerkIntervalMaxSeconds,
    float JerkMaxAngleDegrees,
    float SpeedJitter,
    float UpBiasPerJerk,

    SparklerLineTrailParams Trail)
{
    public static FishParams Defaults { get; } = new(
        SubShellCount: 80,
        SubShellSpeedMin: 16.0f,
        SubShellSpeedMax: 26.0f,
        SubShellLifetimeMinSeconds: 1.8f,
        SubShellLifetimeMaxSeconds: 3.2f,
        SubShellGravityScale: 0.55f,
        SubShellDrag: 0.05f,

        JerkCountMin: 3,
        JerkCountMax: 8,
        JerkIntervalMinSeconds: 0.12f,
        JerkIntervalMaxSeconds: 0.40f,
        JerkMaxAngleDegrees: 45.0f,
        SpeedJitter: 0.12f,
        UpBiasPerJerk: 0.10f,

        Trail: SparklerLineTrailParams.Defaults with
        {
            SparkRate = 140.0f,
            SparkLifetimeSeconds = 0.55f,
            SparkSpeed = 2.2f,
            SparkDirectionJitter = 0.35f,
            MinSpawnPerTick = 24
        });
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
    float TrailSmokeChance = 0.15f,
    TrailProfile? TrailProfile = null)
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
        TrailSmokeChance: 0.15f,
        TrailProfile: null);

    public TrailProfile Trail => TrailProfile ?? new TrailProfile(
        Id: "subshell_trail_spoke_inline",
        ParticleCount: TrailParticleCount,
        ParticleLifetimeSeconds: TrailParticleLifetimeSeconds,
        Speed: TrailSpeed,
        SmokeChance: TrailSmokeChance,
        Color: new Vector4(1.0f, 0.8f, 0.55f, 1.0f));
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
    string? PopFlashColorSchemeId = null,
    TrailProfile? TrailProfile = null)
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
        PopFlashColorSchemeId: null,
        TrailProfile: null);

    public TrailProfile Trail => TrailProfile ?? new TrailProfile(
        Id: "subshell_trail_finale_inline",
        ParticleCount: TrailParticleCount,
        ParticleLifetimeSeconds: TrailParticleLifetime,
        Speed: TrailSpeed,
        SmokeChance: TrailSmokeChance,
        Color: new Vector4(1.0f, 0.75f, 0.4f, 1.0f));
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

public sealed record BlingParams(
    FireworkBurstShape CoreBurstShape,
    int? CoreParticleCount = null,
    float? CoreParticleLifetimeSeconds = null,
    float? CoreBurstSpeed = null,
    float? CoreBurstSparkleRateHz = null,
    float? CoreBurstSparkleIntensity = null,
    BurstEmissionSettings? CoreEmission = null,
    ColorSchemeId? CoreColorSchemeId = null,
    FireworkBurstShape RingBurstShape = FireworkBurstShape.Ring,
    int RingParticleCount = 1200,
    float RingBurstSpeed = 9.0f,
    float RingParticleLifetimeSeconds = 2.8f,
    float RingSparkleRateHz = 0.0f,
    float RingSparkleIntensity = 0.25f,
    ColorSchemeId? RingColorSchemeId = null,
    float RingDelaySeconds = 0.0f,
    CrackleStarProfile? RingCrackle = null)
{
    public static BlingParams Defaults { get; } = new(
        CoreBurstShape: FireworkBurstShape.Peony,
        CoreParticleCount: null,
        CoreParticleLifetimeSeconds: null,
        CoreBurstSpeed: null,
        CoreBurstSparkleRateHz: null,
        CoreBurstSparkleIntensity: null,
        CoreEmission: null,
        CoreColorSchemeId: null,
        RingBurstShape: FireworkBurstShape.Ring,
        RingParticleCount: 1100,
        RingBurstSpeed: 9.5f,
        RingParticleLifetimeSeconds: 3.0f,
        RingSparkleRateHz: 0.0f,
        RingSparkleIntensity: 0.25f,
        RingColorSchemeId: null,
        RingDelaySeconds: 0.0f,
        RingCrackle: null);

    public BurstEmissionSettings CoreEmissionSettings => CoreEmission ?? BurstEmissionSettings.Defaults;
    public CrackleStarProfile RingCrackleProfile => RingCrackle ?? CrackleStarProfile.Defaults;
}

public sealed record CrackleStarProfile(
    float CrackleStarProbability,
    int ClusterCountMin,
    int ClusterCountMax,
    float ClusterConeAngleDegrees,
    float MicroSpeedMulMin,
    float MicroSpeedMulMax,
    float MicroLifetimeMinSeconds,
    float MicroLifetimeMaxSeconds,
    float ClusterStaggerMaxSeconds,
    float NormalSparkMixProbability = 0.0f)
{
    public static CrackleStarProfile Defaults { get; } = new(
        CrackleStarProbability: 0.18f,
        ClusterCountMin: 6,
        ClusterCountMax: 14,
        ClusterConeAngleDegrees: 10.0f,
        MicroSpeedMulMin: 0.55f,
        MicroSpeedMulMax: 0.95f,
        MicroLifetimeMinSeconds: 0.035f,
        MicroLifetimeMaxSeconds: 0.11f,
        ClusterStaggerMaxSeconds: 0.22f,
        NormalSparkMixProbability: 0.10f
    );
}

public enum StrobeSpawnMode
{
    Immediate,
    Jittered
}

public sealed record StrobeParams(
    SubShellId SubShellProfileId,
    int StrobeCount,
    Color StrobeColor,
    float StrobeRadiusMeters,
    float StrobeLifetimeSeconds,
    float SpreadRadiusFraction,
    StrobeSpawnMode SpawnMode,
    float SpawnJitterSeconds)
{
    public static StrobeParams Defaults { get; } = new(
        SubShellProfileId: new SubShellId("subshell_strobe"),
        StrobeCount: 100,
        StrobeColor: Color.FromArgb(255, 255, 255, 255),
        StrobeRadiusMeters: 0.025f,
        StrobeLifetimeSeconds: 0.25f,
        SpreadRadiusFraction: 0.75f,
        SpawnMode: StrobeSpawnMode.Jittered,
        SpawnJitterSeconds: 0.15f);
}
