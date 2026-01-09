using System.Numerics;
using System.Runtime.InteropServices;

namespace FireworksApp.Rendering;

internal enum ParticleKind : uint
{
    Dead = 0,
    Shell = 1,
    Spark = 2,
    Smoke = 3,
    Crackle = 4,
    PopFlash = 5,
    FinaleSpark = 6
}

internal static class ParticleKindBudget
{
    public static int GetBudget(ParticleKind kind) => kind switch
    {
        ParticleKind.Shell => 50_000,
        ParticleKind.Spark => 1_500_000,      // Increased from 400k
        ParticleKind.Smoke => 500_000,    // Increased from 1.2M
        ParticleKind.Crackle => 500_000,    // Increased from 200k
        ParticleKind.PopFlash => 50_000,
        ParticleKind.FinaleSpark => 800_000, // Increased from 400k
        _ => 0
    };

    // Total capacity needed for backing particle buffer (sum of all budgets)
    // Total: 50k + 800k + 1500k + 400k + 50k + 800k = 3,600,000 (~280 MB)
    // 50k + 400k + 1200k + 200k + 50k + 400k = 2,300,000
    public static int GetTotalCapacity()
    {
        int total = 0;
        foreach (ParticleKind kind in Enum.GetValues<ParticleKind>())
        {
            if (kind != ParticleKind.Dead)
                total += GetBudget(kind);
        }
        return total;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct GpuParticle
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Age;
    public float Lifetime;
    public Vector4 BaseColor;
    public Vector4 Color;
    public uint Kind;
    public uint _pad0;
    public uint _pad1;
    public uint _pad2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FrameCBData
{
    public Matrix4x4 ViewProjection;
    public Vector3 CameraRightWS;
    public float DeltaTime;
    public Vector3 CameraUpWS;
    public float Time;

    public Vector3 CrackleBaseColor;
    public float CrackleBaseSize;
    public Vector3 CracklePeakColor;
    public float CrackleFlashSizeMul;
    public Vector3 CrackleFadeColor;
    public float CrackleTau;

    public Vector3 SchemeTint;
    public float _stpad0;

    public uint ParticlePass;
    public uint _ppad0;
    public uint _ppad1;
    public uint _ppad2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SceneCBData
{
    public Matrix4x4 WorldViewProjection;
    public Matrix4x4 World;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LightingCBData
{
    public Vector3 LightDirectionWS;
    public float _pad0;
    public Vector3 LightColor;
    public float _pad1;
    public Vector3 AmbientColor;
    public float _pad2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GroundVertex
{
    public Vector3 Position;
    public Vector3 Normal;

    public GroundVertex(Vector3 position, Vector3 normal)
    {
        Position = position;
        Normal = normal;
    }
}
