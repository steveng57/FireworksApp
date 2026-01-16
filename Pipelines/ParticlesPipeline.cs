using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using System.Diagnostics;
using FireworksApp.Simulation;

namespace FireworksApp.Rendering;

internal sealed partial class ParticlesPipeline : IDisposable
{
    private const int TotalUavSlots = 8;

    private ID3D11Buffer? _particleBuffer;
    private ID3D11Buffer?[]? _particleUploadBuffers;
    private int _uploadBufferIndex;
    private int _uploadBufferElementCapacity;
    private ID3D11ShaderResourceView? _particleSRV;
    private ID3D11UnorderedAccessView? _particleUAV;

    private ID3D11ComputeShader? _cs;
    private ID3D11ComputeShader? _csSpawn;
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;

    private ID3D11BlendState? _blendAdditive;
    private ID3D11BlendState? _blendAlpha;
    private ID3D11DepthStencilState? _depthReadNoWrite;

    private ID3D11Buffer? _frameCB;
    private ID3D11Buffer? _passCB;
    private ID3D11Buffer? _passCBAdditive;
    private ID3D11Buffer? _passCBAlpha;
    private ID3D11Buffer? _spawnCB;

    private ID3D11Buffer? _spawnRequestBuffer;
    private ID3D11ShaderResourceView? _spawnRequestSRV;

    private ID3D11Buffer? _spawnDirectionBuffer;
    private ID3D11ShaderResourceView? _spawnDirectionSRV;
    private int _spawnRequestCapacity;
    private int _spawnDirectionCapacity;
    private int _spawnRequestStride = Marshal.SizeOf<GpuSpawnRequest>();
    private int _spawnDirectionStride = Marshal.SizeOf<Vector3>();

    private int _capacity;

    private readonly Dictionary<ParticleKind, ID3D11Buffer?> _aliveIndexBufferByKind = new();
    private readonly Dictionary<ParticleKind, ID3D11UnorderedAccessView?> _aliveUAVByKind = new();
    private readonly Dictionary<ParticleKind, ID3D11ShaderResourceView?> _aliveSRVByKind = new();
    private readonly Dictionary<ParticleKind, int> _lastAliveCountByKind = new();

    private readonly Dictionary<ParticleKind, ID3D11Buffer?> _aliveDrawArgsByKind = new();

    private ID3D11Buffer? _perKindCountersBuffer;
    private ID3D11UnorderedAccessView? _perKindCountersUAV;

    private ID3D11Buffer? _detonationBuffer;
    private ID3D11UnorderedAccessView? _detonationUAV;
    private ID3D11Buffer? _detonationReadback;
    private ID3D11Buffer? _detonationCountBuffer;
    private ID3D11Buffer? _detonationCountReadback;
    private int _detonationCapacity;

    private readonly Dictionary<ParticleKind, int> _totalDroppedByKind = new();

    private readonly List<GpuSpawnRequest> _pendingSpawnRequests = new();
    private readonly List<Vector3> _pendingSpawnDirections = new();

    private readonly ID3D11UnorderedAccessView?[] _uavs = new ID3D11UnorderedAccessView?[TotalUavSlots];
    private readonly uint[] _initialCounts = new uint[TotalUavSlots];
    private readonly ID3D11UnorderedAccessView?[] _nullUavs = new ID3D11UnorderedAccessView?[TotalUavSlots];
    private static readonly uint[] s_counterZeros = new uint[6];

    private const string GpuSpawnEnvVar = "FIREWORKS_GPU_SPAWN";

    private static readonly ParticleKind[] s_allKinds =
        [ParticleKind.Shell, ParticleKind.Spark, ParticleKind.Smoke, ParticleKind.Crackle, ParticleKind.PopFlash];

    private static readonly ParticleKind[] s_kindsAdditive =
        [ParticleKind.Shell, ParticleKind.Spark, ParticleKind.Crackle, ParticleKind.PopFlash];

    private static readonly ParticleKind[] s_kindsAlpha =
        [ParticleKind.Smoke];

    public int Capacity => _capacity;
    public ID3D11Buffer? UploadBuffer => GetNextUploadBuffer();
    public ID3D11Buffer? ParticleBuffer => _particleBuffer;

    public int UploadBufferElementCapacity => _uploadBufferElementCapacity;

    private bool _gpuSpawnEnabled;
    public bool GpuSpawnEnabled
    {
        get => _gpuSpawnEnabled && _csSpawn is not null && _particleUAV is not null;
        set => _gpuSpawnEnabled = value;
    }

    public bool CanGpuSpawn => _gpuSpawnEnabled && _csSpawn is not null && _particleUAV is not null;

    private bool _enableCounterReadback = true;
    public bool EnableCounterReadback
    {
        get => _enableCounterReadback;
        set => _enableCounterReadback = value;
    }

    private ID3D11Buffer? _perKindCountersReadback;

    public int ReadDetonations(ID3D11DeviceContext context, Span<DetonationEvent> destination)
    {
        if (_detonationReadback is null || _detonationBuffer is null || _detonationCountBuffer is null || _detonationCountReadback is null)
            return 0;

        context.CopyResource(_detonationCountReadback, _detonationCountBuffer);

        uint count;
        var mappedCount = context.Map(_detonationCountReadback, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            unsafe
            {
                count = *(uint*)mappedCount.DataPointer;
            }
        }
        finally
        {
            context.Unmap(_detonationCountReadback, 0);
        }

        if (count == 0 || destination.IsEmpty)
            return 0;

        int toCopy = (int)System.Math.Min((uint)destination.Length, count);

        context.CopyResource(_detonationReadback, _detonationBuffer);

        var mapped = context.Map(_detonationReadback, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            unsafe
            {
                var src = (DetonationEvent*)mapped.DataPointer;
                for (int i = 0; i < toCopy; i++)
                {
                    destination[i] = src[i];
                }
            }
        }
        finally
        {
            context.Unmap(_detonationReadback, 0);
        }

        return toCopy;
    }

    public string GetPerfCountersSummary()
    {
        try
        {
            var kinds = s_allKinds;
            var s = string.Empty;
            for (int i = 0; i < kinds.Length; i++)
            {
                ParticleKind kind = kinds[i];
                _lastAliveCountByKind.TryGetValue(kind, out int alive);
                _totalDroppedByKind.TryGetValue(kind, out int dropped);
                if (s.Length > 0)
                    s += " ";
                s += $"{kind}:{alive}(-{dropped})";
            }
            return s;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        DisposeCore();
    }

    partial void DisposeCore();
}
