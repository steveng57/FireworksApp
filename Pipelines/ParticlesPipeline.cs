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
    private ID3D11Buffer? _particleBuffer;
    private ID3D11Buffer? _particleUploadBuffer;
    private ID3D11ShaderResourceView? _particleSRV;
    private ID3D11UnorderedAccessView? _particleUAV;

    private ID3D11Buffer? _aliveAddIndexBuffer;
    private ID3D11UnorderedAccessView? _aliveAddUAV;
    private ID3D11ShaderResourceView? _aliveAddSRV;
    private ID3D11Buffer? _aliveAlphaIndexBuffer;
    private ID3D11UnorderedAccessView? _aliveAlphaUAV;
    private ID3D11ShaderResourceView? _aliveAlphaSRV;
    private ID3D11Buffer? _aliveAddCountDefault; // 4-byte default buffer
    private ID3D11Buffer? _aliveAddCountStaging;
    private ID3D11Buffer? _aliveAlphaCountDefault; // 4-byte default buffer
    private ID3D11Buffer? _aliveAlphaCountStaging;
    private int _lastAliveAddCount;
    private int _lastAliveAlphaCount;
    private long _lastAliveLogTick;

    private ID3D11ComputeShader? _cs;
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;

    private ID3D11BlendState? _blendAdditive;
    private ID3D11BlendState? _blendAlpha;
    private ID3D11DepthStencilState? _depthReadNoWrite;

    private ID3D11Buffer? _frameCB;

    private int _capacity;

    // Debug counters (optional)
    private ID3D11Buffer? _debugCountersBuffer; // UAV buffer with counters
    private ID3D11UnorderedAccessView? _debugCountersUAV;
    private ID3D11Buffer? _debugCountersReadback; // staging

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

        // Alive index buffers (structured uint) for additive and alpha
        _aliveAddIndexBuffer?.Dispose();
        _aliveAddIndexBuffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
            Usage = ResourceUsage.Default,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.BufferStructured,
            ByteWidth = (uint)(sizeof(uint) * _capacity),
            StructureByteStride = (uint)sizeof(uint)
        });

        _aliveAlphaIndexBuffer?.Dispose();
        _aliveAlphaIndexBuffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
            Usage = ResourceUsage.Default,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.BufferStructured,
            ByteWidth = (uint)(sizeof(uint) * _capacity),
            StructureByteStride = (uint)sizeof(uint)
        });

        _aliveAddUAV?.Dispose();
        _aliveAddUAV = device.CreateUnorderedAccessView(_aliveAddIndexBuffer, new UnorderedAccessViewDescription
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

        _aliveAlphaUAV?.Dispose();
        _aliveAlphaUAV = device.CreateUnorderedAccessView(_aliveAlphaIndexBuffer, new UnorderedAccessViewDescription
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

        _aliveAddSRV?.Dispose();
        _aliveAddSRV = device.CreateShaderResourceView(_aliveAddIndexBuffer, new ShaderResourceViewDescription
        {
            ViewDimension = ShaderResourceViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferShaderResourceView
            {
                FirstElement = 0,
                NumElements = (uint)_capacity
            }
        });

        _aliveAlphaSRV?.Dispose();
        _aliveAlphaSRV = device.CreateShaderResourceView(_aliveAlphaIndexBuffer, new ShaderResourceViewDescription
        {
            ViewDimension = ShaderResourceViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferShaderResourceView
            {
                FirstElement = 0,
                NumElements = (uint)_capacity
            }
        });

        // 4-byte buffers to receive the structure count via CopyStructureCount
        _aliveAddCountDefault?.Dispose();
        _aliveAddCountDefault = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.None,
            Usage = ResourceUsage.Default,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None,
            ByteWidth = 4
        });

        _aliveAddCountStaging?.Dispose();
        _aliveAddCountStaging = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.None,
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
            ByteWidth = 4
        });

        _aliveAlphaCountDefault?.Dispose();
        _aliveAlphaCountDefault = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.None,
            Usage = ResourceUsage.Default,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None,
            ByteWidth = 4
        });

        _aliveAlphaCountStaging?.Dispose();
        _aliveAlphaCountStaging = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.None,
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
            ByteWidth = 4
        });

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

        // Debug counters: 32 uints structured buffer with UAV
        _debugCountersBuffer?.Dispose();
        _debugCountersBuffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.UnorderedAccess,
            Usage = ResourceUsage.Default,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.BufferStructured,
            ByteWidth = (uint)(sizeof(uint) * 32),
            StructureByteStride = (uint)sizeof(uint)
        });
        _debugCountersUAV?.Dispose();
        _debugCountersUAV = device.CreateUnorderedAccessView(_debugCountersBuffer, new UnorderedAccessViewDescription
        {
            ViewDimension = UnorderedAccessViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferUnorderedAccessView
            {
                FirstElement = 0,
                NumElements = 32,
                Flags = BufferUnorderedAccessViewFlags.None
            }
        });
        _debugCountersReadback?.Dispose();
        _debugCountersReadback = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.None,
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
            ByteWidth = (uint)(sizeof(uint) * 32)
        });
    }

    public void Update(ID3D11DeviceContext context, Matrix4x4 view, Matrix4x4 proj, Vector3 schemeTint, float scaledDt)
    {
        if (_cs is null || _particleUAV is null || _aliveAddUAV is null || _aliveAlphaUAV is null || _aliveAddCountDefault is null || _aliveAddCountStaging is null || _aliveAlphaCountDefault is null || _aliveAlphaCountStaging is null || _frameCB is null)
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

            SchemeTint = schemeTint,

            ParticlePass = 0u
        };

        var mapped = context.Map(_frameCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        Marshal.StructureToPtr(frame, mapped.DataPointer, false);
        context.Unmap(_frameCB, 0);

        context.CSSetShader(_cs);
        context.CSSetConstantBuffer(0, _frameCB);
        // Bind particle UAV at u0 and alive additive/alpha at u1/u2; reset append counters to 0
        var uavs = new ID3D11UnorderedAccessView?[] { _particleUAV, _aliveAddUAV, _aliveAlphaUAV, _debugCountersUAV };
        uint[] initialCounts = new uint[] { 0xFFFFFFFFu, 0u, 0u, 0xFFFFFFFFu };
        context.CSSetUnorderedAccessViews(0u, (uint)uavs.Length, uavs!, initialCounts);

        // Clear debug counters to zero each frame if available
        if (_debugCountersUAV is not null)
        {
            context.ClearUnorderedAccessView(_debugCountersUAV, new Int4(0, 0, 0, 0));
        }

        uint groups = (uint)((_capacity + 255) / 256);
        context.Dispatch(groups, 1, 1);

        // Copy append counts from alive UAVs into 4-byte buffers
        context.CopyStructureCount(_aliveAddCountDefault, 0, _aliveAddUAV);
        context.CopyStructureCount(_aliveAlphaCountDefault, 0, _aliveAlphaUAV);
        // Read back via staging buffers
        context.CopyResource(_aliveAddCountStaging, _aliveAddCountDefault);
        context.CopyResource(_aliveAlphaCountStaging, _aliveAlphaCountDefault);
        var mappedAdd = context.Map(_aliveAddCountStaging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        _lastAliveAddCount = Marshal.ReadInt32(mappedAdd.DataPointer);
        context.Unmap(_aliveAddCountStaging, 0);
        var mappedAlpha = context.Map(_aliveAlphaCountStaging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        _lastAliveAlphaCount = Marshal.ReadInt32(mappedAlpha.DataPointer);
        context.Unmap(_aliveAlphaCountStaging, 0);

        // Minimal debug logging once per second
        long now = Environment.TickCount64;
        if (now - _lastAliveLogTick > 1000)
        {
            int total = System.Math.Max(0, _lastAliveAddCount) + System.Math.Max(0, _lastAliveAlphaCount);
            if (_debugCountersBuffer is not null && _debugCountersReadback is not null)
            {
                context.CopyResource(_debugCountersReadback, _debugCountersBuffer);
                var mappedDbg = context.Map(_debugCountersReadback, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                try
                {
                    var ptr = mappedDbg.DataPointer;
                    uint processed = (uint)Marshal.ReadInt32(ptr, 0 * 4);
                    uint aliveKept = (uint)Marshal.ReadInt32(ptr, 1 * 4);
                    uint killedLife = (uint)Marshal.ReadInt32(ptr, 2 * 4);
                    uint killedGround = (uint)Marshal.ReadInt32(ptr, 3 * 4);
                    uint killedInvalid = (uint)Marshal.ReadInt32(ptr, 4 * 4);
                    uint appendedAdd = (uint)Marshal.ReadInt32(ptr, 5 * 4);
                    uint appendedAlpha = (uint)Marshal.ReadInt32(ptr, 6 * 4);
                    System.Diagnostics.Debug.WriteLine($"AliveAdd={_lastAliveAddCount} AliveAlpha={_lastAliveAlphaCount} Total={total} Capacity={_capacity} | Proc={processed} Kept={aliveKept} KL={killedLife} KG={killedGround} KI={killedInvalid} AppAdd={appendedAdd} AppAlpha={appendedAlpha}");
                }
                finally
                {
                    context.Unmap(_debugCountersReadback, 0);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"AliveAdd={_lastAliveAddCount} AliveAlpha={_lastAliveAlphaCount} Total={total} Capacity={_capacity}");
            }
            _lastAliveLogTick = now;
        }

        // Unbind UAVs
        context.CSSetUnorderedAccessView(0, null);
        context.CSSetUnorderedAccessView(1, null);
        context.CSSetUnorderedAccessView(2, null);
        context.CSSetUnorderedAccessView(3, null);
        context.CSSetShader(null);
    }

    public void Draw(ID3D11DeviceContext context, Matrix4x4 view, Matrix4x4 proj, Vector3 schemeTint, ID3D11DepthStencilState? depthStencilState, bool additive)
    {
        if (_vs is null || _ps is null || _particleSRV is null || _frameCB is null)
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

                ParticlePass = additive ? 0u : 1u,
                AliveCount = (uint)(additive ? System.Math.Max(0, _lastAliveAddCount) : System.Math.Max(0, _lastAliveAlphaCount))
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
        {
            context.OMSetDepthStencilState(_depthReadNoWrite, 0);
        }
        else
        {
            context.OMSetDepthStencilState(null, 0);
        }

        context.OMSetBlendState(additive ? _blendAdditive : _blendAlpha, new Color4(0, 0, 0, 0), uint.MaxValue);

        context.VSSetShader(_vs);
        context.PSSetShader(_ps);

        context.VSSetConstantBuffer(0, _frameCB);
        context.VSSetShaderResource(0, _particleSRV);
        // Bind the appropriate alive list SRV to t1
        if (additive)
            context.VSSetShaderResource(1, _aliveAddSRV);
        else
            context.VSSetShaderResource(1, _aliveAlphaSRV);

        // Instanced quads: 6 verts per particle instance; draw only alive (clamped to capacity)
        int alive = additive ? _lastAliveAddCount : _lastAliveAlphaCount;
        uint instanceCount = (uint)System.Math.Min(_capacity, System.Math.Max(0, alive));
        context.DrawInstanced(6, instanceCount, 0, 0);

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

        _aliveAddSRV?.Dispose();
        _aliveAddSRV = null;
        _aliveAddUAV?.Dispose();
        _aliveAddUAV = null;
        _aliveAddIndexBuffer?.Dispose();
        _aliveAddIndexBuffer = null;
        _aliveAlphaSRV?.Dispose();
        _aliveAlphaSRV = null;
        _aliveAlphaUAV?.Dispose();
        _aliveAlphaUAV = null;
        _aliveAlphaIndexBuffer?.Dispose();
        _aliveAlphaIndexBuffer = null;
        _aliveAddCountDefault?.Dispose();
        _aliveAddCountDefault = null;
        _aliveAddCountStaging?.Dispose();
        _aliveAddCountStaging = null;
        _aliveAlphaCountDefault?.Dispose();
        _aliveAlphaCountDefault = null;
        _aliveAlphaCountStaging?.Dispose();
        _aliveAlphaCountStaging = null;

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

        _debugCountersReadback?.Dispose();
        _debugCountersReadback = null;
        _debugCountersUAV?.Dispose();
        _debugCountersUAV = null;
        _debugCountersBuffer?.Dispose();
        _debugCountersBuffer = null;
    }
}
