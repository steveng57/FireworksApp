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

    // Simple orbit camera (around origin)
    private float _cameraYaw = 0.0f;
    private float _cameraPitch = 0.55f;
    private float _cameraDistance = 35.0f;
    private bool _cameraDirty = true;

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
        CreateSceneConstants();
        UpdateSceneConstants();
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
        _cameraDistance = System.Math.Clamp(_cameraDistance, 5.0f, 200.0f);
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

        // Present
        _swapChain.Present(1, PresentFlags.None);
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
    }

    private void UpdateSceneConstants()
    {
        if (_context is null || _sceneCB is null)
            return;

        float aspect = _height > 0 ? (float)_width / _height : 1.0f;

        // Orbit camera around the origin (pad center)
        var target = Vector3.Zero;
        var up = Vector3.UnitY;

        float cy = (float)System.Math.Cos(_cameraYaw);
        float sy = (float)System.Math.Sin(_cameraYaw);
        float cp = (float)System.Math.Cos(_cameraPitch);
        float sp = (float)System.Math.Sin(_cameraPitch);

        // Spherical coordinates: forward is +Z
        var eyeOffset = new Vector3(sy * cp, sp, cy * cp) * _cameraDistance;
        var eye = target + eyeOffset;

        var view = Matrix4x4.CreateLookAt(eye, target, up);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(System.Math.PI / 4.0), aspect, 0.1f, 500.0f);
        var world = Matrix4x4.Identity;

        // HLSL expects column-major by default; System.Numerics uses row-major.
        // Transpose so mul(position, WorldViewProjection) matches.
        var wvp = Matrix4x4.Transpose(world * view * proj);

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

        // --- Create a 20m x 20m quad on the ground (XZ plane, Y=0) ---
        // Two triangles making a rectangle centered at origin.
        const float half = 10.0f;
        PadVertex[] verts =
        {
            new PadVertex(-half, 0.0f, -half),
            new PadVertex( half, 0.0f, -half),
            new PadVertex(-half, 0.0f,  half),

            new PadVertex( half, 0.0f, -half),
            new PadVertex( half, 0.0f,  half),
            new PadVertex(-half, 0.0f,  half),
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
        _context.Draw(6, 0);
    }

    public void Dispose()
    {
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
