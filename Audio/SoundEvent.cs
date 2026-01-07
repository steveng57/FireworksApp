using System;
using System.Numerics;

namespace FireworksApp.Audio;

public enum SoundEventType
{
    ShellLaunch,
    ShellBurst,
    Crackle,
    FinaleCluster
}

public readonly record struct SoundEvent(
    SoundEventType Type,
    Vector3 Position,
    float Gain = 1.0f,
    bool Loop = false,
    TimeSpan? Delay = null);
