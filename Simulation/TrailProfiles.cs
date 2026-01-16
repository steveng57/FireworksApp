using System.Numerics;

namespace FireworksApp.Simulation;

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

public sealed record class SubShellTrailProfile(
    string Id,
    int ParticleCount,
    float ParticleLifetimeSeconds,
    float Speed,
    float SmokeChance,
    Vector4 Color);

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

public static class SubShellTrailPresets
{
    private static readonly Vector4 FinaleTrailColor = new(1.0f, 0.75f, 0.4f, 1.0f);
    private static readonly Vector4 SpokeTrailColor = new(1.0f, 0.8f, 0.55f, 1.0f);

    public static SubShellTrailProfile FinaleDefault => new(
        Id: "subshell_trail_finale",
        ParticleCount: 6,
        ParticleLifetimeSeconds: 0.4f,
        Speed: 3.0f,
        SmokeChance: 0.15f,
        Color: FinaleTrailColor);

    public static SubShellTrailProfile SpokeWheel => new(
        Id: "subshell_trail_spoke_wheel",
        ParticleCount: 6,
        ParticleLifetimeSeconds: 0.4f,
        Speed: 3.0f,
        SmokeChance: 0.15f,
        Color: SpokeTrailColor);
}
