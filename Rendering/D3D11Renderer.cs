using System;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;

namespace FireworksSkeleton.Rendering;

public sealed class D3D11Renderer : IDisposable
{
    private readonly nint _hwnd;

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain? _swapChain;
    private ID3D11RenderTargetView? _rtv;

    public D3D11Renderer(nint hwnd)
    {
        _hwnd = hwnd;
    }

    public void Initialize(int width, int height)
    {
        CreateDeviceAndSwapChain(width, height);
        CreateRenderTarget();
    }

    public void Render()
    {
        if (_context is null || _rtv is null || _swapChain is null)
            return;

        _context.OMSetRenderTargets(_rtv, null);
        _context.ClearRenderTargetView(_rtv, new(0.02f, 0.02f, 0.05f, 1.0f));
        _swapChain.Present(1, PresentFlags.None);
    }

    private void CreateDeviceAndSwapChain(int width, int height)
    {
        D3D11CreateDevice(
            adapter: null,
            driverType: DriverType.Hardware,
            flags: DeviceCreationFlags.BgraSupport,
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
            SwapEffect = SwapEffect.FlipDiscard
        };

        _swapChain = factory.CreateSwapChainForHwnd(_device, _hwnd, desc);
    }

    private void CreateRenderTarget()
    {
        using var backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
        _rtv = _device!.CreateRenderTargetView(backBuffer);
    }

    public void Dispose()
    {
        _context?.ClearState();
        _context?.Flush();

        _rtv?.Dispose();
        _swapChain?.Dispose();
        _context?.Dispose();
        _device?.Dispose();
    }
}
