using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using DiscordScreenshareLoopbackShutup.Models.Configurations;
using DiscordScreenshareLoopbackShutup.Services;
using Nito.AsyncEx.Interop;
using TruePath;

namespace DiscordScreenshareLoopbackShutup;

sealed class Program
{
    public static string Name => "DiscordScreenshareLoopbackShutup";

    public static ShutupService ShutupService { get; private set; } = null!;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        InstallerService.DoInstall();

        using var evt = new EventWaitHandle(false, EventResetMode.AutoReset,
            Name, out var createdNew);

        DoIpc(evt, createdNew);

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();

    private static void DoIpc(EventWaitHandle evt, bool createdNew)
    {
        if (!createdNew)
        {
            evt.Set();
            Environment.Exit(0);
        }

        ShutupService = new ShutupService();
        ShutupService.SetDiscordOutputDevice(Configuration.Current.DiscordOutputDeviceId);

        evt.WaitOne();

        Task.Run(async () =>
        {
            while (true)
            {
                await WaitHandleAsyncFactory.FromWaitHandle(evt);
                Dispatcher.UIThread.Post(() =>
                {
                    var classicDesktopStyleApplicationLifetime =
                        (ClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                    classicDesktopStyleApplicationLifetime!.MainWindow!.Show();
                });
            }
            // ReSharper disable once FunctionNeverReturns
        });
    }

    public static AbsolutePath GetAppropriateProgramFolderPath()
    {
#if DEBUG
        return new AbsolutePath(Environment.ProcessPath!).Parent!.Value;
#endif
        return new AbsolutePath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) /
               Name;
    }
}