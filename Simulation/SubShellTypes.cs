using System.Numerics;

namespace FireworksApp.Simulation;

public enum SubShellSpawnMode
{
    Sphere,
    Ring,
    Cone
}

public sealed record class SubShellProfile(
    string Id,
    ShellId ShellProfileId,
    int Count,
    SubShellSpawnMode SpawnMode,
    float DelaySeconds,
    float InheritParentVelocity,
    float AddedSpeed,
    float DirectionJitter,
    float SpeedJitter,
    float PositionJitter,
    float ChildTimeScale,
    ColorSchemeId? ColorSchemeId,
    FireworkBurstShape? BurstShapeOverride,
    float MinAltitudeToSpawn,
    int MaxSubshellDepth);

public sealed record class SubShellAttachment(
    SubShellId SubShellProfileId,
    float Probability = 1.0f,
    float Scale = 1.0f,
    int DepthBudget = 1);
