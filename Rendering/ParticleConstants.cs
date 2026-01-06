using System.Numerics;

namespace FireworksApp.Rendering;

internal static class ParticleConstants
{
    internal const float SmokeIntensity = 0.45f;

    internal static readonly Vector3 CrackleBaseColor = new(2.2f, 2.0f, 1.6f);
    internal static readonly Vector3 CracklePeakColor = new(3.5f, 3.2f, 2.4f);
    internal static readonly Vector3 CrackleFadeColor = new(1.2f, 1.0f, 0.6f);
    internal const float CrackleBaseSize = 0.010f;
    internal const float CrackleFlashSizeMul = 2.3f;
    internal const float CrackleTau = 0.035f;
}
