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
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Hide();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Measure(Size.Infinity);
        var screen = Screens.ScreenFromWindow(this)!;
        var x = screen.WorkingArea.Width - Bounds.Width * screen.Scaling;
        x -= x / 10;
        var y = screen.WorkingArea.Height - Bounds.Height * screen.Scaling;

        Position = new PixelPoint((int)x, (int)y);
    }
}