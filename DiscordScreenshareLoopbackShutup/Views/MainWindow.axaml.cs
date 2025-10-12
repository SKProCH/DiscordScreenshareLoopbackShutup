using System;
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
}