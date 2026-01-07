using System;
using System.Windows;

namespace FireworksApp;

public partial class OverlayToolbarWindow : Window
{
    public event EventHandler? StartClicked;
    public event EventHandler? StopClicked;

    public OverlayToolbarWindow()
    {
        InitializeComponent();

        StartButton.Click += (_, _) => StartClicked?.Invoke(this, EventArgs.Empty);
        StopButton.Click += (_, _) => StopClicked?.Invoke(this, EventArgs.Empty);
    }
}
