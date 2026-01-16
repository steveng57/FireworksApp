using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using FireworksApp.Simulation;

namespace FireworksApp.Rendering;

internal sealed partial class ParticlesPipeline
{
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
        string? gpuSpawnEnv = Environment.GetEnvironmentVariable(GpuSpawnEnvVar);
        bool disableGpuSpawn = string.Equals(gpuSpawnEnv, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(gpuSpawnEnv, "false", StringComparison.OrdinalIgnoreCase);
        _gpuSpawnEnabled = !disableGpuSpawn && _csSpawn is not null && _particleUAV is not null;
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

        _perKindCountersBuffer?.Dispose();
        _perKindCountersBuffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.UnorderedAccess,
            Usage = ResourceUsage.Default,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.BufferStructured,
            ByteWidth = sizeof(uint) * 6,
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
                NumElements = 6
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

        _detonationCapacity = Tunables.ParticleBudgets.Shell;

        _detonationBuffer?.Dispose();
        _detonationBuffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.UnorderedAccess,
            Usage = ResourceUsage.Default,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.BufferStructured,
            ByteWidth = (uint)(Marshal.SizeOf<DetonationEvent>() * _detonationCapacity),
            StructureByteStride = (uint)Marshal.SizeOf<DetonationEvent>()
        });

        _detonationUAV?.Dispose();
        _detonationUAV = device.CreateUnorderedAccessView(_detonationBuffer, new UnorderedAccessViewDescription
        {
            ViewDimension = UnorderedAccessViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferUnorderedAccessView
            {
                FirstElement = 0,
                NumElements = (uint)_detonationCapacity,
                Flags = BufferUnorderedAccessViewFlags.Append
            }
        });

        _detonationReadback?.Dispose();
        _detonationReadback = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.None,
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
            ByteWidth = (uint)(Marshal.SizeOf<DetonationEvent>() * _detonationCapacity),
            StructureByteStride = (uint)Marshal.SizeOf<DetonationEvent>()
        });

        _detonationCountBuffer?.Dispose();
        _detonationCountBuffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.None,
            Usage = ResourceUsage.Default,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None,
            ByteWidth = sizeof(uint)
        });

        _detonationCountReadback?.Dispose();
        _detonationCountReadback = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.None,
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
            ByteWidth = sizeof(uint)
        });
        }

    partial void DisposeCore()
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
