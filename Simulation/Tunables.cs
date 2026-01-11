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

    // Particle upload tuning
    internal static class ParticleUpload
    {
        // When true, stage particle data into a DEFAULT buffer via UpdateSubresource.
        // This avoids D3D11 Map stalls on some drivers at the cost of a GPU-side copy.
        internal const bool UseUpdateSubresource = false;

        // Ring size for dynamic upload buffers used to stage updates into the main GPU particle buffer.
        // Larger values reduce the chance of CPU stalls caused by reusing a buffer the GPU is still reading.
        internal const int UploadRingSize = 128;

        // Element capacity per upload buffer (in particles). This is the max chunk size for a single Map/Copy.
        internal const int UploadChunkElements = 32_768;

        // Upload budget queue
        // When enabled, particle spawns are queued and drained during Render up to a fixed per-frame budget.
        // This smooths frame time by preventing large single-frame uploads that can trigger D3D11 Map stalls.
        internal const bool UseUploadBudgetQueue = true;

        // Max number of particles uploaded per frame from queued spawns.
        internal const int MaxParticlesUploadedPerFrame = 65_536;

        // Max queued particles per kind (overflow will drop least-important kinds first).
        internal const int MaxQueuedParticlesPerKind = 250_000;
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

        if (ParticleUpload.UploadRingSize <= 0)
            throw new InvalidOperationException($"{nameof(ParticleUpload.UploadRingSize)} must be > 0.");

        if (ParticleUpload.UploadChunkElements <= 0)
            throw new InvalidOperationException($"{nameof(ParticleUpload.UploadChunkElements)} must be > 0.");

        if (ParticleUpload.MaxParticlesUploadedPerFrame <= 0)
            throw new InvalidOperationException($"{nameof(ParticleUpload.MaxParticlesUploadedPerFrame)} must be > 0.");

        if (ParticleUpload.MaxQueuedParticlesPerKind <= 0)
            throw new InvalidOperationException($"{nameof(ParticleUpload.MaxQueuedParticlesPerKind)} must be > 0.");
    }
}
