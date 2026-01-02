using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FireworksSkeleton.Rendering;

namespace FireworksSkeleton;

public partial class MainWindow : Window
{
    private D3D11Renderer? _renderer;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;
        CompositionTarget.Rendering += OnRender;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _renderer = new D3D11Renderer(hwnd);
        _renderer.Initialize((int)ActualWidth, (int)ActualHeight);
    }

    private void OnRender(object? sender, EventArgs e)
    {
        _renderer?.Render();
    }

    private void OnClosing(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRender;
        _renderer?.Dispose();
    }
}
