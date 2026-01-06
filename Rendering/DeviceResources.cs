using System;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;

namespace FireworksApp.Rendering;

internal sealed class DeviceResources : IDisposable
{
    private readonly nint _hwnd;

    public ID3D11Device? Device { get; private set; }
    public ID3D11DeviceContext? Context { get; private set; }
    public IDXGISwapChain? SwapChain { get; private set; }
    public ID3D11RenderTargetView? RenderTargetView { get; private set; }
    public ID3D11Texture2D? DepthTexture { get; private set; }
    public ID3D11DepthStencilView? DepthStencilView { get; private set; }

    public DeviceResources(nint hwnd)
    {
        _hwnd = hwnd;
    }

    public void CreateDeviceAndSwapChain(int width, int height)
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
            device: out var device,
            immediateContext: out var context);

        Device = device;
        Context = context;

        using var dxgiDevice = Device.QueryInterface<IDXGIDevice>();
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

        SwapChain = factory.CreateSwapChainForHwnd(Device, _hwnd, desc);
    }

    public void CreateRenderTarget()
    {
        if (Device is null || SwapChain is null)
            return;

        RenderTargetView?.Dispose();
        using var backBuffer = SwapChain.GetBuffer<ID3D11Texture2D>(0);
        RenderTargetView = Device.CreateRenderTargetView(backBuffer);
    }

    public void CreateDepthStencil(int width, int height)
    {
        if (Device is null)
            return;

        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);

        DepthTexture?.Dispose();
        DepthTexture = Device.CreateTexture2D(new Texture2DDescription
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

        DepthStencilView?.Dispose();
        DepthStencilView = Device.CreateDepthStencilView(DepthTexture);
    }

    public void Resize(int width, int height)
    {
        if (SwapChain is null)
            return;

        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);

        RenderTargetView?.Dispose();
        RenderTargetView = null;
        DepthStencilView?.Dispose();
        DepthStencilView = null;
        DepthTexture?.Dispose();
        DepthTexture = null;

        SwapChain.ResizeBuffers(0, (uint)width, (uint)height, Format.Unknown, SwapChainFlags.None);

        CreateRenderTarget();
        CreateDepthStencil(width, height);
    }

    public void Dispose()
    {
        DepthStencilView?.Dispose();
        DepthStencilView = null;
        DepthTexture?.Dispose();
        DepthTexture = null;
        RenderTargetView?.Dispose();
        RenderTargetView = null;
        SwapChain?.Dispose();
        SwapChain = null;
        Context?.Dispose();
        Context = null;
        Device?.Dispose();
        Device = null;
    }
}
