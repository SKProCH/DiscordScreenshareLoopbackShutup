using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DiscordScreenshareLoopbackShutup.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DiscordScreenshareLoopbackShutup;

public class ShutupService
{
    private readonly AudioDeviceService _audioDeviceService;
    private readonly ReplaySubject<IReadOnlyList<AudioDeviceShutupInformation>> _audioDevicesStatuses = new();
    private string _defaultOutputDeviceId = string.Empty;
    private IDisposable? _deviceEventsDisposable;
    private string? _discordOutputDeviceId = string.Empty;

    public ShutupService()
    {
        _audioDeviceService = new AudioDeviceService();
        _audioDeviceService.DeviceAdded += _ => SubscribeToDevices();
        _audioDeviceService.DeviceRemoved += _ => SubscribeToDevices();
        _audioDeviceService.PropertyValueChanged += _ => EnumerateAndShutup();
        _audioDeviceService.DefaultDeviceChanged += (dataFlow, deviceRole, defaultDeviceId) =>
        {
            if (dataFlow == DataFlow.Render && deviceRole == Role.Console)
            {
                SetDefaultOutputDevice(defaultDeviceId);
            }
        };

        var defaultAudioEndpoint =
            _audioDeviceService.DeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
        SetDefaultOutputDevice(defaultAudioEndpoint.ID);
        SubscribeToDevices();
    }

    public IObservable<IReadOnlyList<AudioDeviceShutupInformation>> AudioDevicesStatuses => _audioDevicesStatuses;

    private void SubscribeToDevices()
    {
        _deviceEventsDisposable?.Dispose();
        _deviceEventsDisposable = _audioDeviceService.DeviceEnumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active | DeviceState.Disabled)
            .Select(device => Observable.FromEvent<AudioSessionManager.SessionCreatedDelegate, IAudioSessionControl>(
                h => (_, session) => h(session),
                action => device.AudioSessionManager.OnSessionCreated += action,
                action => device.AudioSessionManager.OnSessionCreated -= action))
            .Merge()
            .Subscribe(_ => EnumerateAndShutup());

        EnumerateAndShutup();
    }

    private void SetDefaultOutputDevice(string deviceId)
    {
        _defaultOutputDeviceId = deviceId;
        EnumerateAndShutup();
    }

    public void SetDiscordOutputDevice(string? deviceId)
    {
        _discordOutputDeviceId = deviceId;
        EnumerateAndShutup();
    }

    private void EnumerateAndShutup()
    {
        var endPoints = _audioDeviceService.DeviceEnumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        var information = new List<AudioDeviceShutupInformation>(endPoints.Count);
        foreach (var endpoint in endPoints)
        {
            var status = ProcessEndpoint(endpoint);
            information.Add(new AudioDeviceShutupInformation(endpoint.ID, endpoint.FriendlyName, status));
        }

        _audioDevicesStatuses.OnNext(information);
        return;

        ShutupStatus ProcessEndpoint(MMDevice endpoint)
        {
            var isAllowed = endpoint.ID == _discordOutputDeviceId || endpoint.ID == _defaultOutputDeviceId;

            var discordFound = false;
            var sessions = endpoint.AudioSessionManager.Sessions;
            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                var name = session.DisplayName;
                if (string.IsNullOrEmpty(name) && session.GetProcessID > 0)
                {
                    var p = Process.GetProcessById((int)session.GetProcessID);
                    name = p.ProcessName;
                }

                // ReSharper disable once InvertIf
                if (name == "Discord")
                {
                    session.SimpleAudioVolume.Mute = !isAllowed;
                    discordFound = true;
                }
            }

            return (isAllowed, discordFound) switch
            {
                (true, _) => ShutupStatus.ValidOutput,
                (_, true) => ShutupStatus.Muted,
                (_, false) => ShutupStatus.None
            };
        }
    }
}