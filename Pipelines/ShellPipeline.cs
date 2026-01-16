using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;

namespace FireworksApp.Rendering;

internal sealed class ShellPipeline : IDisposable
{
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _vb;
    private ID3D11Buffer? _instanceBuffer;
    private int _instanceCapacity;
    private int _vertexCount;

    public void Initialize(ID3D11Device device)
    {
        LoadShaders(device);
        CreateGeometry(device);
    }

    private void LoadShaders(ID3D11Device device)
    {
        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Shell.hlsl");
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
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
            new InputElementDescription("INSTANCEPOS", 0, Format.R32G32B32_Float, 0, 1, InputClassification.PerInstanceData, 1)
        };
        _inputLayout = device.CreateInputLayout(elements, vsBytes);
    }

    private void CreateGeometry(ID3D11Device device)
    {
        const float radius = 0.10f;
        const int slices = 16;
        const int stacks = 12;

        var verts = new System.Collections.Generic.List<GroundVertex>(slices * stacks * 6);

        for (int stack = 0; stack < stacks; stack++)
        {
            float v0 = (float)stack / stacks;
            float v1 = (float)(stack + 1) / stacks;
            float phi0 = v0 * MathF.PI;
            float phi1 = v1 * MathF.PI;

            for (int slice = 0; slice < slices; slice++)
            {
                float u0 = (float)slice / slices;
                float u1 = (float)(slice + 1) / slices;
                float theta0 = u0 * MathF.Tau;
                float theta1 = u1 * MathF.Tau;

                Vector3 p00 = Spherical(radius, theta0, phi0);
                Vector3 p01 = Spherical(radius, theta1, phi0);
                Vector3 p10 = Spherical(radius, theta0, phi1);
                Vector3 p11 = Spherical(radius, theta1, phi1);

                Vector3 n00 = Vector3.Normalize(p00);
                Vector3 n01 = Vector3.Normalize(p01);
                Vector3 n10 = Vector3.Normalize(p10);
                Vector3 n11 = Vector3.Normalize(p11);

                verts.Add(new GroundVertex(p00, n00));
                verts.Add(new GroundVertex(p10, n10));
                verts.Add(new GroundVertex(p11, n11));

                verts.Add(new GroundVertex(p00, n00));
                verts.Add(new GroundVertex(p11, n11));
                verts.Add(new GroundVertex(p01, n01));
            }
        }

        _vertexCount = verts.Count;
        int stride = Marshal.SizeOf<GroundVertex>();

        _vb?.Dispose();
        _vb = device.CreateBuffer(
            verts.ToArray(),
            new BufferDescription
            {
                BindFlags = BindFlags.VertexBuffer,
                Usage = ResourceUsage.Default,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None,
                ByteWidth = (uint)(stride * verts.Count),
                StructureByteStride = (uint)stride
            });
    }

    private static Vector3 Spherical(float r, float theta, float phi)
    {
        float sinPhi = MathF.Sin(phi);
        float x = r * (MathF.Cos(theta) * sinPhi);
        float y = r * MathF.Cos(phi);
        float z = r * (MathF.Sin(theta) * sinPhi);
        return new Vector3(x, y, z);
    }

    public void Draw(
        ID3D11DeviceContext context,
        IReadOnlyList<D3D11Renderer.ShellRenderState> shells,
        Matrix4x4 view,
        Matrix4x4 proj,
        ID3D11Buffer? sceneCB,
        ID3D11Buffer? objectCB)
    {
        if (_vb is null || _vs is null || _ps is null || _inputLayout is null)
            return;

        int instanceCount = shells.Count;
        if (instanceCount == 0)
            return;

        int vertexStride = Marshal.SizeOf<GroundVertex>();
        uint[] strides = new[] { (uint)vertexStride, (uint)Marshal.SizeOf<Vector3>() };
        uint[] offsets = new[] { 0u, 0u };

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(_inputLayout);
        EnsureInstanceBuffer(context.Device, instanceCount);
        if (_instanceBuffer is null)
            return;

        var mappedInstances = context.Map(_instanceBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            unsafe
            {
                var dst = (Vector3*)mappedInstances.DataPointer;
                for (int i = 0; i < instanceCount; i++)
                {
                    dst[i] = shells[i].Position;
                }
            }
        }
        finally
        {
            context.Unmap(_instanceBuffer, 0);
        }

        context.IASetVertexBuffers(0, 2, new[] { _vb, _instanceBuffer }, strides, offsets);

        context.VSSetShader(_vs);
        context.PSSetShader(_ps);

        if (sceneCB != null)
        {
            context.VSSetConstantBuffer(0, sceneCB);
        }

        context.DrawInstanced((uint)_vertexCount, (uint)instanceCount, 0, 0);
    }

    public void Dispose()
    {
        _vb?.Dispose();
        _vb = null;

        _instanceBuffer?.Dispose();
        _instanceBuffer = null;

        _inputLayout?.Dispose();
        _inputLayout = null;

        _vs?.Dispose();
        _vs = null;

        _ps?.Dispose();
        _ps = null;
    }

    private void EnsureInstanceBuffer(ID3D11Device? device, int requiredInstances)
    {
        if (device is null || requiredInstances <= 0)
            return;

        if (_instanceBuffer is not null && requiredInstances <= _instanceCapacity)
            return;

        _instanceBuffer?.Dispose();

        _instanceCapacity = _instanceCapacity > 0 ? Math.Max(requiredInstances, _instanceCapacity * 2) : Math.Max(requiredInstances, 64);

        _instanceBuffer = device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.VertexBuffer,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            MiscFlags = ResourceOptionFlags.None,
            ByteWidth = (uint)(_instanceCapacity * Marshal.SizeOf<Vector3>()),
            StructureByteStride = (uint)Marshal.SizeOf<Vector3>()
        });
    }
}
