using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;
using System.Diagnostics;
using FireworksApp.Simulation;

namespace FireworksApp.Rendering;

internal sealed class ParticlesPipeline : IDisposable
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

    // Per-kind alive index buffers (u1..u6)
    private readonly Dictionary<ParticleKind, ID3D11Buffer?> _aliveIndexBufferByKind = new();
    private readonly Dictionary<ParticleKind, ID3D11UnorderedAccessView?> _aliveUAVByKind = new();
    private readonly Dictionary<ParticleKind, ID3D11ShaderResourceView?> _aliveSRVByKind = new();
    private readonly Dictionary<ParticleKind, int> _lastAliveCountByKind = new();

    // Per-kind indirect draw args buffers (for DrawInstancedIndirect)
    private readonly Dictionary<ParticleKind, ID3D11Buffer?> _aliveDrawArgsByKind = new();

    // Per-kind atomic counters for budget enforcement (RW buffer)
    private ID3D11Buffer? _perKindCountersBuffer;
    private ID3D11UnorderedAccessView? _perKindCountersUAV;

    private readonly Dictionary<ParticleKind, int> _totalDroppedByKind = new();

    private readonly List<GpuSpawnRequest> _pendingSpawnRequests = new();
    private readonly List<Vector3> _pendingSpawnDirections = new();

    // Reused per-frame state to avoid transient allocations.
    private readonly ID3D11UnorderedAccessView?[] _uavs = new ID3D11UnorderedAccessView?[TotalUavSlots];
    private readonly uint[] _initialCounts = new uint[TotalUavSlots];
    private readonly ID3D11UnorderedAccessView?[] _nullUavs = new ID3D11UnorderedAccessView?[TotalUavSlots];
    private static readonly uint[] s_counterZeros = new uint[7];

    private const string GpuSpawnEnvVar = "FIREWORKS_GPU_SPAWN";

    private static readonly ParticleKind[] s_allKinds =
        [ParticleKind.Shell, ParticleKind.Spark, ParticleKind.Smoke, ParticleKind.Crackle, ParticleKind.PopFlash, ParticleKind.FinaleSpark];

    private static readonly ParticleKind[] s_kindsAdditive =
        [ParticleKind.Shell, ParticleKind.Spark, ParticleKind.Crackle, ParticleKind.PopFlash, ParticleKind.FinaleSpark];

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

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuSpawnRequest
    {
        public uint RequestKind;
        public uint ParticleStart;
        public uint DirStart;
        public uint Count;

        public Vector3 Origin;
        public float Speed;

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

        // Clamp params to keep parity with CPU path.
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

    public bool QueueSmokeSpawn(
        int particleStart,
        int count,
        Vector3 origin,
        Vector4 baseColor,
        float lifetimeMinSeconds,
        float lifetimeMaxSeconds,
        uint seed)
    {
        if (!CanGpuSpawn || count <= 0)
            return false;

        int maxCount = System.Math.Max(0, _capacity - particleStart);
        if (maxCount <= 0)
            return false;
        count = System.Math.Min(count, maxCount);

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

    public string GetPerfCountersSummary()
    {
        // Avoid allocations beyond small concatenations; called at ~1Hz.
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

    private void EnsureSpawnBuffer(ref ID3D11Buffer? buffer, ref ID3D11ShaderResourceView? srv, ref int capacity, int requiredElements, int stride, ID3D11Device device)
    {
        if (requiredElements <= 0)
            return;

        if (buffer is not null && requiredElements <= capacity)
            return;

        buffer?.Dispose();
        srv?.Dispose();

        capacity = System.Math.Max(requiredElements, 1);

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

        EnsureSpawnBuffer(ref _spawnRequestBuffer, ref _spawnRequestSRV, ref _spawnRequestCapacity, reqCount, _spawnRequestStride, device);
        if (dirCount > 0)
            EnsureSpawnBuffer(ref _spawnDirectionBuffer, ref _spawnDirectionSRV, ref _spawnDirectionCapacity, dirCount, _spawnDirectionStride, device);

        if (_spawnRequestBuffer is null || _spawnRequestSRV is null)
            return;
        if (dirCount > 0 && (_spawnDirectionBuffer is null || _spawnDirectionSRV is null))
            return;

        // Write requests
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

        // Write directions if present
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

    public void Initialize(ID3D11Device device, int particleCapacity)
    {
        _capacity = particleCapacity;

        int stride = Marshal.SizeOf<GpuParticle>();

        _particleBuffer?.Dispose();
        _particleBuffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
            Usage = ResourceUsage.Default,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.BufferStructured,
            ByteWidth = (uint)(stride * _capacity),
            StructureByteStride = (uint)stride
        });

        if (_particleUploadBuffers is not null)
        {
            for (int i = 0; i < _particleUploadBuffers.Length; i++)
                _particleUploadBuffers[i]?.Dispose();
        }

        // Ring of small upload buffers:
        // Use Dynamic + WriteDiscard to reduce chance of CPU stalls when mapping for frequent updates.
        int uploadRingSize = Tunables.GpuUpload.UploadRingSize;
        int uploadChunkElements = Tunables.GpuUpload.UploadChunkElements;
        _uploadBufferElementCapacity = System.Math.Min(_capacity, uploadChunkElements);
        _particleUploadBuffers = new ID3D11Buffer?[uploadRingSize];
        _uploadBufferIndex = 0;

        uint byteWidth = (uint)(stride * _uploadBufferElementCapacity);
        try
        {
            for (int i = 0; i < uploadRingSize; i++)
            {
                _particleUploadBuffers[i] = device.CreateBuffer(new BufferDescription
                {
                    BindFlags = BindFlags.VertexBuffer,
                    Usage = ResourceUsage.Dynamic,
                    CPUAccessFlags = CpuAccessFlags.Write,
                    MiscFlags = ResourceOptionFlags.None,
                    ByteWidth = byteWidth,
                    StructureByteStride = 0
                });
            }
        }
        catch (Exception ex) 
        {
            Debug.WriteLine($"Warning: Failed to create full upload buffer ring: {ex.Message}");
            throw;
        }

        // Clear the newly created particle buffer to Dead/Zero without allocating a huge managed array.
        // This is especially important on resize/maximize, where reinitialization previously caused large allocation spikes.
        if (_particleUploadBuffers is not null && _particleUploadBuffers.Length > 0)
        {
            var ctx = device.ImmediateContext;

            var dead = new GpuParticle { Kind = (uint)ParticleKind.Dead, Color = Vector4.Zero };

            int remaining = _capacity;
            int dstElement = 0;
            while (remaining > 0)
            {
                int chunkElements = System.Math.Min(remaining, _uploadBufferElementCapacity);

                var upload = _particleUploadBuffers[_uploadBufferIndex];
                _uploadBufferIndex = (_uploadBufferIndex + 1) % _particleUploadBuffers.Length;
                if (upload is null)
                    break;

                var mapped = ctx.Map(upload, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
                try
                {
                    unsafe
                    {
                        var dst = (GpuParticle*)mapped.DataPointer;
                        for (int i = 0; i < chunkElements; i++)
                            dst[i] = dead;
                    }
                }
                finally
                {
                    ctx.Unmap(upload, 0);
                }

                var srcBox = new Box
                {
                    Left = 0,
                    Right = chunkElements * stride,
                    Top = 0,
                    Bottom = 1,
                    Front = 0,
                    Back = 1
                };

                ctx.CopySubresourceRegion(_particleBuffer, 0, (uint)(dstElement * stride), 0, 0, upload, 0, srcBox);

                dstElement += chunkElements;
                remaining -= chunkElements;
            }
        }

        _particleSRV?.Dispose();
        _particleSRV = device.CreateShaderResourceView(_particleBuffer, new ShaderResourceViewDescription
        {
            ViewDimension = ShaderResourceViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferShaderResourceView
            {
                FirstElement = 0,
                NumElements = (uint)_capacity
            }
        });

        _particleUAV?.Dispose();
        _particleUAV = device.CreateUnorderedAccessView(_particleBuffer, new UnorderedAccessViewDescription
        {
            ViewDimension = UnorderedAccessViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferUnorderedAccessView
            {
                FirstElement = 0,
                NumElements = (uint)_capacity
            }
        });

        _frameCB?.Dispose();
        _frameCB = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ConstantBuffer,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            ByteWidth = (uint)Marshal.SizeOf<FrameCBData>()
        });

        _passCB?.Dispose();
        _passCB = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ConstantBuffer,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            ByteWidth = (uint)Marshal.SizeOf<PassCBData>()
        });

        _spawnCB?.Dispose();
        _spawnCB = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ConstantBuffer,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            ByteWidth = (uint)Marshal.SizeOf<SpawnCBData>()
        });

        // Pre-baked pass constant buffers (avoid per-pass Map/Unmap during Draw).
        _passCBAdditive?.Dispose();
        _passCBAdditive = device.CreateBuffer(
            new PassCBData { ParticlePass = 0u },
            new BufferDescription
            {
                BindFlags = BindFlags.ConstantBuffer,
                Usage = ResourceUsage.Immutable,
                CPUAccessFlags = CpuAccessFlags.None,
                ByteWidth = (uint)Marshal.SizeOf<PassCBData>()
            });

        _passCBAlpha?.Dispose();
        _passCBAlpha = device.CreateBuffer(
            new PassCBData { ParticlePass = 1u },
            new BufferDescription
            {
                BindFlags = BindFlags.ConstantBuffer,
                Usage = ResourceUsage.Immutable,
                CPUAccessFlags = CpuAccessFlags.None,
                ByteWidth = (uint)Marshal.SizeOf<PassCBData>()
            });

        CreatePerKindAliveBuffers(device);

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Particles.hlsl");
        string source = File.ReadAllText(shaderPath);

        ReadOnlyMemory<byte> csBlob = ShaderCompilerHelper.CompileAndCatch(source, "CSUpdate", shaderPath, "cs_5_0");
        ReadOnlyMemory<byte> csSpawnBlob = ShaderCompilerHelper.CompileAndCatch(source, "CSSpawn", shaderPath, "cs_5_0");
        var vsBlob = ShaderCompilerHelper.CompileAndCatch(source, "VSMain", shaderPath, "vs_5_0");
        var psBlob = ShaderCompilerHelper.CompileAndCatch(source, "PSParticle", shaderPath, "ps_5_0");

        byte[] csBytes = csBlob.ToArray();
        byte[] csSpawnBytes = csSpawnBlob.ToArray();
        byte[] vsBytes = vsBlob.ToArray();
        byte[] psBytes = psBlob.ToArray();

        _cs?.Dispose();
        _csSpawn?.Dispose();
        _vs?.Dispose();
        _ps?.Dispose();

        _cs = device.CreateComputeShader(csBytes);
        _csSpawn = csSpawnBytes.Length > 0 ? device.CreateComputeShader(csSpawnBytes) : null;
        bool enableGpuSpawn = string.Equals(Environment.GetEnvironmentVariable(GpuSpawnEnvVar), "1", StringComparison.OrdinalIgnoreCase);
        _gpuSpawnEnabled = enableGpuSpawn && _csSpawn is not null && _particleUAV is not null;
        GpuSpawnEnabled = true;
        _vs = device.CreateVertexShader(vsBytes);
        _ps = device.CreatePixelShader(psBytes);

        _blendAdditive?.Dispose();
        _blendAdditive = device.CreateBlendState(new BlendDescription
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = false,
            RenderTarget =
            {
                [0] = new RenderTargetBlendDescription
                {
                    BlendEnable = true,
                    SourceBlend = Blend.One,
                    DestinationBlend = Blend.One,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = Blend.One,
                    DestinationBlendAlpha = Blend.One,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteEnable.All
                }
            }
        });

        _blendAlpha?.Dispose();
        _blendAlpha = device.CreateBlendState(new BlendDescription
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = false,
            RenderTarget =
            {
                [0] = new RenderTargetBlendDescription
                {
                    BlendEnable = true,
                    SourceBlend = Blend.SourceAlpha,
                    DestinationBlend = Blend.InverseSourceAlpha,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = Blend.One,
                    DestinationBlendAlpha = Blend.InverseSourceAlpha,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteEnable.All
                }
            }
        });

        _depthReadNoWrite?.Dispose();
        _depthReadNoWrite = device.CreateDepthStencilState(new DepthStencilDescription
        {
            DepthEnable = true,
            DepthWriteMask = DepthWriteMask.Zero,
            DepthFunc = ComparisonFunction.LessEqual,
            StencilEnable = false
        });

    }


        private void CreatePerKindAliveBuffers(ID3D11Device device)
        {
        var kinds = s_allKinds;

            foreach (var kind in kinds)
            {
                int budget = ParticleKindBudget.GetBudget(kind);
                _lastAliveCountByKind[kind] = 0;
                _totalDroppedByKind[kind] = 0;

                // Alive index buffer (structured uint)
                if (_aliveIndexBufferByKind.TryGetValue(kind, out var oldIndexBuf))
                    oldIndexBuf?.Dispose();
                _aliveIndexBufferByKind[kind] = device.CreateBuffer(new BufferDescription
                {
                    BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
                    Usage = ResourceUsage.Default,
                    CPUAccessFlags = CpuAccessFlags.None,
                    MiscFlags = ResourceOptionFlags.BufferStructured,
                    ByteWidth = (uint)(sizeof(uint) * budget),
                    StructureByteStride = sizeof(uint)
                });

                // UAV for append (u1..u6)
                if (_aliveUAVByKind.TryGetValue(kind, out var oldUav))
                    oldUav?.Dispose();
                _aliveUAVByKind[kind] = device.CreateUnorderedAccessView(_aliveIndexBufferByKind[kind], new UnorderedAccessViewDescription
                {
                    ViewDimension = UnorderedAccessViewDimension.Buffer,
                    Format = Format.Unknown,
                    Buffer = new BufferUnorderedAccessView
                    {
                        FirstElement = 0,
                        NumElements = (uint)budget,
                        Flags = BufferUnorderedAccessViewFlags.Append
                    }
                });

                // SRV for draw (t1)
                if (_aliveSRVByKind.TryGetValue(kind, out var oldSrv))
                    oldSrv?.Dispose();
                _aliveSRVByKind[kind] = device.CreateShaderResourceView(_aliveIndexBufferByKind[kind], new ShaderResourceViewDescription
                {
                    ViewDimension = ShaderResourceViewDimension.Buffer,
                    Format = Format.Unknown,
                    Buffer = new BufferShaderResourceView
                    {
                        FirstElement = 0,
                        NumElements = (uint)budget
                    }
                });

                // Indirect args buffer: { VertexCountPerInstance, InstanceCount, StartVertexLocation, StartInstanceLocation }
                // We always draw quads as 6 verts per instance.
                if (_aliveDrawArgsByKind.TryGetValue(kind, out var oldArgsBuf))
                    oldArgsBuf?.Dispose();
                _aliveDrawArgsByKind[kind] = device.CreateBuffer(
                    new uint[] { 6u, 0u, 0u, 0u },
                    new BufferDescription
                    {
                        BindFlags = BindFlags.None,
                        Usage = ResourceUsage.Default,
                        CPUAccessFlags = CpuAccessFlags.None,
                        MiscFlags = ResourceOptionFlags.DrawIndirectArguments,
                        ByteWidth = sizeof(uint) * 4
                    });

            }

            // Per-kind atomic counters buffer (7 uints: one per Kind including Dead=0)
            _perKindCountersBuffer?.Dispose();
            _perKindCountersBuffer = device.CreateBuffer(new BufferDescription
            {
                BindFlags = BindFlags.UnorderedAccess,
                Usage = ResourceUsage.Default,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.BufferStructured,
                ByteWidth = sizeof(uint) * 7,
                StructureByteStride = sizeof(uint)
            });

            _perKindCountersUAV?.Dispose();
            _perKindCountersUAV = device.CreateUnorderedAccessView(_perKindCountersBuffer, new UnorderedAccessViewDescription
            {
                ViewDimension = UnorderedAccessViewDimension.Buffer,
                Format = Format.Unknown,
                Buffer = new BufferUnorderedAccessView
                {
                    FirstElement = 0,
                    NumElements = 7
                }
            });

            _perKindCountersReadback?.Dispose();
            _perKindCountersReadback = device.CreateBuffer(new BufferDescription
            {
                BindFlags = BindFlags.None,
                Usage = ResourceUsage.Staging,
                CPUAccessFlags = CpuAccessFlags.Read,
                MiscFlags = ResourceOptionFlags.None,
                ByteWidth = sizeof(uint) * 7,
                StructureByteStride = sizeof(uint)
            });
        }

    public void Update(ID3D11DeviceContext context, Matrix4x4 view, Matrix4x4 proj, Vector3 schemeTint, float scaledDt)
    {
        if (_cs is null || _particleUAV is null || _frameCB is null || _perKindCountersUAV is null)
            return;

        DispatchPendingSpawns(context);

        var right = new Vector3(view.M11, view.M21, view.M31);
        var up = new Vector3(view.M12, view.M22, view.M32);
        var vp = Matrix4x4.Transpose(view * proj);

        var frame = new FrameCBData
        {
            ViewProjection = vp,
            CameraRightWS = right,
            DeltaTime = scaledDt,
            CameraUpWS = up,
            Time = (float)(Environment.TickCount64 / 1000.0),

            SmokeFadeInFraction = Tunables.SmokeFadeInFraction,
            SmokeFadeOutStartFraction = Tunables.SmokeFadeOutStartFraction,

            CrackleBaseColor = ParticleConstants.CrackleBaseColor,
            CrackleBaseSize = ParticleConstants.CrackleBaseSize,
            CracklePeakColor = ParticleConstants.CracklePeakColor,
            CrackleFlashSizeMul = ParticleConstants.CrackleFlashSizeMul,
            CrackleFadeColor = ParticleConstants.CrackleFadeColor,
            CrackleTau = ParticleConstants.CrackleTau,

            SchemeTint = schemeTint
        };

        var mapped = context.Map(_frameCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        Marshal.StructureToPtr(frame, mapped.DataPointer, false);
        context.Unmap(_frameCB, 0);

        // Clear per-kind atomic counters to 0
        context.UpdateSubresource(s_counterZeros, _perKindCountersBuffer);

        // Bind UAVs: u0=particles, u1..u6=per-kind alive, u7=counters
        // Set append buffer initial counts to 0
        Array.Clear(_uavs);
        Array.Clear(_initialCounts);

        _uavs[0] = _particleUAV;
        _initialCounts[0] = uint.MaxValue; // structured buffer (not append)

        var kinds = s_allKinds;
        for (int k = 0; k < kinds.Length; k++)
        {
            if (_aliveUAVByKind.TryGetValue(kinds[k], out var kindUav))
            {
                _uavs[k + 1] = kindUav;
                _initialCounts[k + 1] = 0; // reset append counter
            }
        }

        _uavs[7] = _perKindCountersUAV;
        _initialCounts[7] = uint.MaxValue;

        context.CSSetShader(_cs);
        context.CSSetConstantBuffer(0, _frameCB);
        context.CSSetUnorderedAccessViews(0, _uavs, _initialCounts);

        uint groups = (uint)((_capacity + 255) / 256);
        context.Dispatch(groups, 1, 1);

        // Unbind all UAVs
        context.CSSetUnorderedAccessViews(0, _nullUavs, null);
        context.CSSetShader(null);

        if (_enableCounterReadback && _perKindCountersReadback is not null && _perKindCountersBuffer is not null)
        {
            context.CopyResource(_perKindCountersReadback, _perKindCountersBuffer);

            var mappedCounters = context.Map(_perKindCountersReadback, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                unsafe
                {
                    uint* src = (uint*)mappedCounters.DataPointer;
                    _lastAliveCountByKind[ParticleKind.Shell] = (int)src[1];
                    _lastAliveCountByKind[ParticleKind.Spark] = (int)src[2];
                    _lastAliveCountByKind[ParticleKind.Smoke] = (int)src[3];
                    _lastAliveCountByKind[ParticleKind.Crackle] = (int)src[4];
                    _lastAliveCountByKind[ParticleKind.PopFlash] = (int)src[5];
                    _lastAliveCountByKind[ParticleKind.FinaleSpark] = (int)src[6];
                }
            }
            finally
            {
                context.Unmap(_perKindCountersReadback, 0);
            }
        }

        // GPU-driven draw args (no CPU readback): write alive instance counts into each args buffer.
        foreach (var kind in kinds)
        {
            if (!_aliveUAVByKind.TryGetValue(kind, out var uav) || uav is null)
                continue;
            if (!_aliveDrawArgsByKind.TryGetValue(kind, out var argsBuf) || argsBuf is null)
                continue;

            // Write instance count (DWORD offset 4) into the args buffer.
            context.CopyStructureCount(argsBuf, sizeof(uint), uav);
        }
    }

    public void Draw(ID3D11DeviceContext context, Matrix4x4 view, Matrix4x4 proj, Vector3 schemeTint, ID3D11DepthStencilState? depthStencilState, bool additive)
    {
        if (_vs is null || _ps is null || _particleSRV is null || _frameCB is null)
            return;

        // Determine which kinds to draw in this pass
        var kinds = additive ? s_kindsAdditive : s_kindsAlpha;

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(null);
        context.IASetVertexBuffers(0, 0, Array.Empty<ID3D11Buffer>(), Array.Empty<uint>(), Array.Empty<uint>());

        if (additive)
        {
            context.OMSetDepthStencilState(_depthReadNoWrite, 0);
            context.OMSetBlendState(_blendAdditive, new Color4(0, 0, 0, 0), uint.MaxValue);
        }
        else
        {
            context.OMSetDepthStencilState(null, 0);
            context.OMSetBlendState(_blendAlpha, new Color4(0, 0, 0, 0), uint.MaxValue);
        }

        context.VSSetShader(_vs);
        context.PSSetShader(_ps);
        context.VSSetShaderResource(0, _particleSRV);

        // Frame CB is updated in `Update`; only set it here.
        context.VSSetConstantBuffer(0, _frameCB);

        // Bind pass CB without mapping per pass.
        // Fallback to dynamic buffer if the cached ones are unavailable.
        var passCb = additive ? _passCBAdditive : _passCBAlpha;
        if (passCb != null)
        {
            context.VSSetConstantBuffer(1, passCb);
        }
        else if (_passCB != null)
        {
            var mappedPass = context.Map(_passCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                var pass = new PassCBData
                {
                    ParticlePass = additive ? 0u : 1u
                };
                Marshal.StructureToPtr(pass, mappedPass.DataPointer, false);
            }
            finally
            {
                context.Unmap(_passCB, 0);
            }

            context.VSSetConstantBuffer(1, _passCB);
        }

        // Draw each kind with its own alive index buffer
        foreach (var kind in kinds)
        {
            if (!_aliveSRVByKind.TryGetValue(kind, out var srv) || srv is null)
                continue;

            if (!_aliveDrawArgsByKind.TryGetValue(kind, out var argsBuf) || argsBuf is null)
                continue;

            context.VSSetShaderResource(1, srv);

            // Instanced quads: 6 verts per particle instance; instance count comes from GPU (CopyStructureCount).
            context.DrawInstancedIndirect(argsBuf, 0);
        }

        context.VSSetShaderResource(0, null);
        context.VSSetShaderResource(1, null);
        context.OMSetBlendState(null, new Color4(0, 0, 0, 0), uint.MaxValue);
        context.OMSetDepthStencilState(depthStencilState, 0);
    }

    public void Dispose()
    {
        if (_particleUploadBuffers is not null)
        {
            for (int i = 0; i < _particleUploadBuffers.Length; i++)
                _particleUploadBuffers[i]?.Dispose();
        }
        _particleUploadBuffers = null;

        _particleSRV?.Dispose();
        _particleSRV = null;

        _particleUAV?.Dispose();
        _particleUAV = null;

        _particleBuffer?.Dispose();
        _particleBuffer = null;

        _passCB?.Dispose();
        _passCB = null;

        _passCBAdditive?.Dispose();
        _passCBAdditive = null;

        _passCBAlpha?.Dispose();
        _passCBAlpha = null;

        _frameCB?.Dispose();
        _frameCB = null;

        _spawnRequestBuffer?.Dispose();
        _spawnRequestBuffer = null;
        _spawnRequestSRV?.Dispose();
        _spawnRequestSRV = null;

        _spawnDirectionBuffer?.Dispose();
        _spawnDirectionBuffer = null;
        _spawnDirectionSRV?.Dispose();
        _spawnDirectionSRV = null;

        _spawnCB?.Dispose();
        _spawnCB = null;

        foreach (var kvp in _aliveIndexBufferByKind)
            kvp.Value?.Dispose();
        _aliveIndexBufferByKind.Clear();

        foreach (var kvp in _aliveUAVByKind)
            kvp.Value?.Dispose();
        _aliveUAVByKind.Clear();

        foreach (var kvp in _aliveSRVByKind)
            kvp.Value?.Dispose();
        _aliveSRVByKind.Clear();

        foreach (var kvp in _aliveDrawArgsByKind)
            kvp.Value?.Dispose();
        _aliveDrawArgsByKind.Clear();

        _perKindCountersBuffer?.Dispose();
        _perKindCountersBuffer = null;

        _perKindCountersUAV?.Dispose();
        _perKindCountersUAV = null;

        _perKindCountersReadback?.Dispose();
        _perKindCountersReadback = null;

        _csSpawn?.Dispose();
        _csSpawn = null;

        _cs?.Dispose();
        _cs = null;

        _vs?.Dispose();
        _vs = null;

        _ps?.Dispose();
        _ps = null;

        _blendAdditive?.Dispose();
        _blendAdditive = null;

        _blendAlpha?.Dispose();
        _blendAlpha = null;

        _depthReadNoWrite?.Dispose();
        _depthReadNoWrite = null;
    }
}


