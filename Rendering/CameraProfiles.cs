using System;
using System.Collections.Generic;
using System.Numerics;

namespace FireworksApp.Rendering;

public enum CameraProfileKind
{
    Standard,
    AerialOrbit,
    GroundOrbit,
    FixedCinematic
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
    public float FieldOfViewRadians { get; }
    public Vector3 FixedPosition { get; }
    public Vector3 FixedTarget { get; }
    public bool EnableShake { get; }

    public CameraProfile(
        string id,
        CameraProfileKind kind,
        float targetHeightMeters,
        float defaultDistanceMeters,
        float defaultPitchRadians,
        float orbitSpeedRadiansPerSecond = 0.0f,
        bool allowPan = true,
        float fieldOfViewRadians = (float)(System.Math.PI / 4.0),
        Vector3 fixedPosition = default,
        Vector3 fixedTarget = default,
        bool enableShake = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        if (fieldOfViewRadians <= 0.01f || fieldOfViewRadians >= 3.10f)
            throw new ArgumentOutOfRangeException(nameof(fieldOfViewRadians), "Field of view must be between 0 and PI radians.");

        Id = id;
        Kind = kind;
        TargetHeightMeters = targetHeightMeters;
        DefaultDistanceMeters = defaultDistanceMeters;
        DefaultPitchRadians = defaultPitchRadians;
        OrbitSpeedRadiansPerSecond = orbitSpeedRadiansPerSecond;
        AllowPan = allowPan;
        FieldOfViewRadians = fieldOfViewRadians;
        FixedPosition = fixedPosition;
        FixedTarget = fixedTarget;
        EnableShake = enableShake;
    }
}

public static class CameraProfiles
{
    public const string StandardId = "standard";
    public const string AerialOrbitId = "aerial_orbit";
    public const string GroundOrbitId = "ground_orbit";
    public const string FixedCinematicId = "fixed_cinematic";

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
        true
        );


    public static CameraProfile GroundOrbit { get; } = new(
        GroundOrbitId,
        CameraProfileKind.GroundOrbit,
        targetHeightMeters: 75.0f,
        defaultDistanceMeters: 200.0f,
        defaultPitchRadians: -0.37f,
        orbitSpeedRadiansPerSecond: 0.22f,
        allowPan: true
        );

    // Stable, tripod-style shot: locked position/look, moderate FOV, no shake.
    public static CameraProfile FixedCinematic { get; } = new(
        FixedCinematicId,
        CameraProfileKind.FixedCinematic,
        targetHeightMeters: 8.0f,
        defaultDistanceMeters: 240.0f,
        defaultPitchRadians: 0.18f,
        orbitSpeedRadiansPerSecond: 0.0f,
        allowPan: false,
        fieldOfViewRadians: 52.0f * (float)(System.Math.PI / 180.0),
        fixedPosition: new Vector3(-10.0f, 10.5f, -22.0f),
        fixedTarget: new Vector3(0.0f, 8.0f, 0.0f),
        enableShake: false);

    public static IReadOnlyDictionary<string, CameraProfile> All { get; } =
        new Dictionary<string, CameraProfile>(StringComparer.OrdinalIgnoreCase)
        {
            { Standard.Id, Standard },
            { AerialOrbit.Id, AerialOrbit },
            { GroundOrbit.Id, GroundOrbit  },
            { FixedCinematic.Id, FixedCinematic },
        };
}
