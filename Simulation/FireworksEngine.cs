using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using FireworksApp.Audio;
using FireworksApp.Rendering;

namespace FireworksApp.Simulation;

using Math = System.Math;

public enum SubShellKind
{
    FinaleSalute,
    SpokeWheelPop
}

public sealed class SubShell
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Age;
    public float DetonateAt;
    public bool Detonated;

    public float GravityScale;
    public float Drag;

    public SubShellKind Kind;
    public FinaleSaluteParams? FinalePop;
    public SubShellSpokeWheelPopParams? SpokeWheelPop;
    public ColorScheme? ColorScheme;
    public Vector4 PopColor;
}

    public readonly struct PendingWillowHandoff
    {
        public readonly float TriggerTime;
        public readonly Vector3 Position;
        public readonly Vector3 ParentVelocity;
        public readonly PeonyToWillowParams Params;
        public readonly ColorScheme ColorScheme;

        public PendingWillowHandoff(float TriggerTime, Vector3 Position, Vector3 ParentVelocity, PeonyToWillowParams Params, ColorScheme ColorScheme)
        {
            this.TriggerTime = TriggerTime;
            this.Position = Position;
            this.ParentVelocity = ParentVelocity;
            this.Params = Params;
            this.ColorScheme = ColorScheme;
        }
    }

public sealed class Comet
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Age;
    public float LifetimeSeconds;
    public bool Alive;

    public float GravityScale;
    public float Drag;
    public CometParams Params;
    public Vector4 BaseColor;
}

public sealed class FireworksEngine
{
    public event Action<SoundEvent>? SoundEvent;

    private void EmitSound(in SoundEvent ev) => SoundEvent?.Invoke(ev);

    private readonly FireworksProfileSet _profiles;
    private readonly List<Canister> _canisters;
    private readonly List<FireworkShell> _shells = new();
    private readonly List<SubShell> _subShells = new();
    private readonly List<Comet> _comets = new();
    private readonly List<GroundEffectInstance> _groundEffects = new();
    private readonly List<PendingSubShellSpawn> _pendingSubshells = new();
    private readonly List<PendingWillowHandoff> _pendingWillow = new();
    private readonly Dictionary<uint, FireworkShell> _gpuShells = new();
    private readonly DetonationEvent[] _detonationBuffer = new DetonationEvent[1024];
    private uint _nextShellId = 1;
    private readonly Random _rng = new();

    private ShowScript _show = ShowScript.Empty;
    private int _nextEventIndex;

    // drag for shells (must match FireworkShell.Update usage)
    private const float ShellDragK = Tunables.ShellDragK;

    public float ShowTimeSeconds { get; private set; }

    // Global time scaling: 1.0 = normal, 0.8 = 20% slower, etc.
    public float TimeScale { get; set; } = Tunables.DefaultTimeScale;

    public FireworksEngine(FireworksProfileSet profiles)
    {
        Tunables.Validate();
        _profiles = profiles;
        _canisters = profiles.Canisters.Values.Select(cp => new Canister(cp)).ToList();
    }

    public void Launch(string canisterId, string shellProfileId, D3D11Renderer renderer, string? colorSchemeId = null, float? muzzleVelocity = null)
    {
        TriggerEvent(new ShowEvent(TimeSeconds: ShowTimeSeconds, CanisterId: canisterId, ShellProfileId: shellProfileId, GroundEffectProfileId: null, ColorSchemeId: colorSchemeId, MuzzleVelocity: muzzleVelocity), renderer);
    }

    public IReadOnlyList<Canister> Canisters => _canisters;
    public IReadOnlyList<FireworkShell> Shells => _shells;
    public IReadOnlyList<GroundEffectInstance> GroundEffects => _groundEffects;

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
        UpdateCore(dt, renderer, applyTimeScale: true);
    }

    internal void UpdateUnscaled(float dt, D3D11Renderer renderer)
    {
        UpdateCore(dt, renderer, applyTimeScale: false);
    }

    private void UpdateCore(float dt, D3D11Renderer renderer, bool applyTimeScale)
    {
        if (dt <= 0)
            return;

        float stepDt = applyTimeScale ? (dt * TimeScale) : dt;
        if (stepDt <= 0)
            return;

        ShowTimeSeconds += stepDt;

        // Update canister reload timers.
        foreach (var c in _canisters)
            c.Update(stepDt);

        ProcessGpuDetonations(renderer);

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
            shell.Update(stepDt);

            // trail
            shell.EmitTrail(renderer, stepDt);

            if (shell.TryExplode(out var explosion))
            {
                // Emit parent burst as before
                Explode(explosion, renderer);

                // Schedule subshells per attachments before removing parent
                var attachments = GetShellAttachments(shell.Profile);
                if (attachments != null && attachments.Count > 0)
                {
                    foreach (var att in attachments)
                    {
                        if (_rng.NextDouble() > att.Probability)
                            continue;

                        if (!_profiles.SubShells.TryGetValue(att.SubShellProfileId, out var subProf))
                            continue;

                        int nextDepth = shell.SubshellDepth + 1;
                        if (nextDepth > subProf.MaxSubshellDepth || nextDepth > att.DepthBudget)
                            continue;

                        if (explosion.Position.Y < subProf.MinAltitudeToSpawn)
                            continue;

                        int count = System.Math.Max(0, (int)System.MathF.Round(subProf.Count * att.Scale));
                        if (count <= 0)
                            continue;

                        Vector3[] dirs = subProf.SpawnMode switch
                        {
                            SubShellSpawnMode.Ring => EmissionStyles.EmitRing(count, Vector3.UnitY),
                            SubShellSpawnMode.Cone => EmitConeDirections(count, Vector3.UnitY, subProf.DirectionJitter),
                            _ => EmissionStyles.EmitPeony(count)
                        };

                        var pending = new PendingSubShellSpawn(
                            SpawnTime: ShowTimeSeconds + System.MathF.Max(0.0f, subProf.DelaySeconds),
                            Position: explosion.Position,
                            ParentVelocity: shell.Velocity,
                            SubProfile: subProf,
                            Attachment: att,
                            ParentColorScheme: shell.ColorScheme,
                            ParentDepth: shell.SubshellDepth,
                            Directions: dirs);

                        _pendingSubshells.Add(pending);
                    }
                }

                _shells.RemoveAt(i);
            }
            else if (shell.IsTerminalFading)
            {
                if (!shell.Alive)
                    _shells.RemoveAt(i);
            }
            else if (!shell.Alive)
            {
                _shells.RemoveAt(i);
            }
        }

        // Mirror GPU-simulated shells for trails/visual interpolation.
        UpdateGpuShellTrails(stepDt, renderer);

        UpdateSubShells(stepDt, renderer);
        UpdateComets(stepDt, renderer);

        // Process delayed willow handoffs
        if (_pendingWillow.Count > 0)
        {
            for (int i = _pendingWillow.Count - 1; i >= 0; i--)
            {
                var p = _pendingWillow[i];
                if (ShowTimeSeconds >= p.TriggerTime)
                {
                    ProcessWillowHandoff(p, renderer);
                    _pendingWillow.RemoveAt(i);
                }
            }
        }

        // Process delayed subshell spawns
        if (_pendingSubshells.Count > 0)
        {
            for (int i = _pendingSubshells.Count - 1; i >= 0; i--)
            {
                var p = _pendingSubshells[i];
                if (ShowTimeSeconds >= p.SpawnTime)
                {
                    SpawnResolvedSubShells(p, renderer);
                    _pendingSubshells.RemoveAt(i);
                }
            }
        }

        // Update ground effects and emit particles.
        for (int i = _groundEffects.Count - 1; i >= 0; i--)
        {
            var ge = _groundEffects[i];
            ge.Update(stepDt);

            if (ge.Alive)
            {
                EmitGroundEffect(ge, stepDt, ShowTimeSeconds, renderer);
            }
            else
            {
                _groundEffects.RemoveAt(i);
            }
        }

        // Provide canister + shell positions to renderer.
        var canisterStates = _canisters
            .Select(c => new D3D11Renderer.CanisterRenderState(
                Position: new Vector3(c.Profile.Position.X, 0.0f, c.Profile.Position.Y),
                Direction: c.Profile.LaunchDirection))
            .ToArray();
        renderer.SetCanisters(canisterStates);

        // Provide shell positions/velocities to renderer (CPU + GPU mirrors) for trails/interpolation.
        var shellStatesList = new List<D3D11Renderer.ShellRenderState>(_shells.Count + _gpuShells.Count);
        for (int i = 0; i < _shells.Count; i++)
        {
            var s = _shells[i];
            shellStatesList.Add(new D3D11Renderer.ShellRenderState(s.Position, s.Velocity));
        }

        if (renderer.ShellsGpuRendered && _gpuShells.Count > 0)
        {
            foreach (var shell in _gpuShells.Values)
            {
                shellStatesList.Add(new D3D11Renderer.ShellRenderState(shell.Position, shell.Velocity));
            }
        }

        renderer.SetShells(shellStatesList);
    }

    private void UpdateGpuShellTrails(float dt, D3D11Renderer renderer)
    {
        if (dt <= 0.0f || !renderer.ShellsGpuRendered || _gpuShells.Count == 0)
            return;

        List<uint>? toRemove = null;

        foreach (var kvp in _gpuShells)
        {
            var shell = kvp.Value;

            shell.Update(dt);
            shell.EmitTrail(renderer, dt);

            // If the mirror falls below ground or otherwise dies, drop it so trails stop.
            if (!shell.Alive)
            {
                toRemove ??= new List<uint>();
                toRemove.Add(kvp.Key);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
                _gpuShells.Remove(toRemove[i]);
        }
    }

    // Attachments resolver: replace missing property access with a method.
    // If future profiles add attachments, hook them up here.
    private IReadOnlyList<SubShellAttachment>? GetShellAttachments(FireworkShellProfile profile)
    {
        // No attachments currently defined in FireworkShellProfile.
        // Return null to indicate none; routine remains intact.
        return null;
    }

    // Local helper for emitting cone directions for subshell spawns
    private static Vector3[] EmitConeDirections(int count, Vector3 axis, float maxAngle)
    {
        var dirs = new Vector3[count];
        axis = axis.LengthSquared() < 1e-6f ? Vector3.UnitY : Vector3.Normalize(axis);
        for (int i = 0; i < count; i++)
        {
            dirs[i] = JitterDirection(axis, maxAngleRadians: MathF.Max(0.0f, maxAngle));
        }
        return dirs;
    }

    private static Vector3 JitterDirection(Vector3 baseDir, float maxAngleRadians)
    {
        baseDir = baseDir.LengthSquared() < 1e-6f ? Vector3.UnitY : Vector3.Normalize(baseDir);
        // Use deterministic Random per-call is fine here; engine has its own _rng elsewhere
        float yaw = (float)(new Random().NextDouble() * MathF.PI * 2f);
        float pitch = (float)(new Random().NextDouble() * maxAngleRadians);
        Vector3 axis = Vector3.Normalize(Vector3.Cross(baseDir, Vector3.UnitY));
        if (axis.LengthSquared() < 1e-6f) axis = Vector3.UnitX;
        var qYaw = Quaternion.CreateFromAxisAngle(baseDir, yaw);
        var qPitch = Quaternion.CreateFromAxisAngle(axis, pitch);
        return Vector3.Normalize(Vector3.Transform(baseDir, qPitch * qYaw));
    }

    private void SpawnResolvedSubShells(in PendingSubShellSpawn pending, D3D11Renderer renderer)
    {
        var dirs = pending.Directions;
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 dir = dirs[i];
            // Position jitter around parent burst
            Vector3 pos = pending.Position + EmissionStyles.RandomUnitVector() * pending.SubProfile.PositionJitter;

            // Velocity blend: inherit portion of parent velocity + added along dir with jitter
            float speedMul = 1.0f + (float)(_rng.NextDouble() * 2.0 - 1.0) * pending.SubProfile.SpeedJitter;
            Vector3 vel = pending.ParentVelocity * pending.SubProfile.InheritParentVelocity + dir * (pending.SubProfile.AddedSpeed * speedMul);

            // Resolve child shell profile and color scheme
            if (!_profiles.Shells.TryGetValue(pending.SubProfile.ShellProfileId, out var childProfile))
                continue;

            var schemeId = pending.SubProfile.ColorSchemeId ?? childProfile.ColorSchemeId;
            if (!_profiles.ColorSchemes.TryGetValue(schemeId, out var childScheme))
                childScheme = _profiles.ColorSchemes.Values.FirstOrDefault();

            var child = new FireworkShell(
                childProfile,
                childScheme,
                pos,
                vel,
                dragK: ShellDragK,
                fuseOverrideSeconds: null)
            {
                SubshellDepth = pending.ParentDepth + 1,
                ParentShellId = null,
                BurstShapeOverride = pending.SubProfile.BurstShapeOverride,
                ShellId = _nextShellId++
            };

            if (renderer.ShellsGpuRendered)
            {
                _gpuShells[child.ShellId] = child;
                renderer.QueueShellGpu(pos, vel, child.FuseTimeSeconds, ShellDragK, ColorUtil.PickBaseColor(childScheme), child.ShellId);
            }
            else
            {
                _shells.Add(child);
            }
        }
    }

    private readonly struct PendingSubShellSpawn
    {
        public readonly float SpawnTime;
        public readonly Vector3 Position;
        public readonly Vector3 ParentVelocity;
        public readonly SubShellProfile SubProfile;
        public readonly SubShellAttachment Attachment;
        public readonly ColorScheme ParentColorScheme;
        public readonly int ParentDepth;
        public readonly Vector3[] Directions;

        public PendingSubShellSpawn(float SpawnTime, Vector3 Position, Vector3 ParentVelocity, SubShellProfile SubProfile, SubShellAttachment Attachment, ColorScheme ParentColorScheme, int ParentDepth, Vector3[] Directions)
        {
            this.SpawnTime = SpawnTime;
            this.Position = Position;
            this.ParentVelocity = ParentVelocity;
            this.SubProfile = SubProfile;
            this.Attachment = Attachment;
            this.ParentColorScheme = ParentColorScheme;
            this.ParentDepth = ParentDepth;
            this.Directions = Directions;
        }
    }

    private void UpdateSubShells(float dt, D3D11Renderer renderer)
    {
        if (dt <= 0.0f || _subShells.Count == 0)
            return;

        const float groundY = 0.0f;
        Vector3 gravity = new(0.0f, -9.81f, 0.0f);

        for (int i = _subShells.Count - 1; i >= 0; i--)
        {
            var s = _subShells[i];

            if (s.Detonated)
            {
                _subShells.RemoveAt(i);
                continue;
            }

            s.Age += dt;

            // Emit trail particles for this subshell
            if (s.Kind == SubShellKind.FinaleSalute && s.FinalePop is { } finalePop && finalePop.EnableSubShellTrails && s.Velocity.LengthSquared() > 1e-4f)
            {
                EmitSubShellTrail(s, finalePop, renderer);
            }

            // Integrate (semi-implicit Euler)
            s.Velocity += gravity * s.GravityScale * dt;
            s.Velocity = s.Velocity * MathF.Exp(-s.Drag * dt);
            s.Position += s.Velocity * dt;

            bool hitGround = s.Position.Y <= groundY;
            if (hitGround)
            {
                s.Position = new Vector3(s.Position.X, groundY, s.Position.Z);
                s.Detonated = true;
                DetonateSubShell(s, renderer);
                _subShells.RemoveAt(i);
                continue;
            }

            if (s.Age >= s.DetonateAt)
            {
                s.Detonated = true;
                DetonateSubShell(s, renderer);
                _subShells.RemoveAt(i);
                continue;
            }

            // write back struct/class state
            _subShells[i] = s;
        }
    }

    private void DetonateSubShell(SubShell s, D3D11Renderer renderer)
    {
        switch (s.Kind)
        {
            case SubShellKind.FinaleSalute when s.FinalePop is { } finale:
                SpawnPopFlash(s.Position, finale, renderer);
                break;

            case SubShellKind.SpokeWheelPop when s.SpokeWheelPop is { } wheel:
                SpawnSpokeWheelPopFlash(s, wheel, renderer);
                break;
        }
    }

    private void UpdateComets(float dt, D3D11Renderer renderer)
    {
        if (dt <= 0.0f || _comets.Count == 0)
            return;

        const float groundY = 0.0f;
        Vector3 gravity = new(0.0f, -9.81f, 0.0f);

        for (int i = _comets.Count - 1; i >= 0; i--)
        {
            var c = _comets[i];

            if (!c.Alive)
            {
                _comets.RemoveAt(i);
                continue;
            }

            c.Age += dt;

            // Emit trail particles for this comet
            if (c.Velocity.LengthSquared() > 1e-4f)
            {
                EmitCometTrail(c, renderer);
            }

            // Integrate (semi-implicit Euler)
            c.Velocity += gravity * c.GravityScale * dt;
            c.Velocity = c.Velocity * MathF.Exp(-c.Drag * dt);
            c.Position += c.Velocity * dt;

            // Check if lifetime exceeded or hit ground
            bool hitGround = c.Position.Y <= groundY;
            bool expired = c.Age >= c.LifetimeSeconds;

            if (hitGround || expired)
            {
                c.Alive = false;
                _comets.RemoveAt(i);
                continue;
            }

            // write back struct/class state
            _comets[i] = c;
        }
    }

    private void TriggerEvent(ShowEvent ev, D3D11Renderer renderer)
    {
        var canister = _canisters.FirstOrDefault(c => c.Profile.Id == ev.CanisterId);
        if (canister is null)
            return;

        // Ground effect start (scheduled like shells).
        if (!string.IsNullOrWhiteSpace(ev.GroundEffectProfileId))
        {
            if (!_profiles.GroundEffects.TryGetValue(ev.GroundEffectProfileId!, out var geProfile))
                return;

            string schemeId = ev.ColorSchemeId ?? geProfile.ColorSchemeId;
            if (!_profiles.ColorSchemes.TryGetValue(schemeId, out var geScheme))
                geScheme = _profiles.ColorSchemes.Values.FirstOrDefault();

            if (geScheme is null)
                return;

            _groundEffects.Add(new GroundEffectInstance(geProfile, canister, geScheme, startTimeSeconds: ShowTimeSeconds));
            return;
        }

        // Shell launch (existing behavior).
        if (string.IsNullOrWhiteSpace(ev.ShellProfileId))
            return;

        if (!_profiles.Shells.TryGetValue(ev.ShellProfileId!, out var shellProfile))
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
        if (!_profiles.ColorSchemes.TryGetValue(colorSchemeId, out var shellScheme))
            shellScheme = _profiles.ColorSchemes.Values.FirstOrDefault();

        var shellBaseColor = ColorUtil.PickBaseColor(shellScheme);

        // *** CHANGED: pass drag and fuse override (burst slightly before apex)
        var shell = new FireworkShell(
            shellProfile,
            shellScheme,
            launchPos,
            launchVel,
            dragK: ShellDragK,
            fuseOverrideSeconds: timeToPeak * 0.95f)
        {
            ShellId = _nextShellId++
        };

        if (renderer.ShellsGpuRendered)
        {
            _gpuShells[shell.ShellId] = shell;
            renderer.QueueShellGpu(launchPos, launchVel, shell.FuseTimeSeconds, ShellDragK, shellBaseColor, shell.ShellId);
        }
        else
        {
            _shells.Add(shell);
        }
        canister.OnFired();

        EmitSound(new SoundEvent(
            SoundEventType.ShellLaunch,
            Position: launchPos,
            Gain: 1.0f,
            Loop: false));

        renderer.ShellSpawnCount++;
    }

    private void SpawnFinaleSalute(Vector3 origin, FinaleSaluteParams p)
    {
        int count = System.Math.Clamp(p.SubShellCount, 1, 5000);
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = RandomUnitVector();
            dir = Vector3.Normalize(dir + Vector3.UnitY * p.SubShellUpBias);

            float u = (float)_rng.NextDouble();
            float speed = p.SubShellSpeedMin + u * (p.SubShellSpeedMax - p.SubShellSpeedMin);
            Vector3 vel = dir * speed;

            float baseDelay = Lerp(p.DetonateDelayMin, p.DetonateDelayMax, (float)_rng.NextDouble());
            float jitter = ((float)_rng.NextDouble() * 2.0f - 1.0f) * p.DetonateJitterMax;
            float detonateAt = System.Math.Clamp(baseDelay + jitter, p.DetonateDelayMin, p.DetonateDelayMax);

            _subShells.Add(new SubShell
            {
                Position = origin,
                Velocity = vel,
                Age = 0.0f,
                DetonateAt = detonateAt,
                Detonated = false,
                GravityScale = p.SubShellGravityScale,
                Drag = p.SubShellDrag,
                Kind = SubShellKind.FinaleSalute,
                FinalePop = p,
                SpokeWheelPop = null,
                ColorScheme = null,
                PopColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
            });
        }
    }

    private void SpawnSpokeWheelPop(Vector3 origin, SubShellSpokeWheelPopParams p, ColorScheme parentScheme, Vector4 parentBaseColor)
    {
        int count = Math.Clamp(p.SubShellCount, 1, 512);
        float startRad = p.RingStartAngleDegrees * (MathF.PI / 180.0f);
        float arcRad = (p.RingEndAngleDegrees - p.RingStartAngleDegrees) * (MathF.PI / 180.0f);
        if (MathF.Abs(arcRad) < 1e-4f)
            arcRad = MathF.Tau;

        float step = arcRad / count;
        float angleJitter = p.AngleJitterDegrees * (MathF.PI / 180.0f);

        var flashScheme = ResolvePopFlashScheme(p, parentScheme);

        for (int i = 0; i < count; i++)
        {
            // Even angular spacing across the configured arc; optional jitter avoids a rigid wheel.
            float angle = startRad + step * i;
            if (angleJitter > 0.0f)
            {
                angle += angleJitter * ((float)_rng.NextDouble() * 2.0f - 1.0f);
            }

            Vector3 dir = new(MathF.Cos(angle), 0.0f, MathF.Sin(angle));
            if (dir.LengthSquared() < 1e-6f)
                dir = Vector3.UnitZ;
            dir = Vector3.Normalize(dir);

            Vector3 tangent = Vector3.Cross(Vector3.UnitY, dir);
            if (tangent.LengthSquared() > 1e-6f)
                tangent = Vector3.Normalize(tangent);

            float fuse = Lerp(p.SubShellFuseMinSeconds, p.SubShellFuseMaxSeconds, (float)_rng.NextDouble());

            Vector3 vel = dir * p.SubShellSpeed;
            if (MathF.Abs(p.TangentialSpeed) > 1e-4f && tangent.LengthSquared() > 1e-6f)
            {
                // Tangential component adds a subtle wheel spin feel without breaking the spoke silhouette.
                vel += tangent * p.TangentialSpeed;
            }

            Vector3 spawnPos = origin + dir * p.RingRadius;
            Vector4 popColor = PickWheelPopColor(p, flashScheme, parentBaseColor);

            _subShells.Add(new SubShell
            {
                Position = spawnPos,
                Velocity = vel,
                Age = 0.0f,
                DetonateAt = MathF.Max(0.0f, fuse),
                Detonated = false,
                GravityScale = p.SubShellGravityScale,
                Drag = p.SubShellDrag,
                Kind = SubShellKind.SpokeWheelPop,
                FinalePop = null,
                SpokeWheelPop = p,
                ColorScheme = flashScheme,
                PopColor = popColor
            });
        }
    }

    private ColorScheme ResolvePopFlashScheme(SubShellSpokeWheelPopParams p, ColorScheme parentScheme)
    {
        if (!string.IsNullOrWhiteSpace(p.PopFlashColorSchemeId) && _profiles.ColorSchemes.TryGetValue(p.PopFlashColorSchemeId, out var scheme))
            return scheme;

        return parentScheme ?? _profiles.ColorSchemes.Values.FirstOrDefault() ?? new ColorScheme("default_pop", new[] { Colors.White }, 0.0f, 0.5f);
    }

    private Vector4 PickWheelPopColor(SubShellSpokeWheelPopParams p, ColorScheme scheme, Vector4 parentBaseColor)
    {
        if (p.PopFlashColors is { Length: > 0 })
        {
            var c = p.PopFlashColors[_rng.Next(p.PopFlashColors.Length)];
            return ColorUtil.FromColor(c);
        }

        if (scheme is not null)
            return ColorUtil.PickBaseColor(scheme);

        return parentBaseColor;
    }

    private void SpawnPopFlash(Vector3 position, FinaleSaluteParams p, D3D11Renderer renderer)
    {
        renderer.SpawnPopFlash(position, p.PopFlashLifetime, p.PopFlashSize, p.PopPeakIntensity, p.PopFadeGamma);

        // Dense, tight spark burst for realism (no smoke, no color variance beyond white->silver).
        // Do NOT increase radius: keep speed slightly lower so the energy reads tighter.
        // Particle count goal: 3-6x the *shell* burst count. We approximate with a fixed high count per pop.
        renderer.SpawnFinaleSaluteSparks(
            position: position,
            particleCount: System.Math.Clamp(p.SparkParticleCount, 0, 250000),
            baseSpeed: 9.0f,
            speedJitterFrac: 0.20f,
            particleLifetimeMinSeconds: 0.40f,
            particleLifetimeMaxSeconds: 0.80f,
            sparkleRateHzMin: 18.0f,
            sparkleRateHzMax: 42.0f,
            sparkleIntensity: 0.85f,
            microFragmentChance: 0.25f,
            microLifetimeMinSeconds: 0.20f,
            microLifetimeMaxSeconds: 0.40f,
            microSpeedMulMin: 0.55f,
            microSpeedMulMax: 0.85f);
    }

    private void SpawnSpokeWheelPopFlash(SubShell s, SubShellSpokeWheelPopParams p, D3D11Renderer renderer)
    {
        float flashLifetime = MathF.Max(0.05f, p.PopFlashLifetime);
        float flashRadius = MathF.Max(0.05f, p.PopFlashRadius);

        renderer.SpawnPopFlash(s.Position, flashLifetime, flashRadius, p.PopFlashIntensity, p.PopFlashFadeGamma);

        int particleCount = Math.Clamp(p.PopFlashParticleCount, 0, 250_000);
        if (particleCount <= 0)
            return;

        var dirs = EmissionStyles.EmitPeony(particleCount);
        // Speed chosen so the spark front roughly matches the configured radius over its lifetime.
        float speed = flashRadius / flashLifetime;

        // Fast, bright micro-burst to read as a sharp pop rather than a full peony.
        renderer.SpawnBurstDirectedExplode(
            s.Position,
            s.PopColor.LengthSquared() > 1e-6f ? s.PopColor : new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            speed,
            dirs,
            particleLifetimeSeconds: flashLifetime * 0.9f,
            sparkleRateHz: 18.0f,
            sparkleIntensity: MathF.Min(1.2f, p.PopFlashIntensity * 0.35f));
    }

    private void EmitSubShellTrail(SubShell s, FinaleSaluteParams p, D3D11Renderer renderer)
    {
        if (!p.EnableSubShellTrails || s.Velocity.LengthSquared() < 1e-4f)
            return;

        Vector3 dir = Vector3.Normalize(s.Velocity);
        int particleCount = Math.Clamp(p.TrailParticleCount, 1, 20);

        Span<Vector3> dirs = stackalloc Vector3[particleCount];
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 baseDir = -dir;

            // small cone around baseDir
            float angle = 8f * (MathF.PI / 180f);
            float yaw = (float)_rng.NextDouble() * MathF.PI * 2f;
            float pitch = (float)_rng.NextDouble() * angle;

            Vector3 axis = Vector3.Normalize(Vector3.Cross(baseDir, Vector3.UnitY));
            if (axis.LengthSquared() < 1e-6f)
                axis = Vector3.UnitX;

            var qYaw = Quaternion.CreateFromAxisAngle(baseDir, yaw);
            var qPitch = Quaternion.CreateFromAxisAngle(axis, pitch);
            dirs[i] = Vector3.Normalize(Vector3.Transform(baseDir, qPitch * qYaw));
        }

        // Subshell trail color: slightly dimmer and more orange than main shells
        var trailColor = new Vector4(1.0f, 0.75f, 0.4f, 1.0f);

        renderer.SpawnBurstDirected(
            s.Position,
            trailColor,
            speed: p.TrailSpeed,
            directions: dirs,
            particleLifetimeSeconds: p.TrailParticleLifetime);

        if (_rng.NextDouble() < p.TrailSmokeChance)
        {
            renderer.SpawnSmoke(s.Position);
        }
    }

    private void SpawnComet(Vector3 origin, CometParams p, Vector4 baseColor)
    {
        int count = System.Math.Clamp(p.CometCount, 1, 500);
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = RandomUnitVector();
            dir = Vector3.Normalize(dir + Vector3.UnitY * p.CometUpBias);

            float u = (float)_rng.NextDouble();
            float speed = p.CometSpeedMin + u * (p.CometSpeedMax - p.CometSpeedMin);
            Vector3 vel = dir * speed;

            _comets.Add(new Comet
            {
                Position = origin,
                Velocity = vel,
                Age = 0.0f,
                LifetimeSeconds = p.CometLifetimeSeconds,
                Alive = true,
                GravityScale = p.CometGravityScale,
                Drag = p.CometDrag,
                Params = p,
                BaseColor = baseColor
            });
        }
    }

    private void EmitCometTrail(Comet c, D3D11Renderer renderer)
    {
        var p = c.Params;
        if (c.Velocity.LengthSquared() < 1e-4f)
            return;

        Vector3 dir = Vector3.Normalize(c.Velocity);
        int particleCount = Math.Clamp(p.TrailParticleCount, 1, 20);

        Span<Vector3> dirs = stackalloc Vector3[particleCount];
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 baseDir = -dir;

            // small cone around baseDir
            float angle = 8f * (MathF.PI / 180f);
            float yaw = (float)_rng.NextDouble() * MathF.PI * 2f;
            float pitch = (float)_rng.NextDouble() * angle;

            Vector3 axis = Vector3.Normalize(Vector3.Cross(baseDir, Vector3.UnitY));
            if (axis.LengthSquared() < 1e-6f)
                axis = Vector3.UnitX;

            var qYaw = Quaternion.CreateFromAxisAngle(baseDir, yaw);
            var qPitch = Quaternion.CreateFromAxisAngle(axis, pitch);
            dirs[i] = Vector3.Normalize(Vector3.Transform(baseDir, qPitch * qYaw));
        }

        // Use configured trail color or fall back to base color
        var trailColor = p.TrailColor ?? c.BaseColor;

        renderer.SpawnBurstDirected(
            c.Position,
            trailColor,
            speed: p.TrailSpeed,
            directions: dirs,
            particleLifetimeSeconds: p.TrailParticleLifetime);

        if (_rng.NextDouble() < p.TrailSmokeChance)
        {
            renderer.SpawnSmoke(c.Position);
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private Vector3 RandomUnitVector()
    {
        float z = (float)(_rng.NextDouble() * 2.0 - 1.0);
        float a = (float)(_rng.NextDouble() * System.Math.PI * 2.0);
        float r = MathF.Sqrt(MathF.Max(0.0f, 1.0f - z * z));
        return new Vector3(r * MathF.Cos(a), z, r * MathF.Sin(a));
    }

    private void EmitGroundEffect(GroundEffectInstance ge, float dt, float showTimeSeconds, D3D11Renderer renderer)
    {
        // Ground origin near canister top (visual-only).
        Vector3 origin = new(ge.Canister.Profile.Position.X, 0.30f, ge.Canister.Profile.Position.Y);
        origin.Y = System.Math.Max(0.05f, origin.Y);

        // Optional per-effect vertical offset (meters).
        origin.Y += ge.Profile.HeightOffsetMeters;

        var profile = ge.Profile;

        // Pick base HDR-ish color from scheme.
        Vector4 baseColor = ColorUtil.PickBaseColor(ge.ColorScheme) * profile.BrightnessScalar;

        // Time-based brightness shaping.
        float t = ge.ElapsedSeconds;
        float lifeT = profile.DurationSeconds > 1e-4f ? (t / profile.DurationSeconds) : 1.0f;
        lifeT = System.Math.Clamp(lifeT, 0.0f, 1.0f);
        float envelope = 1.0f - 0.6f * lifeT;

        switch (profile.Type)
        {
            case GroundEffectType.Fountain:
                EmitFountain(profile, origin, baseColor, envelope, dt, showTimeSeconds, renderer, ge);
                break;
            case GroundEffectType.Spinner:
                EmitSpinner(profile, origin, baseColor, envelope, dt, showTimeSeconds, renderer, ge);
                break;
            case GroundEffectType.Strobe:
                EmitStrobe(profile, origin, baseColor, envelope, dt, showTimeSeconds, renderer, ge);
                break;
            case GroundEffectType.Mine:
                EmitMine(profile, origin, baseColor, envelope, dt, showTimeSeconds, renderer, ge);
                break;
            case GroundEffectType.BengalFlare:
                EmitBengalFlare(profile, origin, baseColor, envelope, dt, showTimeSeconds, renderer, ge);
                break;
            case GroundEffectType.LanceworkPanel:
                EmitLanceworkPanel(profile, origin, baseColor, envelope, dt, showTimeSeconds, renderer, ge);
                break;
            case GroundEffectType.WaterfallCurtain:
                EmitWaterfallCurtain(profile, origin, baseColor, envelope, dt, showTimeSeconds, renderer, ge);
                break;
            case GroundEffectType.ChaserLine:
                EmitChaserLine(profile, origin, baseColor, envelope, dt, showTimeSeconds, renderer, ge);
                break;
            case GroundEffectType.GroundBloom:
                EmitGroundBloom(profile, origin, baseColor, envelope, dt, showTimeSeconds, renderer, ge);
                break;
            case GroundEffectType.PulsingGlitterFountain:
                EmitPulsingGlitterFountain(profile, origin, baseColor, envelope, dt, showTimeSeconds, renderer, ge);
                break;
        }
    }

    private void EmitBengalFlare(GroundEffectProfile profile, Vector3 origin, Vector4 baseColor, float envelope, float dt, float time, D3D11Renderer renderer, GroundEffectInstance ge)
    {
        // Ragged flame edge + gentle intensity flicker
        float noise = 1.0f + profile.FlameNoiseAmplitude * (0.5f - (float)_rng.NextDouble());
        float flicker = 1.0f + profile.FlickerIntensity * (0.5f - (float)_rng.NextDouble());
        Vector4 flameColor = baseColor * envelope * (1.8f * noise * flicker);

        // Core flame column
        float flameHeight = MathF.Max(0.1f, profile.FlameHeightMeters);
        float speed = 0.5f * (profile.ParticleVelocityRange.X + profile.ParticleVelocityRange.Y);
        speed = MathF.Max(speed, flameHeight * 3.0f);

        float rate = MathF.Max(0.0f, profile.EmissionRate);
        ge.AddEmission(rate * dt);
        int count = ge.ConsumeWholeParticles();
        if (count > 0)
        {
            float coneRad = MathF.Max(4.0f, profile.ConeAngleDegrees * 0.35f) * (MathF.PI / 180.0f);
            var dirs = GroundEmissionStyles.EmitCone(count, axis: Vector3.UnitY, coneAngleRadians: coneRad, rng: _rng);

            renderer.SpawnGroundEffectDirected(
                origin,
                flameColor,
                speed,
                dirs,
                particleLifetimeSeconds: MathF.Max(0.15f, profile.ParticleLifetimeSeconds),
                gravityFactor: MathF.Min(profile.GravityFactor, 0.35f));
        }

        // Occasional sparks
        float sparkRate = MathF.Max(0.0f, profile.OccasionalSparkRate);
        if (sparkRate > 0.0f)
        {
            ge.AddEmission(sparkRate * dt);
            int sparks = ge.ConsumeWholeParticles();
            if (sparks > 0)
            {
                var dirs = GroundEmissionStyles.EmitCone(sparks, axis: Vector3.UnitY, coneAngleRadians: 20.0f * (MathF.PI / 180.0f), rng: _rng);
                renderer.SpawnGroundEffectDirected(
                    origin,
                    flameColor * 1.2f,
                    speed * 1.2f,
                    dirs,
                    particleLifetimeSeconds: MathF.Min(1.1f, profile.ParticleLifetimeSeconds),
                    gravityFactor: 0.9f);
            }
        }

        // Halo / "stage lighting" (approximated with low-speed, longer-lived particles)
        float haloRate = MathF.Max(0.0f, profile.LocalLightIntensity) * 220.0f;
        float haloRadius = MathF.Max(0.5f, profile.LocalLightRadiusMeters);
        if (haloRate > 0.0f)
        {
            ge.AddEmission(haloRate * dt);
            int haloCount = ge.ConsumeWholeParticles();
            if (haloCount > 0)
            {
                float jitter = haloRadius * 0.08f;
                Vector3 haloOrigin = origin + new Vector3(
                    ((float)_rng.NextDouble() * 2.0f - 1.0f) * jitter,
                    0.0f,
                    ((float)_rng.NextDouble() * 2.0f - 1.0f) * jitter);
                var dirs = GroundEmissionStyles.EmitCone(haloCount, axis: Vector3.UnitY, coneAngleRadians: 75.0f * (MathF.PI / 180.0f), rng: _rng);

                renderer.SpawnGroundEffectDirected(
                    haloOrigin,
                    flameColor * 0.22f,
                    speed: 0.6f,
                    dirs,
                    particleLifetimeSeconds: 0.55f,
                    gravityFactor: 0.15f);
            }
        }
    }

    private void EmitLanceworkPanel(GroundEffectProfile profile, Vector3 origin, Vector4 baseColor, float envelope, float dt, float time, D3D11Renderer renderer, GroundEffectInstance ge)
    {
        int w = profile.GridWidth;
        int h = profile.GridHeight;
        if (w <= 0 || h <= 0 || profile.PatternFrames is null || profile.PatternFrames.Length == 0)
            return;

        float frameDur = MathF.Max(0.02f, profile.PatternFrameDurationSeconds);
        int frameIndex = (int)MathF.Floor(ge.ElapsedSeconds / frameDur) % profile.PatternFrames.Length;
        ulong mask = profile.PatternFrames[frameIndex];

        // panel is placed behind the canister in +Z with a small tilt (visual-only)
        float cellSpacing = 0.35f;
        Vector3 panelOrigin = origin + new Vector3(0.0f, 0.25f, 2.2f);

        float cellHeight = MathF.Max(0.05f, profile.CellFlameHeightMeters);
        float speed = MathF.Max(0.4f, cellHeight * 2.2f);
        float flicker = 1.0f + profile.CellFlickerAmount * (0.5f - (float)_rng.NextDouble());
        Vector4 col = baseColor * envelope * flicker;

        int onCells = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int bit = y * w + x;
                if (bit >= 64)
                    continue;
                if (((mask >> bit) & 1UL) == 0UL)
                    continue;

                onCells++;
                Vector3 cellPos = panelOrigin + new Vector3(
                    (x - (w - 1) * 0.5f) * cellSpacing,
                    (h - 1 - y) * cellSpacing,
                    0.0f);

                // each cell uses a small steady "flame" made of a few particles each frame
                const int particlesPerCellPerFrame = 8;
                var dirs = GroundEmissionStyles.EmitCone(particlesPerCellPerFrame, axis: Vector3.UnitY, coneAngleRadians: 10.0f * (MathF.PI / 180.0f), rng: _rng);
                renderer.SpawnGroundEffectDirected(
                    cellPos,
                    col,
                    speed,
                    dirs,
                    particleLifetimeSeconds: MathF.Max(0.2f, profile.ParticleLifetimeSeconds),
                    gravityFactor: 0.18f);
            }
        }

        // Keep a light amount of smoke for large patterns
        if (onCells >= (w * h) / 4 && profile.SmokeAmount > 0.01f && _rng.NextDouble() < profile.SmokeAmount * 0.10f)
        {
            renderer.SpawnSmoke(panelOrigin);
        }
    }

    private void EmitWaterfallCurtain(GroundEffectProfile profile, Vector3 origin, Vector4 baseColor, float envelope, float dt, float time, D3D11Renderer renderer, GroundEffectInstance ge)
    {
        int emitters = profile.EmitterCount > 0 ? profile.EmitterCount : 24;
        float width = MathF.Max(0.5f, profile.CurtainWidthMeters);
        float height = profile.EmitterHeightMeters;
        if (height <= 0.01f)
            height = 4.0f;

        float density = MathF.Max(0.0f, profile.DensityOverTime);

        // a "curtain" behind the pad (+Z)
        Vector3 lineCenter = new(origin.X, height, origin.Z + 10.0f);
        float speed = MathF.Max(0.5f, profile.SparkFallSpeed);
        float lifetime = MathF.Max(0.2f, profile.ParticleLifetimeSeconds);

        // Per-update spawn a small batch per emitter
        float perEmitterRate = MathF.Max(0.0f, profile.EmissionRate);
        if (perEmitterRate <= 0.0f)
            perEmitterRate = 380.0f;

        float totalRate = perEmitterRate * emitters * density;
        ge.AddEmission(totalRate * dt);
        int totalCount = ge.ConsumeWholeParticles();
        if (totalCount <= 0)
            return;

        Vector4 col = baseColor * envelope;
        float lateralJitter = 8.0f * (MathF.PI / 180.0f);
        var dirs = GroundEmissionStyles.EmitDownwardJitter(totalCount, lateralJitterRadians: lateralJitter, rng: _rng);

        // Spread particles along the emitter line by jittering origin per spawn call chunk.
        // Keep it simple: one call but we jitter the emitter-center a bit.
        float half = width * 0.5f;
        Vector3 o = lineCenter + new Vector3(
            ((float)_rng.NextDouble() * 2.0f - 1.0f) * half,
            0.0f,
            0.0f);

        renderer.SpawnGroundEffectDirected(
            o,
            col,
            speed,
            dirs,
            particleLifetimeSeconds: lifetime,
            gravityFactor: 1.25f);
    }

    private void EmitChaserLine(GroundEffectProfile profile, Vector3 origin, Vector4 baseColor, float envelope, float dt, float time, D3D11Renderer renderer, GroundEffectInstance ge)
    {
        int points = profile.PointCount > 0 ? profile.PointCount : 18;
        float spacing = MathF.Max(0.1f, profile.PointSpacingMeters);
        float chaseSpeed = MathF.Max(0.1f, profile.ChaseSpeed);

        // line across pad in X at ground level
        float totalLen = (points - 1) * spacing;
        Vector3 start = origin + new Vector3(-totalLen * 0.5f, 0.0f, 0.0f);

        // Determine the active point index
        float pos = ge.ElapsedSeconds * chaseSpeed;
        int idx = (int)MathF.Floor(pos) % points;
        if (profile.ReverseOrBounce)
        {
            int period = System.Math.Max(1, points - 1) * 2;
            int tIdx = (int)MathF.Floor(pos) % period;
            idx = tIdx <= (points - 1) ? tIdx : (period - tIdx);
        }

        Vector3 p = start + new Vector3(idx * spacing, 0.0f, 0.0f);

        int burstCount = profile.BurstParticlesPerPoint > 0 ? profile.BurstParticlesPerPoint : 900;
        burstCount = System.Math.Clamp(burstCount, 1, 250000);

        float spread = 25.0f * (MathF.PI / 180.0f);
        var dirs = GroundEmissionStyles.EmitUpwardPuff(burstCount, spreadRadians: spread, rng: _rng);
        Vector4 col = baseColor * envelope * 1.1f;
        renderer.SpawnGroundEffectDirected(
            p,
            col,
            speed: MathF.Max(0.5f, profile.BurstVelocity),
            dirs,
            particleLifetimeSeconds: MathF.Max(0.25f, profile.ParticleLifetimeSeconds),
            gravityFactor: MathF.Max(0.4f, profile.GravityFactor));
    }

    private void EmitGroundBloom(GroundEffectProfile profile, Vector3 origin, Vector4 baseColor, float envelope, float dt, float time, D3D11Renderer renderer, GroundEffectInstance ge)
    {
        Vector4 col = baseColor * envelope;

        float baseOmega = profile.AngularVelocityRadiansPerSec;
        float omega = baseOmega;
        if (MathF.Abs(profile.SpinRateOverTime) > 1e-4f)
            omega = baseOmega + profile.SpinRateOverTime * ge.ElapsedSeconds;

        float phase = omega * ge.ElapsedSeconds;

        float rate = MathF.Max(0.0f, profile.EmissionRate);
        ge.AddEmission(rate * dt);
        int count = ge.ConsumeWholeParticles();
        if (count <= 0)
            return;

        // Slight drift along ground
        Vector3 drift = profile.GroundDriftVelocity;
        Vector3 driftedOrigin = origin + new Vector3(drift.X, 0.0f, drift.Z) * ge.ElapsedSeconds;

        float speed = 0.5f * (profile.ParticleVelocityRange.X + profile.ParticleVelocityRange.Y);
        speed = MathF.Max(speed, 6.0f);
        var dirs = GroundEmissionStyles.EmitSpinnerTangents(count, phase, axis: profile.SpinnerAxis ?? Vector3.UnitY, rng: _rng);

        renderer.SpawnGroundEffectDirected(
            driftedOrigin,
            col,
            speed,
            dirs,
            particleLifetimeSeconds: MathF.Max(0.2f, profile.ParticleLifetimeSeconds),
            gravityFactor: profile.GravityFactor);
    }

    private void EmitPulsingGlitterFountain(GroundEffectProfile profile, Vector3 origin, Vector4 baseColor, float envelope, float dt, float time, D3D11Renderer renderer, GroundEffectInstance ge)
    {
        float hz = MathF.Max(0.1f, profile.PulseFrequencyHz);
        float pulse = 0.5f + 0.5f * MathF.Sin(MathF.Tau * hz * ge.ElapsedSeconds);
        float depth = System.Math.Clamp(profile.PulseDepth, 0.0f, 1.0f);
        float mul = (1.0f - depth) + depth * pulse;

        // Use fountain logic but with pulsing emission and occasional "glitter" spikes.
        float flicker = 1.0f + profile.FlickerIntensity * (0.5f - (float)_rng.NextDouble());
        Vector4 col = baseColor * envelope * flicker;

        float rate = MathF.Max(0.0f, profile.EmissionRate) * (0.4f + 1.6f * mul);
        ge.AddEmission(rate * dt);
        int count = ge.ConsumeWholeParticles();
        if (count <= 0)
            return;

        float coneRad = profile.ConeAngleDegrees * (MathF.PI / 180.0f);
        var dirs = GroundEmissionStyles.EmitCone(count, axis: Vector3.UnitY, coneAngleRadians: coneRad, rng: _rng);
        float speed = 0.5f * (profile.ParticleVelocityRange.X + profile.ParticleVelocityRange.Y);

        // Split into base + glitter
        float glitterRatio = System.Math.Clamp(profile.GlitterParticleRatio, 0.0f, 1.0f);
        int glitter = (int)(count * glitterRatio);
        int baseCount = count - glitter;

        if (baseCount > 0)
        {
            renderer.SpawnGroundEffectDirected(
                origin,
                col,
                speed,
                dirs.AsSpan(0, baseCount),
                particleLifetimeSeconds: profile.ParticleLifetimeSeconds,
                gravityFactor: profile.GravityFactor);
        }

        if (glitter > 0)
        {
            float glow = 1.0f + 1.5f * mul;
            renderer.SpawnGroundEffectDirected(
                origin,
                col * glow,
                speed * 1.15f,
                dirs.AsSpan(baseCount, glitter),
                particleLifetimeSeconds: MathF.Max(0.12f, profile.GlowDecayTimeSeconds),
                gravityFactor: profile.GravityFactor);
        }
    }

    private void EmitFountain(GroundEffectProfile profile, Vector3 origin, Vector4 baseColor, float envelope, float dt, float time, D3D11Renderer renderer, GroundEffectInstance ge)
    {
        float flicker = 1.0f + profile.FlickerIntensity * (0.5f - (float)_rng.NextDouble());
        baseColor *= envelope * flicker;

        float rate = System.Math.Max(0.0f, profile.EmissionRate);
        ge.AddEmission(rate * dt);
        int count = ge.ConsumeWholeParticles();
        if (count <= 0)
            return;

        float coneRad = profile.ConeAngleDegrees * (MathF.PI / 180.0f);
        var dirs = GroundEmissionStyles.EmitCone(count, axis: Vector3.UnitY, coneAngleRadians: coneRad, rng: _rng);

        float speed = 0.5f * (profile.ParticleVelocityRange.X + profile.ParticleVelocityRange.Y);
        renderer.SpawnGroundEffectDirected(
            origin,
            baseColor,
            speed,
            dirs,
            particleLifetimeSeconds: profile.ParticleLifetimeSeconds,
            gravityFactor: profile.GravityFactor);

        if (profile.SmokeAmount > 0.01f && _rng.NextDouble() < profile.SmokeAmount * 0.15f)
        {
            renderer.SpawnSmoke(origin);
        }
    }

    private void EmitSpinner(GroundEffectProfile profile, Vector3 origin, Vector4 baseColor, float envelope, float dt, float time, D3D11Renderer renderer, GroundEffectInstance ge)
    {
        baseColor *= envelope;

        float rate = System.Math.Max(0.0f, profile.EmissionRate);
        ge.AddEmission(rate * dt);
        int count = ge.ConsumeWholeParticles();
        if (count <= 0)
            return;

        float omega = profile.AngularVelocityRadiansPerSec;
        float phase = omega * ge.ElapsedSeconds;

        float speed = 0.5f * (profile.ParticleVelocityRange.X + profile.ParticleVelocityRange.Y);
        var dirs = GroundEmissionStyles.EmitSpinnerTangents(count, phase, axis: profile.SpinnerAxis ?? Vector3.UnitY, rng: _rng);

        Vector3 jitter = new(
            ((float)_rng.NextDouble() * 2.0f - 1.0f) * 0.03f,
            0.0f,
            ((float)_rng.NextDouble() * 2.0f - 1.0f) * 0.03f);

        renderer.SpawnGroundEffectDirected(
            origin + jitter,
            baseColor,
            speed,
            dirs,
            particleLifetimeSeconds: profile.ParticleLifetimeSeconds,
            gravityFactor: profile.GravityFactor);
    }

    private void EmitStrobe(GroundEffectProfile profile, Vector3 origin, Vector4 baseColor, float envelope, float dt, float time, D3D11Renderer renderer, GroundEffectInstance ge)
    {
        float interval = System.Math.Max(0.02f, profile.FlashIntervalSeconds);
        float duty = System.Math.Clamp(profile.FlashDutyCycle, 0.05f, 0.95f);

        float phase = (ge.ElapsedSeconds % interval) / interval;
        bool on = phase < duty;

        float baseRate = System.Math.Max(0.0f, profile.EmissionRate);
        float onMul = on ? profile.FlashBrightness : profile.ResidualSparkDensity;

        ge.AddEmission(baseRate * onMul * dt);
        int count = ge.ConsumeWholeParticles();
        if (count <= 0)
            return;

        baseColor *= envelope * (on ? profile.FlashBrightness : 0.35f);

        var dirs = GroundEmissionStyles.EmitCone(count, axis: Vector3.UnitY, coneAngleRadians: 12.0f * (MathF.PI / 180.0f), rng: _rng);
        float speed = 0.5f * (profile.ParticleVelocityRange.X + profile.ParticleVelocityRange.Y);

        renderer.SpawnGroundEffectDirected(
            origin,
            baseColor,
            speed,
            dirs,
            particleLifetimeSeconds: profile.ParticleLifetimeSeconds,
            gravityFactor: profile.GravityFactor);
    }

    private void EmitMine(GroundEffectProfile profile, Vector3 origin, Vector4 baseColor, float envelope, float dt, float time, D3D11Renderer renderer, GroundEffectInstance ge)
    {
        float burstRate = System.Math.Max(0.1f, profile.BurstRate);
        int expectedBurst = (int)MathF.Floor(ge.ElapsedSeconds * burstRate);

        while (ge.BurstCounter <= expectedBurst)
        {
            ge.NextBurstIndex();

            int count = System.Math.Clamp(profile.ParticlesPerBurst, 1, 250000);
            float coneRad = profile.ConeAngleDegrees * (MathF.PI / 180.0f);
            var dirs = GroundEmissionStyles.EmitCone(count, axis: Vector3.UnitY, coneAngleRadians: coneRad, rng: _rng);

            float speed = 0.5f * (profile.ParticleVelocityRange.X + profile.ParticleVelocityRange.Y);
            renderer.SpawnGroundEffectDirected(
                origin,
                baseColor * envelope,
                speed,
                dirs,
                particleLifetimeSeconds: profile.ParticleLifetimeSeconds,
                gravityFactor: profile.GravityFactor);

            if (profile.SmokeAmount > 0.01f)
            {
                renderer.SpawnSmoke(origin);
            }
        }
    }

    private void Explode(ShellExplosion explosion, D3D11Renderer renderer)
    {
        if (explosion.BurstShape == FireworkBurstShape.PeonyToWillow)
        {
            // Initial peony-style burst
            var dirs = EmissionStyles.EmitPeony(Math.Max(1, explosion.ParticleCount));
            Vector4 baseColor = explosion.BaseColor;
            float speed = 10.0f; // peony base speed
            float lifetime = explosion.ParticleLifetimeSeconds;

            renderer.SpawnBurstDirectedExplode(
                explosion.Position,
                baseColor,
                speed,
                dirs,
                particleLifetimeSeconds: lifetime,
                sparkleRateHz: explosion.BurstSparkleRateHz,
                sparkleIntensity: explosion.BurstSparkleIntensity);
            renderer.SpawnSmoke(explosion.Position);

            // Schedule willow takeover
            var p = explosion.PeonyToWillowParams;
            _pendingWillow.Add(new PendingWillowHandoff(
                TriggerTime: ShowTimeSeconds + MathF.Max(0.0f, p.HandoffDelaySeconds),
                Position: explosion.Position,
                ParentVelocity: Vector3.Zero,
                Params: p,
                ColorScheme: _profiles.ColorSchemes.Values.FirstOrDefault() ?? new ColorScheme("default", new[] { System.Windows.Media.Colors.Gold }, 0.05f, 1.0f)));

            EmitSound(new SoundEvent(
                SoundEventType.ShellBurst,
                Position: explosion.Position,
                Gain: 1.0f,
                Loop: false));
            return;
        }

        if (explosion.BurstShape == FireworkBurstShape.FinaleSalute)
        {
            SpawnFinaleSalute(explosion.Position, explosion.FinaleSalute);
            EmitSound(new SoundEvent(
                SoundEventType.FinaleCluster,
                Position: explosion.Position,
                Gain: 1.0f,
                Loop: false));
            return;
        }

        if (explosion.BurstShape == FireworkBurstShape.Comet)
        {
            SpawnComet(explosion.Position, explosion.Comet, explosion.BaseColor);
            EmitSound(new SoundEvent(
                SoundEventType.ShellBurst,
                Position: explosion.Position,
                Gain: 0.6f,
                Loop: false));
            return;
        }

        if (explosion.BurstShape == FireworkBurstShape.SubShellSpokeWheelPop)
        {
            SpawnSpokeWheelPop(explosion.Position, explosion.SubShellSpokeWheelPop, explosion.ColorScheme, explosion.BaseColor);
            EmitSound(new SoundEvent(
                SoundEventType.ShellBurst,
                Position: explosion.Position,
                Gain: 0.65f,
                Loop: false));
            return;
        }

        Vector3 ringAxis = Vector3.UnitY;
        if (explosion.RingAxis is { } configuredRingAxis && configuredRingAxis.LengthSquared() >= 1e-6f)
            ringAxis = Vector3.Normalize(configuredRingAxis);

        if (explosion.RingAxisRandomTiltDegrees > 0.0f)
        {
            float maxTiltRadians = explosion.RingAxisRandomTiltDegrees * (MathF.PI / 180.0f);

            // Randomize the axis within a cone centered on the configured axis.
            // This produces variation in all directions (not just a single world-space axis).
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

        var dirsDefault = explosion.BurstShape switch
        {
            FireworkBurstShape.Peony => EmissionStyles.EmitPeony(explosion.ParticleCount),
              FireworkBurstShape.Chrysanthemum => EmissionStyles.EmitChrysanthemum(explosion.ParticleCount, explosion.Emission),
              FireworkBurstShape.Willow => EmissionStyles.EmitWillow(explosion.ParticleCount, explosion.Emission),
              FireworkBurstShape.Palm => EmissionStyles.EmitPalm(explosion.ParticleCount, explosion.Emission),
            FireworkBurstShape.Ring => EmissionStyles.EmitRing(explosion.ParticleCount, axis: ringAxis),
              FireworkBurstShape.Horsetail => EmissionStyles.EmitHorsetail(explosion.ParticleCount, axis: ringAxis, settings: explosion.Emission),
            FireworkBurstShape.DoubleRing => EmissionStyles.EmitDoubleRing(explosion.ParticleCount, axis: ringAxis),
            FireworkBurstShape.Spiral => EmissionStyles.EmitSpiral(explosion.ParticleCount),
            _ => EmissionStyles.EmitPeony(explosion.ParticleCount)
        };

        Vector4 baseColorDefault = explosion.BaseColor;

        float speedDefault = explosion.BurstSpeed ?? (explosion.BurstShape switch
        {
            FireworkBurstShape.Willow => 7.0f,
            FireworkBurstShape.Palm => 13.0f,
            FireworkBurstShape.Horsetail => 6.0f,
            _ => 10.0f
        });

        float lifetimeDefault = explosion.BurstShape switch
        {
            FireworkBurstShape.Willow => explosion.ParticleLifetimeSeconds * 2.2f,
            FireworkBurstShape.Palm => explosion.ParticleLifetimeSeconds * 1.2f,
            FireworkBurstShape.Horsetail => explosion.ParticleLifetimeSeconds * 2.5f,
            _ => explosion.ParticleLifetimeSeconds
        };

        renderer.SpawnBurstDirectedExplode(
            explosion.Position,
            baseColorDefault,
            speedDefault,
            dirsDefault,
            particleLifetimeSeconds: lifetimeDefault,
            sparkleRateHz: explosion.BurstSparkleRateHz,
            sparkleIntensity: explosion.BurstSparkleIntensity);
        renderer.SpawnSmoke(explosion.Position);

        EmitSound(new SoundEvent(
            SoundEventType.ShellBurst,
            Position: explosion.Position,
            Gain: 1.0f,
            Loop: false));

        if (explosion.BurstSparkleRateHz > 0.0f && explosion.BurstSparkleIntensity > 0.0f)
        {
            EmitSound(new SoundEvent(
                SoundEventType.Crackle,
                Position: explosion.Position,
                Gain: System.Math.Clamp(explosion.BurstSparkleIntensity, 0.15f, 1.0f),
                Loop: false));
        }
    }

    private void ProcessWillowHandoff(in PendingWillowHandoff pending, D3D11Renderer renderer)
    {
        // Spawn subshells representing willow embers using configured subshell profile
        if (!_profiles.SubShells.TryGetValue(pending.Params.WillowSubshellProfileId, out var subProf))
            return;

        int count = Math.Max(1, (int)MathF.Round(pending.Params.PeonySparkCount * pending.Params.HandoffFraction));
        // Use willow emission: bias downward like willow
        var dirs = EmissionStyles.EmitWillow(count, BurstEmissionSettings.Defaults);

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 dir = dirs[i];
            Vector3 pos = pending.Position + EmissionStyles.RandomUnitVector() * pending.Params.PeonySpeedMax * 0.02f;
            Vector3 vel = dir * (pending.Params.PeonySpeedMax * pending.Params.WillowVelocityScale);

            if (!_profiles.Shells.TryGetValue(subProf.ShellProfileId, out var childProfile))
                continue;

            var schemeId = subProf.ColorSchemeId ?? childProfile.ColorSchemeId;
            if (!_profiles.ColorSchemes.TryGetValue(schemeId, out var childScheme))
                childScheme = pending.ColorScheme;

            var child = new FireworkShell(
                childProfile,
                childScheme,
                pos,
                vel,
                dragK: ShellDragK * pending.Params.WillowDragMultiplier,
                fuseOverrideSeconds: childProfile.FuseTimeSeconds * pending.Params.WillowLifetimeMultiplier)
            {
                SubshellDepth = 1,
                ParentShellId = null,
                BurstShapeOverride = FireworkBurstShape.Willow,
                ShellId = _nextShellId++
            };

            if (renderer.ShellsGpuRendered)
            {
                _gpuShells[child.ShellId] = child;
                renderer.QueueShellGpu(pos, vel, child.FuseTimeSeconds, ShellDragK * pending.Params.WillowDragMultiplier, ColorUtil.PickBaseColor(childScheme), child.ShellId);
            }
            else
            {
                _shells.Add(child);
            }
        }
    }

    private void ProcessGpuDetonations(D3D11Renderer renderer)
    {
        if (!renderer.ShellsGpuRendered || _gpuShells.Count == 0)
            return;

        var span = _detonationBuffer.AsSpan();
        int count = renderer.ReadDetonations(span);
        for (int i = 0; i < count; i++)
        {
            var ev = span[i];
            if (!_gpuShells.TryGetValue(ev.ShellId, out var shell))
                continue;

            var explosion = new ShellExplosion(
                ev.Position,
                BurstShape: shell.BurstShapeOverride ?? shell.Profile.BurstShape,
                ExplosionRadius: shell.Profile.ExplosionRadius,
                ParticleCount: shell.Profile.ParticleCount,
                ParticleLifetimeSeconds: shell.Profile.ParticleLifetimeSeconds,
                BurstSpeed: shell.Profile.BurstSpeed,
                BurstSparkleRateHz: shell.Profile.BurstSparkleRateHz,
                BurstSparkleIntensity: shell.Profile.BurstSparkleIntensity,
                RingAxis: shell.Profile.RingAxis,
                RingAxisRandomTiltDegrees: shell.Profile.RingAxisRandomTiltDegrees,
                Emission: shell.Profile.EmissionSettings,
                FinaleSalute: shell.Profile.FinaleSaluteParams,
                Comet: shell.Profile.CometParams,
                BaseColor: ev.BaseColor,
                PeonyToWillowParams: shell.Profile.PeonyToWillowParams,
                SubShellSpokeWheelPop: shell.Profile.SubShellSpokeWheelPopParams,
                ColorScheme: shell.ColorScheme);

            Explode(explosion, renderer);
            _gpuShells.Remove(ev.ShellId);
        }
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

    public uint ShellId { get; init; }

    public Vector3 Position { get; private set; }
    public Vector3 Velocity { get; private set; }
    public float AgeSeconds { get; private set; }
    public bool Alive { get; private set; } = true;

    // Subshell nesting and optional runtime overrides
    public int SubshellDepth { get; init; } = 0;
    public string? ParentShellId { get; init; }
    public FireworkBurstShape? BurstShapeOverride { get; init; }

    // *** CHANGED: drag comes from engine, fuse can be overridden
    public float DragK { get; private set; }
    public float FuseTimeSeconds { get; }

    private enum ShellTerminalState
    {
        Active,
        Fading
    }

    private ShellTerminalState _terminalState = ShellTerminalState.Active;
    private float _fadeRemainingSeconds;
    private float _fadeDurationSeconds;

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

        if (_terminalState == ShellTerminalState.Fading)
        {
            _fadeRemainingSeconds = MathF.Max(0.0f, _fadeRemainingSeconds - dt);
            if (_fadeRemainingSeconds <= 0.0f)
            {
                Alive = false;
                return;
            }
        }

        if (Position.Y <= GroundY)
            Alive = false;
    }

    public void EmitTrail(D3D11Renderer renderer, float dt)
    {
        if (!Alive || Velocity.LengthSquared() < 1e-4f)
            return;

        Vector3 dir = Vector3.Normalize(Velocity);

        int particleCount = Math.Clamp(Profile.TrailParticleCount, 1, 64);
        Span<Vector3> dirs = particleCount <= 64
            ? stackalloc Vector3[64]
            : new Vector3[particleCount];

        dirs = dirs.Slice(0, particleCount);
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

        if (_terminalState == ShellTerminalState.Fading)
        {
            float t = _fadeDurationSeconds > 1e-4f ? (_fadeRemainingSeconds / _fadeDurationSeconds) : 0.0f;
            t = t < 0.0f ? 0.0f : (t > 1.0f ? 1.0f : t);
            trailColor *= t;
        }
        // trailColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f); // pure green for debugging

        renderer.SpawnBurstDirected(
            Position,
            trailColor,
            speed: MathF.Max(0.0f, Profile.TrailSpeed),
            directions: dirs,
            particleLifetimeSeconds: MathF.Max(0.01f, Profile.TrailParticleLifetimeSeconds));

        if (_rng.NextDouble() < Math.Clamp(Profile.TrailSmokeChance, 0.0f, 1.0f))
        {
            renderer.SpawnSmoke(Position);
        }
    }

    public bool IsTerminalFading => _terminalState == ShellTerminalState.Fading;

    public void BeginTerminalFade(float seconds)
    {
        if (!Alive)
            return;

        if (_terminalState == ShellTerminalState.Fading)
            return;

        _fadeDurationSeconds = MathF.Max(0.0f, seconds);
        _fadeRemainingSeconds = _fadeDurationSeconds;
        _terminalState = ShellTerminalState.Fading;

        if (_fadeDurationSeconds <= 0.0f)
            Alive = false;
    }

    public bool TryExplode(out ShellExplosion explosion)
    {
        if (!Alive)
        {
            explosion = default;
            return false;
        }

        if (_terminalState == ShellTerminalState.Fading)
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

        if (Profile.SuppressBurst)
        {
            BeginTerminalFade(Profile.TerminalFadeOutSeconds);
            explosion = default;
            return false;
        }

        explosion = new ShellExplosion(
            Position,
            BurstShape: BurstShapeOverride ?? Profile.BurstShape,
            ExplosionRadius: Profile.ExplosionRadius,
            ParticleCount: Profile.ParticleCount,
            ParticleLifetimeSeconds: Profile.ParticleLifetimeSeconds,
            BurstSpeed: Profile.BurstSpeed,
            BurstSparkleRateHz: Profile.BurstSparkleRateHz,
            BurstSparkleIntensity: Profile.BurstSparkleIntensity,
            RingAxis: Profile.RingAxis,
            RingAxisRandomTiltDegrees: Profile.RingAxisRandomTiltDegrees,
            Emission: Profile.EmissionSettings,
            FinaleSalute: Profile.FinaleSaluteParams,
            Comet: Profile.CometParams,
            BaseColor: ColorUtil.PickBaseColor(ColorScheme),
            PeonyToWillowParams: Profile.PeonyToWillowParams,
            SubShellSpokeWheelPop: Profile.SubShellSpokeWheelPopParams,
            ColorScheme: ColorScheme);

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
    float? BurstSpeed,
    float BurstSparkleRateHz,
    float BurstSparkleIntensity,
    Vector3? RingAxis,
    float RingAxisRandomTiltDegrees,
    BurstEmissionSettings Emission,
    FinaleSaluteParams FinaleSalute,
    CometParams Comet,
    Vector4 BaseColor,
    PeonyToWillowParams PeonyToWillowParams,
    SubShellSpokeWheelPopParams SubShellSpokeWheelPop,
    ColorScheme ColorScheme);

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

    public static Vector4 FromColor(Color color, float boost = 2.2f)
    {
        float r = Clamp01(color.R / 255.0f);
        float g = Clamp01(color.G / 255.0f);
        float b = Clamp01(color.B / 255.0f);
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
    public static Vector3[] EmitChrysanthemum(int count, BurstEmissionSettings settings)
    {
        // More spokes => more distinct "radial streaks" without looking like only a few sheets.
        int spokeCount = settings.ChrysanthemumSpokeCount;

        // How wide each spoke's cone is (bigger => less spiky / more peony-like).
        float spokeJitter = settings.ChrysanthemumSpokeJitter;

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
    public static Vector3[] EmitWillow(int count, BurstEmissionSettings settings)
    {
        float downwardBlend = settings.WillowDownwardBlend; // 0 = peony-like, 1 = straight down

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
    public static Vector3[] EmitPalm(int count, BurstEmissionSettings settings)
    {
        // Fewer, stronger fronds = clearer palm shape.
        int frondCount = settings.PalmFrondCount;

        // Angle of fronds away from +Y (0 = straight up, pi/2 = horizontal).
        // ~35–40° feels very palm-like.
        float frondConeAngle = settings.PalmFrondConeAngleRadians;

        // How much we let each particle deviate from its frond direction.
        // Smaller => tighter fronds.
        float frondJitterAngle = settings.PalmFrondJitterAngleRadians;

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
    public static Vector3[] EmitHorsetail(int count, BurstEmissionSettings settings)
        => EmitHorsetail(count, axis: Vector3.UnitY, settings: settings);

    /// <summary>
    /// Horsetail, oriented: like <see cref="EmitHorsetail(int)"/>, but the tail direction is based on an axis.
    /// The emission is biased toward <c>-axis</c>.
    /// </summary>
    public static Vector3[] EmitHorsetail(int count, Vector3 axis, BurstEmissionSettings settings)
    {
        var dirs = new Vector3[count];

        float downwardBlend = settings.HorsetailDownwardBlend;   // how hard we pull toward the tail direction
        float minUpDot = settings.HorsetailMinDownDot;           // clamp so it never starts too aligned with +axis
        float jitterAngle = settings.HorsetailJitterAngleRadians;

        axis = axis.LengthSquared() < 1e-6f ? Vector3.UnitY : Vector3.Normalize(axis);
        Vector3 tailDir = -axis;

        for (int i = 0; i < count; i++)
        {
            // Start from a random direction
            Vector3 d = RandomUnitVector();

            // Ensure it's at least somewhat toward the tail direction by clamping against +axis.
            // When dot(d, +axis) is too large, remove some component along +axis.
            float upDot = Vector3.Dot(d, axis);
            if (upDot > minUpDot)
            {
                // Move d away from +axis and renormalize. Slight randomness helps avoid patterns.
                d -= axis * (upDot - minUpDot);
                if (d.LengthSquared() < 1e-8f)
                    d = tailDir;
                d = Vector3.Normalize(d);
            }

            // Strongly bias toward tail direction
            d = Vector3.Normalize(Vector3.Lerp(d, tailDir, downwardBlend));

            // Add a bit of cone jitter so it isn't a razor-thin column
            d = JitterDirection(d, jitterAngle);

            dirs[i] = d;
        }

        return dirs;
    }

    public static Vector3[] EmitDoubleRing(int count, Vector3 axis)
    {
        if (count <= 0)
            return Array.Empty<Vector3>();

        // Normalize axis or default to +Y if degenerate.
        axis = axis.LengthSquared() > 1e-6f ? Vector3.Normalize(axis) : Vector3.UnitY;

        // Build an orthonormal basis {basis1, basis2, axis}.
        Vector3 tmp = MathF.Abs(axis.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 basis1 = Vector3.Normalize(Vector3.Cross(axis, tmp));
        Vector3 basis2 = Vector3.Normalize(Vector3.Cross(axis, basis1));

        var dirs = new Vector3[count];

        int half = Math.Max(1, count / 2);
        float twoPi = (float)(Math.PI * 2.0);

        // Two latitudes measured from the axis:
        // innerRing ≈ equator, outerRing closer to the axis.
        float innerPhi = 80.0f * (MathF.PI / 180.0f); // near equator
        float outerPhi = 40.0f * (MathF.PI / 180.0f); // “inner” ring, more vertical

        for (int i = 0; i < count; i++)
        {
            bool outer = i >= half;
            int idxInRing = outer ? (i - half) : i;
            int ringCount = outer ? Math.Max(1, count - half) : half;

            float t = ringCount > 1 ? idxInRing / (ringCount - 1.0f) : 0.0f;
            float angle = t * twoPi;

            float phi = outer ? outerPhi : innerPhi;
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);

            Vector3 inPlane = basis1 * MathF.Cos(angle) + basis2 * MathF.Sin(angle);
            Vector3 dir = axis * cosPhi + inPlane * sinPhi;

            // Tiny random jitter so it feels organic.
            const float jitter = 0.03f;
            dir += new Vector3(
                (float)(s_rng.NextDouble() * 2.0 - 1.0) * jitter,
                (float)(s_rng.NextDouble() * 2.0 - 1.0) * jitter,
                (float)(s_rng.NextDouble() * 2.0 - 1.0) * jitter);

            dirs[i] = Vector3.Normalize(dir);
        }

        return dirs;
    }

    public static Vector3[] EmitSpiral(
    int count,
    int armCount = 5,
    float twistCount = 2.5f,
    float pitch = 1.2f)
    {
        if (count <= 0)
            return Array.Empty<Vector3>();

        var dirs = new Vector3[count];
        float twoPi = (float)(Math.PI * 2.0);

        // Random phase per arm so it’s not perfectly symmetrical.
        var armPhase = new float[armCount];
        for (int a = 0; a < armCount; a++)
            armPhase[a] = (float)(s_rng.NextDouble() * twoPi);

        for (int i = 0; i < count; i++)
        {
            int arm = i % armCount;
            float t = (float)i / Math.Max(1, count - 1); // 0..1
            float angle = t * twistCount * twoPi + armPhase[arm];

            // Height sweeps from -pitch..+pitch
            float height = (t - 0.5f) * pitch * 2.0f;

            Vector3 dir = new Vector3(
                MathF.Cos(angle),
                height,
                MathF.Sin(angle));

            // Slight jitter so arms look fiery, not geometric.
            const float jitter = 0.15f;
            dir += new Vector3(
                (float)(s_rng.NextDouble() * 2.0 - 1.0) * jitter,
                (float)(s_rng.NextDouble() * 2.0 - 1.0) * jitter * 0.5f,
                (float)(s_rng.NextDouble() * 2.0 - 1.0) * jitter);

            dirs[i] = Vector3.Normalize(dir);
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

    public static Vector3 RandomUnitVector()
    {
        float z = (float)(s_rng.NextDouble() * 2.0 - 1.0);
        float a = (float)(s_rng.NextDouble() * Math.PI * 2.0);
        float r = MathF.Sqrt(MathF.Max(0.0f, 1.0f - z * z));
        return new Vector3(r * MathF.Cos(a), z, r * MathF.Sin(a));
    }

}

