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
    // Ping-pong particle buffers (alive-list)
    private ID3D11Buffer? _particlesA;
    private ID3D11Buffer? _particlesB;
    private ID3D11ShaderResourceView? _srvA;
    private ID3D11ShaderResourceView? _srvB;
    private ID3D11UnorderedAccessView? _uavA;
    private ID3D11UnorderedAccessView? _uavB;

    // Upload buffer used by spawns (unchanged for step 2; renderer still writes particles directly)
    private ID3D11Buffer? _particleUploadBuffer;

    // Current selection: true = A is input, false = B is input
    private bool _aIsIn = true;
    private int _aliveCount;
    private ID3D11Buffer? _counterReadback;

    private ID3D11ComputeShader? _cs;
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;

    private ID3D11BlendState? _blendAdditive;
    private ID3D11BlendState? _blendAlpha;
    private ID3D11DepthStencilState? _depthReadNoWrite;

    private ID3D11Buffer? _frameCB;
    // Spawn buffer for per-frame injections
    private ID3D11Buffer? _spawnBuffer;
    private ID3D11ShaderResourceView? _spawnSRV;
    private const int MaxSpawnsPerFrame = 65536;

    private int _capacity;

    public int Capacity => _capacity;
    public ID3D11Buffer? UploadBuffer => _particleUploadBuffer;
    // Expose current input particle buffer for existing spawn paths
    public ID3D11Buffer? ParticleBuffer => _aIsIn ? _particlesA : _particlesB;

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

        _particlesA?.Dispose();
        _particlesA = device.CreateBuffer(
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

        _particlesB?.Dispose();
        _particlesB = device.CreateBuffer(
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

        _srvA?.Dispose();
        _srvA = device.CreateShaderResourceView(_particlesA, new ShaderResourceViewDescription
        {
            ViewDimension = ShaderResourceViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferShaderResourceView
            {
                FirstElement = 0,
                NumElements = (uint)_capacity
            }
        });

        _srvB?.Dispose();
        _srvB = device.CreateShaderResourceView(_particlesB, new ShaderResourceViewDescription
            {
                ViewDimension = ShaderResourceViewDimension.Buffer,
                Format = Format.Unknown,
                Buffer = new BufferShaderResourceView
                {
                    FirstElement = 0,
                    NumElements = (uint)_capacity
                }
            });

        _uavA?.Dispose();
        _uavA = device.CreateUnorderedAccessView(_particlesA, new UnorderedAccessViewDescription
        {
            ViewDimension = UnorderedAccessViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferUnorderedAccessView
            {
                FirstElement = 0,
                    NumElements = (uint)_capacity,
                    Flags = BufferUnorderedAccessViewFlags.Append
            }
        });

        _uavB?.Dispose();
        _uavB = device.CreateUnorderedAccessView(_particlesB, new UnorderedAccessViewDescription
            {
                ViewDimension = UnorderedAccessViewDimension.Buffer,
                Format = Format.Unknown,
                Buffer = new BufferUnorderedAccessView
                {
                    FirstElement = 0,
                    NumElements = (uint)_capacity,
                    Flags = BufferUnorderedAccessViewFlags.Append
                }
            });

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Particles.hlsl");
        string source = File.ReadAllText(shaderPath);

        ReadOnlyMemory<byte> csBlob = default;
        try
        {
            csBlob = Compiler.Compile(source, "CSUpdate", shaderPath, "cs_5_0");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        var vsBlob = Compiler.Compile(source, "VSParticle", shaderPath, "vs_5_0");
        var psBlob = Compiler.Compile(source, "PSParticle", shaderPath, "ps_5_0");

        byte[] csBytes = csBlob.ToArray();
        byte[] vsBytes = vsBlob.ToArray();
        byte[] psBytes = psBlob.ToArray();

        _cs?.Dispose();
        _vs?.Dispose();
        _ps?.Dispose();

        _cs = device.CreateComputeShader(csBytes);
        _vs = device.CreateVertexShader(vsBytes);
        _ps = device.CreatePixelShader(psBytes);

        _frameCB?.Dispose();
        _frameCB = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ConstantBuffer,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            ByteWidth = (uint)Marshal.SizeOf<FrameCBData>()
        });

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

        // Create spawn buffer resources
        int strideSpawn = Marshal.SizeOf<GpuParticle>();
        _spawnBuffer?.Dispose();
        _spawnBuffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ShaderResource,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            MiscFlags = ResourceOptionFlags.BufferStructured,
            ByteWidth = (uint)(strideSpawn * MaxSpawnsPerFrame),
            StructureByteStride = (uint)strideSpawn
        });
        _spawnSRV?.Dispose();
        _spawnSRV = device.CreateShaderResourceView(_spawnBuffer, new ShaderResourceViewDescription
        {
            ViewDimension = ShaderResourceViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferShaderResourceView
            {
                FirstElement = 0,
                NumElements = (uint)MaxSpawnsPerFrame
            }
        });
    }

    public void Update(ID3D11DeviceContext context, Matrix4x4 view, Matrix4x4 proj, Vector3 schemeTint, float scaledDt, System.Collections.Generic.IReadOnlyList<GpuParticle>? pendingSpawns)
    {
        if (_cs is null || _frameCB is null)
            return;

        var right = new Vector3(view.M11, view.M21, view.M31);
        var up = new Vector3(view.M12, view.M22, view.M32);
        var vp = Matrix4x4.Transpose(view * proj);

        int spawnCount = pendingSpawns?.Count ?? 0;
        if (spawnCount > MaxSpawnsPerFrame)
            spawnCount = MaxSpawnsPerFrame;

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

            SchemeTint = schemeTint,

            ParticlePass = 0u,
            SpawnCount = (uint)spawnCount
        };

        var mapped = context.Map(_frameCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        Marshal.StructureToPtr(frame, mapped.DataPointer, false);
        context.Unmap(_frameCB, 0);

        // Select In SRV and Out UAV based on ping-pong state
        var inSRV = _aIsIn ? _srvA : _srvB;
        var outUAV = _aIsIn ? _uavB : _uavA;

        if (inSRV is null || outUAV is null)
            return;

        // Bind with initial append counter = 0
        context.CSSetShader(_cs);
        context.CSSetConstantBuffer(0, _frameCB);
        context.CSSetShaderResource(0, inSRV);
        // Reset counter via initialCounts parameter
        context.CSSetUnorderedAccessView(0, outUAV, 0);

        // Dispatch only alive count threads (currently tracked; clamped)
        int dispatchCount = System.Math.Clamp(_aliveCount, 0, _capacity);
        uint groups = (uint)((dispatchCount + 255) / 256);
        context.Dispatch(groups, 1, 1);
        System.Diagnostics.Debug.WriteLine($"Spawns={spawnCount}, AliveOut={dispatchCount}");

        // After survivor update, optionally append spawns
        if (spawnCount > 0 && _spawnBuffer != null && _spawnSRV != null)
        {
            // Upload pendingSpawns to spawn buffer
            var mappedSpawn = context.Map(_spawnBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                int stride = Marshal.SizeOf<GpuParticle>();
                nint basePtr = mappedSpawn.DataPointer;
                for (int i = 0; i < spawnCount; i++)
                {
                    Marshal.StructureToPtr(pendingSpawns![i], basePtr + (i * stride), false);
                }
            }
            finally
            {
                context.Unmap(_spawnBuffer, 0);
            }

            // Bind spawn SRV and same Out UAV, then dispatch CSAppendSpawns
            context.CSSetShaderResource(1, _spawnSRV);
            // Reuse the same compute shader entry? We need CSAppendSpawns
            string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Particles.hlsl");
            string source = File.ReadAllText(shaderPath);
            var csAppendBlob = Compiler.Compile(source, "CSAppendSpawns", shaderPath, "cs_5_0");
            var csAppend = context.Device.CreateComputeShader(csAppendBlob.ToArray());
            context.CSSetShader(csAppend);
            uint groupsSpawns = (uint)((spawnCount + 255) / 256);
            context.Dispatch(groupsSpawns, 1, 1);
            context.CSSetShaderResource(1, null);
            csAppend.Dispose();
        }

        // Read back new alive count
        if (_counterReadback != null)
        {
            context.CopyStructureCount(_counterReadback, 0, outUAV);
            var mappedCount = context.Map(_counterReadback, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                int count = Marshal.ReadInt32(mappedCount.DataPointer);
                _aliveCount = System.Math.Clamp(count, 0, _capacity);
            }
            finally
            {
                context.Unmap(_counterReadback, 0);
            }
        }

        // Swap buffers: Out becomes In
        _aIsIn = !_aIsIn;

        // Unbind
        context.CSSetUnorderedAccessView(0, null);
        context.CSSetShaderResource(0, null);
        context.CSSetShader(null);
    }

    public void Draw(ID3D11DeviceContext context, Matrix4x4 view, Matrix4x4 proj, Vector3 schemeTint, ID3D11DepthStencilState? depthStencilState, bool additive)
    {
        // Pick the SRV that contains the CURRENT alive list we want to render.
        var currentSRV = _aIsIn ? _srvB : _srvA;

        if (_vs is null || _ps is null || currentSRV is null || _frameCB is null)
            return;

        var mappedPass = context.Map(_frameCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            var right = new Vector3(view.M11, view.M21, view.M31);
            var up = new Vector3(view.M12, view.M22, view.M32);
            var vp = Matrix4x4.Transpose(view * proj);

            var frame = new FrameCBData
            {
                ViewProjection = vp,
                CameraRightWS = right,
                DeltaTime = 0.0f,
                CameraUpWS = up,
                Time = (float)(Environment.TickCount64 / 1000.0),

                CrackleBaseColor = ParticleConstants.CrackleBaseColor,
                CrackleBaseSize = ParticleConstants.CrackleBaseSize,
                CracklePeakColor = ParticleConstants.CracklePeakColor,
                CrackleFlashSizeMul = ParticleConstants.CrackleFlashSizeMul,
                CrackleFadeColor = ParticleConstants.CrackleFadeColor,
                CrackleTau = ParticleConstants.CrackleTau,

                SchemeTint = schemeTint,
                ParticlePass = additive ? 0u : 1u
            };

            Marshal.StructureToPtr(frame, mappedPass.DataPointer, false);
        }
        finally
        {
            context.Unmap(_frameCB, 0);
        }

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(null);
        context.IASetVertexBuffers(0, 0, Array.Empty<ID3D11Buffer>(), Array.Empty<uint>(), Array.Empty<uint>());

        if (additive)
            context.OMSetDepthStencilState(_depthReadNoWrite, 0);
        else
            context.OMSetDepthStencilState(null, 0);

        context.OMSetBlendState(additive ? _blendAdditive : _blendAlpha, new Color4(0, 0, 0, 0), uint.MaxValue);

        context.VSSetShader(_vs);
        context.PSSetShader(_ps);

        context.VSSetConstantBuffer(0, _frameCB);

        // Bind particle SRV at t0 (vertex shader expects ParticlesRO at t0)
        const int ParticleSrvSlot = 0;
        context.VSSetShaderResource(ParticleSrvSlot, currentSRV);
        context.PSSetShaderResource(ParticleSrvSlot, currentSRV);

        // Draw ONLY alive instances (drawing capacity renders stale garbage)
        uint instances = (uint)System.Math.Clamp(_aliveCount, 0, _capacity);
        if (instances > 0)
        {
            System.Diagnostics.Debug.WriteLine($"Draw alive={_aliveCount} aIsIn={_aIsIn}");

            context.DrawInstanced(6, instances, 0, 0);
        }

        // Unbind SRVs
        context.VSSetShaderResource(ParticleSrvSlot, null);
        context.PSSetShaderResource(ParticleSrvSlot, null);

        context.OMSetBlendState(null, new Color4(0, 0, 0, 0), uint.MaxValue);
        context.OMSetDepthStencilState(depthStencilState, 0);
    }


    public void Dispose()
    {
        _particleUploadBuffer?.Dispose();
        _particleUploadBuffer = null;

        _srvA?.Dispose(); _srvA = null;
        _srvB?.Dispose(); _srvB = null;

        _uavA?.Dispose(); _uavA = null;
        _uavB?.Dispose(); _uavB = null;

        _particlesA?.Dispose(); _particlesA = null;
        _particlesB?.Dispose(); _particlesB = null;

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
