using System;
using Avalonia;
using Avalonia.Controls;

namespace DiscordScreenshareLoopbackShutup.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Deactivated += OnDeactivated;
        LayoutUpdated += OnLayoutUpdated;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Hide();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdatePosition();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (IsVisible)
            UpdatePosition();
    }

    private void UpdatePosition()
    {
        var screen = Screens.ScreenFromWindow(this);
        if (screen is null)
            return;

        var workingArea = screen.WorkingArea;
        var remainingWidth = workingArea.Width - Bounds.Width * screen.Scaling;
        var x = workingArea.X + remainingWidth * 0.9;
        var y = workingArea.Bottom - Bounds.Height * screen.Scaling;
        var position = new PixelPoint((int)x, (int)y);
        if (Position != position)
            Position = position;
    }
}