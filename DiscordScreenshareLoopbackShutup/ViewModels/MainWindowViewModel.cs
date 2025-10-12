using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Threading;
using DiscordScreenshareLoopbackShutup.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace DiscordScreenshareLoopbackShutup.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ShutupService ShutupService { get; }

    public MainWindowViewModel(ShutupService shutupService)
    {
        ShutupService = shutupService;

        this.WhenAnyValue(model => model.SelectedDeviceId)
            .WhereNotNull()
            .Subscribe(information =>
            {
                ShutupService.SetDiscordOutputDevice(SelectedDeviceId!);
            });

        ShutupService.AudioDevicesStatuses
            .Subscribe(list =>
            {
                AudioDeviceStatuses = list;
            });
    }
    
    [Reactive]
    public partial IReadOnlyList<AudioDeviceShutupInformation> AudioDeviceStatuses { get; set; }
    
    [Reactive]
    public partial AudioDeviceShutupInformation? SelectedItem { get; set; }
    
    [Reactive]
    public partial string? SelectedDeviceId { get; set; }
}