using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FireworksApp.Rendering;

namespace FireworksApp.Simulation;

using Math = System.Math;

public sealed class FireworksEngine
{
    private readonly FireworksProfileSet _profiles;
    private readonly List<Canister> _canisters;
    private readonly List<FireworkShell> _shells = new();
    private readonly Random _rng = new();

    private ShowScript _show = ShowScript.Empty;
    private int _nextEventIndex;

    // *** CHANGED: drag for shells (must match FireworkShell.Update usage)
    private const float ShellDragK = 0.020f;

    public float ShowTimeSeconds { get; private set; }

    // Global time scaling: 1.0 = normal, 0.8 = 20% slower, etc.
    public float TimeScale { get; set; } = 0.80f;

    public FireworksEngine(FireworksProfileSet profiles)
    {
        _profiles = profiles;
        _canisters = profiles.Canisters.Values.Select(cp => new Canister(cp)).ToList();
    }

    public void Launch(string canisterId, string shellProfileId, D3D11Renderer renderer, string? colorSchemeId = null, float? muzzleVelocity = null)
    {
        TriggerEvent(new ShowEvent(ShowTimeSeconds, canisterId, shellProfileId, colorSchemeId, muzzleVelocity), renderer);
    }

    public IReadOnlyList<Canister> Canisters => _canisters;
    public IReadOnlyList<FireworkShell> Shells => _shells;

    public void LoadShow(ShowScript show)
    {
        _show = show ?? ShowScript.Empty;
        _nextEventIndex = 0;
        ShowTimeSeconds = 0;

        // Ensure chronological ordering.
        if (_show.Events is List<ShowEvent> list)
            list.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
        else
            _show = new ShowScript(_show.Events.OrderBy(e => e.TimeSeconds).ToArray());
    }

    public void Update(float dt, D3D11Renderer renderer)
    {
        if (dt <= 0)
            return;

        // Apply global timescale
        float scaledDt = dt * TimeScale;
        if (scaledDt <= 0)
            return;

        ShowTimeSeconds += scaledDt;

        // Update canister reload timers.
        foreach (var c in _canisters)
            c.Update(scaledDt);

        // Fire show events.
        var events = _show.Events;
        while (_nextEventIndex < events.Count && events[_nextEventIndex].TimeSeconds <= ShowTimeSeconds)
        {
            TriggerEvent(events[_nextEventIndex], renderer);
            _nextEventIndex++;
        }

        // Update shells.
        for (int i = _shells.Count - 1; i >= 0; i--)
        {
            var shell = _shells[i];
            shell.Update(scaledDt);

            // trail
            shell.EmitTrail(renderer, scaledDt);

            if (shell.TryExplode(out var explosion))
            {
                Explode(explosion, renderer);
                _shells.RemoveAt(i);
            }
            else if (!shell.Alive)
            {
                _shells.RemoveAt(i);
            }
        }

        // Provide canister + shell positions to renderer.
        var canisterStates = _canisters
            .Select(c => new D3D11Renderer.CanisterRenderState(
                Position: new Vector3(c.Profile.Position.X, 0.0f, c.Profile.Position.Y),
                Direction: c.Profile.LaunchDirection))
            .ToArray();
        renderer.SetCanisters(canisterStates);

        // Provide shell positions to renderer.
        var shellStates = _shells.Select(s => new D3D11Renderer.ShellRenderState(s.Position)).ToArray();
        renderer.SetShells(shellStates);
    }

    private void TriggerEvent(ShowEvent ev, D3D11Renderer renderer)
    {
        var canister = _canisters.FirstOrDefault(c => c.Profile.Id == ev.CanisterId);
        if (canister is null)
            return;

        if (!_profiles.Shells.TryGetValue(ev.ShellProfileId, out var shellProfile))
            return;

        var muzzle = ev.MuzzleVelocity ??
            (canister.Type.MuzzleVelocityMin + (float)_rng.NextDouble() * (canister.Type.MuzzleVelocityMax - canister.Type.MuzzleVelocityMin));

        if (!canister.CanFire)
            return;

        Vector3 launchPos = new(canister.Profile.Position.X, 0.30f, canister.Profile.Position.Y);

        Vector3 baseDir = canister.Profile.LaunchDirection;
        if (baseDir.LengthSquared() < 1e-6f)
            baseDir = Vector3.UnitY;
        else
            baseDir = Vector3.Normalize(baseDir);

        // Small random deviation around the aimed direction to avoid identical trajectories.
        // Keep deviation modest so shows remain predictable.
        float maxAngle = (float)(3.5 * System.Math.PI / 180.0); // degrees -> radians
        float yaw = (float)(_rng.NextDouble() * System.Math.PI * 2.0);
        float pitch = (float)(_rng.NextDouble() * maxAngle);

        Vector3 axis = Vector3.Cross(baseDir, Vector3.UnitY);
        if (axis.LengthSquared() < 1e-6f)
            axis = Vector3.UnitX;
        else
            axis = Vector3.Normalize(axis);

        var qYaw = Quaternion.CreateFromAxisAngle(baseDir, yaw);
        var qPitch = Quaternion.CreateFromAxisAngle(axis, pitch);
        Vector3 dir = Vector3.Normalize(Vector3.Transform(baseDir, qPitch * qYaw));

        // *** CHANGED: start from the "vacuum" muzzle, then retune with drag
        Vector3 launchVel = dir * muzzle;

        // We want the shell to roughly reach the canister's nominal burst height.
        float targetHeight = canister.Type.NominalBurstHeightM;
        float targetPeakY = launchPos.Y + targetHeight;

        Vector3 gravity = new(0f, -9.81f, 0f); // must match FireworkShell.Gravity magnitude
        Vector3 tunedVel = launchVel;
        float timeToPeak = shellProfile.FuseTimeSeconds; // fallback if solver doesn't converge

        // Simple iterative scaling loop to hit the desired peak
        for (int iter = 0; iter < 6; iter++)
        {
            var (peakY, tPeak) = SimulatePeakHeight(launchPos, tunedVel, ShellDragK, gravity);
            timeToPeak = tPeak;

            float currentHeight = peakY - launchPos.Y;
            if (currentHeight <= 0.1f)
            {
                // Not really going up – nudge speed a bit and try again.
                tunedVel *= 1.1f;
                continue;
            }

            float error = targetPeakY - peakY;
            if (MathF.Abs(error) < 0.5f)
            {
                // Close enough (within ~0.5m)
                break;
            }

            // Scale speed based on ratio of desired height to simulated height.
            float scale = MathF.Sqrt(targetHeight / currentHeight);
            tunedVel *= scale;
        }

        // Use tuned velocity from now on.
        launchVel = tunedVel;

        var colorSchemeId = ev.ColorSchemeId ?? shellProfile.ColorSchemeId;
        if (!_profiles.ColorSchemes.TryGetValue(colorSchemeId, out var scheme))
            scheme = _profiles.ColorSchemes.Values.FirstOrDefault();

        // *** CHANGED: pass drag and fuse override (burst slightly before apex)
        var shell = new FireworkShell(
            shellProfile,
            scheme,
            launchPos,
            launchVel,
            dragK: ShellDragK,
            fuseOverrideSeconds: timeToPeak * 0.95f);

        _shells.Add(shell);
        canister.OnFired();

        renderer.ShellSpawnCount++;
    }

    private void Explode(ShellExplosion explosion, D3D11Renderer renderer)
    {
        Vector3 ringAxis = Vector3.UnitY;
        if (explosion.RingAxis is { } configuredRingAxis && configuredRingAxis.LengthSquared() >= 1e-6f)
            ringAxis = Vector3.Normalize(configuredRingAxis);

        if (explosion.RingAxisRandomTiltDegrees > 0.0f)
        {
            float maxTiltRadians = explosion.RingAxisRandomTiltDegrees * (MathF.PI / 180.0f);

            // Randomize the axis within a cone centered on the configured axis.
            // This produces variation in *all* directions (not just a single world-space axis).
            float yaw = (float)(_rng.NextDouble() * MathF.Tau);
            float u = (float)_rng.NextDouble();
            float tilt = maxTiltRadians * MathF.Sqrt(u);

            Vector3 tangent1 = Vector3.Cross(ringAxis, Vector3.UnitY);
            if (tangent1.LengthSquared() < 1e-6f)
                tangent1 = Vector3.Cross(ringAxis, Vector3.UnitX);
            tangent1 = Vector3.Normalize(tangent1);
            Vector3 tangent2 = Vector3.Normalize(Vector3.Cross(ringAxis, tangent1));

            Vector3 offset = (tangent1 * MathF.Cos(yaw) + tangent2 * MathF.Sin(yaw)) * MathF.Sin(tilt);
            ringAxis = Vector3.Normalize(ringAxis * MathF.Cos(tilt) + offset);
        }

        var dirs = explosion.BurstShape switch
        {
            FireworkBurstShape.Peony => EmissionStyles.EmitPeony(explosion.ParticleCount),
            FireworkBurstShape.Chrysanthemum => EmissionStyles.EmitChrysanthemum(explosion.ParticleCount),
            FireworkBurstShape.Willow => EmissionStyles.EmitWillow(explosion.ParticleCount),
            FireworkBurstShape.Palm => EmissionStyles.EmitPalm(explosion.ParticleCount),
            FireworkBurstShape.Ring => EmissionStyles.EmitRing(explosion.ParticleCount, axis: ringAxis),
            FireworkBurstShape.Horsetail => EmissionStyles.EmitHorsetail(explosion.ParticleCount),
            _ => EmissionStyles.EmitPeony(explosion.ParticleCount)
        };

        // Convert Color to HDR-ish Vector4 expected by current particle shader.
        Vector4 baseColor = explosion.BaseColor;

        // Match the renderer's original burst look: speed is not derived from radius/lifetime.
        // Keep a characteristic base speed and let the renderer apply per-particle variance.
        float speed = explosion.BurstShape switch
        {
            FireworkBurstShape.Willow => 7.0f,
            FireworkBurstShape.Palm => 13.0f,
            FireworkBurstShape.Horsetail => 6.0f,
            _ => 10.0f
        };

        float lifetime = explosion.BurstShape switch
        {
            FireworkBurstShape.Willow => explosion.ParticleLifetimeSeconds * 2.2f,
            FireworkBurstShape.Palm => explosion.ParticleLifetimeSeconds * 1.2f,
            FireworkBurstShape.Horsetail => explosion.ParticleLifetimeSeconds * 2.5f,
            _ => explosion.ParticleLifetimeSeconds
        };

        renderer.SpawnBurstDirectedExplode(
            explosion.Position,
            baseColor,
            speed,
            dirs,
            particleLifetimeSeconds: lifetime);
        renderer.SpawnSmoke(explosion.Position);
    }

    // *** ALREADY ADDED: used by TriggerEvent now
    private (float peakY, float timeToPeak) SimulatePeakHeight(
        Vector3 startPos,
        Vector3 initialVelocity,
        float dragK,
        Vector3 gravity)
    {
        Vector3 p = startPos;
        Vector3 v = initialVelocity;

        float peakY = p.Y;
        float time = 0f;
        float timeAtPeak = 0f;

        const float dt = 1f / 240f;     // tiny step for accuracy
        const float maxTime = 12f;      // safety cap

        for (int i = 0; i < (int)(maxTime / dt); i++)
        {
            // Same integration as FireworkShell.Update
            v += gravity * dt;

            float speed = v.Length();
            if (speed > 0f)
            {
                Vector3 dragAccel = -v / speed * (dragK * speed * speed);
                v += dragAccel * dt;
            }

            p += v * dt;
            time += dt;

            if (p.Y > peakY)
            {
                peakY = p.Y;
                timeAtPeak = time;
            }

            // Once vertical velocity is going down for a bit, we’ve passed the apex
            if (v.Y <= 0f && time > 0.1f)
                break;
        }

        return (peakY, timeAtPeak);
    }

}

public sealed class Canister
{
    private float _cooldown;

    private readonly CanisterType _type;

    public CanisterProfile Profile { get; }

    public CanisterType Type => _type;

    public bool CanFire => _cooldown <= 0;

    public Canister(CanisterProfile profile)
    {
        Profile = profile;
        _type = DefaultCanisterTypes.All.FirstOrDefault(t => t.Id == profile.CanisterTypeId)
            ?? throw new InvalidOperationException($"Unknown canister type '{profile.CanisterTypeId}' for canister '{profile.Id}'.");
    }

    public void Update(float dt) => _cooldown = MathF.Max(0, _cooldown - dt);

    public void OnFired() => _cooldown = MathF.Max(0, _type.ReloadSeconds);
}

public sealed class FireworkShell
{
    private const float GroundY = 0.0f;
    private static readonly Vector3 Gravity = new(0, -9.81f, 0);
    private static readonly Random _rng = new();

    public FireworkShellProfile Profile { get; }
    public ColorScheme ColorScheme { get; }

    public Vector3 Position { get; private set; }
    public Vector3 Velocity { get; private set; }
    public float AgeSeconds { get; private set; }
    public bool Alive { get; private set; } = true;

    // *** CHANGED: drag comes from engine, fuse can be overridden
    public float DragK { get; private set; }
    public float FuseTimeSeconds { get; }

    // *** CHANGED: new constructor signature
    public FireworkShell(
        FireworkShellProfile profile,
        ColorScheme colorScheme,
        Vector3 position,
        Vector3 velocity,
        float dragK,
        float? fuseOverrideSeconds = null)
    {
        Profile = profile;
        ColorScheme = colorScheme;
        Position = position;
        Velocity = velocity;
        DragK = dragK;
        FuseTimeSeconds = fuseOverrideSeconds ?? profile.FuseTimeSeconds;
    }

    public void Update(float dt)
    {
        if (!Alive) return;

        Velocity += Gravity * dt;

        float speed = Velocity.Length();
        if (speed > 0f)
        {
            float k = DragK;
            Vector3 dragAccel = -Velocity / speed * (k * speed * speed);
            Velocity += dragAccel * dt;
        }

        Position += Velocity * dt;
        AgeSeconds += dt;

        if (Position.Y <= GroundY)
            Alive = false;
    }

    public void EmitTrail(D3D11Renderer renderer, float dt)
    {
        if (!Alive || Velocity.LengthSquared() < 1e-4f)
            return;

        Vector3 dir = Vector3.Normalize(Velocity);

        Span<Vector3> dirs = stackalloc Vector3[12];
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 baseDir = -dir;

            // tiny cone around baseDir
            float angle = 5f * (MathF.PI / 180f);
            float yaw = (float)(_rng.NextDouble() * MathF.PI * 2f);
            float pitch = (float)(_rng.NextDouble() * angle);

            Vector3 axis = Vector3.Normalize(Vector3.Cross(baseDir, Vector3.UnitY));
            if (axis.LengthSquared() < 1e-6f)
                axis = Vector3.UnitX;

            var qYaw = Quaternion.CreateFromAxisAngle(baseDir, yaw);
            var qPitch = Quaternion.CreateFromAxisAngle(axis, pitch);
            dirs[i] = Vector3.Normalize(Vector3.Transform(baseDir, qPitch * qYaw));
        }

        // LOCAL trail color
        var trailColor = new Vector4(1.0f, 0.85f, 0.5f, 1.0f);
        // trailColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f); // pure green for debugging

        renderer.SpawnBurstDirected(
            Position,
            trailColor,
            speed: 5.0f,
            directions: dirs,
            particleLifetimeSeconds: 0.6f);

        if (_rng.NextDouble() < 0.2)
        {
            renderer.SpawnSmoke(Position);
        }
    }

    public bool TryExplode(out ShellExplosion explosion)
    {
        if (!Alive)
        {
            explosion = default;
            return false;
        }

        // *** CHANGED: use per-shell fuse, not profile's fixed value
        if (AgeSeconds < FuseTimeSeconds)
        {
            explosion = default;
            return false;
        }

        explosion = new ShellExplosion(
            Position,
            BurstShape: Profile.BurstShape,
            ExplosionRadius: Profile.ExplosionRadius,
            ParticleCount: Profile.ParticleCount,
            ParticleLifetimeSeconds: Profile.ParticleLifetimeSeconds,
            RingAxis: Profile.RingAxis,
            RingAxisRandomTiltDegrees: Profile.RingAxisRandomTiltDegrees,
            BaseColor: ColorUtil.PickBaseColor(ColorScheme));

        Alive = false;
        return true;
    }
}

public readonly record struct ShellExplosion(
    Vector3 Position,
    FireworkBurstShape BurstShape,
    float ExplosionRadius,
    int ParticleCount,
    float ParticleLifetimeSeconds,
    Vector3? RingAxis,
    float RingAxisRandomTiltDegrees,
    Vector4 BaseColor);

internal static class ColorUtil
{
    private static readonly Random s_rng = new();

    public static Vector4 PickBaseColor(ColorScheme scheme)
    {
        if (scheme.BaseColors.Length == 0)
            return new Vector4(1, 1, 1, 1);

        var c = scheme.BaseColors[s_rng.Next(scheme.BaseColors.Length)];

        // Apply simple RGB variation.
        float v = (float)(s_rng.NextDouble() * 2.0 - 1.0) * scheme.ColorVariation;

        float r = Clamp01(c.R / 255.0f + v);
        float g = Clamp01(c.G / 255.0f + v);
        float b = Clamp01(c.B / 255.0f + v);

        // upscale a bit for additive HDR-ish look
        float boost = 2.2f;
        return new Vector4(r * boost, g * boost, b * boost, 1.0f);
    }

    private static float Clamp01(float x) => x < 0 ? 0 : (x > 1 ? 1 : x);
}

// EmissionStyles unchanged...


internal static class EmissionStyles
{
    private static readonly Random s_rng = new();

    /// <summary>
    /// Peony: soft, roughly uniform spherical burst. Equivalent to the old "sphere".
    /// </summary>
    public static Vector3[] EmitPeony(int count)
    {
        var dirs = new Vector3[count];
        for (int i = 0; i < count; i++)
            dirs[i] = RandomUnitVector();
        return dirs;
    }

    /// <summary>
    /// Chrysanthemum: spherical burst with a slightly more structured "spiky" look.
    /// We bias particles toward a set of "spokes" distributed over the full sphere,
    /// then apply isotropic 3D jitter around each spoke so each cluster is a narrow cone (not a plane).
    /// </summary>
    public static Vector3[] EmitChrysanthemum(int count)
    {
        // More spokes => more distinct "radial streaks" without looking like only a few sheets.
        const int spokeCount = 24;

        // How wide each spoke's cone is (bigger => less spiky / more peony-like).
        const float spokeJitter = 0.12f;

        // Precompute spoke directions across the *full* sphere (not locked to a plane).
        var spokes = new Vector3[spokeCount];
        for (int s = 0; s < spokeCount; s++)
            spokes[s] = RandomUnitVector();

        var dirs = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            // Deterministic index gives a more even distribution of particles per spoke.
            Vector3 baseSpoke = spokes[i % spokeCount];

            // Isotropic jitter around the spoke (3D), then renormalize.
            Vector3 jitterDir = RandomUnitVector();
            Vector3 d = baseSpoke + (jitterDir * spokeJitter);
            if (d.LengthSquared() < 1e-8f)
                d = baseSpoke;
            dirs[i] = Vector3.Normalize(d);
        }

        return dirs;
    }

    /// <summary>
    /// Willow: still roughly spherical, but each direction is gently blended toward "down"
    /// so gravity + extended lifetime produce long drooping arcs.
    /// </summary>
    public static Vector3[] EmitWillow(int count)
    {
        const float downwardBlend = 0.35f; // 0 = peony-like, 1 = straight down

        var dirs = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            // Start from a random spherical direction (same as Peony).
            Vector3 d = RandomUnitVector();

            // Gently bias toward down.
            Vector3 down = new(0f, -1f, 0f);
            d = Vector3.Normalize(Vector3.Lerp(d, down, downwardBlend));

            dirs[i] = d;
        }

        return dirs;
    }

    /// <summary>
    /// Palm: a small number of strong comet-like "fronds" radiating from near the zenith.
    /// Each frond is a narrow cone so it reads as a streak rather than a fuzzy blob.
    /// </summary>
    public static Vector3[] EmitPalm(int count)
    {
        // Fewer, stronger fronds = clearer palm shape.
        const int frondCount = 7;

        // Angle of fronds away from +Y (0 = straight up, pi/2 = horizontal).
        // ~35–40° feels very palm-like.
        const float frondConeAngle = 0.65f; // radians

        // How much we let each particle deviate from its frond direction.
        // Smaller => tighter fronds.
        const float frondJitterAngle = 0.08f; // radians

        var frondDirs = new Vector3[frondCount];

        // Precompute frond directions around a cone centered on +Y.
        for (int i = 0; i < frondCount; i++)
        {
            float azimuth = (float)(i * (Math.PI * 2.0) / frondCount);
            float s = MathF.Sin(frondConeAngle);
            float c = MathF.Cos(frondConeAngle);

            // Cone around +Y.
            frondDirs[i] = Vector3.Normalize(new Vector3(
                s * MathF.Cos(azimuth),
                c,
                s * MathF.Sin(azimuth)));
        }

        var dirs = new Vector3[count];

        // Spread particles evenly across fronds so each frond is clearly visible.
        int perFrond = Math.Max(1, count / frondCount);
        int idx = 0;

        for (int f = 0; f < frondCount; f++)
        {
            Vector3 baseDir = frondDirs[f];

            for (int j = 0; j < perFrond && idx < count; j++, idx++)
            {
                dirs[idx] = JitterDirection(baseDir, maxAngleRadians: frondJitterAngle);
            }
        }

        // If count isn't perfectly divisible, fill any remaining particles
        // by sampling random fronds with the same jitter.
        while (idx < count)
        {
            Vector3 baseDir = frondDirs[s_rng.Next(frondCount)];
            dirs[idx++] = JitterDirection(baseDir, maxAngleRadians: frondJitterAngle);
        }

        return dirs;
    }

    /// <summary>
    /// Ring: directions around a circle perpendicular to the given axis (default +Y).
    /// Equivalent to the old "donut" direction pattern.
    /// </summary>
    public static Vector3[] EmitRing(int count, Vector3 axis)
    {
        axis = axis.LengthSquared() < 1e-6f ? Vector3.UnitY : Vector3.Normalize(axis);

        Vector3 basis1 = Vector3.Normalize(Vector3.Cross(axis, Vector3.UnitX));
        if (basis1.LengthSquared() < 1e-6f)
            basis1 = Vector3.Normalize(Vector3.Cross(axis, Vector3.UnitZ));
        Vector3 basis2 = Vector3.Normalize(Vector3.Cross(axis, basis1));

        const float thickness = 0.15f;

        var dirs = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float a = (float)(s_rng.NextDouble() * System.Math.PI * 2.0);
            float tilt = (float)(s_rng.NextDouble() * 2.0 - 1.0) * thickness;

            Vector3 ring = (basis1 * MathF.Cos(a) + basis2 * MathF.Sin(a));
            Vector3 d = Vector3.Normalize(ring + axis * tilt);
            dirs[i] = d;
        }

        return dirs;
    }

    /// <summary>
    /// Horsetail: a dense, downward "waterfall" effect.
    /// Similar to a willow but with much stronger downward bias and
    /// minimal upward components, so gravity makes a thick drooping tail.
    /// </summary>
    public static Vector3[] EmitHorsetail(int count)
    {
        var dirs = new Vector3[count];

        const float downwardBlend = 0.75f;   // how hard we pull toward straight down
        const float minDownY = -0.25f;  // don't let stars start too horizontal/upward
        const float jitterAngle = 0.15f;   // small cone jitter for variation

        Vector3 down = new(0f, -1f, 0f);

        for (int i = 0; i < count; i++)
        {
            // Start from a random direction
            Vector3 d = RandomUnitVector();

            // Ensure it's at least somewhat downward: clamp Y so it's never near +Y
            if (d.Y > minDownY)
            {
                d.Y = minDownY;
            }

            d = Vector3.Normalize(d);

            // Strongly bias toward straight down
            d = Vector3.Normalize(Vector3.Lerp(d, down, downwardBlend));

            // Add a bit of cone jitter so it isn't a razor-thin column
            d = JitterDirection(d, jitterAngle);

            dirs[i] = d;
        }

        return dirs;
    }


    private static Vector3 JitterDirection(Vector3 baseDir, float maxAngleRadians)
    {
        if (baseDir.LengthSquared() < 1e-8f)
            return Vector3.UnitY;

        baseDir = Vector3.Normalize(baseDir);
        float yaw = (float)(s_rng.NextDouble() * System.Math.PI * 2.0);
        float pitch = (float)(s_rng.NextDouble() * maxAngleRadians);

        // Use a stable axis perpendicular to baseDir.
        Vector3 axis = Vector3.Cross(baseDir, Vector3.UnitY);
        if (axis.LengthSquared() < 1e-6f)
            axis = Vector3.UnitX;
        else
            axis = Vector3.Normalize(axis);

        var qYaw = Quaternion.CreateFromAxisAngle(baseDir, yaw);
        var qPitch = Quaternion.CreateFromAxisAngle(axis, pitch);
        return Vector3.Normalize(Vector3.Transform(baseDir, qPitch * qYaw));
    }

    private static Vector3 RandomUnitVector()
    {
        float z = (float)(s_rng.NextDouble() * 2.0 - 1.0);
        float a = (float)(s_rng.NextDouble() * System.Math.PI * 2.0);
        float r = MathF.Sqrt(MathF.Max(0.0f, 1.0f - z * z));
        return new Vector3(r * MathF.Cos(a), z, r * MathF.Sin(a));
    }
}
