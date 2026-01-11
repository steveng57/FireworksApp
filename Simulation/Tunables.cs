using System;

namespace FireworksApp.Simulation;

internal static class Tunables
{
    // Simulation pacing
    internal const float DefaultTimeScale = 0.55f;

    // Physics integration
    internal const float ShellDragK = 0.020f;

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
