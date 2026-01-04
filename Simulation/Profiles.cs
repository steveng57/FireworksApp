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
    Ring
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
    Vector3? RingAxis = null,
    float RingAxisRandomTiltDegrees = 0.0f);

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
    IReadOnlyDictionary<string, ColorScheme> ColorSchemes);
