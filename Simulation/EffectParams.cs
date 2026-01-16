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
        SubShellDelaySeconds: null);
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
    SubShellTrailProfile? TrailProfile = null)
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

    public SubShellTrailProfile Trail => TrailProfile ?? new SubShellTrailProfile(
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
    SubShellTrailProfile? TrailProfile = null)
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

    public SubShellTrailProfile Trail => TrailProfile ?? new SubShellTrailProfile(
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
