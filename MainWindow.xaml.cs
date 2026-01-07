using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace FireworksApp;

public partial class MainWindow : Window
{
    private OverlayToolbarWindow? _toolbarWindow;
    private HwndSource? _hwndSource;
    private bool _inSizeMove;

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);
        };

        Loaded += (_, _) =>
        {
            _toolbarWindow ??= new OverlayToolbarWindow
            {
                Owner = this
            };
            _toolbarWindow.Loaded += (_, _) => UpdateToolbarLocation();
            _toolbarWindow.StartClicked += (_, _) => DxView.Start();
            _toolbarWindow.StopClicked += (_, _) => DxView.Stop();

            DxView.Start();
            _toolbarWindow.Show();
            UpdateToolbarLocation();
        };

        Unloaded += (_, _) =>
        {
            DxView.Stop();
            _toolbarWindow?.Close();
            _toolbarWindow = null;
        };

        LocationChanged += (_, _) => UpdateToolbarLocation();
        SizeChanged += (_, _) => UpdateToolbarLocation();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                _toolbarWindow?.Hide();
            else
            {
                if (_toolbarWindow is not null)
                {
                    if (_toolbarWindow.IsVisible == false)
                        _toolbarWindow.Show();
                }
                UpdateToolbarLocation();
            }
        };

        CompositionTarget.Rendering += (_, _) =>
        {
            Title = $"FireworksApp | Down:{DxView.MouseDownCount} Up:{DxView.MouseUpCount} Move:{DxView.MouseMoveCount} Wheel:{DxView.MouseWheelCount} SetCursor:{DxView.SetCursorCount} Shells:{DxView.RendererSpawnCount}";
        };
    }

    private void UpdateToolbarLocation()
    {
        if (!IsLoaded || WindowState == WindowState.Minimized)
            return;

   //     if (_inSizeMove)
   //         return;

        if (_toolbarWindow is null)
            return;

        // PointToScreen() returns WPF DIPs. Under DPI scaling we need physical pixels for the overlay window.
        // Use Win32 ClientToScreen with WPF->device transform to avoid double-scaling / drift while dragging.
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
            return;

        var m = source.CompositionTarget.TransformToDevice;
        var clientH = System.Math.Max(0, DxView.ActualHeight);

        var pt = new POINT
        {
            X = 0,
            Y = (int)System.Math.Round(clientH * m.M22)
        };

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == nint.Zero)
            return;

        if (!ClientToScreen(hwnd, ref pt))
            return;

        var bottomLeft = new Point(pt.X, pt.Y);

        const double inset = 12;
        // bottomLeft is in physical pixels; WPF window coords are in DIPs.
        var insetX = inset;
        var insetY = inset;
        _toolbarWindow.Left = (bottomLeft.X / m.M11) + insetX;
        var height = _toolbarWindow.ActualHeight;
        if (height <= 0)
        {
            _toolbarWindow.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            height = _toolbarWindow.DesiredSize.Height;
        }

        _toolbarWindow.Top = (bottomLeft.Y / m.M22) - height - insetY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(nint hWnd, ref POINT lpPoint);

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        const int WM_ENTERSIZEMOVE = 0x0231;
        const int WM_EXITSIZEMOVE = 0x0232;

        switch (msg)
        {
            case WM_ENTERSIZEMOVE:
                _inSizeMove = true;
                DxView.PauseRendering();
                break;
            case WM_EXITSIZEMOVE:
                _inSizeMove = false;
                DxView.ResumeRendering();
                UpdateToolbarLocation();
                break;
        }

        return 0;
    }
}
