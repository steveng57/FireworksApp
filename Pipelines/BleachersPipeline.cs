using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;

namespace FireworksApp.Rendering;

internal sealed class BleachersPipeline : IDisposable
{
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _vb;
    private int _vertexCount;

    private const float WidthMeters = 60.0f;
    private const int RowCount = 12;
    private const float RowRiseMeters = 0.40f;
    private const float RowRunMeters = 0.85f;
    private const float PostSpacingMeters = 3.0f;
    private const float PostSizeMeters = 0.12f;
    private const float BeamThicknessMeters = 0.12f;

    public void Initialize(ID3D11Device device)
    {
        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Bleachers.hlsl");
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

        var verts = BuildGeometry();
        _vertexCount = verts.Length;

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

    public void Draw(
        ID3D11DeviceContext context,
        IReadOnlyList<Matrix4x4> worlds,
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

        if (worlds.Count == 0)
        {
            DrawSingle(context, objectCB, Matrix4x4.Identity, view, proj);
            return;
        }

        for (int i = 0; i < worlds.Count; i++)
        {
            DrawSingle(context, objectCB, worlds[i], view, proj);
        }

        if (sceneCB != null)
        {
            context.VSSetConstantBuffer(0, sceneCB);
        }
    }

    private static GroundVertex[] BuildGeometry()
    {
        float halfWidth = WidthMeters * 0.5f;
        var verts = new List<GroundVertex>(RowCount * 36 + 2048);

        for (int i = 0; i < RowCount; i++)
        {
            float y0 = i * RowRiseMeters;
            float y1 = y0 + RowRiseMeters;
            float z0 = i * RowRunMeters;
            float z1 = z0 + RowRunMeters;
            AddBox(verts, -halfWidth, halfWidth, y0, y1, z0, z1);

            // Horizontal support beam at this row level
            float beamY0 = y0;
            float beamY1 = beamY0 + BeamThicknessMeters;
            float beamZCenter = z0 + RowRunMeters * 0.5f;
            float beamZ0 = beamZCenter - BeamThicknessMeters * 0.5f;
            float beamZ1 = beamZCenter + BeamThicknessMeters * 0.5f;
            AddBox(verts, -halfWidth, halfWidth, beamY0, beamY1, beamZ0, beamZ1);

            // Vertical posts along width for this row (skip row 0 which sits on ground)
            if (i > 0)
            {
                int postCountX = Math.Max(2, (int)MathF.Floor(WidthMeters / PostSpacingMeters) + 1);
                float postStep = WidthMeters / (postCountX - 1);
                float postHalf = PostSizeMeters * 0.5f;
                float postY0 = 0.0f;
                float postY1 = y0;
                float postZCenter = z0;
                float postZ0 = postZCenter - postHalf;
                float postZ1 = postZCenter + postHalf;
                for (int px = 0; px < postCountX; px++)
                {
                    float xCenter = -halfWidth + px * postStep;
                    float postX0 = xCenter - postHalf;
                    float postX1 = xCenter + postHalf;
                    if (postY1 > postY0)
                        AddBox(verts, postX0, postX1, postY0, postY1, postZ0, postZ1);
                }
            }
        }

        return verts.ToArray();
    }

    private static void AddBox(List<GroundVertex> verts, float x0, float x1, float y0, float y1, float z0, float z1)
    {
        var p000 = new Vector3(x0, y0, z0);
        var p100 = new Vector3(x1, y0, z0);
        var p010 = new Vector3(x0, y1, z0);
        var p110 = new Vector3(x1, y1, z0);

        var p001 = new Vector3(x0, y0, z1);
        var p101 = new Vector3(x1, y0, z1);
        var p011 = new Vector3(x0, y1, z1);
        var p111 = new Vector3(x1, y1, z1);

        // Top (+Y)
        AddQuad(verts, p010, p110, p111, p011, Vector3.UnitY);
        // Bottom (-Y)
        AddQuad(verts, p001, p101, p100, p000, -Vector3.UnitY);
        // Front (-Z, pad-facing edge at z0)
        AddQuad(verts, p000, p100, p110, p010, -Vector3.UnitZ);
        // Back (+Z)
        AddQuad(verts, p101, p001, p011, p111, Vector3.UnitZ);
        // Left (-X)
        AddQuad(verts, p001, p000, p010, p011, -Vector3.UnitX);
        // Right (+X)
        AddQuad(verts, p100, p101, p111, p110, Vector3.UnitX);
    }

    private static void AddQuad(List<GroundVertex> verts, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
    {
        verts.Add(new GroundVertex(p0, normal));
        verts.Add(new GroundVertex(p1, normal));
        verts.Add(new GroundVertex(p2, normal));

        verts.Add(new GroundVertex(p0, normal));
        verts.Add(new GroundVertex(p2, normal));
        verts.Add(new GroundVertex(p3, normal));
    }

    private static void UploadObjectConstants(ID3D11DeviceContext context, ID3D11Buffer? objectCB, in Matrix4x4 world, in Matrix4x4 view, in Matrix4x4 proj)
    {
        if (objectCB is null)
            return;

        var wvp = Matrix4x4.Transpose(world * view * proj);
        var obj = new SceneCBData
        {
            WorldViewProjection = wvp,
            World = Matrix4x4.Transpose(world)
        };

        var mapped = context.Map(objectCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        Marshal.StructureToPtr(obj, mapped.DataPointer, false);
        context.Unmap(objectCB, 0);
        context.VSSetConstantBuffer(0, objectCB);
    }

    private void DrawSingle(ID3D11DeviceContext context, ID3D11Buffer? objectCB, in Matrix4x4 world, in Matrix4x4 view, in Matrix4x4 proj)
    {
        UploadObjectConstants(context, objectCB, world, view, proj);
        context.Draw((uint)_vertexCount, 0);
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
