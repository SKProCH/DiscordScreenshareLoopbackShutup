using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Threading;
using DiscordScreenshareLoopbackShutup.Models;
using DiscordScreenshareLoopbackShutup.Models.Configurations;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace DiscordScreenshareLoopbackShutup.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ShutupService ShutupService { get; }

    public MainWindowViewModel(ShutupService shutupService, ILogger<MainWindowViewModel> logger)
    {
        ShutupService = shutupService;

        this.WhenAnyValue(model => model.SelectedDeviceId)
            .WhereNotNull()
            .Subscribe(deviceId =>
            {
                logger.LogInformation("User selected device: {DeviceId}", deviceId);
                ShutupService.SetDiscordOutputDevice(deviceId);
                Configuration.Edit(configuration => configuration.DiscordOutputDeviceId = deviceId);
            });

        ShutupService.AudioDevicesStatuses
            .Subscribe(list =>
            {
                AudioDeviceStatuses = list;
            });

        SelectedDeviceId = Configuration.Current.DiscordOutputDeviceId;
    }
    
    [Reactive]
    public partial IReadOnlyList<AudioDeviceShutupInformation> AudioDeviceStatuses { get; set; }
    
    [Reactive]
    public partial string? SelectedDeviceId { get; set; }
}