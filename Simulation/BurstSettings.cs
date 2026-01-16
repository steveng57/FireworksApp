using System.Numerics;

namespace FireworksApp.Simulation;

public sealed record BurstEmissionSettings(
    int ChrysanthemumSpokeCount,
    float ChrysanthemumSpokeJitter,
    float WillowDownwardBlend,
    int PalmFrondCount,
    float PalmFrondConeAngleRadians,
    float PalmFrondJitterAngleRadians,
    float HorsetailDownwardBlend,
    float HorsetailMinDownDot,
    float HorsetailJitterAngleRadians)
{
    public static BurstEmissionSettings Defaults { get; } = new(
        ChrysanthemumSpokeCount: 24,
        ChrysanthemumSpokeJitter: 0.12f,
        WillowDownwardBlend: 0.35f,
        PalmFrondCount: 7,
        PalmFrondConeAngleRadians: 0.65f,
        PalmFrondJitterAngleRadians: 0.08f,
        HorsetailDownwardBlend: 0.75f,
        HorsetailMinDownDot: -0.25f,
        HorsetailJitterAngleRadians: 0.15f);
}
