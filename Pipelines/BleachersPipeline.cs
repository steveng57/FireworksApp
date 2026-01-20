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
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct BleacherVertex
    {
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly uint FigureId;

        public BleacherVertex(Vector3 position, Vector3 normal, uint figureId)
        {
            Position = position;
            Normal = normal;
            FigureId = figureId;
        }
    }

    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _vb;
    private int _vertexCount;

    private readonly record struct BleacherDetailProfile(
        float WidthMeters,
        int RowCount,
        float RowRiseMeters,
        float RowRunMeters,
        float PostSpacingMeters,
        float PostSizeMeters,
        float BeamThicknessMeters,
        bool StairEnabled,
        float StairWidthMeters,
        float StairInsetMeters,
        float StairTreadThickness,
        float StairSideClearance,
        bool RailingEnabled,
        float RailHeightMeters,
        float RailPostSpacingMeters,
        float RailRadiusMeters,
        float RailPostRadiusMeters,
        float RailOffsetFromEdge,
        bool IncludeMidRail,
        float MidRailHeightFactor,
        float SideRailFrontInsetMeters)
    {
        public float HalfWidth => WidthMeters * 0.5f;
        public float DepthMeters => RowCount * RowRunMeters;
    }

    private readonly record struct StairSpec(float CenterX, float X0, float X1);

    private static readonly BleacherDetailProfile Profile = new(
        WidthMeters: 60.0f,
        RowCount: 12,
        RowRiseMeters: 0.40f,
        RowRunMeters: 0.85f,
        PostSpacingMeters: 3.0f,
        PostSizeMeters: 0.12f,
        BeamThicknessMeters: 0.12f,
        StairEnabled: true,
        StairWidthMeters: 1.2f,
        StairInsetMeters: 1.2f,
        StairTreadThickness: 0.10f,
        StairSideClearance: 0.05f,
        RailingEnabled: true,
        RailHeightMeters: 1.10f,
        RailPostSpacingMeters: 2.0f,
        RailRadiusMeters: 0.035f,
        RailPostRadiusMeters: 0.04f,
        RailOffsetFromEdge: 0.10f,
        IncludeMidRail: true,
        MidRailHeightFactor: 0.55f,
        SideRailFrontInsetMeters: 0.25f);

    // Audience placement
    private const float AudienceSpacingMin = 0.55f;
    private const float AudienceSpacingMax = 0.65f;
    private const float AudienceFillBase = 0.8f;
    private const float AudienceJitterX = 0.05f;
    private const float AudienceJitterZ = 0.03f;
    private const float AudienceYawJitterDeg = 8.0f;
    private const float AudienceScaleMin = 0.9f;
    private const float AudienceScaleMax = 1.1f;
    private const float AudienceStandingChance = 0.02f;
    private const int AudienceSeed = 23456789;

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
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32_UInt, 24, 0)
        };
        _inputLayout = device.CreateInputLayout(elements, vsBytes);

        var verts = BuildGeometry(Profile);
        _vertexCount = verts.Length;

        int stride = Marshal.SizeOf<BleacherVertex>();
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

        int stride = Marshal.SizeOf<BleacherVertex>();
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

    private static BleacherVertex[] BuildGeometry(in BleacherDetailProfile profile)
    {
        float halfWidth = profile.HalfWidth;
        var verts = new List<BleacherVertex>(profile.RowCount * 64 + 52000);
        var stairs = BuildStairSpecs(profile);

        for (int i = 0; i < profile.RowCount; i++)
        {
            float y0 = i * profile.RowRiseMeters;
            float y1 = y0 + profile.RowRiseMeters;
            float z0 = i * profile.RowRunMeters;
            float z1 = z0 + profile.RowRunMeters;

            AddSeatRow(verts, profile, stairs, i, halfWidth);

            // Horizontal support beam at this row level
            float beamY0 = y0;
            float beamY1 = beamY0 + profile.BeamThicknessMeters;
            float beamZCenter = z0 + profile.RowRunMeters * 0.5f;
            float beamZ0 = beamZCenter - profile.BeamThicknessMeters * 0.5f;
            float beamZ1 = beamZCenter + profile.BeamThicknessMeters * 0.5f;
            AddBox(verts, -halfWidth, halfWidth, beamY0, beamY1, beamZ0, beamZ1);

            // Vertical posts along width for this row (skip row 0 which sits on ground)
            if (i > 0)
            {
                int postCountX = Math.Max(2, (int)MathF.Floor(profile.WidthMeters / profile.PostSpacingMeters) + 1);
                float postStep = profile.WidthMeters / (postCountX - 1);
                float postHalf = profile.PostSizeMeters * 0.5f;
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

        AddStairs(verts, profile, stairs);
        AddRailings(verts, profile);
        AddAudience(verts, halfWidth, profile, stairs);

        return verts.ToArray();
    }

    private static StairSpec[] BuildStairSpecs(in BleacherDetailProfile profile)
    {
        if (!profile.StairEnabled)
            return Array.Empty<StairSpec>();

        float halfWidth = profile.HalfWidth;
        var centers = new[] { -halfWidth + profile.StairInsetMeters, 0.0f, halfWidth - profile.StairInsetMeters };
        var specs = new List<StairSpec>(centers.Length);
        float halfSpan = profile.StairWidthMeters * 0.5f + profile.StairSideClearance;

        for (int i = 0; i < centers.Length; i++)
        {
            float x0 = centers[i] - halfSpan;
            float x1 = centers[i] + halfSpan;
            x0 = MathF.Max(x0, -halfWidth);
            x1 = MathF.Min(x1, halfWidth);
            if (x1 - x0 > 0.05f)
            {
                specs.Add(new StairSpec(centers[i], x0, x1));
            }
        }

        specs.Sort((a, b) => a.X0.CompareTo(b.X0));
        return specs.ToArray();
    }

    private static void AddSeatRow(List<BleacherVertex> verts, in BleacherDetailProfile profile, ReadOnlySpan<StairSpec> stairs, int rowIndex, float halfWidth)
    {
        float y0 = rowIndex * profile.RowRiseMeters;
        float y1 = y0 + profile.RowRiseMeters;
        float z0 = rowIndex * profile.RowRunMeters;
        float z1 = z0 + profile.RowRunMeters;

        float minSegmentWidth = 0.50f;
        float segmentStart = -halfWidth;

        for (int i = 0; i < stairs.Length; i++)
        {
            float segmentEnd = MathF.Min(stairs[i].X0, halfWidth);
            if (segmentEnd - segmentStart >= minSegmentWidth)
            {
                AddBox(verts, segmentStart, segmentEnd, y0, y1, z0, z1);
            }

            segmentStart = MathF.Max(segmentStart, stairs[i].X1);
        }

        if (halfWidth - segmentStart >= minSegmentWidth)
        {
            AddBox(verts, segmentStart, halfWidth, y0, y1, z0, z1);
        }
    }

    private static void AddStairs(List<BleacherVertex> verts, in BleacherDetailProfile profile, ReadOnlySpan<StairSpec> stairs)
    {
        if (!profile.StairEnabled || stairs.Length == 0)
            return;

        float halfWidth = profile.HalfWidth;
        float stairHalfWidth = profile.StairWidthMeters * 0.5f;

        for (int row = 0; row < profile.RowCount; row++)
        {
            float y0 = row * profile.RowRiseMeters;
            float y1 = y0 + profile.RowRiseMeters;
            float z0 = row * profile.RowRunMeters;
            float z1 = z0 + profile.RowRunMeters;

            for (int i = 0; i < stairs.Length; i++)
            {
                float stairX0 = MathF.Max(stairs[i].CenterX - stairHalfWidth, -halfWidth);
                float stairX1 = MathF.Min(stairs[i].CenterX + stairHalfWidth, halfWidth);
                AddBox(verts, stairX0, stairX1, y0, y1, z0, z1);
            }
        }
    }

    private static void AddRailings(List<BleacherVertex> verts, in BleacherDetailProfile profile)
    {
        if (!profile.RailingEnabled)
            return;

        float halfWidth = profile.HalfWidth;
        float depth = profile.DepthMeters;
        float topDeckY = (profile.RowCount - 1) * profile.RowRiseMeters;
        float backZ = depth - profile.RailOffsetFromEdge;
        float leftX = -halfWidth + profile.RailOffsetFromEdge;
        float rightX = halfWidth - profile.RailOffsetFromEdge;
        float sideStartZ = profile.StairEnabled ? MathF.Max(profile.SideRailFrontInsetMeters, 0.0f) : 0.0f;
        sideStartZ = MathF.Min(sideStartZ, depth);

        AddRailRun(
            verts,
            new Vector3(leftX, topDeckY, backZ),
            new Vector3(rightX, topDeckY, backZ),
            profile.RailHeightMeters,
            profile.RailPostRadiusMeters,
            profile.RailRadiusMeters,
            profile.RailPostSpacingMeters,
            profile.IncludeMidRail,
            profile.MidRailHeightFactor);

        AddRailRun(
            verts,
            new Vector3(leftX, topDeckY, sideStartZ),
            new Vector3(leftX, topDeckY, depth),
            profile.RailHeightMeters,
            profile.RailPostRadiusMeters,
            profile.RailRadiusMeters,
            profile.RailPostSpacingMeters,
            profile.IncludeMidRail,
            profile.MidRailHeightFactor);

        AddRailRun(
            verts,
            new Vector3(rightX, topDeckY, sideStartZ),
            new Vector3(rightX, topDeckY, depth),
            profile.RailHeightMeters,
            profile.RailPostRadiusMeters,
            profile.RailRadiusMeters,
            profile.RailPostSpacingMeters,
            profile.IncludeMidRail,
            profile.MidRailHeightFactor);
    }

    private static void AddRailRun(
        List<BleacherVertex> verts,
        in Vector3 start,
        in Vector3 end,
        float postHeight,
        float postRadius,
        float railRadius,
        float postSpacing,
        bool includeMidRail,
        float midRailHeightFactor)
    {
        float length = MathF.Max(MathF.Abs(end.X - start.X), MathF.Abs(end.Z - start.Z));
        if (length < 1e-3f)
            return;

        int postCount = Math.Max(2, (int)MathF.Floor(length / MathF.Max(postSpacing, 0.01f)) + 1);
        float step = length / (postCount - 1);
        var dir = Vector3.Normalize(new Vector3(end.X - start.X, 0.0f, end.Z - start.Z));
        bool alongX = MathF.Abs(dir.X) > MathF.Abs(dir.Z);

        for (int i = 0; i < postCount; i++)
        {
            float dist = step * i;
            var pos = start + dir * dist;
            float px0 = pos.X - postRadius;
            float px1 = pos.X + postRadius;
            float pz0 = pos.Z - postRadius;
            float pz1 = pos.Z + postRadius;
            float py0 = start.Y;
            float py1 = py0 + postHeight;
            AddBox(verts, px0, px1, py0, py1, pz0, pz1);
        }

        float railY0 = start.Y + postHeight - railRadius;
        float railY1 = railY0 + railRadius * 2.0f;

        if (alongX)
        {
            float x0 = MathF.Min(start.X, end.X);
            float x1 = MathF.Max(start.X, end.X);
            float z0 = start.Z - railRadius;
            float z1 = start.Z + railRadius;
            AddBox(verts, x0, x1, railY0, railY1, z0, z1);

            if (includeMidRail)
            {
                float midY0 = start.Y + postHeight * midRailHeightFactor - railRadius;
                float midY1 = midY0 + railRadius * 2.0f;
                AddBox(verts, x0, x1, midY0, midY1, z0, z1);
            }
        }
        else
        {
            float z0 = MathF.Min(start.Z, end.Z);
            float z1 = MathF.Max(start.Z, end.Z);
            float x0 = start.X - railRadius;
            float x1 = start.X + railRadius;
            AddBox(verts, x0, x1, railY0, railY1, z0, z1);

            if (includeMidRail)
            {
                float midY0 = start.Y + postHeight * midRailHeightFactor - railRadius;
                float midY1 = midY0 + railRadius * 2.0f;
                AddBox(verts, x0, x1, midY0, midY1, z0, z1);
            }
        }
    }

    private static bool IsInStairSpan(float x, ReadOnlySpan<StairSpec> stairs)
    {
        for (int i = 0; i < stairs.Length; i++)
        {
            if (x >= stairs[i].X0 && x <= stairs[i].X1)
                return true;
        }

        return false;
    }

    private static float AdvancePastStair(float x, ReadOnlySpan<StairSpec> stairs)
    {
        float next = x;
        for (int i = 0; i < stairs.Length; i++)
        {
            if (x >= stairs[i].X0 - 1e-3f && x <= stairs[i].X1 + 1e-3f)
            {
                next = MathF.Max(next, stairs[i].X1);
            }
        }

        return next;
    }

    private static void AddAudience(List<BleacherVertex> verts, float halfWidth, in BleacherDetailProfile profile, ReadOnlySpan<StairSpec> stairs)
    {
        var rng = new Random(AudienceSeed);
        var variants = CreateSilhouetteVariants();
        var standingIndex = variants.Count - 1;
        uint figureId = 1;

        float audienceStartX = stairs.Length > 0 ? stairs[0].X0 + AudienceSpacingMin * 0.5f : -halfWidth + AudienceSpacingMin * 0.5f;
        float audienceEndX = stairs.Length > 0 ? stairs[^1].X1 - AudienceSpacingMin * 0.5f : halfWidth;

        if (audienceEndX <= audienceStartX)
            return;

        for (int row = 0; row < profile.RowCount; row++)
        {
            float rowY = (row + 1) * profile.RowRiseMeters; // seat surface at top of step
            float rowZ = row * profile.RowRunMeters;
            float seatZ = rowZ + profile.RowRunMeters * 0.35f;

            float t = profile.RowCount <= 1 ? 0.0f : (float)row / (profile.RowCount - 1);
            float fillRow = Math.Clamp(AudienceFillBase * Lerp(1.05f, 0.85f, t), 0.0f, 1.0f);

            float x = audienceStartX;
            while (x <= audienceEndX)
            {
                if (IsInStairSpan(x, stairs))
                {
                    x = AdvancePastStair(x, stairs) + AudienceSpacingMin;
                    continue;
                }

                if (rng.NextDouble() <= fillRow)
                {
                    bool standing = row >= profile.RowCount / 2 && rng.NextDouble() < AudienceStandingChance;
                    int variantIndex = standing ? standingIndex : rng.Next(standingIndex);
                    var baseVerts = variants[variantIndex];

                    float spacing = Lerp(AudienceSpacingMin, AudienceSpacingMax, (float)rng.NextDouble());
                    float jitterX = (float)((rng.NextDouble() - 0.5) * 2.0 * AudienceJitterX);
                    float jitterZ = (float)((rng.NextDouble() - 0.5) * 2.0 * AudienceJitterZ);
                    float yaw = MathF.PI / 180.0f * (float)((rng.NextDouble() - 0.5) * 2.0 * AudienceYawJitterDeg);
                    float scale = Lerp(AudienceScaleMin, AudienceScaleMax, (float)rng.NextDouble());

                    var rotation = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, yaw);
                    var scaleM = Matrix4x4.CreateScale(scale);
                    var translation = Matrix4x4.CreateTranslation(x + jitterX, rowY, seatZ + jitterZ);
                    var transform = scaleM * rotation * translation;

                    AppendVariant(verts, baseVerts, transform, rotation, figureId);
                    figureId++;
                    x += spacing;
                }
                else
                {
                    float spacing = Lerp(AudienceSpacingMin, AudienceSpacingMax, (float)rng.NextDouble());
                    x += spacing;
                }
            }
        }
    }

    private static List<BleacherVertex[]> CreateSilhouetteVariants()
    {
        var list = new List<BleacherVertex[]>();

        list.Add(BuildSeatedVariant(0.0f, 0.10f, headOffsetZ: -0.05f));
        list.Add(BuildSeatedVariant(-0.05f, -0.05f, headOffsetZ: -0.08f));
        list.Add(BuildSeatedVariant(0.03f, 0.0f, headOffsetZ: -0.02f));
        list.Add(BuildSeatedVariant(0.0f, 0.0f, headOffsetZ: -0.05f, hatHeight: 0.08f));

        list.Add(BuildStandingVariant());

        return list;
    }

    private static BleacherVertex[] BuildSeatedVariant(float torsoLeanZ, float torsoShiftZ, float headOffsetZ, float hatHeight = 0.0f)
    {
        var v = new List<BleacherVertex>(120);

        const float seatHeight = 0.12f;
        const float seatDepth = 0.35f;
        const float seatWidth = 0.5f;
        const float legDepth = 0.45f;
        const float legHeight = 0.45f;
        const float torsoHeight = 0.55f;
        const float torsoDepth = 0.28f;
        const float headSize = 0.22f;

        float seatY0 = 0.0f;
        float seatY1 = seatY0 + seatHeight;
        float seatZ0 = -seatDepth;
        float seatZ1 = 0.05f;

        // Seat base
        AddBoxUnlit(v, -seatWidth * 0.5f, seatWidth * 0.5f, seatY0, seatY1, seatZ0, seatZ1);

        // Legs block (tucked toward pad, -Z)
        float legY1 = seatY0 + legHeight;
        AddBoxUnlit(v, -seatWidth * 0.35f, seatWidth * 0.35f, seatY0, legY1, seatZ0 - legDepth, seatZ0 + 0.05f);

        // Torso block
        float torsoY0 = seatY1;
        float torsoY1 = torsoY0 + torsoHeight;
        float torsoZ0 = seatZ0 * 0.6f + torsoShiftZ + torsoLeanZ;
        float torsoZ1 = torsoZ0 + torsoDepth;
        AddBoxUnlit(v, -seatWidth * 0.32f, seatWidth * 0.32f, torsoY0, torsoY1, torsoZ0, torsoZ1);

        // Head
        float headY0 = torsoY1;
        float headZ0 = torsoZ1 + headOffsetZ;
        float headDepth = headSize * 0.6f;
        float headRadius = (headSize + hatHeight) * 0.5f;
        float headCenterY = headY0 + headRadius;
        float headCenterZ = headZ0 + headDepth * 0.5f;
        AddSphereUnlit(v, new Vector3(0.0f, headCenterY, headCenterZ), headRadius, 3, 6);

        return v.ToArray();
    }

    private static BleacherVertex[] BuildStandingVariant()
    {
        var v = new List<BleacherVertex>(90);

        const float width = 0.45f;
        const float legHeight = 1.0f;
        const float torsoHeight = 0.65f;
        const float torsoDepth = 0.28f;
        const float headSize = 0.22f;

        float legY0 = 0.0f;
        float legY1 = legY0 + legHeight;
        AddBoxUnlit(v, -width * 0.25f, width * 0.25f, legY0, legY1, -0.15f, 0.12f);

        float torsoY0 = legY1;
        float torsoY1 = torsoY0 + torsoHeight;
        AddBoxUnlit(v, -width * 0.3f, width * 0.3f, torsoY0, torsoY1, -0.18f, 0.12f);

        float headY0 = torsoY1;
        float headRadius = headSize * 0.45f;
        float headCenterY = headY0 + headRadius;
        float headCenterZ = -0.025f;
        AddSphereUnlit(v, new Vector3(0.0f, headCenterY, headCenterZ), headRadius, 3, 6);

        return v.ToArray();
    }

    private static void AddSphereUnlit(List<BleacherVertex> verts, in Vector3 center, float radius, int latSegments, int lonSegments, uint figureId = 0)
    {
        var normal = Vector3.Zero;

        for (int lat = 0; lat < latSegments; lat++)
        {
            float v0 = (float)lat / latSegments;
            float v1 = (float)(lat + 1) / latSegments;

            float lat0 = MathF.PI * (v0 - 0.5f);
            float lat1 = MathF.PI * (v1 - 0.5f);

            float y0 = MathF.Sin(lat0);
            float y1 = MathF.Sin(lat1);
            float r0 = MathF.Cos(lat0);
            float r1 = MathF.Cos(lat1);

            for (int lon = 0; lon < lonSegments; lon++)
            {
                float u0 = (float)lon / lonSegments;
                float u1 = (float)(lon + 1) / lonSegments;

                float lon0 = u0 * MathF.PI * 2.0f;
                float lon1 = u1 * MathF.PI * 2.0f;

                var p00 = center + radius * new Vector3(r0 * MathF.Cos(lon0), y0, r0 * MathF.Sin(lon0));
                var p01 = center + radius * new Vector3(r0 * MathF.Cos(lon1), y0, r0 * MathF.Sin(lon1));
                var p10 = center + radius * new Vector3(r1 * MathF.Cos(lon0), y1, r1 * MathF.Sin(lon0));
                var p11 = center + radius * new Vector3(r1 * MathF.Cos(lon1), y1, r1 * MathF.Sin(lon1));

                AddTriangle(verts, p00, p10, p11, normal, figureId);
                AddTriangle(verts, p00, p11, p01, normal, figureId);
            }
        }
    }

    private static void AppendVariant(List<BleacherVertex> dest, ReadOnlySpan<BleacherVertex> variant, in Matrix4x4 transform, in Matrix4x4 rotation, uint figureId)
    {
        for (int i = 0; i < variant.Length; i++)
        {
            var p = Vector3.Transform(variant[i].Position, transform);
            var n = Vector3.TransformNormal(variant[i].Normal, rotation);
            dest.Add(new BleacherVertex(p, n, figureId));
        }
    }

    private static void AddBoxUnlit(List<BleacherVertex> verts, float x0, float x1, float y0, float y1, float z0, float z1, uint figureId = 0)
    {
        // Normals set to zero to keep silhouettes dark (ambient only).
        var n = Vector3.Zero;
        AddQuad(verts, new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), n, figureId);
        AddQuad(verts, new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), n, figureId);
        AddQuad(verts, new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), n, figureId);
        AddQuad(verts, new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), n, figureId);
        AddQuad(verts, new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), n, figureId);
        AddQuad(verts, new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), n, figureId);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static void AddBox(List<BleacherVertex> verts, float x0, float x1, float y0, float y1, float z0, float z1, uint figureId = 0)
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
        AddQuad(verts, p010, p110, p111, p011, Vector3.UnitY, figureId);
        // Bottom (-Y)
        AddQuad(verts, p001, p101, p100, p000, -Vector3.UnitY, figureId);
        // Front (-Z, pad-facing edge at z0)
        AddQuad(verts, p000, p100, p110, p010, -Vector3.UnitZ, figureId);
        // Back (+Z)
        AddQuad(verts, p101, p001, p011, p111, Vector3.UnitZ, figureId);
        // Left (-X)
        AddQuad(verts, p001, p000, p010, p011, -Vector3.UnitX, figureId);
        // Right (+X)
        AddQuad(verts, p100, p101, p111, p110, Vector3.UnitX, figureId);
    }

    private static void AddQuad(List<BleacherVertex> verts, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, uint figureId)
    {
        verts.Add(new BleacherVertex(p0, normal, figureId));
        verts.Add(new BleacherVertex(p1, normal, figureId));
        verts.Add(new BleacherVertex(p2, normal, figureId));

        verts.Add(new BleacherVertex(p0, normal, figureId));
        verts.Add(new BleacherVertex(p2, normal, figureId));
        verts.Add(new BleacherVertex(p3, normal, figureId));
    }

    private static void AddTriangle(List<BleacherVertex> verts, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 normal, uint figureId)
    {
        verts.Add(new BleacherVertex(p0, normal, figureId));
        verts.Add(new BleacherVertex(p1, normal, figureId));
        verts.Add(new BleacherVertex(p2, normal, figureId));
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
