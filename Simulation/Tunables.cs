using System;

namespace FireworksApp.Simulation;

internal static class Tunables
{
    // Simulation pacing
    internal const float DefaultTimeScale = 0.55f;

    // Physics integration
    internal const float ShellDragK = 0.020f;

    // Burst emission shaping
    internal static class Emission
    {
        internal const int ChrysanthemumSpokeCount = 24;
        internal const float ChrysanthemumSpokeJitter = 0.12f;

        internal const float WillowDownwardBlend = 0.35f;

        internal const int PalmFrondCount = 7;
        internal const float PalmFrondConeAngleRadians = 0.65f;
        internal const float PalmFrondJitterAngleRadians = 0.08f;

        internal const float HorsetailDownwardBlend = 0.75f;
        internal const float HorsetailMinDownDot = -0.25f;
        internal const float HorsetailJitterAngleRadians = 0.15f;
    }

    // Particle GPU budgets (must be large enough to avoid drops; impacts memory)
    internal static class ParticleBudgets
    {
        internal const int Shell = 50_000;
        internal const int Spark = 2_500_000;
        internal const int Smoke = 100_000;
        internal const int Crackle = 500_000;
        internal const int PopFlash = 50_000;
        internal const int FinaleSpark = 800_000;
    }

    internal static void Validate()
    {
        if (DefaultTimeScale < 0)
            throw new InvalidOperationException($"{nameof(DefaultTimeScale)} must be >= 0.");

        if (ShellDragK < 0)
            throw new InvalidOperationException($"{nameof(ShellDragK)} must be >= 0.");

        if (Emission.ChrysanthemumSpokeCount <= 0)
            throw new InvalidOperationException($"{nameof(Emission.ChrysanthemumSpokeCount)} must be > 0.");

        if (Emission.ChrysanthemumSpokeJitter < 0)
            throw new InvalidOperationException($"{nameof(Emission.ChrysanthemumSpokeJitter)} must be >= 0.");

        if (Emission.WillowDownwardBlend is < 0 or > 1)
            throw new InvalidOperationException($"{nameof(Emission.WillowDownwardBlend)} must be in [0,1].");

        if (Emission.PalmFrondCount <= 0)
            throw new InvalidOperationException($"{nameof(Emission.PalmFrondCount)} must be > 0.");

        if (Emission.PalmFrondConeAngleRadians <= 0)
            throw new InvalidOperationException($"{nameof(Emission.PalmFrondConeAngleRadians)} must be > 0.");

        if (Emission.PalmFrondJitterAngleRadians < 0)
            throw new InvalidOperationException($"{nameof(Emission.PalmFrondJitterAngleRadians)} must be >= 0.");

        if (Emission.HorsetailDownwardBlend is < 0 or > 1)
            throw new InvalidOperationException($"{nameof(Emission.HorsetailDownwardBlend)} must be in [0,1].");

        if (Emission.HorsetailMinDownDot is < -1 or > 1)
            throw new InvalidOperationException($"{nameof(Emission.HorsetailMinDownDot)} must be in [-1,1].");

        if (Emission.HorsetailJitterAngleRadians < 0)
            throw new InvalidOperationException($"{nameof(Emission.HorsetailJitterAngleRadians)} must be >= 0.");

        if (ParticleBudgets.Shell <= 0)
            throw new InvalidOperationException($"{nameof(ParticleBudgets.Shell)} must be > 0.");

        if (ParticleBudgets.Spark <= 0)
            throw new InvalidOperationException($"{nameof(ParticleBudgets.Spark)} must be > 0.");

        if (ParticleBudgets.Smoke <= 0)
            throw new InvalidOperationException($"{nameof(ParticleBudgets.Smoke)} must be > 0.");

        if (ParticleBudgets.Crackle <= 0)
            throw new InvalidOperationException($"{nameof(ParticleBudgets.Crackle)} must be > 0.");

        if (ParticleBudgets.PopFlash <= 0)
            throw new InvalidOperationException($"{nameof(ParticleBudgets.PopFlash)} must be > 0.");

        if (ParticleBudgets.FinaleSpark <= 0)
            throw new InvalidOperationException($"{nameof(ParticleBudgets.FinaleSpark)} must be > 0.");
    }
}
