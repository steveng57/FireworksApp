using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;

namespace FireworksApp.Rendering;

internal sealed class CanisterPipeline : IDisposable
{
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _vb;
    private int _vertexCount;

    public void Initialize(ID3D11Device device)
    {
        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Canister.hlsl");
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

        const float radius = 0.075f;
        const float height = 0.30f;
        const int slices = 32;

        var verts = new System.Collections.Generic.List<GroundVertex>(slices * 6);

        const float padThickness = 0.10f;
        float y0 = padThickness;
        float y1 = padThickness + height;

        for (int i = 0; i < slices; i++)
        {
            float a0 = (float)(i * (System.Math.PI * 2.0) / slices);
            float a1 = (float)((i + 1) * (System.Math.PI * 2.0) / slices);

            float c0 = (float)System.Math.Cos(a0);
            float s0 = (float)System.Math.Sin(a0);
            float c1 = (float)System.Math.Cos(a1);
            float s1 = (float)System.Math.Sin(a1);

            var p00 = new Vector3(radius * c0, y0, radius * s0);
            var p01 = new Vector3(radius * c1, y0, radius * s1);
            var p10 = new Vector3(radius * c0, y1, radius * s0);
            var p11 = new Vector3(radius * c1, y1, radius * s1);

            var n0 = Vector3.Normalize(new Vector3(c0, 0.0f, s0));
            var n1 = Vector3.Normalize(new Vector3(c1, 0.0f, s1));

            verts.Add(new GroundVertex(p00, n0));
            verts.Add(new GroundVertex(p10, n0));
            verts.Add(new GroundVertex(p11, n1));

            verts.Add(new GroundVertex(p00, n0));
            verts.Add(new GroundVertex(p11, n1));
            verts.Add(new GroundVertex(p01, n1));
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

    public void Draw(
        ID3D11DeviceContext context,
        IReadOnlyList<D3D11Renderer.CanisterRenderState> canisters,
        Matrix4x4 view,
        Matrix4x4 proj,
        ID3D11Buffer? sceneCB,
        ID3D11Buffer? lightingCB,
        ID3D11Buffer? objectCB)
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
        }
        if (lightingCB != null)
        {
            context.PSSetConstantBuffer(1, lightingCB);
        }

        if (canisters.Count == 0)
        {
            if (objectCB != null)
            {
                var world0 = Matrix4x4.Identity;
                var wvp0 = Matrix4x4.Transpose(world0 * view * proj);
                var obj0 = new SceneCBData { WorldViewProjection = wvp0, World = Matrix4x4.Transpose(world0) };
                var mapped0 = context.Map(objectCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
                Marshal.StructureToPtr(obj0, mapped0.DataPointer, false);
                context.Unmap(objectCB, 0);
                context.VSSetConstantBuffer(0, objectCB);
            }
            context.Draw((uint)_vertexCount, 0);
            return;
        }

        static Matrix4x4 CreateAlignYToDirection(Vector3 direction)
        {
            if (direction.LengthSquared() < 1e-8f)
                return Matrix4x4.Identity;

            var dir = Vector3.Normalize(direction);
            var from = Vector3.UnitY;
            float dot = Vector3.Dot(from, dir);

            if (dot <= -0.9999f)
            {
                return Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
            }

            if (dot >= 0.9999f)
            {
                return Matrix4x4.Identity;
            }

            var axis = Vector3.Normalize(Vector3.Cross(from, dir));
            float angle = MathF.Acos((float)System.Math.Clamp(dot, -1.0f, 1.0f));
            return Matrix4x4.CreateFromAxisAngle(axis, angle);
        }

        for (int i = 0; i < canisters.Count; i++)
        {
            var c = canisters[i];
            if (objectCB != null)
            {
                var world = CreateAlignYToDirection(c.Direction) * Matrix4x4.CreateTranslation(c.Position);
                var wvp = Matrix4x4.Transpose(world * view * proj);
                var obj = new SceneCBData { WorldViewProjection = wvp, World = Matrix4x4.Transpose(world) };
                var mapped = context.Map(objectCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
                Marshal.StructureToPtr(obj, mapped.DataPointer, false);
                context.Unmap(objectCB, 0);
                context.VSSetConstantBuffer(0, objectCB);
            }
            context.Draw((uint)_vertexCount, 0);
        }

        if (sceneCB != null)
        {
            context.VSSetConstantBuffer(0, sceneCB);
        }
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
