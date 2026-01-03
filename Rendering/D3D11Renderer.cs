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

    public int ShellSpawnCount { get; private set; }
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

    private readonly System.Collections.Generic.List<ShellState> _shells = new();
    private long _lastTick;
    private readonly System.Random _rng = new();

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
    private ID3D11DepthStencilState? _depthReadNoWrite;
    private ID3D11Buffer? _frameCB;
    private int _particleCapacity = 2097152;
    private int _particleWriteCursor;

    private Matrix4x4 _view;
    private Matrix4x4 _proj;

    public int ShellSpawnCount { get; private set; }

    private struct ShellState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Age;
        public float Fuse;
        public bool Alive;
        public bool Bursted;
    }

    private enum ParticleKind : uint
    {
        Dead = 0,
        Shell = 1,
        Spark = 2,
        Smoke = 3
    }

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
    }

    // Simple orbit camera (around origin)
    private float _cameraYaw = 0.0f;
    private float _cameraPitch = 0.55f;
    private float _cameraDistance = 35.0f;
    private bool _cameraDirty = true;

    private Vector3 _cameraTarget = Vector3.Zero;

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
        UpdateSceneConstants();

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
        _cameraDistance = System.Math.Clamp(_cameraDistance, 5.0f, 300.0f);
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
        UpdateSceneConstants();
    }

    public void Render()
    {
        if (_context is null || _rtv is null || _swapChain is null)
            return;

        float dt = GetDeltaTimeSeconds();
        UpdateShells(dt);
        UpdateParticles(dt);

        if (_cameraDirty)
        {
            UpdateSceneConstants();
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

        DrawCanister();

        DrawShells();

        DrawParticles(additive: true);
        DrawParticles(additive: false);

        // Present
        _swapChain.Present(1, PresentFlags.None);
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

    private void UpdateShells(float dt)
    {
        if (dt <= 0)
            return;

        Vector3 gravity = new(0.0f, -9.81f, 0.0f);

        for (int i = 0; i < _shells.Count; i++)
        {
            var s = _shells[i];
            if (!s.Alive)
                continue;

            float prevVy = s.Velocity.Y;

            s.Velocity += gravity * dt;
            s.Position += s.Velocity * dt;
            s.Age += dt;

            // Burst trigger: apex (vy crosses 0) OR fuse timer.
            bool crossedApex = (prevVy > 0.0f && s.Velocity.Y <= 0.0f);
            bool fuseExpired = (s.Age >= s.Fuse);
            if (!s.Bursted && (crossedApex || fuseExpired))
            {
                SpawnBurst(s.Position, RandomBurstColor(), count: 6000);
                s.Bursted = true;

                // IMPORTANT: shells are purely a launcher; after bursting they should not be
                // subject to further lifetime/ground-kill logic in this pass.
                s.Alive = false;
                _shells[i] = s;
                continue;
            }

            // kill if it hits the ground or too old (placeholder)
            if (s.Position.Y <= 0.0f || s.Age > 20.0f)
            {
                s.Alive = false;
            }

            _shells[i] = s;
        }
    }

    public void SpawnShell()
    {
        // Spawn at canister mouth (center of pad)
        Vector3 spawn = new(0.0f, 0.30f, 0.0f);

        // Choose desired apex height in meters
        float apex = 150.0f + (float)_rng.NextDouble() * 50.0f; // 150-200
        float vy = (float)System.Math.Sqrt(2.0 * 9.81 * apex);

        // Small random deviation from vertical (radians)
        float maxAngle = (float)(5.0 * System.Math.PI / 180.0); // 5 degrees
        float yaw = (float)(_rng.NextDouble() * System.Math.PI * 2.0);
        float pitch = (float)(_rng.NextDouble() * maxAngle);
        float sinP = (float)System.Math.Sin(pitch);
        float cosP = (float)System.Math.Cos(pitch);

        Vector3 dir = new(
            sinP * (float)System.Math.Cos(yaw),
            cosP,
            sinP * (float)System.Math.Sin(yaw));

        // Scale so vertical component matches vy
        float speed = vy / System.Math.Max(0.001f, dir.Y);
        Vector3 vel = dir * speed;

        // Fuse is a backup in case numerical conditions miss apex.
        float fuse = 3.0f + (float)_rng.NextDouble() * 2.0f;
        _shells.Add(new ShellState { Position = spawn, Velocity = vel, Age = 0.0f, Fuse = fuse, Alive = true, Bursted = false });
    }

    private Vector4 RandomBurstColor()
    {
        // gold, blue, red, green, magenta, white
        return _rng.Next(6) switch
        {
            0 => new Vector4(1.0f, 0.80f, 0.25f, 1.0f),
            1 => new Vector4(0.25f, 0.55f, 1.00f, 1.0f),
            2 => new Vector4(1.00f, 0.20f, 0.15f, 1.0f),
            3 => new Vector4(0.20f, 1.00f, 0.35f, 1.0f),
            4 => new Vector4(1.00f, 0.20f, 1.00f, 1.0f),
            _ => new Vector4(1.00f, 1.00f, 1.00f, 1.0f)
        };
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

            float lifetime = 2.0f + (float)_rng.NextDouble() * 2.0f;

            staging[i] = new GpuParticle
            {
                Position = position,
                Velocity = vel,
                Age = 0.0f,
                Lifetime = lifetime,
                Color = baseColor,
                Kind = (uint)ParticleKind.Spark
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

    private void UpdateParticles(float dt)
    {
        if (_context is null || _particlesCS is null || _particleUAV is null || _frameCB is null)
            return;

        // Camera basis from view matrix (world-space).
        // view is orthonormal: right = (M11,M21,M31), up=(M12,M22,M32)
        var right = new Vector3(_view.M11, _view.M21, _view.M31);
        var up = new Vector3(_view.M12, _view.M22, _view.M32);

        var vp = Matrix4x4.Transpose(_view * _proj);

        var frame = new FrameCBData
        {
            ViewProjection = vp,
            CameraRightWS = right,
            DeltaTime = dt,
            CameraUpWS = up,
            Time = (float)(Environment.TickCount64 / 1000.0)
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

        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetInputLayout(null);
        _context.IASetVertexBuffers(0, 0, Array.Empty<ID3D11Buffer>(), Array.Empty<uint>(), Array.Empty<uint>());

        _context.OMSetDepthStencilState(_depthReadNoWrite, 0);
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
            if (!s.Alive)
                continue;

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

    private void DrawCanister()
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

        _context.Draw((uint)_canisterVertexCount, 0);
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

    private void UpdateSceneConstants()
    {
        if (_context is null || _sceneCB is null)
            return;

        float aspect = _height > 0 ? (float)_width / _height : 1.0f;

        var up = Vector3.UnitY;

        float cy = (float)System.Math.Cos(_cameraYaw);
        float sy = (float)System.Math.Sin(_cameraYaw);
        float cp = (float)System.Math.Cos(_cameraPitch);
        float sp = (float)System.Math.Sin(_cameraPitch);

        // Spherical coordinates: forward is +Z
        var eyeOffset = new Vector3(sy * cp, sp, cy * cp) * _cameraDistance;

        var target = _cameraTarget;
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
