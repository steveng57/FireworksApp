using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FireworksApp.Audio;
using FireworksApp.Rendering;
using FireworksApp.Camera;
using FireworksApp.Simulation;

namespace FireworksApp;

/// <summary>
/// Hosts a native child HWND for Direct3D rendering inside WPF.
/// </summary>
public sealed class D3DHost : HwndHost
{
    private Point _lastMousePos;
    private bool _panning;

    private IntPtr _hwnd;
    private D3D11Renderer? _renderer;
    private FireworksEngine? _engine;
    private AudioEngine? _audio;
    private ShowScript? _defaultShow;
    private bool _started;
    private bool _renderPaused;
    private bool _graphicsInitialized;
    private bool _isDragging;
    private bool _isPanning;
    private Point _lastMouse;

    private readonly Stopwatch _frameTimer = new();
    private double _accumulatedSimSeconds;
    private bool _motionSuspended;
    //private string? _savedCameraProfileId;

    public int MouseMoveCount { get; private set; }
    public int MouseDownCount { get; private set; }
    public int MouseUpCount { get; private set; }
    public int MouseWheelCount { get; private set; }
    public int SetCursorCount { get; private set; }

    public int RendererSpawnCount => _renderer?.ShellSpawnCount ?? 0;

    private const string WindowClassName = "FireworksApp.D3DHostWindow";
    private static ushort s_classAtom;

    private static readonly NativeMethods.WndProc s_wndProc = ChildWndProc;

    public D3DHost()
    {
        Focusable = true;

        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        const int WS_CHILD = 0x40000000;
        const int WS_VISIBLE = 0x10000000;

        EnsureWindowClassRegistered();

        const int WS_TABSTOP = 0x00010000;

        _hwnd = NativeMethods.CreateWindowEx(
            0,
            WindowClassName,
            string.Empty,
            WS_CHILD | WS_VISIBLE | WS_TABSTOP,
            0,
            0,
            (int)System.Math.Max(1.0, RenderSize.Width),
            (int)System.Math.Max(1.0, RenderSize.Height),
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            GCHandle.ToIntPtr(GCHandle.Alloc(this)));

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create child HWND for D3DHost.");

        _renderer = new D3D11Renderer(_hwnd);

        // We no longer rely on parent hooks for input; the child HWND has its own WndProc.

        return new HandleRef(this, _hwnd);
    }

    private static void EnsureWindowClassRegistered()
    {
        if (s_classAtom != 0)
            return;

        var wc = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = s_wndProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = NativeMethods.GetModuleHandle(null),
            hIcon = IntPtr.Zero,
            hCursor = NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_ARROW),
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero
        };

        s_classAtom = NativeMethods.RegisterClassEx(ref wc);
        if (s_classAtom == 0)
        {
            int err = Marshal.GetLastWin32Error();
            const int ERROR_CLASS_ALREADY_EXISTS = 1410;
            if (err != ERROR_CLASS_ALREADY_EXISTS)
            {
                throw new InvalidOperationException($"Failed to register window class '{WindowClassName}'. Win32Error={err}");
            }
        }
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        Stop();

        _renderer?.Dispose();
        _renderer = null;
        _graphicsInitialized = false;

        if (hwnd.Handle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(hwnd.Handle);
        }
    }

    public void Start()
    {
        if (_renderer == null || _started)
            return;

        _renderPaused = false;

        var size = RenderSize;
        int width = (int)System.Math.Max(1.0, size.Width);
        int height = (int)System.Math.Max(1.0, size.Height);

        if (!_graphicsInitialized)
        {
            _renderer.Initialize(width, height);
            _graphicsInitialized = true;
        }

        _engine = new FireworksEngine(global::FireworksApp.Simulation.DefaultProfiles.Create());
        _defaultShow = global::FireworksApp.Simulation.DefaultShow.Create();
        _engine.LoadShow(_defaultShow);

        _audio = new AudioEngine();
        _engine.SoundEvent += OnSoundEvent;

        // Basic input for simulation testing
        var window = Window.GetWindow(this);
        window?.KeyDown += OnWindowKeyDown;

        _accumulatedSimSeconds = 0;
        _frameTimer.Restart();
        CompositionTarget.Rendering += OnRendering;
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
            return;

        _renderPaused = true;

        // Stop simulation updates immediately so visuals stop.
        CompositionTarget.Rendering -= OnRendering;

        _frameTimer.Stop();
        _accumulatedSimSeconds = 0;

        if (_engine is not null)
            _engine.SoundEvent -= OnSoundEvent;

        _audio?.Dispose();
        _audio = null;

        var window = Window.GetWindow(this);
        window?.KeyDown -= OnWindowKeyDown;

        _started = false;
    }

    public void ToggleCameraMotion()
    {
        if (_renderer is null)
            return;

        if (!_motionSuspended)
        {
            _motionSuspended = true;
            _renderer.SetCameraMotionEnabled(false);
        }
        else
        {
            _motionSuspended = false;
            _renderer.SetCameraMotionEnabled(true);
        }
    }

    public void PauseRendering()
    {
        _renderPaused = true;
    }

    public void ResumeRendering()
    {
        _renderPaused = false;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            if (_engine != null && _defaultShow != null)
                _engine.LoadShow(_defaultShow);
            e.Handled = true;
        }
    }

    private bool IsShowFinished()
    {
        if (_engine is null || _defaultShow is null)
            return false;

        if (_defaultShow.Events.Count == 0)
            return true;

        // Consider the show finished once time is past the last scheduled event.
        // Shells may still be in-flight; this just toggles which shell Space spawns.
        float lastTime = _defaultShow.Events[^1].TimeSeconds;
        return _engine.ShowTimeSeconds >= lastTime;
    }

    private static void LogShellColors(string shellId)
    {
        var profiles = DefaultProfiles.Create();
        if (!profiles.Shells.TryGetValue(shellId, out var shell))
        {
            Debug.WriteLine($"[Fireworks] Shell '{shellId}' not found.");
            return;
        }

        if (!profiles.ColorSchemes.TryGetValue(shell.ColorSchemeId, out var scheme))
        {
            Debug.WriteLine($"[Fireworks] Shell '{shellId}' uses unknown color scheme '{shell.ColorSchemeId}'.");
            return;
        }

        Debug.WriteLine($"[Fireworks] Shell '{shellId}' colorscheme '{scheme.Id}' base colors:");
        foreach (var c in scheme.BaseColors)
            Debug.WriteLine($"[Fireworks]   {c}  (A={c.A} R={c.R} G={c.G} B={c.B})");
    }

    private static Point GetMousePointFromLParam(IntPtr lParam)
    {
        // LOWORD = x, HIWORD = y (signed 16-bit)
        int v = unchecked((int)(long)lParam);
        short x = unchecked((short)(v & 0xFFFF));
        short y = unchecked((short)((v >> 16) & 0xFFFF));
        return new Point(x, y);
    }

    private static IntPtr ChildWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        const int WM_NCCREATE = 0x0081;
        const int WM_NCDESTROY = 0x0082;
        const int WM_KEYDOWN = 0x0100;
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_RBUTTONDOWN = 0x0204;
        const int WM_RBUTTONUP = 0x0205;
        const int WM_MBUTTONDOWN = 0x0207;
        const int WM_MBUTTONUP = 0x0208;
        const int WM_MOUSEMOVE = 0x0200;
        const int WM_MOUSEWHEEL = 0x020A;
        const int WM_SETCURSOR = 0x0020;
        const int VK_SPACE = 0x20;

        if (msg == WM_NCCREATE)
        {
            var cs = Marshal.PtrToStructure<NativeMethods.CREATESTRUCT>(lParam);
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, cs.lpCreateParams);
        }

        var hostPtr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);
        if (hostPtr != IntPtr.Zero)
        {
            var handle = GCHandle.FromIntPtr(hostPtr);
            if (handle.Target is D3DHost host)
            {
                switch ((int)msg)
                {
                    case WM_KEYDOWN:
                        if ((int)wParam == VK_SPACE)
                        {
                            if (host._engine != null && host._defaultShow != null)
                                host._engine.LoadShow(host._defaultShow);
                            return IntPtr.Zero;
                        }
                        break;

                    case WM_SETCURSOR:
                        host.SetCursorCount++;
                        NativeMethods.SetCursor(NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_ARROW));
                        return new IntPtr(1);

                    case WM_LBUTTONDOWN:
                        host.MouseDownCount++;
                        host._isDragging = true;
                        host._isPanning = false;
                        host._lastMouse = GetMousePointFromLParam(lParam);
                        NativeMethods.SetCapture(hWnd);
                        NativeMethods.SetFocus(hWnd);
                        return IntPtr.Zero;

                    case WM_LBUTTONUP:
                        host.MouseUpCount++;
                        host._isDragging = false;
                        host._isPanning = false;
                        NativeMethods.ReleaseCapture();
                        return IntPtr.Zero;

                    case WM_RBUTTONDOWN:
                    case WM_MBUTTONDOWN:
                        host.MouseDownCount++;
                        host._isDragging = true;
                        host._isPanning = true;
                        host._lastMouse = GetMousePointFromLParam(lParam);
                        NativeMethods.SetCapture(hWnd);
                        NativeMethods.SetFocus(hWnd);
                        return IntPtr.Zero;

                    case WM_RBUTTONUP:
                    case WM_MBUTTONUP:
                        host.MouseUpCount++;
                        host._isDragging = false;
                        host._isPanning = false;
                        NativeMethods.ReleaseCapture();
                        return IntPtr.Zero;

                    case WM_MOUSEMOVE:
                        host.MouseMoveCount++;
                        if (host._isDragging && host._renderer != null)
                        {
                            var pos = GetMousePointFromLParam(lParam);
                            var dx = (float)(pos.X - host._lastMouse.X);
                            var dy = (float)(pos.Y - host._lastMouse.Y);
                            host._lastMouse = pos;
                            if (host._isPanning)
                            {
                                host._renderer.PanCamera(dx, dy);
                            }
                            else
                            {
                                host._renderer.OnMouseDrag(dx, dy);
                            }
                        }
                        return IntPtr.Zero;

                    case WM_MOUSEWHEEL:
                        host.MouseWheelCount++;
                        if (host._renderer != null)
                        {
                            short delta = (short)((long)wParam >> 16);
                            host._renderer.OnMouseWheel(delta);
                        }
                        return IntPtr.Zero;

                    case WM_NCDESTROY:
                        handle.Free();
                        NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
                        break;
                }
            }
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    void OnRendering(object? sender, EventArgs e)
    {
        if (_renderPaused)
            return;

        if (_renderer is null)
            return;

        const double fixedStepSeconds = 1.0 / 60.0;
        const double maxFrameSeconds = 0.25;

        if (!_frameTimer.IsRunning)
            _frameTimer.Restart();

        double realDtSeconds = _frameTimer.Elapsed.TotalSeconds;
        _frameTimer.Restart();

        if (realDtSeconds < 0)
            realDtSeconds = 0;
        if (realDtSeconds > maxFrameSeconds)
            realDtSeconds = maxFrameSeconds;

        float timeScale = _engine?.TimeScale ?? 1.0f;
        double scaledFixedStepSeconds = fixedStepSeconds * timeScale;
        double scaledFrameSeconds = realDtSeconds * timeScale;
        _accumulatedSimSeconds += scaledFrameSeconds;

        int steps = 0;
        const int maxStepsPerFrame = 8;

        while (_accumulatedSimSeconds >= scaledFixedStepSeconds && steps < maxStepsPerFrame)
        {
            _engine?.UpdateUnscaled((float)scaledFixedStepSeconds, _renderer);
            _accumulatedSimSeconds -= scaledFixedStepSeconds;
            steps++;
        }

        if (steps == maxStepsPerFrame)
            _accumulatedSimSeconds = 0;

        float alpha = scaledFixedStepSeconds > 1e-9
            ? (float)(_accumulatedSimSeconds / scaledFixedStepSeconds)
            : 0.0f;
        alpha = System.Math.Clamp(alpha, 0.0f, 1.0f);

        _renderer.ApplyInterpolation(alpha);

        if (_audio is not null)
        {
            _audio.ListenerPosition = _renderer.CameraPosition;
            _audio.Update(TimeSpan.FromSeconds(scaledFrameSeconds));
        }

        _renderer.Render((float)scaledFrameSeconds);
    }

    private void OnSoundEvent(SoundEvent ev)
    {
        _audio?.Enqueue(ev);
    }


    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (_renderer != null &&
            sizeInfo.NewSize.Width > 0 &&
            sizeInfo.NewSize.Height > 0)
        {
            _renderer.Resize(
                (int)sizeInfo.NewSize.Width,
                (int)sizeInfo.NewSize.Height);
        }
    }

    private static class NativeMethods
    {
        public const int IDC_ARROW = 32512;

        public const int GWLP_USERDATA = -21;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [DllImport("user32.dll")]
        public static extern IntPtr SetCursor(IntPtr hCursor);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct CREATESTRUCT
        {
            public IntPtr lpCreateParams;
            public IntPtr hInstance;
            public IntPtr hMenu;
            public IntPtr hwndParent;
            public int cy;
            public int cx;
            public int y;
            public int x;
            public int style;
            public IntPtr lpszName;
            public IntPtr lpszClass;
            public int dwExStyle;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        

        

        [DllImport("user32.dll")]
        public static extern IntPtr SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct TRACKMOUSEEVENT
        {
            public uint cbSize;
            public uint dwFlags;
            public IntPtr hwndTrack;
            public uint dwHoverTime;
        }

        private const uint TME_LEAVE = 0x00000002;

        [DllImport("user32.dll")]
        private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT tme);

        public static void TrackMouseLeave(IntPtr hwnd)
        {
            var tme = new TRACKMOUSEEVENT
            {
                cbSize = (uint)Marshal.SizeOf<TRACKMOUSEEVENT>(),
                dwFlags = TME_LEAVE,
                hwndTrack = hwnd,
                dwHoverTime = 0
            };
            TrackMouseEvent(ref tme);
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        Point p = e.GetPosition(this);
        float dx = (float)(p.X - _lastMousePos.X);
        float dy = (float)(p.Y - _lastMousePos.Y);
        _lastMousePos = p;

        if (_panning)
        {
            _renderer?.PanCamera(dx, dy);
            e.Handled = true;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _renderer?.OnMouseDrag(dx, dy);
            e.Handled = true;
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _renderer?.OnMouseWheel(e.Delta);
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        _lastMousePos = e.GetPosition(this);

        if (e.ChangedButton == MouseButton.Middle)
        {
            _panning = true;
            CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && _panning)
        {
            _panning = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }
}
