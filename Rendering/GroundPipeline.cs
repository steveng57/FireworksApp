using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;

namespace FireworksApp.Rendering;

internal sealed class GroundPipeline : IDisposable
{
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _vb;

    public void Initialize(ID3D11Device device)
    {
        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Ground.hlsl");
        string source = File.ReadAllText(shaderPath);

        var vsBlob = Compiler.Compile(source, "VSMain", shaderPath, "vs_5_0");
        var psBlob = Compiler.Compile(source, "PSMain", shaderPath, "ps_5_0");
        byte[] vsBytes = vsBlob.ToArray();
        byte[] psBytes = psBlob.ToArray();

        _vs = device.CreateVertexShader(vsBytes);
        _ps = device.CreatePixelShader(psBytes);

        var elements = new[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0)
        };
        _inputLayout = device.CreateInputLayout(elements, vsBytes);

        const float half = 200.0f;
        Vector3 n = Vector3.UnitY;
        GroundVertex[] verts =
        {
            new GroundVertex(new Vector3(-half, 0.0f, -half), n),
            new GroundVertex(new Vector3( half, 0.0f, -half), n),
            new GroundVertex(new Vector3(-half, 0.0f,  half), n),

            new GroundVertex(new Vector3( half, 0.0f, -half), n),
            new GroundVertex(new Vector3( half, 0.0f,  half), n),
            new GroundVertex(new Vector3(-half, 0.0f,  half), n),
        };

        int stride = Marshal.SizeOf<GroundVertex>();
        _vb?.Dispose();
        _vb = device.CreateBuffer(
            verts,
            new BufferDescription
            {
                BindFlags = BindFlags.VertexBuffer,
                Usage = ResourceUsage.Default,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None,
                ByteWidth = (uint)(stride * verts.Length),
                StructureByteStride = (uint)stride
            });
    }

    public void Draw(ID3D11DeviceContext context, ID3D11Buffer? sceneCB, ID3D11Buffer? lightingCB, ID3D11RasterizerState? rasterizerState)
    {
        if (_vs is null || _ps is null || _vb is null || _inputLayout is null)
            return;

        int stride = Marshal.SizeOf<GroundVertex>();
        uint[] strides = new[] { (uint)stride };
        uint[] offsets = new[] { 0u };
        var buffers = new[] { _vb };

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(_inputLayout);
        context.IASetVertexBuffers(0, 1, buffers, strides, offsets);

        context.VSSetShader(_vs);
        context.PSSetShader(_ps);

        if (sceneCB != null)
        {
            context.VSSetConstantBuffer(0, sceneCB);
            context.PSSetConstantBuffer(0, sceneCB);
        }

        if (lightingCB != null)
        {
            context.PSSetConstantBuffer(1, lightingCB);
        }

        if (rasterizerState != null)
        {
            context.RSSetState(rasterizerState);
        }

        context.Draw(6, 0);
    }

    public void Dispose()
    {
        _vb?.Dispose();
        _vb = null;

        _inputLayout?.Dispose();
        _inputLayout = null;

        _vs?.Dispose();
        _vs = null;

        _ps?.Dispose();
        _ps = null;
    }
}
