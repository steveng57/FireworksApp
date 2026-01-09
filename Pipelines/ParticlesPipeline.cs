using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace FireworksApp.Rendering;

internal sealed class ParticlesPipeline : IDisposable
{
    private const int TotalUavSlots = 8;

    private ID3D11Buffer? _particleBuffer;
    private ID3D11Buffer? _particleUploadBuffer;
    private ID3D11ShaderResourceView? _particleSRV;
    private ID3D11UnorderedAccessView? _particleUAV;

    private ID3D11ComputeShader? _cs;
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;

    private ID3D11BlendState? _blendAdditive;
    private ID3D11BlendState? _blendAlpha;
    private ID3D11DepthStencilState? _depthReadNoWrite;

    private ID3D11Buffer? _frameCB;
    private ID3D11Buffer? _passCB;
    private ID3D11Buffer? _passCBAdditive;
    private ID3D11Buffer? _passCBAlpha;

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

    // Reused per-frame state to avoid transient allocations.
    private readonly ID3D11UnorderedAccessView?[] _uavs = new ID3D11UnorderedAccessView?[TotalUavSlots];
    private readonly uint[] _initialCounts = new uint[TotalUavSlots];
    private readonly ID3D11UnorderedAccessView?[] _nullUavs = new ID3D11UnorderedAccessView?[TotalUavSlots];
    private static readonly uint[] s_counterZeros = new uint[7];

    private static readonly ParticleKind[] s_allKinds =
        [ParticleKind.Shell, ParticleKind.Spark, ParticleKind.Smoke, ParticleKind.Crackle, ParticleKind.PopFlash, ParticleKind.FinaleSpark];

    private static readonly ParticleKind[] s_kindsAdditive =
        [ParticleKind.Shell, ParticleKind.Spark, ParticleKind.Crackle, ParticleKind.PopFlash, ParticleKind.FinaleSpark];

    private static readonly ParticleKind[] s_kindsAlpha =
        [ParticleKind.Smoke];

    public int Capacity => _capacity;
    public ID3D11Buffer? UploadBuffer => _particleUploadBuffer;
    public ID3D11Buffer? ParticleBuffer => _particleBuffer;

    public void Initialize(ID3D11Device device, int particleCapacity)
    {
        _capacity = particleCapacity;

        int stride = Marshal.SizeOf<GpuParticle>();

        var init = new GpuParticle[_capacity];
        for (int i = 0; i < init.Length; i++)
        {
            init[i].Kind = (uint)ParticleKind.Dead;
            init[i].Color = Vector4.Zero;
        }

        _particleBuffer?.Dispose();
        _particleBuffer = device.CreateBuffer(
            init,
            new BufferDescription
            {
                BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
                Usage = ResourceUsage.Default,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.BufferStructured,
                ByteWidth = (uint)(stride * _capacity),
                StructureByteStride = (uint)stride
            });

        _particleUploadBuffer?.Dispose();
        _particleUploadBuffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.None,
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Write,
            MiscFlags = ResourceOptionFlags.BufferStructured,
            ByteWidth = (uint)(stride * _capacity),
            StructureByteStride = (uint)stride
        });

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
        var vsBlob = ShaderCompilerHelper.CompileAndCatch(source, "VSMain", shaderPath, "vs_5_0");
        var psBlob = ShaderCompilerHelper.CompileAndCatch(source, "PSParticle", shaderPath, "ps_5_0");

        byte[] csBytes = csBlob.ToArray();
        byte[] vsBytes = vsBlob.ToArray();
        byte[] psBytes = psBlob.ToArray();

        _cs?.Dispose();
        _vs?.Dispose();
        _ps?.Dispose();

        _cs = device.CreateComputeShader(csBytes);
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
        }

    public void Update(ID3D11DeviceContext context, Matrix4x4 view, Matrix4x4 proj, Vector3 schemeTint, float scaledDt)
    {
        if (_cs is null || _particleUAV is null || _frameCB is null || _perKindCountersUAV is null)
            return;

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
        _particleUploadBuffer?.Dispose();
        _particleUploadBuffer = null;

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

        _frameCB?.Dispose();
        _frameCB = null;
    }
}


