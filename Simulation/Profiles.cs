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

public sealed record class CanisterProfile(
    string Id,
    Vector2 Position,
    Vector3 LaunchDirection,
    float MuzzleVelocity,
    float ReloadTimeSeconds,
    string DefaultShellProfileId);

public sealed record class FireworkShellProfile(
    string Id,
    FireworkBurstShape BurstShape,
    string ColorSchemeId,
    float FuseTimeSeconds,
    float ExplosionRadius,
    int ParticleCount,
    float ParticleLifetimeSeconds);

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
