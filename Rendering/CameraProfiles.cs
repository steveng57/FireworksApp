using System;
using System.Collections.Generic;

namespace FireworksApp.Rendering;

public enum CameraProfileKind
{
    Standard,
    AerialOrbit
}

public sealed record class CameraProfile
{
    public string Id { get; }
    public CameraProfileKind Kind { get; }
    public float TargetHeightMeters { get; }
    public float DefaultDistanceMeters { get; }
    public float DefaultPitchRadians { get; }
    public float OrbitSpeedRadiansPerSecond { get; }
    public bool AllowPan { get; }

    public CameraProfile(
        string id,
        CameraProfileKind kind,
        float targetHeightMeters,
        float defaultDistanceMeters,
        float defaultPitchRadians,
        float orbitSpeedRadiansPerSecond = 0.0f,
        bool allowPan = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        Id = id;
        Kind = kind;
        TargetHeightMeters = targetHeightMeters;
        DefaultDistanceMeters = defaultDistanceMeters;
        DefaultPitchRadians = defaultPitchRadians;
        OrbitSpeedRadiansPerSecond = orbitSpeedRadiansPerSecond;
        AllowPan = allowPan;
    }
}

public static class CameraProfiles
{
    public const string StandardId = "standard";
    public const string AerialOrbitId = "aerial_orbit";

    public static CameraProfile Standard { get; } = new(
        StandardId,
        CameraProfileKind.Standard,
        75.0f,
        200.0f,
        0.15f,
        0.0f,
        true);

    public static CameraProfile AerialOrbit { get; } = new(
        AerialOrbitId,
        CameraProfileKind.AerialOrbit,
        75.0f,
        240.0f,
        0.25f,
        0.22f,
        true);

    public static IReadOnlyDictionary<string, CameraProfile> All { get; } =
        new Dictionary<string, CameraProfile>(StringComparer.OrdinalIgnoreCase)
        {
            { Standard.Id, Standard },
            { AerialOrbit.Id, AerialOrbit }
        };
}
