using System;
using System.Collections.Generic;
using DiscordScreenshareLoopbackShutup.Models;
using DiscordScreenshareLoopbackShutup.Models.Configurations;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace DiscordScreenshareLoopbackShutup.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConfigurationManager _configurationManager;
    private readonly ShutupService ShutupService;

    public MainWindowViewModel(ShutupService shutupService, ConfigurationManager configurationManager,
        ILogger<MainWindowViewModel> logger)
    {
        ShutupService = shutupService;
        _configurationManager = configurationManager;

        this.WhenAnyValue(model => model.SelectedDeviceId)
            .WhereNotNull()
            .Subscribe(deviceId =>
            {
                logger.LogInformation("User selected device: {DeviceId}", deviceId);
                ShutupService.SetDiscordOutputDevice(deviceId);
                _configurationManager.Edit(configuration => configuration.DiscordOutputDeviceId = deviceId);
            });

        ShutupService.AudioDevicesStatuses
            .Subscribe(list => { AudioDeviceStatuses = list; });

        SelectedDeviceId = _configurationManager.Configuration.DiscordOutputDeviceId;
    }

    [Reactive] public partial IReadOnlyList<AudioDeviceShutupInformation> AudioDeviceStatuses { get; set; }

    [Reactive] public partial string? SelectedDeviceId { get; set; }
}