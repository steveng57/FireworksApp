using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using System.Windows;

namespace FireworksApp.Rendering;

// Simple vertex for the launch pad (just a position in 3D).
[StructLayout(LayoutKind.Sequential)]
public struct PadVertex
{
    public Vector3 Position;

    public PadVertex(float x, float y, float z)
    {
        Position = new Vector3(x, y, z);
    }

// ...existing code...

    public int ShellSpawnCount { get; set; }
}

public sealed class D3D11Renderer : IDisposable
{
    private readonly nint _hwnd;

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
    private ID3D11VertexShader? _padVS;
    private ID3D11PixelShader? _padPS;
    private ID3D11InputLayout? _padInputLayout;
    private ID3D11Buffer? _padVB;
    private ID3D11RasterizerState? _padRasterizerState;

    // Ground pipeline
    private ID3D11VertexShader? _groundVS;
    private ID3D11PixelShader? _groundPS;
    private ID3D11InputLayout? _groundInputLayout;
    private ID3D11Buffer? _groundVB;
    private ID3D11Buffer? _sceneCB;
    private ID3D11Buffer? _lightingCB;
    private ID3D11Buffer? _objectCB;

    // Canister pipeline
    private ID3D11VertexShader? _canisterVS;
    private ID3D11PixelShader? _canisterPS;
    private ID3D11InputLayout? _canisterInputLayout;
    private ID3D11Buffer? _canisterVB;
    private int _canisterVertexCount;

    // Shell (firework) pipeline (reuse simple lit shader for now)
    private ID3D11VertexShader? _shellVS;
    private ID3D11PixelShader? _shellPS;
    private ID3D11InputLayout? _shellInputLayout;
    private ID3D11Buffer? _shellVB;
    private int _shellVertexCount;

    private System.Collections.Generic.IReadOnlyList<CanisterRenderState> _canisters = Array.Empty<CanisterRenderState>();

    private System.Collections.Generic.IReadOnlyList<ShellRenderState> _shells = Array.Empty<ShellRenderState>();
    private long _lastTick;
    private readonly System.Random _rng = new();

    // Shell trail tuning
    private static int ShellTrailParticlesPerSecond = 220;
    private static float ShellTrailLifetimeSeconds = 0.37f;
    private static float ShellTrailSpeed = 0.23f;

    // GPU particles (sparks)
    private ID3D11Buffer? _particleBuffer;
    private ID3D11Buffer? _particleUploadBuffer;
    private ID3D11ShaderResourceView? _particleSRV;
    private ID3D11UnorderedAccessView? _particleUAV;
    private ID3D11ComputeShader? _particlesCS;
    private ID3D11VertexShader? _particlesVS;
    private ID3D11PixelShader? _particlesPS;
    private ID3D11BlendState? _blendAdditive;
    private ID3D11BlendState? _blendAlpha;
    private ID3D11BlendState? _blendAlphaPremult;
    private ID3D11DepthStencilState? _depthReadNoWrite;
    private ID3D11Buffer? _frameCB;
    private int _particleCapacity = 2097152;
    private int _particleWriteCursor;

    private Matrix4x4 _view;
    private Matrix4x4 _proj;

    public int ShellSpawnCount { get; set; }

    public readonly record struct ShellRenderState(Vector3 Position);

    public readonly record struct CanisterRenderState(Vector3 Position, Vector3 Direction);

    // Shell simulation moved to Simulation layer; renderer only needs positions.

    private enum ParticleKind : uint
    {
        Dead = 0,
        Shell = 1,
        Spark = 2,
        Smoke = 3,
        Crackle = 4
    }

    // Smoke tuning
    private const float SmokeIntensity = 0.45f;

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuParticle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Age;
        public float Lifetime;
        public Vector4 Color;
        public uint Kind;
        public uint _pad0;
        public uint _pad1;
        public uint _pad2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameCBData
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

    private Vector3 _schemeTint = Vector3.One;

    // Crackling spark tuning (world meters / HDR-ish colors)
    private static readonly Vector3 CrackleBaseColor = new(2.2f, 2.0f, 1.6f);
    private static readonly Vector3 CracklePeakColor = new(3.5f, 3.2f, 2.4f);
    private static readonly Vector3 CrackleFadeColor = new(1.2f, 1.0f, 0.6f);
    private const float CrackleBaseSize = 0.010f;
    private const float CrackleFlashSizeMul = 2.3f;
    private const float CrackleTau = 0.035f;

    // Simple orbit camera (around origin)
    private float _cameraYaw = 0.0f;
    private float _cameraPitch = 0.15f;
    private float _cameraDistance = 200.0f;
    private bool _cameraDirty = true;

    private Vector3 _cameraTarget = new(0.0f, 80.0f, 0.0f);
    
    // Smoothed camera state for nicer motion
    private Vector3 _cameraTargetSmoothed = Vector3.Zero;
    private float _cameraDistanceSmoothed = 200.0f;
    [StructLayout(LayoutKind.Sequential)]
    private struct SceneCBData
    {
        public Matrix4x4 WorldViewProjection;
        public Matrix4x4 World;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LightingCBData
    {
        public Vector3 LightDirectionWS;
        public float _pad0;
        public Vector3 LightColor;
        public float _pad1;
        public Vector3 AmbientColor;
        public float _pad2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GroundVertex
    {
        public Vector3 Position;
        public Vector3 Normal;

        public GroundVertex(Vector3 position, Vector3 normal)
        {
            Position = position;
            Normal = normal;
        }
    }

    public D3D11Renderer(nint hwnd)
    {
        _hwnd = hwnd;
    }

    public void Initialize(int width, int height)
    {
        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);

        _width = width;
        _height = height;

        CreateDeviceAndSwapChain(width, height);
        CreateRenderTarget();
        CreateDepthStencil(width, height);
        CreateDepthStencilState();
        SetViewport(width, height);
        LoadPadShadersAndGeometry();
        LoadGroundShadersAndGeometry();
        LoadCanisterShadersAndGeometry();
        LoadShellShaders();
        CreateShellGeometry();
        CreateSceneConstants();
        UpdateSceneConstants(0.0f);

        CreateParticleSystem();

        _lastTick = System.Diagnostics.Stopwatch.GetTimestamp();
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

    public void Resize(int width, int height)
    {
        if (_swapChain is null || _device is null || _context is null)
            return;

        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);

        _width = width;
        _height = height;

        _context.OMSetRenderTargets(Array.Empty<ID3D11RenderTargetView>(), null);
        _rtv?.Dispose();
        _rtv = null;

        _dsv?.Dispose();
        _dsv = null;
        _depthTex?.Dispose();
        _depthTex = null;

        _swapChain.ResizeBuffers(
            bufferCount: 2,
            width: (uint)width,
            height: (uint)height,
            newFormat: Format.B8G8R8A8_UNorm,
            swapChainFlags: SwapChainFlags.None);

        CreateRenderTarget();
        CreateDepthStencil(width, height);
        SetViewport(width, height);
        UpdateSceneConstants(0.0f);

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
    }

    public void SetShells(System.Collections.Generic.IReadOnlyList<ShellRenderState> shells)
    {
        _shells = shells ?? Array.Empty<ShellRenderState>();
    }

    public void SetCanisters(System.Collections.Generic.IReadOnlyList<CanisterRenderState> canisters)
    {
        _canisters = canisters ?? Array.Empty<CanisterRenderState>();
    }

    public void SpawnBurst(Vector3 position, Vector4 baseColor, int count)
    {
        if (_context is null || _particleBuffer is null || _particleUploadBuffer is null)
            return;

        count = System.Math.Clamp(count, 1, _particleCapacity);

        int stride = Marshal.SizeOf<GpuParticle>();
        int start = _particleWriteCursor;

        var staging = new GpuParticle[count];
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
                Color = baseColor,
                Kind = crackle ? (uint)ParticleKind.Crackle : (uint)ParticleKind.Spark,
                // Use padding as deterministic per-particle randomness for crackle.
                _pad0 = (uint)_rng.Next(),
                _pad1 = (uint)_rng.Next(),
                _pad2 = (uint)_rng.Next()
            };
        }

        // Write into a rotating region. Wrap with a split update when needed.
        // Use `Map` + memcpy because Vortice's `UpdateSubresource` overloads vary by version.
        int firstCount = System.Math.Min(count, _particleCapacity - start);
        int remaining = count - firstCount;

        var mapped = _context.Map(_particleUploadBuffer, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
        try
        {
            nint basePtr = mapped.DataPointer;
            if (firstCount > 0)
            {
                nint dst = basePtr + (start * stride);
                for (int i = 0; i < firstCount; i++)
                {
                    Marshal.StructureToPtr(staging[i], dst + (i * stride), false);
                }
            }

            if (remaining > 0)
            {
                for (int i = 0; i < remaining; i++)
                {
                    Marshal.StructureToPtr(staging[firstCount + i], basePtr + (i * stride), false);
                }
            }
        }
        finally
        {
            _context.Unmap(_particleUploadBuffer, 0);
        }

        if (firstCount > 0)
        {
            var srcBox = new Box
            {
                Left = (int)(start * stride),
                Right = (int)((start + firstCount) * stride),
                Top = 0,
                Bottom = 1,
                Front = 0,
                Back = 1
            };

            _context.CopySubresourceRegion(_particleBuffer, 0, (uint)(start * stride), 0, 0, _particleUploadBuffer, 0, srcBox);
        }

        if (remaining > 0)
        {
            var srcBox = new Box
            {
                Left = 0,
                Right = (int)(remaining * stride),
                Top = 0,
                Bottom = 1,
                Front = 0,
                Back = 1
            };

            _context.CopySubresourceRegion(_particleBuffer, 0, 0, 0, 0, _particleUploadBuffer, 0, srcBox);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    public void SpawnBurstDirectedExplode(Vector3 position, Vector4 baseColor, float speed, System.ReadOnlySpan<Vector3> directions, float particleLifetimeSeconds)
    {
        _schemeTint = new Vector3(baseColor.X, baseColor.Y, baseColor.Z);
        SpawnBurstDirected(position, baseColor, speed, directions, particleLifetimeSeconds);
    }

    public void SpawnSmoke(Vector3 burstCenter)
    {
        if (_context is null || _particleBuffer is null || _particleUploadBuffer is null)
            return;

        int count = _rng.Next(200, 601);
        count = System.Math.Clamp(count, 1, _particleCapacity);

        int stride = Marshal.SizeOf<GpuParticle>();
        int start = _particleWriteCursor;

        var staging = new GpuParticle[count];

        // Start color is overwritten in the compute shader per life; keep alpha at 1 here.
        Vector4 startColor = new(0.35f, 0.33f, 0.30f, 1.0f);

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = RandomUnitVector();
            Vector3 pos = burstCenter + dir * 0.5f;

            float outwardSpeed = 1.5f + (float)_rng.NextDouble() * 1.5f;
            Vector3 outward = dir * outwardSpeed;

            float upSpeed = 1.5f + (float)_rng.NextDouble() * 2.0f;
            Vector3 up = new(0.0f, upSpeed, 0.0f);

            Vector3 vel = outward * 0.7f + up;

            float lifetime = 4.0f + (float)_rng.NextDouble() * 4.0f;

            staging[i] = new GpuParticle
            {
                Position = pos,
                Velocity = vel,
                Age = 0.0f,
                Lifetime = lifetime,
                Color = startColor,
                Kind = (uint)ParticleKind.Smoke,
                _pad0 = (uint)_rng.Next(),
                _pad1 = (uint)_rng.Next(),
                _pad2 = (uint)_rng.Next()
            };
        }

        int firstCount = System.Math.Min(count, _particleCapacity - start);
        int remaining = count - firstCount;

        var mapped = _context.Map(_particleUploadBuffer, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
        try
        {
            nint basePtr = mapped.DataPointer;
            if (firstCount > 0)
            {
                nint dst = basePtr + (start * stride);
                for (int i = 0; i < firstCount; i++)
                {
                    Marshal.StructureToPtr(staging[i], dst + (i * stride), false);
                }
            }

            if (remaining > 0)
            {
                for (int i = 0; i < remaining; i++)
                {
                    Marshal.StructureToPtr(staging[firstCount + i], basePtr + (i * stride), false);
                }
            }
        }
        finally
        {
            _context.Unmap(_particleUploadBuffer, 0);
        }

        if (firstCount > 0)
        {
            var srcBox = new Box
            {
                Left = (int)(start * stride),
                Right = (int)((start + firstCount) * stride),
                Top = 0,
                Bottom = 1,
                Front = 0,
                Back = 1
            };

            _context.CopySubresourceRegion(_particleBuffer, 0, (uint)(start * stride), 0, 0, _particleUploadBuffer, 0, srcBox);
        }

        if (remaining > 0)
        {
            var srcBox = new Box
            {
                Left = 0,
                Right = (int)(remaining * stride),
                Top = 0,
                Bottom = 1,
                Front = 0,
                Back = 1
            };

            _context.CopySubresourceRegion(_particleBuffer, 0, 0, 0, 0, _particleUploadBuffer, 0, srcBox);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    public void SpawnBurstDirected(Vector3 position, Vector4 baseColor, float speed, System.ReadOnlySpan<Vector3> directions, float particleLifetimeSeconds)
    {
        if (_context is null || _particleBuffer is null || _particleUploadBuffer is null)
            return;

        int count = System.Math.Clamp(directions.Length, 1, _particleCapacity);

        int stride = Marshal.SizeOf<GpuParticle>();
        int start = _particleWriteCursor;

        var staging = new GpuParticle[count];
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
                // Center around the requested lifetime but keep some variation.
                float j = (float)(_rng.NextDouble() * 2.0 - 1.0);
                lifetime = System.Math.Max(0.05f, particleLifetimeSeconds * (1.0f + 0.35f * j));
            }

            staging[i] = new GpuParticle
            {
                Position = position,
                Velocity = vel,
                Age = 0.0f,
                Lifetime = lifetime,
                Color = baseColor,
                Kind = crackle ? (uint)ParticleKind.Crackle : (uint)ParticleKind.Spark,
                _pad0 = (uint)_rng.Next(),
                _pad1 = (uint)_rng.Next(),
                _pad2 = (uint)_rng.Next()
            };
        }

        int firstCount = System.Math.Min(count, _particleCapacity - start);
        int remaining = count - firstCount;

        var mapped = _context.Map(_particleUploadBuffer, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
        try
        {
            nint basePtr = mapped.DataPointer;
            if (firstCount > 0)
            {
                nint dst = basePtr + (start * stride);
                for (int i = 0; i < firstCount; i++)
                {
                    Marshal.StructureToPtr(staging[i], dst + (i * stride), false);
                }
            }

            if (remaining > 0)
            {
                for (int i = 0; i < remaining; i++)
                {
                    Marshal.StructureToPtr(staging[firstCount + i], basePtr + (i * stride), false);
                }
            }
        }
        finally
        {
            _context.Unmap(_particleUploadBuffer, 0);
        }

        if (firstCount > 0)
        {
            var srcBox = new Box
            {
                Left = (int)(start * stride),
                Right = (int)((start + firstCount) * stride),
                Top = 0,
                Bottom = 1,
                Front = 0,
                Back = 1
            };

            _context.CopySubresourceRegion(_particleBuffer, 0, (uint)(start * stride), 0, 0, _particleUploadBuffer, 0, srcBox);
        }

        if (remaining > 0)
        {
            var srcBox = new Box
            {
                Left = 0,
                Right = (int)(remaining * stride),
                Top = 0,
                Bottom = 1,
                Front = 0,
                Back = 1
            };

            _context.CopySubresourceRegion(_particleBuffer, 0, 0, 0, 0, _particleUploadBuffer, 0, srcBox);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    private void CreateShellGeometry()
    {
        if (_device is null)
            return;

        // Debug visibility: make the shell larger for now.
        // Physical will be smaller later (5cm diameter => radius 0.025m).
        const float radius = 0.10f;
        const int slices = 16;
        const int stacks = 12;

        var verts = new System.Collections.Generic.List<GroundVertex>(slices * stacks * 6);

        for (int stack = 0; stack < stacks; stack++)
        {
            float v0 = (float)stack / stacks;
            float v1 = (float)(stack + 1) / stacks;
            float phi0 = (float)(v0 * System.Math.PI);
            float phi1 = (float)(v1 * System.Math.PI);

            for (int slice = 0; slice < slices; slice++)
            {
                float u0 = (float)slice / slices;
                float u1 = (float)(slice + 1) / slices;
                float theta0 = (float)(u0 * System.Math.PI * 2.0);
                float theta1 = (float)(u1 * System.Math.PI * 2.0);

                Vector3 p00 = Spherical(radius, theta0, phi0);
                Vector3 p10 = Spherical(radius, theta0, phi1);
                Vector3 p01 = Spherical(radius, theta1, phi0);
                Vector3 p11 = Spherical(radius, theta1, phi1);

                Vector3 n00 = Vector3.Normalize(p00);
                Vector3 n10 = Vector3.Normalize(p10);
                Vector3 n01 = Vector3.Normalize(p01);
                Vector3 n11 = Vector3.Normalize(p11);

                verts.Add(new GroundVertex(p00, n00));
                verts.Add(new GroundVertex(p10, n10));
                verts.Add(new GroundVertex(p11, n11));

                verts.Add(new GroundVertex(p00, n00));
                verts.Add(new GroundVertex(p11, n11));
                verts.Add(new GroundVertex(p01, n01));
            }
        }

        _shellVertexCount = verts.Count;
        int stride = Marshal.SizeOf<GroundVertex>();

        _shellVB?.Dispose();
        _shellVB = _device.CreateBuffer(
            verts.ToArray(),
            new BufferDescription
            {
                BindFlags = BindFlags.VertexBuffer,
                Usage = ResourceUsage.Default,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None,
                ByteWidth = (uint)(stride * verts.Count),
                StructureByteStride = (uint)stride
            });
    }

    private static Vector3 Spherical(float r, float theta, float phi)
    {
        float sinPhi = (float)System.Math.Sin(phi);
        float x = r * (float)(System.Math.Cos(theta) * sinPhi);
        float y = r * (float)System.Math.Cos(phi);
        float z = r * (float)(System.Math.Sin(theta) * sinPhi);
        return new Vector3(x, y, z);
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
        if (_context is null || _particleBuffer is null || _particleUploadBuffer is null)
            return;

        count = System.Math.Clamp(count, 1, _particleCapacity);

        // Trail is a slightly warm white and additive blended in the first particle pass.
        Vector4 color = new(1.0f, 0.92f, 0.55f, 1.0f);

        int stride = Marshal.SizeOf<GpuParticle>();
        int start = _particleWriteCursor;

        var staging = new GpuParticle[count];

        Vector3 dir = velocity.LengthSquared() > 1e-6f ? Vector3.Normalize(velocity) : Vector3.UnitY;
        Vector3 back = -dir;
        Vector3 right = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
        if (right.LengthSquared() < 1e-6f)
            right = Vector3.UnitX;
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, dir));

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

        int firstCount = System.Math.Min(count, _particleCapacity - start);
        int remaining = count - firstCount;

        var mapped = _context.Map(_particleUploadBuffer, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
        try
        {
            nint basePtr = mapped.DataPointer;
            if (firstCount > 0)
            {
                nint dst = basePtr + (start * stride);
                for (int i = 0; i < firstCount; i++)
                {
                    Marshal.StructureToPtr(staging[i], dst + (i * stride), false);
                }
            }

            if (remaining > 0)
            {
                for (int i = 0; i < remaining; i++)
                {
                    Marshal.StructureToPtr(staging[firstCount + i], basePtr + (i * stride), false);
                }
            }
        }
        finally
        {
            _context.Unmap(_particleUploadBuffer, 0);
        }

        if (firstCount > 0)
        {
            var srcBox = new Box
            {
                Left = (int)(start * stride),
                Right = (int)((start + firstCount) * stride),
                Top = 0,
                Bottom = 1,
                Front = 0,
                Back = 1
            };

            _context.CopySubresourceRegion(_particleBuffer, 0, (uint)(start * stride), 0, 0, _particleUploadBuffer, 0, srcBox);
        }

        if (remaining > 0)
        {
            var srcBox = new Box
            {
                Left = 0,
                Right = (int)(remaining * stride),
                Top = 0,
                Bottom = 1,
                Front = 0,
                Back = 1
            };

            _context.CopySubresourceRegion(_particleBuffer, 0, 0, 0, 0, _particleUploadBuffer, 0, srcBox);
        }

        _particleWriteCursor = (start + count) % _particleCapacity;
    }

    private Vector3 RandomUnitVector()
    {
        // Uniform on sphere
        float z = (float)(_rng.NextDouble() * 2.0 - 1.0);
        float a = (float)(_rng.NextDouble() * System.Math.PI * 2.0);
        float r = (float)System.Math.Sqrt(System.Math.Max(0.0, 1.0 - z * z));
        return new Vector3(r * (float)System.Math.Cos(a), z, r * (float)System.Math.Sin(a));
    }

    private void CreateParticleSystem()
    {
        if (_device is null)
            return;

        int stride = Marshal.SizeOf<GpuParticle>();

        _particleBuffer?.Dispose();
        _particleUploadBuffer?.Dispose();
        _particleSRV?.Dispose();
        _particleUAV?.Dispose();
        _particlesCS?.Dispose();
        _particlesVS?.Dispose();
        _particlesPS?.Dispose();
        _blendAdditive?.Dispose();
        _blendAlpha?.Dispose();
        _blendAlphaPremult?.Dispose();
        _depthReadNoWrite?.Dispose();
        _frameCB?.Dispose();

        // Init dead particles.
        var init = new GpuParticle[_particleCapacity];
        for (int i = 0; i < init.Length; i++)
        {
            init[i].Kind = (uint)ParticleKind.Dead;
            init[i].Color = Vector4.Zero;
        }

        _particleBuffer = _device.CreateBuffer(
            init,
            new BufferDescription
            {
                BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
                Usage = ResourceUsage.Default,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.BufferStructured,
                ByteWidth = (uint)(stride * _particleCapacity),
                StructureByteStride = (uint)stride
            });

        // Premultiplied alpha for softer overlapping smoke (reduces visible quad edges).
        _blendAlphaPremult = _device.CreateBlendState(new BlendDescription
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = false,
            RenderTarget =
            {
                [0] = new RenderTargetBlendDescription
                {
                    BlendEnable = true,
                    SourceBlend = Blend.One,
                    DestinationBlend = Blend.InverseSourceAlpha,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = Blend.One,
                    DestinationBlendAlpha = Blend.InverseSourceAlpha,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteEnable.All
                }
            }
        });

        _particleUploadBuffer = _device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.None,
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Write,
            MiscFlags = ResourceOptionFlags.BufferStructured,
            ByteWidth = (uint)(stride * _particleCapacity),
            StructureByteStride = (uint)stride
        });

        _particleSRV = _device.CreateShaderResourceView(_particleBuffer, new ShaderResourceViewDescription
        {
            ViewDimension = ShaderResourceViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferShaderResourceView
            {
                FirstElement = 0,
                NumElements = (uint)_particleCapacity
            }
        });

        _particleUAV = _device.CreateUnorderedAccessView(_particleBuffer, new UnorderedAccessViewDescription
        {
            ViewDimension = UnorderedAccessViewDimension.Buffer,
            Format = Format.Unknown,
            Buffer = new BufferUnorderedAccessView
            {
                FirstElement = 0,
                NumElements = (uint)_particleCapacity
            }
        });

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Particles.hlsl");
        string source = File.ReadAllText(shaderPath);

        var csBlob = Compiler.Compile(source, "CSUpdate", shaderPath, "cs_5_0");
        var vsBlob = Compiler.Compile(source, "VSParticle", shaderPath, "vs_5_0");
        var psBlob = Compiler.Compile(source, "PSParticle", shaderPath, "ps_5_0");

        byte[] csBytes = csBlob.ToArray();
        byte[] vsBytes = vsBlob.ToArray();
        byte[] psBytes = psBlob.ToArray();

        _particlesCS = _device.CreateComputeShader(csBytes);
        _particlesVS = _device.CreateVertexShader(vsBytes);
        _particlesPS = _device.CreatePixelShader(psBytes);

        _frameCB = _device.CreateBuffer(new BufferDescription
        {
            BindFlags = BindFlags.ConstantBuffer,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write,
            ByteWidth = (uint)Marshal.SizeOf<FrameCBData>()
        });

        _blendAdditive = _device.CreateBlendState(new BlendDescription
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = false,
            RenderTarget =
            {
                [0] = new RenderTargetBlendDescription
                {
                    BlendEnable = true,
                    SourceBlend = Blend.One,
                    DestinationBlend = Blend.One,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = Blend.One,
                    DestinationBlendAlpha = Blend.One,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteEnable.All
                }
            }
        });

        _blendAlpha = _device.CreateBlendState(new BlendDescription
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = false,
            RenderTarget =
            {
                [0] = new RenderTargetBlendDescription
                {
                    BlendEnable = true,
                    SourceBlend = Blend.SourceAlpha,
                    DestinationBlend = Blend.InverseSourceAlpha,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = Blend.One,
                    DestinationBlendAlpha = Blend.InverseSourceAlpha,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteEnable.All
                }
            }
        });

        _depthReadNoWrite = _device.CreateDepthStencilState(new DepthStencilDescription
        {
            DepthEnable = true,
            DepthWriteMask = DepthWriteMask.Zero,
            DepthFunc = ComparisonFunction.LessEqual,
            StencilEnable = false
        });
    }

    private void UpdateParticles(float scaledDt)
    {
        if (_context is null || _particlesCS is null || _particleUAV is null || _frameCB is null)
            return;

        // `scaledDt` is already time-scaled by the simulation layer.

        // Camera basis from view matrix (world-space).
        // view is orthonormal: right = (M11,M21,M31), up=(M12,M22,M32)
        var right = new Vector3(_view.M11, _view.M21, _view.M31);
        var up = new Vector3(_view.M12, _view.M22, _view.M32);

        var vp = Matrix4x4.Transpose(_view * _proj);

        var frame = new FrameCBData
        {
            ViewProjection = vp,
            CameraRightWS = right,
            DeltaTime = scaledDt,
            CameraUpWS = up,
            Time = (float)(Environment.TickCount64 / 1000.0),

            CrackleBaseColor = CrackleBaseColor,
            CrackleBaseSize = CrackleBaseSize,
            CracklePeakColor = CracklePeakColor,
            CrackleFlashSizeMul = CrackleFlashSizeMul,
            CrackleFadeColor = CrackleFadeColor,
            CrackleTau = CrackleTau,

            SchemeTint = _schemeTint,

            // Default to additive pass unless overridden right before drawing.
            ParticlePass = 0u
        };

        var mapped = _context.Map(_frameCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        Marshal.StructureToPtr(frame, mapped.DataPointer, false);
        _context.Unmap(_frameCB, 0);

        _context.CSSetShader(_particlesCS);
        _context.CSSetConstantBuffer(0, _frameCB);
        _context.CSSetUnorderedAccessView(0, _particleUAV);

        uint groups = (uint)((_particleCapacity + 255) / 256);
        _context.Dispatch(groups, 1, 1);

        // Unbind UAV to avoid hazards with SRV on draw.
        _context.CSSetUnorderedAccessView(0, null);
        _context.CSSetShader(null);
    }

    private void DrawParticles(bool additive)
    {
        if (_context is null || _particlesVS is null || _particlesPS is null || _particleSRV is null || _frameCB is null)
            return;

        // Mark the current particle render pass for the shader.
        // 0 = additive (bright), 1 = alpha (smoke/embers)
        var mappedPass = _context.Map(_frameCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            // ParticlePass is located after the existing data; do a full struct write to keep it simple.
            // Rebuild the same data as UpdateParticles but keep DeltaTime = 0 here (not used in VS/PS).
            var right = new Vector3(_view.M11, _view.M21, _view.M31);
            var up = new Vector3(_view.M12, _view.M22, _view.M32);
            var vp = Matrix4x4.Transpose(_view * _proj);

            var frame = new FrameCBData
            {
                ViewProjection = vp,
                CameraRightWS = right,
                DeltaTime = 0.0f,
                CameraUpWS = up,
                Time = (float)(Environment.TickCount64 / 1000.0),

                CrackleBaseColor = CrackleBaseColor,
                CrackleBaseSize = CrackleBaseSize,
                CracklePeakColor = CracklePeakColor,
                CrackleFlashSizeMul = CrackleFlashSizeMul,
                CrackleFadeColor = CrackleFadeColor,
                CrackleTau = CrackleTau,

                SchemeTint = _schemeTint,

                ParticlePass = additive ? 0u : 1u
            };

            Marshal.StructureToPtr(frame, mappedPass.DataPointer, false);
        }
        finally
        {
            _context.Unmap(_frameCB, 0);
        }

        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetInputLayout(null);
        _context.IASetVertexBuffers(0, 0, Array.Empty<ID3D11Buffer>(), Array.Empty<uint>(), Array.Empty<uint>());

        // For additive sparks we keep depth read so particles sit in the scene.
        // For alpha-blended smoke we disable depth to avoid it looking like a hard foreground card.
        if (additive)
        {
            _context.OMSetDepthStencilState(_depthReadNoWrite, 0);
        }
        else
        {
            _context.OMSetDepthStencilState(null, 0);
        }
        _context.OMSetBlendState(additive ? _blendAdditive : _blendAlpha, new Color4(0, 0, 0, 0), uint.MaxValue);

        _context.VSSetShader(_particlesVS);
        _context.PSSetShader(_particlesPS);

        _context.VSSetConstantBuffer(0, _frameCB);
        _context.VSSetShaderResource(0, _particleSRV);

        // Pass 1: additive (bright). Pass 2: alpha (embers). Shader currently outputs same color;
        // we split passes to match desired blend modes.
        _context.Draw((uint)(_particleCapacity * 6), 0);

        _context.VSSetShaderResource(0, null);
        _context.OMSetBlendState(null, new Color4(0, 0, 0, 0), uint.MaxValue);
        _context.OMSetDepthStencilState(_depthStencilState, 0);
    }

    private void DrawShells()
    {
        if (_context is null || _shellVB is null || _shellVS is null || _shellPS is null || _shellInputLayout is null)
            return;

        int stride = Marshal.SizeOf<GroundVertex>();
        uint[] strides = new[] { (uint)stride };
        uint[] offsets = new[] { 0u };

        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetInputLayout(_shellInputLayout);
        _context.IASetVertexBuffers(0, 1, new[] { _shellVB }, strides, offsets);

        _context.VSSetShader(_shellVS);
        _context.PSSetShader(_shellPS);

        for (int i = 0; i < _shells.Count; i++)
        {
            var s = _shells[i];
            var world = Matrix4x4.CreateTranslation(s.Position);
            var wvp = Matrix4x4.Transpose(world * _view * _proj);

            if (_objectCB != null)
            {
                var obj = new SceneCBData { WorldViewProjection = wvp, World = Matrix4x4.Transpose(world) };
                var mapped = _context.Map(_objectCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
                Marshal.StructureToPtr(obj, mapped.DataPointer, false);
                _context.Unmap(_objectCB, 0);
                _context.VSSetConstantBuffer(0, _objectCB);
            }
            _context.Draw((uint)_shellVertexCount, 0);
        }

        // Restore scene constants for subsequent draws
        if (_sceneCB != null)
        {
            _context.VSSetConstantBuffer(0, _sceneCB);
        }
    }

    private void LoadShellShaders()
    {
        if (_device is null)
            return;

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Shell.hlsl");
        string source = File.ReadAllText(shaderPath);

        var vsBlob = Compiler.Compile(source, "VSMain", shaderPath, "vs_5_0");
        var psBlob = Compiler.Compile(source, "PSMain", shaderPath, "ps_5_0");
        byte[] vsBytes = vsBlob.ToArray();
        byte[] psBytes = psBlob.ToArray();

        _shellVS = _device.CreateVertexShader(vsBytes);
        _shellPS = _device.CreatePixelShader(psBytes);

        var elements = new[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0)
        };
        _shellInputLayout = _device.CreateInputLayout(elements, vsBytes);
    }

    private void LoadCanisterShadersAndGeometry()
    {
        if (_device is null)
            return;

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Canister.hlsl");
        string source = File.ReadAllText(shaderPath);

        var vsBlob = Compiler.Compile(source, "VSMain", shaderPath, "vs_5_0");
        var psBlob = Compiler.Compile(source, "PSMain", shaderPath, "ps_5_0");
        byte[] vsBytes = vsBlob.ToArray();
        byte[] psBytes = psBlob.ToArray();

        _canisterVS = _device.CreateVertexShader(vsBytes);
        _canisterPS = _device.CreatePixelShader(psBytes);

        var elements = new[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0)
        };
        _canisterInputLayout = _device.CreateInputLayout(elements, vsBytes);

        // Cylinder dimensions (meters)
        const float radius = 0.075f; // 15cm diameter
        const float height = 0.30f;  // 30cm tall
        const int slices = 32;

        var verts = new System.Collections.Generic.List<GroundVertex>(slices * 6);

        const float padThickness = 0.10f;
        float y0 = padThickness;
        float y1 = padThickness + height;

        for (int i = 0; i < slices; i++)
        {
            float a0 = (float)(i * (System.Math.PI * 2.0) / slices);
            float a1 = (float)((i + 1) * (System.Math.PI * 2.0) / slices);

            float c0 = (float)System.Math.Cos(a0);
            float s0 = (float)System.Math.Sin(a0);
            float c1 = (float)System.Math.Cos(a1);
            float s1 = (float)System.Math.Sin(a1);

            var p00 = new Vector3(radius * c0, y0, radius * s0);
            var p01 = new Vector3(radius * c1, y0, radius * s1);
            var p10 = new Vector3(radius * c0, y1, radius * s0);
            var p11 = new Vector3(radius * c1, y1, radius * s1);

            var n0 = Vector3.Normalize(new Vector3(c0, 0.0f, s0));
            var n1 = Vector3.Normalize(new Vector3(c1, 0.0f, s1));

            // Side quad (two triangles)
            verts.Add(new GroundVertex(p00, n0));
            verts.Add(new GroundVertex(p10, n0));
            verts.Add(new GroundVertex(p11, n1));

            verts.Add(new GroundVertex(p00, n0));
            verts.Add(new GroundVertex(p11, n1));
            verts.Add(new GroundVertex(p01, n1));
        }

        _canisterVertexCount = verts.Count;

        int stride = Marshal.SizeOf<GroundVertex>();
        _canisterVB?.Dispose();
        _canisterVB = _device.CreateBuffer(
            verts.ToArray(),
            new BufferDescription
            {
                BindFlags = BindFlags.VertexBuffer,
                Usage = ResourceUsage.Default,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None,
                ByteWidth = (uint)(stride * verts.Count),
                StructureByteStride = (uint)stride
            });
    }

    private void DrawCanisters()
    {
        if (_context is null || _canisterVS is null || _canisterPS is null || _canisterVB is null || _canisterInputLayout is null)
            return;

        int stride = Marshal.SizeOf<GroundVertex>();
        uint[] strides = new[] { (uint)stride };
        uint[] offsets = new[] { 0u };
        var buffers = new[] { _canisterVB };

        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetInputLayout(_canisterInputLayout);
        _context.IASetVertexBuffers(0, 1, buffers, strides, offsets);

        _context.VSSetShader(_canisterVS);
        _context.PSSetShader(_canisterPS);

        if (_sceneCB != null)
        {
            _context.VSSetConstantBuffer(0, _sceneCB);
        }
        if (_lightingCB != null)
        {
            _context.PSSetConstantBuffer(1, _lightingCB);
        }

        // Draw each canister at its world-space position.
        // If none provided, draw a single default at origin for backward compatibility.
        if (_canisters.Count == 0)
        {
            if (_objectCB != null)
            {
                var world0 = Matrix4x4.Identity;
                var wvp0 = Matrix4x4.Transpose(world0 * _view * _proj);
                var obj0 = new SceneCBData { WorldViewProjection = wvp0, World = Matrix4x4.Transpose(world0) };
                var mapped0 = _context.Map(_objectCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
                Marshal.StructureToPtr(obj0, mapped0.DataPointer, false);
                _context.Unmap(_objectCB, 0);
                _context.VSSetConstantBuffer(0, _objectCB);
            }
            _context.Draw((uint)_canisterVertexCount, 0);
            return;
        }

        static Matrix4x4 CreateAlignYToDirection(Vector3 direction)
        {
            if (direction.LengthSquared() < 1e-8f)
                return Matrix4x4.Identity;

            var dir = Vector3.Normalize(direction);
            var from = Vector3.UnitY;
            float dot = Vector3.Dot(from, dir);

            // 180-degree flip: pick any orthogonal axis.
            if (dot <= -0.9999f)
            {
                return Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
            }

            // Already aligned.
            if (dot >= 0.9999f)
            {
                return Matrix4x4.Identity;
            }

            var axis = Vector3.Normalize(Vector3.Cross(from, dir));
            float angle = MathF.Acos((float)System.Math.Clamp(dot, -1.0f, 1.0f));
            return Matrix4x4.CreateFromAxisAngle(axis, angle);
        }

        for (int i = 0; i < _canisters.Count; i++)
        {
            var c = _canisters[i];
            if (_objectCB != null)
            {
                var world = CreateAlignYToDirection(c.Direction) * Matrix4x4.CreateTranslation(c.Position);
                var wvp = Matrix4x4.Transpose(world * _view * _proj);
                var obj = new SceneCBData { WorldViewProjection = wvp, World = Matrix4x4.Transpose(world) };
                var mapped = _context.Map(_objectCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
                Marshal.StructureToPtr(obj, mapped.DataPointer, false);
                _context.Unmap(_objectCB, 0);
                _context.VSSetConstantBuffer(0, _objectCB);
            }
            _context.Draw((uint)_canisterVertexCount, 0);
        }

        // Restore scene constants for subsequent draws
        if (_sceneCB != null)
        {
            _context.VSSetConstantBuffer(0, _sceneCB);
        }
    }

    private void CreateDeviceAndSwapChain(int width, int height)
    {
        var creationFlags = DeviceCreationFlags.BgraSupport;
#if DEBUG
        creationFlags |= DeviceCreationFlags.Debug;
#endif

        D3D11CreateDevice(
            adapter: null,
            driverType: DriverType.Hardware,
            flags: creationFlags,
            featureLevels: null,
            device: out _device,
            immediateContext: out _context);

        using var dxgiDevice = _device!.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        using var factory = adapter.GetParent<IDXGIFactory2>();

        var desc = new SwapChainDescription1
        {
            Width = (uint)width,
            Height = (uint)height,
            Format = Format.B8G8R8A8_UNorm,
            BufferCount = 2,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = new SampleDescription(1, 0),
            SwapEffect = SwapEffect.FlipDiscard,
            Scaling = Scaling.Stretch,
            Stereo = false,
            AlphaMode = AlphaMode.Ignore,
            Flags = SwapChainFlags.None
        };

        _swapChain = factory.CreateSwapChainForHwnd(_device, _hwnd, desc);
    }

    private void CreateRenderTarget()
    {
        using var backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
        _rtv = _device!.CreateRenderTargetView(backBuffer);
    }

    private void CreateDepthStencil(int width, int height)
    {
        if (_device is null)
            return;

        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);

        _depthTex?.Dispose();
        _depthTex = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.D24_UNorm_S8_UInt,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None
        });

        _dsv?.Dispose();
        _dsv = _device.CreateDepthStencilView(_depthTex);
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
        Marshal.StructureToPtr(scene, mapped.DataPointer, false);
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
            Marshal.StructureToPtr(light, mappedLight.DataPointer, false);
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

        // --- Compile simple pad shaders ---
        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Pad.hlsl");
        string source;
        try
        {
            source = File.ReadAllText(shaderPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to read shader file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Console.WriteLine("Shader file read error:");
            Console.WriteLine(ex.Message);
            throw;
        }

        byte[] vsBytes;
        byte[] psBytes;

        // Vortice signature: Compile(shaderSource, entryPoint, sourceName, profile, ...)
        try
        {
            var vsBlob = Compiler.Compile(source, "VSMain", shaderPath, "vs_5_0");
            vsBytes = vsBlob.ToArray();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Vertex shader compilation failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Console.WriteLine("Vertex Shader Compilation Error:");
            Console.WriteLine(ex.Message);
            throw;
        }

        try 
        {
            var psBlob = Compiler.Compile(source, "PSMain", shaderPath, "ps_5_0");
            psBytes = psBlob.ToArray();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Pixel shader compilation failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Console.WriteLine("Pixel Shader Compilation Error:");
            Console.WriteLine(ex.Message);
            throw;
        }
        

        _padVS = _device.CreateVertexShader(vsBytes);
        _padPS = _device.CreatePixelShader(psBytes);

        // Input layout: POSITION (float3)
        var elements = new[]
        {
            // ctor signature in your Vortice version is:
            // InputElementDescription(string semanticName, int semanticIndex, Format format, int alignedByteOffset, int slot)
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0)
        };

        _padInputLayout = _device.CreateInputLayout(elements, vsBytes);

        _padRasterizerState?.Dispose();
        _padRasterizerState = _device.CreateRasterizerState(new RasterizerDescription
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None,
            FrontCounterClockwise = false,
            DepthClipEnable = true
        });

        // --- Create a 20m x 20m pad with 10cm thickness (a simple box) ---
        const float half = 10.0f;
        const float thickness = 0.10f;
        float y0 = 0.0f;
        float y1 = thickness;

        // Top face + 4 sides (no bottom, since it sits on ground)
        PadVertex[] verts =
        {
            // Top (y1)
            new PadVertex(-half, y1, -half),
            new PadVertex( half, y1, -half),
            new PadVertex(-half, y1,  half),
            new PadVertex( half, y1, -half),
            new PadVertex( half, y1,  half),
            new PadVertex(-half, y1,  half),

            // +Z side (front)
            new PadVertex(-half, y0,  half),
            new PadVertex( half, y0,  half),
            new PadVertex(-half, y1,  half),
            new PadVertex( half, y0,  half),
            new PadVertex( half, y1,  half),
            new PadVertex(-half, y1,  half),

            // -Z side (back)
            new PadVertex( half, y0, -half),
            new PadVertex(-half, y0, -half),
            new PadVertex( half, y1, -half),
            new PadVertex(-half, y0, -half),
            new PadVertex(-half, y1, -half),
            new PadVertex( half, y1, -half),

            // +X side (right)
            new PadVertex( half, y0,  half),
            new PadVertex( half, y0, -half),
            new PadVertex( half, y1,  half),
            new PadVertex( half, y0, -half),
            new PadVertex( half, y1, -half),
            new PadVertex( half, y1,  half),

            // -X side (left)
            new PadVertex(-half, y0, -half),
            new PadVertex(-half, y0,  half),
            new PadVertex(-half, y1, -half),
            new PadVertex(-half, y0,  half),
            new PadVertex(-half, y1,  half),
            new PadVertex(-half, y1, -half),
        };

        int stride = Marshal.SizeOf<PadVertex>();

        _padVB?.Dispose();
        _padVB = _device.CreateBuffer(
            verts,
            new BufferDescription
            {
                BindFlags = BindFlags.VertexBuffer,
                Usage = ResourceUsage.Default,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None,
                ByteWidth = (uint)(stride * verts.Length),
                StructureByteStride = (uint)stride
            });
    }

    private void LoadGroundShadersAndGeometry()
    {
        if (_device is null)
            return;

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Ground.hlsl");
        string source = File.ReadAllText(shaderPath);

        var vsBlob = Compiler.Compile(source, "VSMain", shaderPath, "vs_5_0");
        var psBlob = Compiler.Compile(source, "PSMain", shaderPath, "ps_5_0");
        byte[] vsBytes = vsBlob.ToArray();
        byte[] psBytes = psBlob.ToArray();

        _groundVS = _device.CreateVertexShader(vsBytes);
        _groundPS = _device.CreatePixelShader(psBytes);

        var elements = new[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0)
        };
        _groundInputLayout = _device.CreateInputLayout(elements, vsBytes);

        // Large ground plane centered at origin on XZ.
        const float half = 200.0f;
        Vector3 n = Vector3.UnitY;
        GroundVertex[] verts =
        {
            new GroundVertex(new Vector3(-half, 0.0f, -half), n),
            new GroundVertex(new Vector3( half, 0.0f, -half), n),
            new GroundVertex(new Vector3(-half, 0.0f,  half), n),

            new GroundVertex(new Vector3( half, 0.0f, -half), n),
            new GroundVertex(new Vector3( half, 0.0f,  half), n),
            new GroundVertex(new Vector3(-half, 0.0f,  half), n),
        };

        int stride = Marshal.SizeOf<GroundVertex>();
        _groundVB?.Dispose();
        _groundVB = _device.CreateBuffer(
            verts,
            new BufferDescription
            {
                BindFlags = BindFlags.VertexBuffer,
                Usage = ResourceUsage.Default,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None,
                ByteWidth = (uint)(stride * verts.Length),
                StructureByteStride = (uint)stride
            });
    }

    private void DrawGround()
    {
        if (_context is null || _groundVS is null || _groundPS is null || _groundVB is null || _groundInputLayout is null)
            return;

        int stride = Marshal.SizeOf<GroundVertex>();
        uint[] strides = new[] { (uint)stride };
        uint[] offsets = new[] { 0u };
        var buffers = new[] { _groundVB };

        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetInputLayout(_groundInputLayout);
        _context.IASetVertexBuffers(0, 1, buffers, strides, offsets);

        _context.VSSetShader(_groundVS);
        _context.PSSetShader(_groundPS);

        if (_sceneCB != null)
        {
            _context.VSSetConstantBuffer(0, _sceneCB);
            _context.PSSetConstantBuffer(0, _sceneCB);
        }

        if (_lightingCB != null)
        {
            _context.PSSetConstantBuffer(1, _lightingCB);
        }

        if (_padRasterizerState != null)
        {
            _context.RSSetState(_padRasterizerState);
        }

        _context.Draw(6, 0);
    }

    private void DrawLaunchPad()
    {
        if (_context is null || _padVS is null || _padPS is null || _padVB is null || _padInputLayout is null)
            return;

        int stride = Marshal.SizeOf<PadVertex>();
        int offset = 0;

        // Set IA state
        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetInputLayout(_padInputLayout);

        // Your Vortice version expects:
        // IASetVertexBuffers(int startSlot, int numBuffers, ID3D11Buffer[] vertexBuffers, int[] strides, int[] offsets)
        var buffers = new[] { _padVB };
        uint[] strides = new[] { (uint) stride };
        uint[] offsets = new[] { (uint) offset };
        _context.IASetVertexBuffers(0, 1, buffers, strides, offsets);

        // Set shaders
        _context.VSSetShader(_padVS);
        _context.PSSetShader(_padPS);

        if (_sceneCB != null)
        {
            _context.VSSetConstantBuffer(0, _sceneCB);
            _context.PSSetConstantBuffer(0, _sceneCB);
        }

        if (_padRasterizerState != null)
        {
            _context.RSSetState(_padRasterizerState);
        }

        // No special blending/depth yet
        _context.Draw(30, 0);
    }

    public void Dispose()
    {
        _particleUploadBuffer?.Dispose();
        _particleUploadBuffer = null;

        _particleSRV?.Dispose();
        _particleSRV = null;

        _particleUAV?.Dispose();
        _particleUAV = null;

        _particleBuffer?.Dispose();
        _particleBuffer = null;

        _particlesCS?.Dispose();
        _particlesCS = null;

        _particlesVS?.Dispose();
        _particlesVS = null;

        _particlesPS?.Dispose();
        _particlesPS = null;

        _blendAdditive?.Dispose();
        _blendAdditive = null;

        _blendAlpha?.Dispose();
        _blendAlpha = null;

        _blendAlphaPremult?.Dispose();
        _blendAlphaPremult = null;

        _depthReadNoWrite?.Dispose();
        _depthReadNoWrite = null;

        _frameCB?.Dispose();
        _frameCB = null;

        _shellInputLayout?.Dispose();
        _shellInputLayout = null;

        _shellVS?.Dispose();
        _shellVS = null;

        _shellPS?.Dispose();
        _shellPS = null;

        _objectCB?.Dispose();
        _objectCB = null;
        _depthStencilState?.Dispose();
        _depthStencilState = null;

        _groundVB?.Dispose();
        _groundVB = null;

        _groundInputLayout?.Dispose();
        _groundInputLayout = null;

        _groundVS?.Dispose();
        _groundVS = null;

        _groundPS?.Dispose();
        _groundPS = null;

        _sceneCB?.Dispose();
        _sceneCB = null;

        _padRasterizerState?.Dispose();
        _padRasterizerState = null;

        _padVB?.Dispose();
        _padVB = null;

        _padInputLayout?.Dispose();
        _padInputLayout = null;

        _padVS?.Dispose();
        _padVS = null;

        _padPS?.Dispose();
        _padPS = null;

        _rtv?.Dispose();
        _rtv = null;

        _dsv?.Dispose();
        _dsv = null;

        _depthTex?.Dispose();
        _depthTex = null;

        _swapChain?.Dispose();
        _swapChain = null;

        _context?.ClearState();
        _context?.Flush();
        _context?.Dispose();
        _context = null;

        _device?.Dispose();
        _device = null;
    }
}
