using System;

namespace FireworksApp.Simulation;

internal static class Tunables
{
    // Simulation pacing
    internal const float DefaultTimeScale = 0.55f;

    // Physics integration
    internal const float ShellDragK = 0.020f;

    // Smoke tuning
    internal const float SmokeLifetimeMinSeconds = 1.0f;
    internal const float SmokeLifetimeMaxSeconds = 5.0f;
    internal const float SmokeFadeInFraction = 0.20f;
    internal const float SmokeFadeOutStartFraction = 0.60f;

    // Particle GPU budgets (must be large enough to avoid drops; impacts memory)
    internal static class ParticleBudgets
    {
        internal const int Shell = 50_000;
        internal const int Spark = 2_000_000;
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

        if (SmokeLifetimeMinSeconds < 0)
            throw new InvalidOperationException($"{nameof(SmokeLifetimeMinSeconds)} must be >= 0.");

        if (SmokeLifetimeMaxSeconds < 0)
            throw new InvalidOperationException($"{nameof(SmokeLifetimeMaxSeconds)} must be >= 0.");

        if (SmokeLifetimeMaxSeconds < SmokeLifetimeMinSeconds)
            throw new InvalidOperationException($"{nameof(SmokeLifetimeMaxSeconds)} must be >= {nameof(SmokeLifetimeMinSeconds)}.");

        if (SmokeFadeInFraction is < 0.0f or > 1.0f)
            throw new InvalidOperationException($"{nameof(SmokeFadeInFraction)} must be in [0,1].");

        if (SmokeFadeOutStartFraction is < 0.0f or > 1.0f)
            throw new InvalidOperationException($"{nameof(SmokeFadeOutStartFraction)} must be in [0,1].");

        if (SmokeFadeOutStartFraction < SmokeFadeInFraction)
            throw new InvalidOperationException($"{nameof(SmokeFadeOutStartFraction)} must be >= {nameof(SmokeFadeInFraction)}.");

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
