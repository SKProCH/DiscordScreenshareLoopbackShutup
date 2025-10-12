using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DiscordScreenshareLoopbackShutup.ViewModels;
using DiscordScreenshareLoopbackShutup.Views;
using TruePath;

namespace DiscordScreenshareLoopbackShutup;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(new ShutupService()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static AbsolutePath GetAppropriateProgramFolderPath()
    {
#if DEBUG
        return new AbsolutePath(Environment.ProcessPath!).Parent!.Value;
#endif
        return new AbsolutePath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) / "DiscordScreenshareLoopbackShutup";
    }
}