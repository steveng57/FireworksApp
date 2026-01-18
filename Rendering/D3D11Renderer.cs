using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;

namespace FireworksApp.Rendering;

using FireworksApp.Camera;
using FireworksApp.Simulation;

public sealed partial class D3D11Renderer : IDisposable
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

    private readonly PadPipeline _padPipeline = new();
    private ID3D11RasterizerState? _padRasterizerState;

    private readonly GroundPipeline _groundPipeline = new();
    private readonly BleachersPipeline _bleachersPipeline = new();
    private ID3D11Buffer? _sceneCB;
    private ID3D11Buffer? _lightingCB;
    private ID3D11Buffer? _objectCB;

    private readonly CanisterPipeline _canisterPipeline = new();
    private readonly ShellPipeline _shellPipeline = new();

    private Matrix4x4[] _bleacherWorlds = Array.Empty<Matrix4x4>();

    private System.Collections.Generic.IReadOnlyList<CanisterRenderState> _canisters = Array.Empty<CanisterRenderState>();
    private System.Collections.Generic.IReadOnlyList<ShellRenderState> _shells = Array.Empty<ShellRenderState>();
    private System.Collections.Generic.IReadOnlyList<CanisterRenderState> _prevCanisters = Array.Empty<CanisterRenderState>();
    private System.Collections.Generic.IReadOnlyList<ShellRenderState> _prevShells = Array.Empty<ShellRenderState>();

    private ShellRenderState[]? _interpShells;
    private CanisterRenderState[]? _interpCanisters;
    private bool _hasInterpolatedState;
    private long _lastTick;
    private readonly Random _rng = new();

    private const int UnitVectorTableSize = 4096;
    private static readonly Vector3[] s_unitVectorTable = CreateUnitVectorTable();

    private static int ShellTrailParticlesPerSecond = 220;
    private static float ShellTrailLifetimeSeconds = 0.37f;
    private static float ShellTrailSpeed = 0.23f;

    private readonly ParticlesPipeline _particlesPipeline = new();
    private readonly int _particleCapacity = ParticleKindBudget.GetTotalCapacity();
    private int _particleWriteCursor;

    private readonly PerfTelemetry _perf = new();

    private static readonly ArrayPool<GpuParticle> s_particleArrayPool = ArrayPool<GpuParticle>.Shared;
    private static readonly int GpuParticleStride = Marshal.SizeOf<GpuParticle>();

    private Matrix4x4 _view;
    private Matrix4x4 _proj;

    public int ShellSpawnCount { get; set; }
    public bool ShellsGpuRendered => _particlesPipeline.CanGpuSpawn;

    private Vector3 _schemeTint = Vector3.One;

    private readonly CameraController _camera = new();
    public Vector3 CameraPosition { get; private set; }
    public string CurrentCameraProfileId => _camera.Profile.Id;
    public bool CameraMotionEnabled => _camera.MotionEnabled;

    public D3D11Renderer(nint hwnd)
    {
        _hwnd = hwnd;
        _deviceResources = new DeviceResources(hwnd);
        _camera.SetProfile(Tunables.DefaultCameraProfileId);
    }

    public void SetCameraMotionEnabled(bool enabled)
    {
        _camera.SetMotionEnabled(enabled);
    }

    public void SetCameraProfile(string profileId)
    {
        _camera.SetProfile(profileId);
    }

    public void SetCameraProfile(CameraProfile profile)
    {
        _camera.SetProfile(profile);
    }

    public int ReadDetonations(Span<DetonationEvent> destination)
    {
        if (_context is null)
            return 0;

        return _particlesPipeline.ReadDetonations(_context, destination);
    }

    private static GpuParticle[] RentParticleArray(int count)
        => count <= 0 ? Array.Empty<GpuParticle>() : s_particleArrayPool.Rent(count);

    private static void ReturnParticleArray(GpuParticle[]? buffer)
    {
        if (buffer is null || buffer.Length == 0)
            return;

        s_particleArrayPool.Return(buffer, clearArray: false);
    }

    private static Vector3[] CreateUnitVectorTable()
    {
        var rng = new Random(1234567);
        var table = new Vector3[UnitVectorTableSize];
        for (int i = 0; i < table.Length; i++)
        {
            float z = (float)(rng.NextDouble() * 2.0 - 1.0);
            float a = (float)(rng.NextDouble() * Math.PI * 2.0);
            float r = (float)Math.Sqrt(Math.Max(0.0, 1.0 - z * z));
            table[i] = new Vector3(r * (float)Math.Cos(a), z, r * (float)Math.Sin(a));
        }
        return table;
    }
}
