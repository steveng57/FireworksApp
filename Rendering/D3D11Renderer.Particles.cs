using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using FireworksApp.Simulation;

namespace FireworksApp.Rendering;

public sealed partial class D3D11Renderer
{
    public bool QueueShellGpu(Vector3 position, Vector3 velocity, float fuseSeconds, float dragK, Vector4 baseColor, uint shellId)
    {
        if (_context is null || !_particlesPipeline.CanGpuSpawn)
            return false;

        int start = _particleWriteCursor;

        bool queued = _particlesPipeline.QueueShellSpawn(
            start,
            position,
            velocity,
            fuseSeconds,
            dragK,
            baseColor,
            shellId);

        if (!queued)
            return false;

        _particleWriteCursor = (start + 1) % _particleCapacity;
        return true;
    }

    private static uint PackFloat(float value)
        => (uint)BitConverter.SingleToInt32Bits(value);

    public void SpawnBurst(Vector3 position, Vector4 baseColor, int count, float sparkleRateHz = 0.0f, float sparkleIntensity = 0.0f)
    {
        if (_context is null)
            return;

        count = Math.Clamp(count, 1, _particleCapacity);
        int start = _particleWriteCursor;

        if (_particlesPipeline.CanGpuSpawn)
        {
            var dirs = ArrayPool<Vector3>.Shared.Rent(count);
            try
            {
                for (int i = 0; i < count; i++)
                    dirs[i] = RandomUnitVector();

                int remaining = count;
                int offset = 0;
                int cursor = start;
                const float crackleProbability = 0.22f;

                while (remaining > 0)
                {
                    int chunk = Math.Min(remaining, _particleCapacity - cursor);
                    _particlesPipeline.QueueSparkBurstSpawn(
                        particleStart: cursor,
                        directions: dirs.AsSpan(offset, chunk),
                        origin: position,
                        baseColor: baseColor,
                        speed: 12.0f,
                        lifetimeSeconds: 2.8f,
                        sparkleRateHz: sparkleRateHz,
                        sparkleIntensity: sparkleIntensity,
                        crackleProbability: crackleProbability,
                        seed: (uint)_rng.Next());

                    cursor = (cursor + chunk) % _particleCapacity;
                    offset += chunk;
                    remaining -= chunk;
                }

                _particleWriteCursor = (start + count) % _particleCapacity;
                return;
            }
            finally
            {
                ArrayPool<Vector3>.Shared.Return(dirs);
            }
        }

        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (particleBuffer is null || uploadBuffer is null)
            return;

        var staging = RentParticleArray(count);
        try
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = RandomUnitVector();
                float u = _rng.NextSingle();
                float speed = 6.0f + (u * u) * 12.0f;
                Vector3 vel = dir * speed;

                bool crackle = (_rng.NextSingle() < 0.22f);

                float lifetime;
                if (crackle)
                {
                    lifetime = 0.03f + _rng.NextSingle() * 0.06f;
                }
                else
                {
                    lifetime = 2.0f + _rng.NextSingle() * 2.0f;
                }

                staging[i] = new GpuParticle
                {
                    Position = position,
                    Velocity = vel,
                    Age = 0.0f,
                    Lifetime = lifetime,
                    BaseColor = baseColor,
                    Color = baseColor,
                    Kind = crackle ? (uint)ParticleKind.Crackle : (uint)ParticleKind.Spark,
                    _pad0 = crackle ? (uint)_rng.Next() : PackFloat(sparkleRateHz),
                    _pad1 = crackle ? (uint)_rng.Next() : PackFloat(sparkleIntensity),
                    _pad2 = (uint)_rng.Next()
                };
            }

            WriteParticlesToBuffer(staging, start, count, particleBuffer, uploadBuffer);
        }
        finally
        {
            ReturnParticleArray(staging);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    private void UpdateParticles(float scaledDt, bool useInterpolatedState)
    {
        if (_context is null)
            return;

        _particlesPipeline.Update(_context, _view, _proj, _schemeTint, scaledDt);
    }

    private void SpawnShellTrails(float dt, IReadOnlyList<ShellRenderState> shells)
    {
        if (_context is null)
            return;

        if (dt <= 0.0f || shells.Count == 0)
            return;

        float expected = ShellTrailParticlesPerSecond * dt;
        int baseCount = expected > 0 ? (int)expected : 0;

        foreach (var shell in shells)
        {
            var vel = shell.Velocity;
            if (vel.LengthSquared() < 1e-8f)
                continue;

            int count = baseCount;
            float frac = expected - baseCount;
            if (_rng.NextSingle() < frac)
                count++;

            if (count <= 0)
                continue;

            SpawnShellTrail(shell.Position, vel, count);
        }
    }

    public void SpawnPopFlash(Vector3 position, float lifetimeSeconds, float size, float peakIntensity, float fadeGamma, Vector4 baseColor)
    {
        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (_context is null || particleBuffer is null || uploadBuffer is null)
            return;

        int stride = GpuParticleStride;
        int start = _particleWriteCursor;

        var p = new GpuParticle
        {
            Position = position,
            Velocity = Vector3.Zero,
            Age = 0.0f,
            Lifetime = Math.Max(0.01f, lifetimeSeconds),
            BaseColor = baseColor,
            Color = baseColor,
            Kind = (uint)ParticleKind.PopFlash,
            _pad0 = PackFloat(Math.Max(0.0f, size)),
            _pad1 = PackFloat(Math.Max(0.0f, peakIntensity)),
            _pad2 = PackFloat(Math.Max(0.0f, fadeGamma))
        };

        var mapped = _context.Map(uploadBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            unsafe
            {
                *(GpuParticle*)mapped.DataPointer = p;
            }
        }
        finally
        {
            _context.Unmap(uploadBuffer, 0);
        }

        var srcBox = new Box
        {
            Left = 0,
            Right = stride,
            Top = 0,
            Bottom = 1,
            Front = 0,
            Back = 1
        };

        _context.CopySubresourceRegion(particleBuffer, 0, (uint)(start * stride), 0, 0, uploadBuffer, 0, srcBox);

        _particleWriteCursor = (start + 1) % _particleCapacity;
    }

    public void SpawnFinaleSaluteSparks(
        Vector3 position,
        int particleCount,
        float baseSpeed,
        float speedJitterFrac,
        float particleLifetimeMinSeconds,
        float particleLifetimeMaxSeconds,
        float sparkleRateHzMin,
        float sparkleRateHzMax,
        float sparkleIntensity,
        float microFragmentChance,
        float microLifetimeMinSeconds,
        float microLifetimeMaxSeconds,
        float microSpeedMulMin,
        float microSpeedMulMax)
    {
        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (_context is null || particleBuffer is null || uploadBuffer is null)
            return;

        int count = Math.Clamp(particleCount, 1, _particleCapacity);

        int stride = GpuParticleStride;
        int start = _particleWriteCursor;

        int maxExtra = (int)MathF.Min(_particleCapacity - count, count * 0.9f);
        int cap = Math.Clamp(count + maxExtra, 1, _particleCapacity);
        var staging = RentParticleArray(cap);
        int produced = 0;

        Vector4 baseColor = new(1.0f, 1.0f, 1.0f, 1.0f);

        float sj = Math.Clamp(speedJitterFrac, 0.0f, 0.65f);
        float lifeMin = Math.Max(0.02f, particleLifetimeMinSeconds);
        float lifeMax = Math.Max(lifeMin, particleLifetimeMaxSeconds);

        float microChance = Math.Clamp(microFragmentChance, 0.0f, 1.0f);
        float microLifeMin = Math.Max(0.02f, microLifetimeMinSeconds);
        float microLifeMax = Math.Max(microLifeMin, microLifetimeMaxSeconds);

        float rateMin = Math.Max(0.0f, sparkleRateHzMin);
        float rateMax = Math.Max(rateMin, sparkleRateHzMax);
        float inten = Math.Max(0.0f, sparkleIntensity);

        try
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = RandomUnitVector();

                float jx = (_rng.NextSingle() * 2.0f - 1.0f) * sj;
                float jy = (_rng.NextSingle() * 2.0f - 1.0f) * sj;
                float jz = (_rng.NextSingle() * 2.0f - 1.0f) * sj;
                dir = Vector3.Normalize(dir + new Vector3(jx, jy, jz));

                float tightMul = 0.85f + 0.10f * _rng.NextSingle();
                float speed = Math.Max(0.0f, baseSpeed) * tightMul;

                float lifeU = _rng.NextSingle();
                float lifetime = lifeMin + lifeU * (lifeMax - lifeMin);

                float rateU = _rng.NextSingle();
                float rateHz = rateMin + rateU * (rateMax - rateMin);

                if (produced < staging.Length)
                {
                    staging[produced++] = new GpuParticle
                    {
                        Position = position,
                        Velocity = dir * speed,
                        Age = 0.0f,
                        Lifetime = lifetime,
                        BaseColor = baseColor,
                        Color = baseColor,
                        Kind = (uint)ParticleKind.FinaleSpark,
                        _pad0 = PackFloat(rateHz),
                        _pad1 = PackFloat(inten),
                        _pad2 = (uint)_rng.Next()
                    };
                }

                if (produced < _particleCapacity && _rng.NextSingle() < microChance)
                {
                    int fragCount = _rng.Next(1, 4);
                    for (int k = 0; k < fragCount && produced < staging.Length && produced < _particleCapacity; k++)
                    {
                        float dev = 0.10f + 0.12f * _rng.NextSingle();
                        Vector3 microDir = Vector3.Normalize(dir + RandomUnitVector() * dev);

                        float mLifeU = _rng.NextSingle();
                        float mLifetime = microLifeMin + mLifeU * (microLifeMax - microLifeMin);

                        float mSpeedMul = microSpeedMulMin + _rng.NextSingle() * (microSpeedMulMax - microSpeedMulMin);
                        float mSpeed = speed * Math.Max(0.0f, mSpeedMul);

                        float microRate = rateHz * (1.4f + 0.8f * _rng.NextSingle());
                        float microInt = inten * 1.35f;

                        float startDelay = 0.12f + 0.30f * _rng.NextSingle();

                        staging[produced++] = new GpuParticle
                        {
                            Position = position,
                            Velocity = microDir * mSpeed,
                            Age = -startDelay,
                            Lifetime = mLifetime,
                            BaseColor = baseColor,
                            Color = baseColor,
                            Kind = (uint)ParticleKind.FinaleSpark,
                            _pad0 = PackFloat(microRate),
                            _pad1 = PackFloat(microInt),
                            _pad2 = (uint)_rng.Next()
                        };
                    }
                }
            }

            int total = Math.Clamp(produced, 1, _particleCapacity);

            WriteParticlesToBuffer(staging, start, total, particleBuffer, uploadBuffer);

            _particleWriteCursor = (start + total) % _particleCapacity;
        }
        finally
        {
            ReturnParticleArray(staging);
        }
    }

    public void SpawnGroundEffectDirected(Vector3 position, Vector4 baseColor, float speed, ReadOnlySpan<Vector3> directions, float particleLifetimeSeconds, float gravityFactor)
    {
        float g = Math.Clamp(gravityFactor, 0.0f, 2.0f);
        float speedMul = 1.0f - 0.15f * (g - 1.0f);
        SpawnBurstDirected(position, baseColor, speed * speedMul, directions, particleLifetimeSeconds);
    }

    public void SpawnBurstDirectedExplode(
        Vector3 position,
        Vector4 baseColor,
        float speed,
        ReadOnlySpan<Vector3> directions,
        float particleLifetimeSeconds,
        float sparkleRateHz = 0.0f,
        float sparkleIntensity = 0.0f)
    {
        _schemeTint = new Vector3(baseColor.X, baseColor.Y, baseColor.Z);
        SpawnBurstDirected(position, baseColor, speed, directions, particleLifetimeSeconds, sparkleRateHz, sparkleIntensity);
    }

    public void SpawnSmoke(Vector3 burstCenter)
    {
        int count = _rng.Next(200, 601);
        count = Math.Clamp(count, 1, _particleCapacity);

        int start = _particleWriteCursor;

        if (_context is not null && _particlesPipeline.CanGpuSpawn)
        {
            Vector4 startColor = new(0.35f, 0.33f, 0.30f, 1.0f);
            bool queued = _particlesPipeline.QueueSmokeSpawn(
                start,
                count,
                burstCenter,
                startColor,
                Tunables.SmokeLifetimeMinSeconds,
                Tunables.SmokeLifetimeMaxSeconds,
                (uint)_rng.Next());

            if (queued)
            {
                _particleWriteCursor = (start + count) % _particleCapacity;
                return;
            }
        }

        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (_context is null || particleBuffer is null || uploadBuffer is null)
            return;

        var staging = RentParticleArray(count);

        Vector4 cpuStartColor = new(0.35f, 0.33f, 0.30f, 1.0f);
        try
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = RandomUnitVector();
                Vector3 pos = burstCenter + dir * 0.5f;

                float outwardSpeed = 1.5f + _rng.NextSingle() * 1.5f;
                Vector3 outward = dir * outwardSpeed;

                float upSpeed = 1.5f + _rng.NextSingle() * 2.0f;
                Vector3 up = new(0.0f, upSpeed, 0.0f);

                Vector3 vel = outward * 0.7f + up;

                float minLife = Math.Max(0.0f, Tunables.SmokeLifetimeMinSeconds);
                float maxLife = Math.Max(minLife, Tunables.SmokeLifetimeMaxSeconds);
                float lifetime = minLife + _rng.NextSingle() * (maxLife - minLife);

                staging[i] = new GpuParticle
                {
                    Position = pos,
                    Velocity = vel,
                    Age = 0.0f,
                    Lifetime = lifetime,
                    BaseColor = cpuStartColor,
                    Color = cpuStartColor,
                    Kind = (uint)ParticleKind.Smoke,
                    _pad0 = (uint)_rng.Next(),
                    _pad1 = (uint)_rng.Next(),
                    _pad2 = (uint)_rng.Next()
                };
            }

            WriteParticlesToBuffer(staging, start, count, particleBuffer, uploadBuffer);
        }
        finally
        {
            ReturnParticleArray(staging);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    public void SpawnBurstDirected(
        Vector3 position,
        Vector4 baseColor,
        float speed,
        ReadOnlySpan<Vector3> directions,
        float particleLifetimeSeconds,
        float sparkleRateHz = 0.0f,
        float sparkleIntensity = 0.0f)
    {
        if (_context is null)
            return;

        int dirCount = directions.Length;
        if (dirCount <= 0)
            return;

        int count = Math.Min(dirCount, _particleCapacity);
        int start = _particleWriteCursor;

        if (_particlesPipeline.CanGpuSpawn)
        {
            var normalized = ArrayPool<Vector3>.Shared.Rent(count);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 dir = directions[i];
                    if (dir.LengthSquared() < 1e-8f)
                        dir = Vector3.UnitY;
                    else
                        dir = Vector3.Normalize(dir);

                    normalized[i] = dir;
                }

                int remaining = count;
                int dirOffset = 0;
                int cursor = start;
                const float crackleProbability = 0.22f;

                while (remaining > 0)
                {
                    int chunk = Math.Min(remaining, _particleCapacity - cursor);
                    _particlesPipeline.QueueSparkBurstSpawn(
                        particleStart: cursor,
                        directions: normalized.AsSpan(dirOffset, chunk),
                        origin: position,
                        baseColor: baseColor,
                        speed: speed,
                        lifetimeSeconds: particleLifetimeSeconds,
                        sparkleRateHz: sparkleRateHz,
                        sparkleIntensity: sparkleIntensity,
                        crackleProbability: crackleProbability,
                        seed: (uint)_rng.Next());

                    cursor = (cursor + chunk) % _particleCapacity;
                    dirOffset += chunk;
                    remaining -= chunk;
                }

                _particleWriteCursor = (start + count) % _particleCapacity;
                return;
            }
            finally
            {
                ArrayPool<Vector3>.Shared.Return(normalized);
            }
        }

        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (particleBuffer is null || uploadBuffer is null)
            return;

        var staging = RentParticleArray(count);
        try
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = directions[i];
                if (dir.LengthSquared() < 1e-8f)
                    dir = Vector3.UnitY;
                else
                    dir = Vector3.Normalize(dir);

                float u = _rng.NextSingle();
                float speedMul = 0.65f + (u * u) * 0.85f;
                Vector3 vel = dir * (speed * speedMul);

                bool crackle = (_rng.NextSingle() < 0.22f);

                float lifetime;
                if (crackle)
                {
                    lifetime = 0.03f + _rng.NextSingle() * 0.06f;
                }
                else
                {
                    float r = _rng.NextSingle();
                    float tail = r * r;
                    float lifeMul = 0.55f + 1.60f * tail;
                    lifetime = Math.Max(0.05f, particleLifetimeSeconds * lifeMul);
                }

                staging[i] = new GpuParticle
                {
                    Position = position,
                    Velocity = vel,
                    Age = 0.0f,
                    Lifetime = lifetime,
                    BaseColor = baseColor,
                    Color = baseColor,
                    Kind = crackle ? (uint)ParticleKind.Crackle : (uint)ParticleKind.Spark,
                    _pad0 = crackle ? (uint)_rng.Next() : PackFloat(sparkleRateHz),
                    _pad1 = crackle ? (uint)_rng.Next() : PackFloat(sparkleIntensity),
                    _pad2 = (uint)_rng.Next()
                };
            }

            WriteParticlesToBuffer(staging, start, count, particleBuffer, uploadBuffer);
        }
        finally
        {
            ReturnParticleArray(staging);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    private float GetDeltaTimeSeconds()
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        long prev = _lastTick;
        _lastTick = now;
        double dt = (now - prev) / (double)System.Diagnostics.Stopwatch.Frequency;
        if (dt < 0.0) dt = 0.0;
        if (dt > 0.05) dt = 0.05;
        return (float)dt;
    }

    private void SpawnShellTrail(Vector3 position, Vector3 velocity, int count)
    {
        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (_context is null || particleBuffer is null || uploadBuffer is null)
            return;

        count = Math.Clamp(count, 1, _particleCapacity);

        Vector4 color = new(1.0f, 0.92f, 0.55f, 1.0f);

        int start = _particleWriteCursor;

        var staging = RentParticleArray(count);

        Vector3 dir = velocity.LengthSquared() > 1e-6f ? Vector3.Normalize(velocity) : Vector3.UnitY;
        Vector3 back = -dir;
        Vector3 right = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
        if (right.LengthSquared() < 1e-6f)
            right = Vector3.UnitX;
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, dir));

        try
        {
            for (int i = 0; i < count; i++)
            {
                float jitterR = (_rng.NextSingle() * 2.0f - 1.0f) * 0.05f;
                float jitterU = (_rng.NextSingle() * 2.0f - 1.0f) * 0.05f;
                float alongBack = _rng.NextSingle() * 0.12f;

                Vector3 pos = position + (back * alongBack) + (right * jitterR) + (up * jitterU);

                Vector3 vel = (back * ShellTrailSpeed) + (right * jitterR * 0.5f) + (up * jitterU * 0.5f);

                staging[i] = new GpuParticle
                {
                    Position = pos,
                    Velocity = vel,
                    Age = 0.0f,
                    Lifetime = ShellTrailLifetimeSeconds,
                    BaseColor = color,
                    Color = color,
                    Kind = (uint)ParticleKind.Spark
                };
            }

            WriteParticlesToBuffer(staging, start, count, particleBuffer, uploadBuffer);
        }
        finally
        {
            ReturnParticleArray(staging);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    private Vector3 RandomUnitVector()
    {
        return s_unitVectorTable[_rng.Next(UnitVectorTableSize)];
    }

    private void WriteParticlesToBuffer(ReadOnlySpan<GpuParticle> staging, int start, int count, ID3D11Buffer particleBuffer, ID3D11Buffer uploadBuffer)
    {
        if (_context is null)
            return;

        count = Math.Min(count, staging.Length);
        if (count <= 0)
            return;

        int stride = GpuParticleStride;

        static double ToMilliseconds(long startTicks, long endTicks)
            => (endTicks - startTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        int uploadCap = _particlesPipeline.UploadBufferElementCapacity;
        if (uploadCap <= 0)
            uploadCap = 1;

        int producedOffset = 0;
        int remainingToUpload = count;
        ID3D11Buffer? nextUploadBuffer = uploadBuffer;

        while (remainingToUpload > 0)
        {
            int chunkCount = Math.Min(remainingToUpload, uploadCap);
            int chunkStart = (start + producedOffset) % _particleCapacity;

            var chunkUploadBuffer = nextUploadBuffer ?? _particlesPipeline.GetNextUploadBuffer();
            nextUploadBuffer = null;
            if (chunkUploadBuffer is null)
                return;

            int firstCount = Math.Min(chunkCount, _particleCapacity - chunkStart);
            int secondCount = chunkCount - firstCount;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();

            var chunkSpan = staging.Slice(producedOffset, chunkCount);
            var mapped = _context.Map(chunkUploadBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                nint basePtr = mapped.DataPointer;
                unsafe
                {
                    fixed (GpuParticle* srcPtr = chunkSpan)
                    {
                        Buffer.MemoryCopy(srcPtr, (void*)basePtr, chunkCount * stride, chunkCount * stride);
                    }
                }
            }
            finally
            {
                _context.Unmap(chunkUploadBuffer, 0);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                _perf.RecordUpload(ToMilliseconds(t0, t1), checked(chunkCount * stride));
            }

            if (firstCount > 0)
            {
                var srcBox = new Box
                {
                    Left = 0,
                    Right = (int)(firstCount * stride),
                    Top = 0,
                    Bottom = 1,
                    Front = 0,
                    Back = 1
                };

                _context.CopySubresourceRegion(particleBuffer, 0, (uint)(chunkStart * stride), 0, 0, chunkUploadBuffer, 0, srcBox);
            }

            if (secondCount > 0)
            {
                var srcBox = new Box
                {
                    Left = (int)(firstCount * stride),
                    Right = (int)(chunkCount * stride),
                    Top = 0,
                    Bottom = 1,
                    Front = 0,
                    Back = 1
                };

                _context.CopySubresourceRegion(particleBuffer, 0, 0, 0, 0, chunkUploadBuffer, 0, srcBox);
            }

            producedOffset += chunkCount;
            remainingToUpload -= chunkCount;
        }
    }

    private void WriteParticlesToBuffer(List<GpuParticle> staging, int start, int count, ID3D11Buffer particleBuffer, ID3D11Buffer uploadBuffer)
    {
        if (_context is null)
            return;

        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(staging);
        count = Math.Min(count, span.Length);
        if (count <= 0)
            return;

        WriteParticlesToBuffer(span, start, count, particleBuffer, uploadBuffer);
    }

    private void DrawParticles(bool additive)
    {
        if (_context is null)
            return;

        _particlesPipeline.Draw(_context, _view, _proj, _schemeTint, _depthStencilState, additive);
    }
}
