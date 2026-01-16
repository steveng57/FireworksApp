using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using FireworksApp.Simulation;

namespace FireworksApp.Rendering;

internal sealed partial class ParticlesPipeline
{
    [StructLayout(LayoutKind.Sequential)]
    private struct GpuSpawnRequest
    {
        public uint RequestKind;
        public uint ParticleStart;
        public uint DirStart;
        public uint Count;

        public Vector3 Origin;
        public float Speed;

        public float ConeAngleRadians;

        public float Lifetime;
        public float CrackleProbability;

        public float SparkleRateHz;
        public float SparkleIntensity;
        public uint Seed;
        public Vector3 _pad;

        public Vector4 BaseColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpawnCBData
    {
        public uint SpawnRequestCount;
        public uint SpawnDirectionCount;
        public uint ParticleCapacity;
        public uint _pad;
    }

    public bool QueueSparkBurstSpawn(
        int particleStart,
        ReadOnlySpan<Vector3> directions,
        Vector3 origin,
        Vector4 baseColor,
        float speed,
        float lifetimeSeconds,
        float sparkleRateHz,
        float sparkleIntensity,
        float crackleProbability,
        uint seed)
    {
        if (!CanGpuSpawn || directions.Length == 0)
            return false;

        float crackle = System.Math.Clamp(crackleProbability, 0.0f, 1.0f);
        float life = System.Math.Max(0.0f, lifetimeSeconds);
        float rate = System.Math.Max(0.0f, sparkleRateHz);
        float inten = System.Math.Max(0.0f, sparkleIntensity);

        uint dirStart = (uint)_pendingSpawnDirections.Count;
        for (int i = 0; i < directions.Length; i++)
            _pendingSpawnDirections.Add(directions[i]);

        var req = new GpuSpawnRequest
        {
            RequestKind = 1u,
            ParticleStart = (uint)particleStart,
            DirStart = dirStart,
            Count = (uint)directions.Length,
            Origin = origin,
            Speed = speed,
            ConeAngleRadians = 0.0f,
            Lifetime = life,
            CrackleProbability = crackle,
            SparkleRateHz = rate,
            SparkleIntensity = inten,
            Seed = seed,
            _pad = Vector3.Zero,
            BaseColor = baseColor
        };

        _pendingSpawnRequests.Add(req);
        return true;
    }

    public bool QueueShellSpawn(
        int particleStart,
        Vector3 position,
        Vector3 velocity,
        float fuseSeconds,
        float dragK,
        Vector4 baseColor,
        uint seed)
    {
        if (!CanGpuSpawn)
            return false;

        int maxCount = System.Math.Max(0, _capacity - particleStart);
        if (maxCount <= 0)
            return false;

        var req = new GpuSpawnRequest
        {
            RequestKind = 3u,
            ParticleStart = (uint)particleStart,
            DirStart = 0,
            Count = 1,
            Origin = position,
            Speed = 0.0f,
            ConeAngleRadians = 0.0f,
            Lifetime = System.Math.Max(0.0f, fuseSeconds),
            CrackleProbability = 0.0f,
            SparkleRateHz = 0.0f,
            SparkleIntensity = dragK,
            Seed = seed,
            _pad = velocity,
            BaseColor = baseColor
        };

        _pendingSpawnRequests.Add(req);
        return true;
    }

    public bool QueueSmokeSpawn(
        int particleStart,
        int count,
        Vector3 origin,
        Vector4 baseColor,
        float lifetimeMinSeconds,
        float lifetimeMaxSeconds,
        uint seed,
        out int enqueuedCount)
    {
        enqueuedCount = 0;

        if (!CanGpuSpawn || count <= 0)
            return false;

        int maxCount = System.Math.Max(0, _capacity - particleStart);
        if (maxCount <= 0)
            return false;
        count = System.Math.Min(count, maxCount);
        enqueuedCount = count;

        float lifeMin = System.Math.Max(0.0f, lifetimeMinSeconds);
        float lifeMax = System.Math.Max(lifeMin, lifetimeMaxSeconds);

        var req = new GpuSpawnRequest
        {
            RequestKind = 2u,
            ParticleStart = (uint)particleStart,
            DirStart = 0,
            Count = (uint)count,
            Origin = origin,
            Speed = 0.0f,
            ConeAngleRadians = 0.0f,
            Lifetime = lifeMin,
            CrackleProbability = 0.0f,
            SparkleRateHz = lifeMax,
            SparkleIntensity = 0.0f,
            Seed = seed,
            _pad = Vector3.Zero,
            BaseColor = baseColor
        };

        _pendingSpawnRequests.Add(req);
        return true;
    }

    public bool QueueSparkBurstCone(
        int particleStart,
        int count,
        Vector3 baseDirection,
        float coneAngleRadians,
        Vector3 origin,
        Vector4 baseColor,
        float speed,
        float lifetimeSeconds,
        float sparkleRateHz,
        float sparkleIntensity,
        float crackleProbability,
        uint seed)
    {
        if (!CanGpuSpawn || count <= 0)
            return false;

        int maxCount = System.Math.Max(0, _capacity - particleStart);
        if (maxCount <= 0)
            return false;
        count = System.Math.Min(count, maxCount);

        Vector3 dir = baseDirection;
        float lenSq = dir.LengthSquared();
        if (lenSq < 1e-8f)
            dir = Vector3.UnitY;
        else
            dir /= System.MathF.Sqrt(lenSq);

        float angle = System.Math.Max(0.0f, coneAngleRadians);
        float life = System.Math.Max(0.0f, lifetimeSeconds);
        float rate = System.Math.Max(0.0f, sparkleRateHz);
        float inten = System.Math.Max(0.0f, sparkleIntensity);
        float crackle = System.Math.Clamp(crackleProbability, 0.0f, 1.0f);

        var req = new GpuSpawnRequest
        {
            RequestKind = 4u,
            ParticleStart = (uint)particleStart,
            DirStart = 0,
            Count = (uint)count,
            Origin = origin,
            Speed = speed,
            ConeAngleRadians = angle,
            Lifetime = life,
            CrackleProbability = crackle,
            SparkleRateHz = rate,
            SparkleIntensity = inten,
            Seed = seed,
            _pad = dir,
            BaseColor = baseColor
        };

        _pendingSpawnRequests.Add(req);
        return true;
    }

    public ID3D11Buffer? GetNextUploadBuffer()
    {
        var buffers = _particleUploadBuffers;
        if (buffers is null || buffers.Length == 0)
            return null;

        int idx = _uploadBufferIndex;
        if ((uint)idx >= (uint)buffers.Length)
            idx = 0;

        var buf = buffers[idx];
        _uploadBufferIndex = (idx + 1) % buffers.Length;
        return buf;
    }

    private void EnsureSpawnBuffer(ref ID3D11Buffer? buffer, ref ID3D11ShaderResourceView? srv, ref int capacity, int requiredElements, int stride, ID3D11Device device, int prealloc)
    {
        if (requiredElements <= 0)
            return;

        int target = System.Math.Max(requiredElements, prealloc);

        if (buffer is not null && target <= capacity)
            return;

        buffer?.Dispose();
        srv?.Dispose();

        int newCapacity = capacity > 0 ? System.Math.Max(target, capacity * 2) : target;
        capacity = System.Math.Max(newCapacity, 1);

        buffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ShaderResource,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            MiscFlags = ResourceOptionFlags.BufferStructured,
            ByteWidth = (uint)(capacity * stride),
            StructureByteStride = (uint)stride
        });

        srv = device.CreateShaderResourceView(buffer, new ShaderResourceViewDescription
        {
            ViewDimension = ShaderResourceViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferShaderResourceView
            {
                FirstElement = 0,
                NumElements = (uint)capacity
            }
        });
    }

    private void DispatchPendingSpawns(ID3D11DeviceContext context)
    {
        if (!CanGpuSpawn || _spawnCB is null || _particleUAV is null)
        {
            _pendingSpawnRequests.Clear();
            _pendingSpawnDirections.Clear();
            return;
        }

        int reqCount = _pendingSpawnRequests.Count;
        if (reqCount == 0)
            return;

        int dirCount = _pendingSpawnDirections.Count;
        var device = context.Device;
        if (device is null)
            return;

        EnsureSpawnBuffer(ref _spawnRequestBuffer, ref _spawnRequestSRV, ref _spawnRequestCapacity, reqCount, _spawnRequestStride, device, _spawnRequestPrealloc);
        if (dirCount > 0)
            EnsureSpawnBuffer(ref _spawnDirectionBuffer, ref _spawnDirectionSRV, ref _spawnDirectionCapacity, dirCount, _spawnDirectionStride, device, _spawnDirectionPrealloc);

        if (_spawnRequestBuffer is null || _spawnRequestSRV is null)
            return;
        if (dirCount > 0 && (_spawnDirectionBuffer is null || _spawnDirectionSRV is null))
            return;

        var requestSpan = CollectionsMarshal.AsSpan(_pendingSpawnRequests);
        var requestBytes = MemoryMarshal.AsBytes(requestSpan);
        var mappedReq = context.Map(_spawnRequestBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            unsafe
            {
                requestBytes.CopyTo(new Span<byte>((void*)mappedReq.DataPointer, requestBytes.Length));
            }
        }
        finally
        {
            context.Unmap(_spawnRequestBuffer, 0);
        }

        if (dirCount > 0 && _spawnDirectionBuffer is not null)
        {
            var dirSpan = CollectionsMarshal.AsSpan(_pendingSpawnDirections);
            var dirBytes = MemoryMarshal.AsBytes(dirSpan);
            var mappedDir = context.Map(_spawnDirectionBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                unsafe
                {
                    dirBytes.CopyTo(new Span<byte>((void*)mappedDir.DataPointer, dirBytes.Length));
                }
            }
            finally
            {
                context.Unmap(_spawnDirectionBuffer, 0);
            }
        }

        var cbData = new SpawnCBData
        {
            SpawnRequestCount = (uint)reqCount,
            SpawnDirectionCount = (uint)dirCount,
            ParticleCapacity = (uint)_capacity,
            _pad = 0
        };

        var mapped = context.Map(_spawnCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            Marshal.StructureToPtr(cbData, mapped.DataPointer, false);
        }
        finally
        {
            context.Unmap(_spawnCB, 0);
        }

        context.CSSetShader(_csSpawn);
        context.CSSetConstantBuffer(2, _spawnCB);
        context.CSSetShaderResource(1, _spawnRequestSRV);
        if (_spawnDirectionSRV is not null)
            context.CSSetShaderResource(2, _spawnDirectionSRV);
        context.CSSetUnorderedAccessView(0, _particleUAV);

        uint groups = (uint)((reqCount + 63) / 64);
        context.Dispatch(groups, 1, 1);

        context.CSSetShaderResource(1, null);
        context.CSSetShaderResource(2, null);
        context.CSSetUnorderedAccessView(0, null);
        context.CSSetShader(null);

        _pendingSpawnRequests.Clear();
        _pendingSpawnDirections.Clear();
    }
}
