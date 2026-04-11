using System;
using System.Collections.Generic;
using DiscordScreenshareLoopbackShutup.Models;
using DiscordScreenshareLoopbackShutup.Models.Configurations;
using DiscordScreenshareLoopbackShutup.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace DiscordScreenshareLoopbackShutup.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(ShutupService shutupService, ConfigurationManager configurationManager,
        ILogger<MainWindowViewModel> logger)
    {
        this.WhenAnyValue(model => model.SelectedDeviceId)
            .WhereNotNull()
            .Subscribe(deviceId =>
            {
                logger.LogInformation("User selected device: {DeviceId}", deviceId);
                shutupService.SetDiscordOutputDevice(deviceId);
                configurationManager.Edit(configuration => configuration.DiscordOutputDeviceId = deviceId);
            });

        shutupService.AudioDevicesStatuses
            .BindTo(this, model => model.AudioDeviceStatuses);

        SelectedDeviceId = configurationManager.Configuration.DiscordOutputDeviceId;
    }

    [Reactive] public partial IReadOnlyList<AudioDeviceShutupInformation> AudioDeviceStatuses { get; set; }

    [Reactive] public partial string? SelectedDeviceId { get; set; }
}