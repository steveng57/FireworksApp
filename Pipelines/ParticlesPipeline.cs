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

    private ID3D11ComputeShader? _cs;
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;

    private ID3D11BlendState? _blendAdditive;
    private ID3D11BlendState? _blendAlpha;
    private ID3D11DepthStencilState? _depthReadNoWrite;

    private ID3D11Buffer? _frameCB;

    private int _capacity;

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
    }

    public void Update(ID3D11DeviceContext context, Matrix4x4 view, Matrix4x4 proj, Vector3 schemeTint, float scaledDt)
    {
        if (_cs is null || _particleUAV is null || _frameCB is null)
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
        context.CSSetUnorderedAccessView(0, _particleUAV);

        uint groups = (uint)((_capacity + 255) / 256);
        context.Dispatch(groups, 1, 1);

        context.CSSetUnorderedAccessView(0, null);
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

        context.Draw((uint)(_capacity * 6), 0);

        context.VSSetShaderResource(0, null);
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
