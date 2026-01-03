using System.Windows;
using System.Windows.Media;

namespace FireworksApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) => DxView.Start();
        Unloaded += (_, _) => DxView.Stop();

        CompositionTarget.Rendering += (_, _) =>
        {
            Title = $"FireworksApp | Down:{DxView.MouseDownCount} Up:{DxView.MouseUpCount} Move:{DxView.MouseMoveCount} Wheel:{DxView.MouseWheelCount} SetCursor:{DxView.SetCursorCount} Shells:{DxView.RendererSpawnCount}";
        };
    }
}
