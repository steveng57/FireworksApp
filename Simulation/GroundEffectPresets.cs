using System.Numerics;

namespace FireworksApp.Simulation;

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
