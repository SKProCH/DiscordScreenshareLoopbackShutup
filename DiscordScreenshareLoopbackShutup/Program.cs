using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using DiscordScreenshareLoopbackShutup.Models.Configurations;
using DiscordScreenshareLoopbackShutup.Services;
using DiscordScreenshareLoopbackShutup.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx.Interop;
using Serilog;
using TruePath;

namespace DiscordScreenshareLoopbackShutup;

sealed class Program
{
    public static string Name => "DiscordScreenshareLoopbackShutup";

    public static IServiceProvider Services { get; private set; } = null!;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var configManager = new ConfigurationManager(GetAppropriateProgramFolderPath() / "config.toml");
        SetupLogging(configManager.Configuration);
        Log.Logger.Information("Application started: {Command}",
            string.Join(" ", args.Prepend(Environment.ProcessPath)));

        try
        {
            InstallerService.DoInstall();

            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset,
                Name, out var createdNew);

            if (!createdNew)
            {
                Log.Logger.Information("Found already running process, sending signal and exiting");
                evt.Set();
                Environment.Exit(0);
            }

            Services = ConfigureServices(configManager);

            // Resolve ShutupService to ensure it starts
            var shutupService = Services.GetRequiredService<ShutupService>();
            shutupService.SetDiscordOutputDevice(configManager.Configuration.DiscordOutputDeviceId);

            WaitIpcSignal(evt);

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Log.Logger.Fatal(e, "Application terminated");
        }
        finally
        {
            Log.Logger.Information("Application shutdown");
            Log.CloseAndFlush();
        }
    }

    private static ServiceProvider ConfigureServices(ConfigurationManager configurationManager)
    {
        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton(configurationManager);

        // Logging
        services.AddLogging(loggingBuilder => { loggingBuilder.AddSerilog(dispose: true); });

        // Services
        services.AddSingleton<AudioDeviceService>();
        services.AddSingleton<ShutupService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();

    private static void WaitIpcSignal(EventWaitHandle evt)
    {
        Log.Logger.Information("Waiting for IPC signal to initialize UI");
        evt.WaitOne();
        Log.Logger.Information("IPC signal received, showing window");

        Task.Run(async () =>
        {
            while (true)
            {
                await WaitHandleAsyncFactory.FromWaitHandle(evt);
                Dispatcher.UIThread.Post(() =>
                {
                    var classicDesktopStyleApplicationLifetime =
                        (ClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                    Log.Logger.Information("IPC signal received, showing window");
                    classicDesktopStyleApplicationLifetime.MainWindow!.Show();
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

    private static void SetupLogging(IConfiguration configuration)
    {
        var sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var logFilePath = configuration.LogPath;

#if DEBUG
        logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DiscordScreenshareLoopbackShutup.log");
#else
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            logFilePath = Path.Combine(Path.GetTempPath(), "DiscordScreenshareLoopbackShutup.log");
        }
#endif

        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("SessionId", sessionId)
            .WriteTo.Console(
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SessionId}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(logFilePath, shared: true,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SessionId}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}