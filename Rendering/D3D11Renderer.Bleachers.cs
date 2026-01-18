using System;
using System.Diagnostics;
using System.Numerics;

namespace FireworksApp.Rendering;

public sealed partial class D3D11Renderer
{
    private const float BleacherFrontDistanceMeters = 60.0f;
    private static readonly float BleacherDepthMeters = 12 * 0.85f;
    private static readonly Vector3 PadCenter = Vector3.Zero;

    private void LoadBleachersShadersAndGeometry()
    {
        if (_device is null)
            return;

        _bleachersPipeline.Initialize(_device);
        _bleacherWorlds = ComputeBleacherWorlds();
        ValidateBleacherFrontDistances(_bleacherWorlds);
    }

    private Matrix4x4[] ComputeBleacherWorlds()
    {
        var dirs = new[]
        {
            new Vector3(-1.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 1.0f),
            new Vector3(0.0f, 0.0f, -1.0f)
        };

        var worlds = new Matrix4x4[dirs.Length];
        for (int i = 0; i < dirs.Length; i++)
        {
            var dir = NormalizeHorizontal(dirs[i]);
            var front = PadCenter + dir * BleacherFrontDistanceMeters;
            var forward = -dir; // rotate 180° so local -Z faces pad
            var world = Matrix4x4.CreateWorld(front, forward, Vector3.UnitY);
            worlds[i] = world;
        }

        return worlds;
    }

    private static Vector3 NormalizeHorizontal(Vector3 dir)
    {
        dir.Y = 0.0f;
        if (dir.LengthSquared() < 1e-6f)
            return Vector3.UnitZ;
        return Vector3.Normalize(dir);
    }

    private void DrawBleachers()
    {
        if (_context is null)
            return;

        _bleachersPipeline.Draw(_context, _bleacherWorlds, _view, _proj, _sceneCB, _lightingCB, _objectCB);
    }

    [Conditional("DEBUG")]
    private void ValidateBleacherFrontDistances(ReadOnlySpan<Matrix4x4> worlds)
    {
        const float epsilon = 0.05f;
        for (int i = 0; i < worlds.Length; i++)
        {
            var frontWs = Vector3.Transform(Vector3.Zero, worlds[i]);
            var delta = new Vector2(frontWs.X - PadCenter.X, frontWs.Z - PadCenter.Z);
            float radial = delta.Length();
            if (Math.Abs(radial - BleacherFrontDistanceMeters) > epsilon)
            {
                Debug.WriteLine($"[Bleachers] Front edge radius {radial:F3}m (expected {BleacherFrontDistanceMeters:F1}m) at index {i}");
            }
        }
    }
}
