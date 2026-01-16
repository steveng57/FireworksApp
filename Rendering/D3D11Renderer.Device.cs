using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;

namespace FireworksApp.Rendering;

public sealed partial class D3D11Renderer
{
    public void Initialize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

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
        width = Math.Max(1, width);
        height = Math.Max(1, height);

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

    private void SetViewport(int width, int height)
    {
        if (_context is null)
            return;

        width = Math.Max(1, width);
        height = Math.Max(1, height);

        _context.RSSetViewport(new Viewport(0, 0, width, height, 0.0f, 1.0f));

        _camera.MarkDirty();
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

    private void LoadCanisterShadersAndGeometry()
    {
        if (_device is null)
            return;

        _canisterPipeline.Initialize(_device);
    }

    private void CreateParticleSystem()
    {
        if (_device is null)
            return;

        _particlesPipeline.Initialize(_device, _particleCapacity);
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
