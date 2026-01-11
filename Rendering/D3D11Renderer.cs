using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Buffers;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using System.Windows;

// Pipelines moved under `Rendering/Pipelines` (same namespace).

namespace FireworksApp.Rendering;

using FireworksApp.Simulation;

// Simple vertex for the launch pad (just a position in 3D).
[StructLayout(LayoutKind.Sequential)]
public struct PadVertex
{
    public Vector3 Position;
    public Vector4 Color;

    public PadVertex(float x, float y, float z)
    {
        Position = new Vector3(x, y, z);
        Color = Vector4.One;
    }

    public PadVertex(float x, float y, float z, Vector4 color)
    {
        Position = new Vector3(x, y, z);
        Color = color;
    }

// ...existing code...

    public int ShellSpawnCount { get; set; }
}

public sealed class D3D11Renderer : IDisposable
{
    private readonly nint _hwnd;

    private readonly DeviceResources _deviceResources;

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain? _swapChain;
    private ID3D11RenderTargetView? _rtv;
    private ID3D11Texture2D? _depthTex;
    private ID3D11DepthStencilView? _dsv;
    private ID3D11DepthStencilState? _depthStencilState;

    private int _width = 1;
    private int _height = 1;

    // Launch pad pipeline
    private readonly PadPipeline _padPipeline = new();
    private ID3D11RasterizerState? _padRasterizerState;

    // Ground pipeline
    private readonly GroundPipeline _groundPipeline = new();
    private ID3D11Buffer? _sceneCB;
    private ID3D11Buffer? _lightingCB;
    private ID3D11Buffer? _objectCB;

    // Canister pipeline
    private readonly CanisterPipeline _canisterPipeline = new();

    // Shell (firework) pipeline (reuse simple lit shader for now)
    private readonly ShellPipeline _shellPipeline = new();

    private System.Collections.Generic.IReadOnlyList<CanisterRenderState> _canisters = Array.Empty<CanisterRenderState>();

    private System.Collections.Generic.IReadOnlyList<ShellRenderState> _shells = Array.Empty<ShellRenderState>();

    private System.Collections.Generic.IReadOnlyList<CanisterRenderState> _prevCanisters = Array.Empty<CanisterRenderState>();
    private System.Collections.Generic.IReadOnlyList<ShellRenderState> _prevShells = Array.Empty<ShellRenderState>();
    private long _lastTick;
    private readonly System.Random _rng = new();

    private const int UnitVectorTableSize = 4096;
    private static readonly Vector3[] s_unitVectorTable = CreateUnitVectorTable();

    // Shell trail tuning
    private static int ShellTrailParticlesPerSecond = 220;
    private static float ShellTrailLifetimeSeconds = 0.37f;
    private static float ShellTrailSpeed = 0.23f;

    // GPU particles (sparks)
    private readonly ParticlesPipeline _particlesPipeline = new();
    // Particle capacity must be at least the sum of all per-kind budgets (currently 2.3M).
    // We use the calculated total to ensure the backing buffer never overwrites live particles.
    private readonly int _particleCapacity = ParticleKindBudget.GetTotalCapacity();
    private int _particleWriteCursor;

    private readonly PerfTelemetry _perf = new();

    private static readonly ArrayPool<GpuParticle> s_particleArrayPool = ArrayPool<GpuParticle>.Shared;

    private Matrix4x4 _view;
    private Matrix4x4 _proj;

    public int ShellSpawnCount { get; set; }

    private static GpuParticle[] RentParticleArray(int count)
        => count <= 0 ? Array.Empty<GpuParticle>() : s_particleArrayPool.Rent(count);

    private static void ReturnParticleArray(GpuParticle[]? buffer)
    {
        if (buffer is null || buffer.Length == 0)
            return;

        s_particleArrayPool.Return(buffer, clearArray: false);
    }

    public readonly record struct ShellRenderState(Vector3 Position);

    public readonly record struct CanisterRenderState(Vector3 Position, Vector3 Direction);

    // Shell simulation moved to Simulation layer; renderer only needs positions.

    private Vector3 _schemeTint = Vector3.One;

    // Simple orbit camera (around origin)
    private float _cameraYaw = 0.0f;
    private float _cameraPitch = 0.15f;
    private float _cameraDistance = 200.0f;
    private bool _cameraDirty = true;

    private Vector3 _cameraTarget = new(0.0f, 80.0f, 0.0f);
    
    // Smoothed camera state for nicer motion
    private Vector3 _cameraTargetSmoothed = Vector3.Zero;
    private float _cameraDistanceSmoothed = 200.0f;

    public Vector3 CameraPosition { get; private set; }
    

    public D3D11Renderer(nint hwnd)
    {
        _hwnd = hwnd;
        _deviceResources = new DeviceResources(hwnd);
    }

    private static Vector3[] CreateUnitVectorTable()
    {
        // Deterministic table: removes trig/sqrt from hot loops while keeping a good spread.
        // Use a fixed seed so baseline visuals are stable across runs.
        var rng = new System.Random(1234567);
        var table = new Vector3[UnitVectorTableSize];
        for (int i = 0; i < table.Length; i++)
        {
            float z = (float)(rng.NextDouble() * 2.0 - 1.0);
            float a = (float)(rng.NextDouble() * System.Math.PI * 2.0);
            float r = (float)System.Math.Sqrt(System.Math.Max(0.0, 1.0 - z * z));
            table[i] = new Vector3(r * (float)System.Math.Cos(a), z, r * (float)System.Math.Sin(a));
        }
        return table;
    }

    public void Initialize(int width, int height)
    {
        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);

        _width = width;
        _height = height;

        _deviceResources.CreateDeviceAndSwapChain(width, height);
        _deviceResources.CreateRenderTarget();
        _deviceResources.CreateDepthStencil(width, height);

        _device = _deviceResources.Device;
        _context = _deviceResources.Context;
        _swapChain = _deviceResources.SwapChain;
        _rtv = _deviceResources.RenderTargetView;
        _depthTex = _deviceResources.DepthTexture;
        _dsv = _deviceResources.DepthStencilView;

        CreateDepthStencilState();
        SetViewport(width, height);
        LoadPadShadersAndGeometry();
        LoadGroundShadersAndGeometry();
        LoadCanisterShadersAndGeometry();
        _shellPipeline.Initialize(_device!);
        CreateSceneConstants();
        UpdateSceneConstants(0.0f);

        CreateParticleSystem();

        _lastTick = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    public void Resize(int width, int height)
    {
        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);

        _width = width;
        _height = height;

        _deviceResources.Resize(width, height);

        _device = _deviceResources.Device;
        _context = _deviceResources.Context;
        _swapChain = _deviceResources.SwapChain;
        _rtv = _deviceResources.RenderTargetView;
        _depthTex = _deviceResources.DepthTexture;
        _dsv = _deviceResources.DepthStencilView;

        SetViewport(width, height);
        CreateDepthStencilState();
        CreateSceneConstants();
        UpdateSceneConstants(0.0f);

        CreateParticleSystem();
    }

    public void OnMouseDrag(float deltaX, float deltaY)
    {
        // radians per pixel (tune later)
        const float sensitivity = 0.01f;
        _cameraYaw += deltaX * sensitivity;
        _cameraPitch += deltaY * sensitivity;

        // clamp pitch to avoid flipping
        _cameraPitch = System.Math.Clamp(_cameraPitch, -1.2f, 1.2f);
        _cameraDirty = true;
    }

    public void OnMouseWheel(float delta)
    {
        // WPF delta is typically 120 per notch
        _cameraDistance *= (float)System.Math.Pow(0.9, delta / 120.0);
        _cameraDistance = System.Math.Clamp(_cameraDistance, 5.0f, 450.0f);
        _cameraDirty = true;
    }

    public void PanCamera(float deltaX, float deltaY)
    {
        // World-units per pixel (tune later). Scale by distance so panning feels consistent.
        float k = 0.0025f * _cameraDistance;

        // Right and up vectors from current view matrix (world-space).
        // view is orthonormal: right = (M11,M21,M31), up=(M12,M22,M32)
        var right = new Vector3(_view.M11, _view.M21, _view.M31);
        var up = new Vector3(_view.M12, _view.M22, _view.M32);

        _cameraTarget += (-right * deltaX + up * deltaY) * k;
        _cameraDirty = true;
    }

    public void Render(float scaledDt)
    {
        if (_context is null || _rtv is null || _swapChain is null)
            return;

        if (scaledDt < 0.0f)
            scaledDt = 0.0f;
        if (scaledDt > 0.05f)
            scaledDt = 0.05f;

        UpdateParticles(scaledDt);

        // Always update camera + scene constants; smoothing uses scaledDt.
        UpdateSceneConstants(scaledDt);

        // Explicit per-frame state (avoid state leakage as the frame grows)
        _context.RSSetViewport(new Viewport(0, 0, _width, _height, 0.0f, 1.0f));


        if (_cameraDirty)
        {
            UpdateSceneConstants(0.0f);
            _cameraDirty = false;
        }

        // Explicit per-frame state (avoid state leakage as the frame grows)
        _context.RSSetViewport(new Viewport(0, 0, _width, _height, 0.0f, 1.0f));

        if (_padRasterizerState != null)
        {
            _context.RSSetState(_padRasterizerState);
        }

        if (_depthStencilState != null)
        {
            _context.OMSetDepthStencilState(_depthStencilState, 0);
        }

        // Clear background (night)
        _context.OMSetRenderTargets(_rtv, _dsv);
        _context.ClearRenderTargetView(_rtv, new Color4(0.02f, 0.02f, 0.05f, 1.0f));
        if (_dsv != null)
        {
            _context.ClearDepthStencilView(_dsv, DepthStencilClearFlags.Depth, 1.0f, 0);
        }

        DrawGround();

        // Draw the simple launch pad
        DrawLaunchPad();

        DrawCanisters();

        DrawShells();

        DrawParticles(additive: true);
        DrawParticles(additive: false);

        // Present
        _swapChain.Present(1, PresentFlags.None);

        _perf.Tick("Render", appendDetails: () =>
        {
            var counters = _particlesPipeline.GetPerfCountersSummary();
            if (!string.IsNullOrEmpty(counters))
                System.Diagnostics.Debug.Write($" kinds[{counters}]");
        });
    }

    public void SetShells(System.Collections.Generic.IReadOnlyList<ShellRenderState> shells)
    {
        _prevShells = _shells;
        _shells = shells ?? Array.Empty<ShellRenderState>();
    }

    public void SetCanisters(System.Collections.Generic.IReadOnlyList<CanisterRenderState> canisters)
    {
        _prevCanisters = _canisters;
        _canisters = canisters ?? Array.Empty<CanisterRenderState>();
    }

    public void ApplyInterpolation(float alpha)
    {
        alpha = System.Math.Clamp(alpha, 0.0f, 1.0f);

        if (alpha <= 0.0f)
            return;

        if (_prevShells.Count == 0 || _prevCanisters.Count == 0)
            return;

        if (_prevShells.Count != _shells.Count || _prevCanisters.Count != _canisters.Count)
            return;

        var shells = new ShellRenderState[_shells.Count];
        for (int i = 0; i < shells.Length; i++)
        {
            var a = _prevShells[i].Position;
            var b = _shells[i].Position;
            shells[i] = new ShellRenderState(Vector3.Lerp(a, b, alpha));
        }
        _shells = shells;

        var canisters = new CanisterRenderState[_canisters.Count];
        for (int i = 0; i < canisters.Length; i++)
        {
            var aPos = _prevCanisters[i].Position;
            var bPos = _canisters[i].Position;
            canisters[i] = new CanisterRenderState(Vector3.Lerp(aPos, bPos, alpha), _canisters[i].Direction);
        }
        _canisters = canisters;
    }

    private static uint PackFloat(float value)
        => (uint)BitConverter.SingleToInt32Bits(value);

    public void SpawnBurst(Vector3 position, Vector4 baseColor, int count, float sparkleRateHz = 0.0f, float sparkleIntensity = 0.0f)
    {
        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (_context is null || particleBuffer is null || uploadBuffer is null)
            return;

        count = System.Math.Clamp(count, 1, _particleCapacity);

        int stride = Marshal.SizeOf<GpuParticle>();
        int start = _particleWriteCursor;

        var staging = RentParticleArray(count);
        try
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = RandomUnitVector();
                float u = (float)_rng.NextDouble();
                float speed = 6.0f + (u * u) * 12.0f;
                Vector3 vel = dir * speed;

                bool crackle = (_rng.NextDouble() < 0.22);

                float lifetime;
                if (crackle)
                {
                    // 30–90ms per micro-spark
                    lifetime = 0.03f + (float)_rng.NextDouble() * 0.06f;
                }
                else
                {
                    lifetime = 2.0f + (float)_rng.NextDouble() * 2.0f;
                }

                staging[i] = new GpuParticle
                {
                    Position = position,
                    Velocity = vel,
                    Age = 0.0f,
                    Lifetime = lifetime,
                    BaseColor = baseColor,
                    Color = baseColor,
                    Kind = crackle ? (uint)ParticleKind.Crackle : (uint)ParticleKind.Spark,
                    // For sparks, pack sparkle params (rate/intensity) so only burst particles twinkle.
                    // For crackle, keep pads as deterministic randomness.
                    _pad0 = crackle ? (uint)_rng.Next() : PackFloat(sparkleRateHz),
                    _pad1 = crackle ? (uint)_rng.Next() : PackFloat(sparkleIntensity),
                    _pad2 = (uint)_rng.Next()
                };
            }

            WriteParticlesToBuffer(staging, start, count, particleBuffer, uploadBuffer);
        }
        finally
        {
            ReturnParticleArray(staging);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    public void SpawnPopFlash(Vector3 position, float lifetimeSeconds, float size, float peakIntensity, float fadeGamma)
    {
        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (_context is null || particleBuffer is null || uploadBuffer is null)
            return;

        int stride = Marshal.SizeOf<GpuParticle>();
        int start = _particleWriteCursor;

        var p = new GpuParticle
        {
            Position = position,
            Velocity = Vector3.Zero,
            Age = 0.0f,
            Lifetime = System.Math.Max(0.01f, lifetimeSeconds),
            BaseColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            Kind = (uint)ParticleKind.PopFlash,
            _pad0 = PackFloat(System.Math.Max(0.0f, size)),
            _pad1 = PackFloat(System.Math.Max(0.0f, peakIntensity)),
            _pad2 = PackFloat(System.Math.Max(0.0f, fadeGamma))
        };

        var mapped = _context.Map(uploadBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            unsafe
            {
                *(GpuParticle*)mapped.DataPointer = p;
            }
        }
        finally
        {
            _context.Unmap(uploadBuffer, 0);
        }

        var srcBox = new Box
        {
            Left = 0,
            Right = stride,
            Top = 0,
            Bottom = 1,
            Front = 0,
            Back = 1
        };

        _context.CopySubresourceRegion(particleBuffer, 0, (uint)(start * stride), 0, 0, uploadBuffer, 0, srcBox);

        _particleWriteCursor = (start + 1) % _particleCapacity;
    }

    public void SpawnFinaleSaluteSparks(
        Vector3 position,
        int particleCount,
        float baseSpeed,
        float speedJitterFrac,
        float particleLifetimeMinSeconds,
        float particleLifetimeMaxSeconds,
        float sparkleRateHzMin,
        float sparkleRateHzMax,
        float sparkleIntensity,
        float microFragmentChance,
        float microLifetimeMinSeconds,
        float microLifetimeMaxSeconds,
        float microSpeedMulMin,
        float microSpeedMulMax)
    {
        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (_context is null || particleBuffer is null || uploadBuffer is null)
            return;

        int count = System.Math.Clamp(particleCount, 1, _particleCapacity);

        int stride = Marshal.SizeOf<GpuParticle>();
        int start = _particleWriteCursor;

        // We may add micros; keep within particle budget.
        int maxExtra = (int)System.MathF.Min(_particleCapacity - count, count * 0.9f);
        int cap = System.Math.Clamp(count + maxExtra, 1, _particleCapacity);
        var staging = RentParticleArray(cap);
        int produced = 0;

        // Tight, white/silver base. Alpha is an *intensity* factor in this renderer.
        Vector4 baseColor = new(1.0f, 1.0f, 1.0f, 1.0f);

        float sj = System.Math.Clamp(speedJitterFrac, 0.0f, 0.65f);
        float lifeMin = System.Math.Max(0.02f, particleLifetimeMinSeconds);
        float lifeMax = System.Math.Max(lifeMin, particleLifetimeMaxSeconds);

        float microChance = System.Math.Clamp(microFragmentChance, 0.0f, 1.0f);
        float microLifeMin = System.Math.Max(0.02f, microLifetimeMinSeconds);
        float microLifeMax = System.Math.Max(microLifeMin, microLifetimeMaxSeconds);

        float rateMin = System.Math.Max(0.0f, sparkleRateHzMin);
        float rateMax = System.Math.Max(rateMin, sparkleRateHzMax);
        float inten = System.Math.Max(0.0f, sparkleIntensity);

        try
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = RandomUnitVector();

                // Jitter direction so it doesn't read as a perfect sphere.
                // (Jitter affects direction only; flicker affects brightness only.)
                float jx = ((float)_rng.NextDouble() * 2.0f - 1.0f) * sj;
                float jy = ((float)_rng.NextDouble() * 2.0f - 1.0f) * sj;
                float jz = ((float)_rng.NextDouble() * 2.0f - 1.0f) * sj;
                dir = Vector3.Normalize(dir + new Vector3(jx, jy, jz));

                // Slightly bias inward to make it feel tighter (but do NOT increase radius).
                float tightMul = 0.85f + 0.10f * (float)_rng.NextDouble();
                float speed = System.Math.Max(0.0f, baseSpeed) * tightMul;

                float lifeU = (float)_rng.NextDouble();
                float lifetime = lifeMin + lifeU * (lifeMax - lifeMin);

                float rateU = (float)_rng.NextDouble();
                float rateHz = rateMin + rateU * (rateMax - rateMin);

                // Encode twinkle params into pad fields used in shader.
                if (produced < staging.Length)
                {
                    staging[produced++] = new GpuParticle
                    {
                        Position = position,
                        Velocity = dir * speed,
                        Age = 0.0f,
                        Lifetime = lifetime,
                        BaseColor = baseColor,
                        Color = baseColor,
                        Kind = (uint)ParticleKind.FinaleSpark,
                        _pad0 = PackFloat(rateHz),
                        _pad1 = PackFloat(inten),
                        _pad2 = (uint)_rng.Next()
                    };
                }

                // Optional micro-fragments: 20-30% chance, 1-3 fragments.
                if (produced < _particleCapacity && _rng.NextDouble() < microChance)
                {
                    int fragCount = _rng.Next(1, 4);
                    for (int k = 0; k < fragCount && produced < staging.Length && produced < _particleCapacity; k++)
                    {
                        float dev = 0.10f + 0.12f * (float)_rng.NextDouble();
                        Vector3 microDir = Vector3.Normalize(dir + RandomUnitVector() * dev);

                        float mLifeU = (float)_rng.NextDouble();
                        float mLifetime = microLifeMin + mLifeU * (microLifeMax - microLifeMin);

                        float mSpeedMul = microSpeedMulMin + (float)_rng.NextDouble() * (microSpeedMulMax - microSpeedMulMin);
                        float mSpeed = speed * System.Math.Max(0.0f, mSpeedMul);

                        // More aggressive flicker for micro-frags: bump rate and intensity.
                        float microRate = rateHz * (1.4f + 0.8f * (float)_rng.NextDouble());
                        float microInt = inten * 1.35f;

                        // Stagger their start time so they appear mid-flight.
                        float startDelay = 0.12f + 0.30f * (float)_rng.NextDouble();

                        staging[produced++] = new GpuParticle
                        {
                            Position = position,
                            Velocity = microDir * mSpeed,
                            Age = -startDelay,
                            Lifetime = mLifetime,
                            BaseColor = baseColor,
                            Color = baseColor,
                            Kind = (uint)ParticleKind.FinaleSpark,
                            _pad0 = PackFloat(microRate),
                            _pad1 = PackFloat(microInt),
                            _pad2 = (uint)_rng.Next()
                        };
                    }
                }
            }

            int total = System.Math.Clamp(produced, 1, _particleCapacity);

            WriteParticlesToBuffer(staging, start, total, particleBuffer, uploadBuffer);

            _particleWriteCursor = (start + total) % _particleCapacity;
        }
        finally
        {
            ReturnParticleArray(staging);
        }

    }

    public void SpawnGroundEffectDirected(Vector3 position, Vector4 baseColor, float speed, System.ReadOnlySpan<Vector3> directions, float particleLifetimeSeconds, float gravityFactor)
    {
        // Current GPU shader uses a single global gravity; we keep gravityFactor for forward compatibility.
        // For now, modulate speed slightly to approximate different “weight”.
        float g = System.Math.Clamp(gravityFactor, 0.0f, 2.0f);
        float speedMul = 1.0f - 0.15f * (g - 1.0f);
        SpawnBurstDirected(position, baseColor, speed * speedMul, directions, particleLifetimeSeconds);
    }

    public void SpawnBurstDirectedExplode(
        Vector3 position,
        Vector4 baseColor,
        float speed,
        System.ReadOnlySpan<Vector3> directions,
        float particleLifetimeSeconds,
        float sparkleRateHz = 0.0f,
        float sparkleIntensity = 0.0f)
    {
        _schemeTint = new Vector3(baseColor.X, baseColor.Y, baseColor.Z);
        SpawnBurstDirected(position, baseColor, speed, directions, particleLifetimeSeconds, sparkleRateHz, sparkleIntensity);
    }

    public void SpawnSmoke(Vector3 burstCenter)
    {
        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (_context is null || particleBuffer is null || uploadBuffer is null)
            return;

        int count = _rng.Next(200, 601);
        count = System.Math.Clamp(count, 1, _particleCapacity);

        int start = _particleWriteCursor;

        var staging = RentParticleArray(count);

        // Start color is overwritten in the compute shader per life; keep alpha at 1 here.
        Vector4 startColor = new(0.35f, 0.33f, 0.30f, 1.0f);
        try
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = RandomUnitVector();
                Vector3 pos = burstCenter + dir * 0.5f;

                float outwardSpeed = 1.5f + (float)_rng.NextDouble() * 1.5f;
                Vector3 outward = dir * outwardSpeed;

                float upSpeed = 1.5f + (float)_rng.NextDouble() * 2.0f;
                Vector3 up = new(0.0f, upSpeed, 0.0f);

                Vector3 vel = outward * 0.7f + up;

                float minLife = System.Math.Max(0.0f, Tunables.SmokeLifetimeMinSeconds);
                float maxLife = System.Math.Max(minLife, Tunables.SmokeLifetimeMaxSeconds);
                float lifetime = minLife + (float)_rng.NextDouble() * (maxLife - minLife);

                staging[i] = new GpuParticle
                {
                    Position = pos,
                    Velocity = vel,
                    Age = 0.0f,
                    Lifetime = lifetime,
                    BaseColor = startColor,
                    Color = startColor,
                    Kind = (uint)ParticleKind.Smoke,
                    _pad0 = (uint)_rng.Next(),
                    _pad1 = (uint)_rng.Next(),
                    _pad2 = (uint)_rng.Next()
                };
            }

            WriteParticlesToBuffer(staging, start, count, particleBuffer, uploadBuffer);
        }
        finally
        {
            ReturnParticleArray(staging);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    public void SpawnBurstDirected(
        Vector3 position,
        Vector4 baseColor,
        float speed,
        System.ReadOnlySpan<Vector3> directions,
        float particleLifetimeSeconds,
        float sparkleRateHz = 0.0f,
        float sparkleIntensity = 0.0f)
    {
        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (_context is null || particleBuffer is null || uploadBuffer is null)
            return;

        int count = System.Math.Clamp(directions.Length, 1, _particleCapacity);

        int start = _particleWriteCursor;

        var staging = RentParticleArray(count);
        try
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = directions[i];
                if (dir.LengthSquared() < 1e-8f)
                    dir = Vector3.UnitY;
                else
                    dir = Vector3.Normalize(dir);

                // Match the original "nice" look: varied speeds and lifetimes, plus crackle.
                float u = (float)_rng.NextDouble();
                float speedMul = 0.65f + (u * u) * 0.85f;
                Vector3 vel = dir * (speed * speedMul);

                bool crackle = (_rng.NextDouble() < 0.22);

                float lifetime;
                if (crackle)
                {
                    lifetime = 0.03f + (float)_rng.NextDouble() * 0.06f;
                }
                else
                {
                    // Wider, more realistic distribution with a long tail so burst stars don't all die together.
                    // Keep respecting the requested base lifetime.
                    float r = (float)_rng.NextDouble();
                    float tail = r * r; // bias toward shorter, with a long tail
                    float lifeMul = 0.55f + 1.60f * tail; // ~0.55x..2.15x
                    lifetime = System.Math.Max(0.05f, particleLifetimeSeconds * lifeMul);
                }

                staging[i] = new GpuParticle
                {
                    Position = position,
                    Velocity = vel,
                    Age = 0.0f,
                    Lifetime = lifetime,
                    BaseColor = baseColor,
                    Color = baseColor,
                    Kind = crackle ? (uint)ParticleKind.Crackle : (uint)ParticleKind.Spark,
                    _pad0 = crackle ? (uint)_rng.Next() : PackFloat(sparkleRateHz),
                    _pad1 = crackle ? (uint)_rng.Next() : PackFloat(sparkleIntensity),
                    _pad2 = (uint)_rng.Next()
                };
            }

            WriteParticlesToBuffer(staging, start, count, particleBuffer, uploadBuffer);
        }
        finally
        {
            ReturnParticleArray(staging);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    

    private float GetDeltaTimeSeconds()
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        long prev = _lastTick;
        _lastTick = now;
        double dt = (now - prev) / (double)System.Diagnostics.Stopwatch.Frequency;
        if (dt < 0.0) dt = 0.0;
        if (dt > 0.05) dt = 0.05; // clamp to avoid big jumps
        return (float)dt;
    }



    private void SpawnShellTrail(Vector3 position, Vector3 velocity, int count)
    {
        var particleBuffer = _particlesPipeline.ParticleBuffer;
        var uploadBuffer = _particlesPipeline.GetNextUploadBuffer();
        if (_context is null || particleBuffer is null || uploadBuffer is null)
            return;

        count = System.Math.Clamp(count, 1, _particleCapacity);

        // Trail is a slightly warm white and additive blended in the first particle pass.
        Vector4 color = new(1.0f, 0.92f, 0.55f, 1.0f);

        int start = _particleWriteCursor;

        var staging = RentParticleArray(count);

        Vector3 dir = velocity.LengthSquared() > 1e-6f ? Vector3.Normalize(velocity) : Vector3.UnitY;
        Vector3 back = -dir;
        Vector3 right = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
        if (right.LengthSquared() < 1e-6f)
            right = Vector3.UnitX;
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, dir));

        try
        {
            for (int i = 0; i < count; i++)
            {
                float jitterR = ((float)_rng.NextDouble() * 2.0f - 1.0f) * 0.05f;
                float jitterU = ((float)_rng.NextDouble() * 2.0f - 1.0f) * 0.05f;
                float alongBack = ((float)_rng.NextDouble()) * 0.12f;

                Vector3 pos = position + (back * alongBack) + (right * jitterR) + (up * jitterU);

                // Slight backward drift to form a continuous streak.
                Vector3 vel = (back * ShellTrailSpeed) + (right * jitterR * 0.5f) + (up * jitterU * 0.5f);

                staging[i] = new GpuParticle
                {
                    Position = pos,
                    Velocity = vel,
                    Age = 0.0f,
                    Lifetime = ShellTrailLifetimeSeconds,
                    Color = color,
                    Kind = (uint)ParticleKind.Spark
                };
            }

            WriteParticlesToBuffer(staging, start, count, particleBuffer, uploadBuffer);
        }
        finally
        {
            ReturnParticleArray(staging);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    private Vector3 RandomUnitVector()
    {
        // Lookup-based: avoids trig/sqrt in hot spawn loops.
        return s_unitVectorTable[_rng.Next(UnitVectorTableSize)];
    }

    private void WriteParticlesToBuffer(GpuParticle[] staging, int start, int count, ID3D11Buffer particleBuffer, ID3D11Buffer uploadBuffer)
    {
        if (_context is null)
            return;

        int stride = Marshal.SizeOf<GpuParticle>();

        static double ToMilliseconds(long start, long end)
            => (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        int uploadCap = _particlesPipeline.UploadBufferElementCapacity;
        if (uploadCap <= 0)
            uploadCap = 1;

        int producedOffset = 0;
        int remainingToUpload = count;

        while (remainingToUpload > 0)
        {
            int chunkCount = System.Math.Min(remainingToUpload, uploadCap);
            int chunkStart = (start + producedOffset) % _particleCapacity;

            // Each chunk uses its own staging buffer from the ring.
            var chunkUploadBuffer = _particlesPipeline.GetNextUploadBuffer();
            if (chunkUploadBuffer is null)
                return;

            // If wrapping occurs, split this chunk into two segments.
            int firstCount = System.Math.Min(chunkCount, _particleCapacity - chunkStart);
            int secondCount = chunkCount - firstCount;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();

            var mapped = _context.Map(chunkUploadBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                nint basePtr = mapped.DataPointer;
                unsafe
                {
                    if (firstCount > 0)
                    {
                        fixed (GpuParticle* srcPtr = &staging[producedOffset])
                        {
                            Buffer.MemoryCopy(srcPtr, (void*)basePtr, firstCount * stride, firstCount * stride);
                        }
                    }

                    if (secondCount > 0)
                    {
                        fixed (GpuParticle* srcPtr = &staging[producedOffset + firstCount])
                        {
                            nint dst = basePtr + (firstCount * stride);
                            Buffer.MemoryCopy(srcPtr, (void*)dst, secondCount * stride, secondCount * stride);
                        }
                    }
                }
            }
            finally
            {
                _context.Unmap(chunkUploadBuffer, 0);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                _perf.RecordUpload(ToMilliseconds(t0, t1), checked(chunkCount * stride));
            }

            if (firstCount > 0)
            {
                var srcBox = new Box
                {
                    Left = 0,
                    Right = (int)(firstCount * stride),
                    Top = 0,
                    Bottom = 1,
                    Front = 0,
                    Back = 1
                };

                _context.CopySubresourceRegion(particleBuffer, 0, (uint)(chunkStart * stride), 0, 0, chunkUploadBuffer, 0, srcBox);
            }

            if (secondCount > 0)
            {
                var srcBox = new Box
                {
                    Left = (int)(firstCount * stride),
                    Right = (int)(chunkCount * stride),
                    Top = 0,
                    Bottom = 1,
                    Front = 0,
                    Back = 1
                };

                _context.CopySubresourceRegion(particleBuffer, 0, 0, 0, 0, chunkUploadBuffer, 0, srcBox);
            }

            producedOffset += chunkCount;
            remainingToUpload -= chunkCount;
        }
    }

    private void WriteParticlesToBuffer(List<GpuParticle> staging, int start, int count, ID3D11Buffer particleBuffer, ID3D11Buffer uploadBuffer)
    {
        if (_context is null)
            return;

        int stride = Marshal.SizeOf<GpuParticle>();

        static double ToMilliseconds(long start, long end)
            => (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        int firstCount = System.Math.Min(count, _particleCapacity - start);
        int remaining = count - firstCount;

        int uploadCap = _particlesPipeline.UploadBufferElementCapacity;
        if (uploadCap <= 0)
            uploadCap = 1;

        // If the list upload is larger than the upload buffer capacity, fall back to chunked upload.
        if (count > uploadCap)
        {
            // Reuse the array chunk path to keep logic in one place.
            WriteParticlesToBuffer(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(staging).ToArray(), start, count, particleBuffer, uploadBuffer);
            return;
        }

        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var mapped = _context.Map(uploadBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            nint basePtr = mapped.DataPointer;

            var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(staging);
            unsafe
            {
                if (firstCount > 0)
                {
                    fixed (GpuParticle* srcPtr = &span[0])
                    {
                        // Upload contiguously at offset 0 in the upload buffer.
                        Buffer.MemoryCopy(srcPtr, (void*)basePtr, firstCount * stride, firstCount * stride);
                    }
                }

                if (remaining > 0)
                {
                    fixed (GpuParticle* srcPtr = &span[firstCount])
                    {
                        // Append remaining segment after the first segment.
                        nint dst = basePtr + (firstCount * stride);
                        Buffer.MemoryCopy(srcPtr, (void*)dst, remaining * stride, remaining * stride);
                    }
                }
            }
        }
        finally
        {
            _context.Unmap(uploadBuffer, 0);
            int bytes = checked(count * stride);
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            _perf.RecordUpload(ToMilliseconds(t0, t1), bytes);
        }

        if (firstCount > 0)
        {
            var srcBox = new Box
            {
                Left = 0,
                Right = (int)(firstCount * stride),
                Top = 0,
                Bottom = 1,
                Front = 0,
                Back = 1
            };

            _context.CopySubresourceRegion(particleBuffer, 0, (uint)(start * stride), 0, 0, uploadBuffer, 0, srcBox);
        }

        if (remaining > 0)
        {
            var srcBox = new Box
            {
                Left = (int)(firstCount * stride),
                Right = (int)((firstCount + remaining) * stride),
                Top = 0,
                Bottom = 1,
                Front = 0,
                Back = 1
            };

            _context.CopySubresourceRegion(particleBuffer, 0, 0, 0, 0, uploadBuffer, 0, srcBox);
        }
    }

    private void CreateParticleSystem()
    {
        if (_device is null)
            return;

        _particlesPipeline.Initialize(_device, _particleCapacity);
    }

    private void UpdateParticles(float scaledDt)
    {
        if (_context is null)
            return;

        _particlesPipeline.Update(_context, _view, _proj, _schemeTint, scaledDt);
    }

    private void DrawParticles(bool additive)
    {
        if (_context is null)
            return;

        _particlesPipeline.Draw(_context, _view, _proj, _schemeTint, _depthStencilState, additive);
    }

    private void DrawShells()
    {
        if (_context is null)
            return;

        _shellPipeline.Draw(_context, _shells, _view, _proj, _sceneCB, _objectCB);
    }

    private void LoadCanisterShadersAndGeometry()
    {
        if (_device is null)
            return;

        _canisterPipeline.Initialize(_device);
    }

    private void DrawCanisters()
    {
        if (_context is null)
            return;

        _canisterPipeline.Draw(
            _context,
            _canisters,
            _view,
            _proj,
            _sceneCB,
            _lightingCB,
            _objectCB);
    }


    private void CreateDepthStencilState()
    {
        if (_device is null)
            return;

        _depthStencilState?.Dispose();
        _depthStencilState = _device.CreateDepthStencilState(new DepthStencilDescription
        {
            DepthEnable = true,
            DepthWriteMask = DepthWriteMask.All,
            DepthFunc = ComparisonFunction.LessEqual,
            StencilEnable = false
        });
    }

    private void CreateSceneConstants()
    {
        if (_device is null)
            return;

        _sceneCB?.Dispose();
        _sceneCB = _device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ConstantBuffer,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            ByteWidth = (uint)Marshal.SizeOf<SceneCBData>()
        });

        _lightingCB?.Dispose();
        _lightingCB = _device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ConstantBuffer,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            ByteWidth = (uint)Marshal.SizeOf<LightingCBData>()
        });

        _objectCB?.Dispose();
        _objectCB = _device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ConstantBuffer,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            ByteWidth = (uint)Marshal.SizeOf<SceneCBData>()
        });
    }

    private void UpdateSceneConstants(float dt)
    {
        if (_context is null || _sceneCB is null)
            return;

        float aspect = _height > 0 ? (float)_width / _height : 1.0f;

        var up = Vector3.UnitY;

        // Smooth follow of target and distance so camera motion is less jerky.
        // When dt <= 0 (e.g., during initialization / resize) snap directly.
        if (dt <= 0.0f)
        {
            _cameraTargetSmoothed = _cameraTarget;
            _cameraDistanceSmoothed = _cameraDistance;
        }
        else
        {
            const float followSpeed = 5.0f;
            const float zoomSpeed = 5.0f;

            float followT = 1.0f - (float)System.Math.Exp(-followSpeed * dt);
            float zoomT = 1.0f - (float)System.Math.Exp(-zoomSpeed * dt);

            // Clamp interpolation factors into [0,1] in case of extreme dt values.
            if (followT < 0.0f) followT = 0.0f;
            if (followT > 1.0f) followT = 1.0f;
            if (zoomT < 0.0f) zoomT = 0.0f;
            if (zoomT > 1.0f) zoomT = 1.0f;

            _cameraTargetSmoothed = Vector3.Lerp(_cameraTargetSmoothed, _cameraTarget, followT);
            _cameraDistanceSmoothed = _cameraDistanceSmoothed + (_cameraDistance - _cameraDistanceSmoothed) * zoomT;
        }

        float cy = (float)System.Math.Cos(_cameraYaw);
        float sy = (float)System.Math.Sin(_cameraYaw);
        float cp = (float)System.Math.Cos(_cameraPitch);
        float sp = (float)System.Math.Sin(_cameraPitch);

        // Spherical coordinates: forward is +Z
        var eyeOffset = new Vector3(sy * cp, sp, cy * cp) * _cameraDistanceSmoothed;

        var target = _cameraTargetSmoothed;
        var eye = target + eyeOffset;

        CameraPosition = eye;

        _view = Matrix4x4.CreateLookAt(eye, target, up);
        // Use clip planes suitable for ~500m fireworks scenes.
        _proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(System.Math.PI / 4.0), aspect, 1.0f, 2000.0f);
        var world = Matrix4x4.Identity;

        // HLSL expects column-major by default; System.Numerics uses row-major.
        // Transpose so mul(position, WorldViewProjection) matches.
        var wvp = Matrix4x4.Transpose(world * _view * _proj);

        var scene = new SceneCBData
        {
            WorldViewProjection = wvp,
            World = Matrix4x4.Transpose(world)
        };

        var mapped = _context.Map(_sceneCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        unsafe
        {
            *(SceneCBData*)mapped.DataPointer = scene;
        }
        _context.Unmap(_sceneCB, 0);

        if (_lightingCB != null)
        {
            var light = new LightingCBData
            {
                LightDirectionWS = Vector3.Normalize(new Vector3(-0.3f, -1.0f, -0.2f)),
                LightColor = new Vector3(1.0f, 1.0f, 1.0f),
                AmbientColor = new Vector3(0.15f, 0.15f, 0.18f)
            };

            var mappedLight = _context.Map(_lightingCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            unsafe
            {
                *(LightingCBData*)mappedLight.DataPointer = light;
            }
            _context.Unmap(_lightingCB, 0);
        }
    }


    private void SetViewport(int width, int height)
    {
        if (_context is null)
            return;

        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);

        _context.RSSetViewport(new Viewport(0, 0, width, height, 0.0f, 1.0f));

        // aspect changed
        _cameraDirty = true;
    }

    private void LoadPadShadersAndGeometry()
    {
        if (_device is null)
            return;

        _padPipeline.Initialize(_device);
        _padRasterizerState = _padPipeline.RasterizerState;
    }

    private void LoadGroundShadersAndGeometry()
    {
        if (_device is null)
            return;

        _groundPipeline.Initialize(_device);
    }

    private void DrawGround()
    {
        if (_context is null)
            return;

        _groundPipeline.Draw(_context, _sceneCB, _lightingCB, _padRasterizerState);
    }

    private void DrawLaunchPad()
    {
        if (_context is null)
            return;

        _padPipeline.Draw(_context, _sceneCB);
    }

    public void Dispose()
    {
        _padPipeline.Dispose();
        _groundPipeline.Dispose();
        _canisterPipeline.Dispose();
        _shellPipeline.Dispose();

        _particlesPipeline.Dispose();

        _objectCB?.Dispose();
        _objectCB = null;
        _depthStencilState?.Dispose();
        _depthStencilState = null;

        

        _sceneCB?.Dispose();
        _sceneCB = null;

        _padRasterizerState = null;

        _deviceResources.Dispose();

        _rtv = null;
        _dsv = null;
        _depthTex = null;
        _swapChain = null;
        _context = null;
        _device = null;
    }
}
