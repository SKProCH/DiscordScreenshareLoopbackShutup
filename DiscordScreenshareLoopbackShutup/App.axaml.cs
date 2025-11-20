using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DiscordScreenshareLoopbackShutup.ViewModels;
using DiscordScreenshareLoopbackShutup.Views;
using Microsoft.Extensions.Logging;

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
                DataContext = new MainWindowViewModel(
                    Program.ShutupService,
                    Program.LoggerFactory.CreateLogger<MainWindowViewModel>())
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}