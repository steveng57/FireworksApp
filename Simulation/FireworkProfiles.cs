using System.Numerics;

namespace FireworksApp.Simulation;

public sealed record class CanisterType(
    string Id,
    float CaliberInches,
    float InnerDiameterMm,
    float TubeLengthMm,
    float MuzzleVelocityMin,
    float MuzzleVelocityMax,
    float NominalBurstHeightM,
    float ReloadSeconds);

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
    float BurstSparkleRateHz = 0.0f,
    float BurstSparkleIntensity = 0.0f,
    bool SuppressBurst = false,
    float TerminalFadeOutSeconds = 0.0f,
    TrailProfile? TrailProfile = null,
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

    public TrailProfile Trail => TrailProfile ?? ShellTrailPresets.Default;
    public int TrailParticleCount => Trail.ParticleCount;
    public float TrailParticleLifetimeSeconds => Trail.ParticleLifetimeSeconds;
    public float TrailSpeed => Trail.Speed;
    public float TrailSmokeChance => Trail.SmokeChance;
    public Vector4 TrailColor => Trail.Color;
}
