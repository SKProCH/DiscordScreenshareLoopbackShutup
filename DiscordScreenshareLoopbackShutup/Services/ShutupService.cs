using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using DiscordScreenshareLoopbackShutup.Models;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DiscordScreenshareLoopbackShutup.Services;

public class ShutupService
{
    private readonly AudioDeviceService _audioDeviceService;
    private readonly BehaviorSubject<IReadOnlyList<AudioDeviceShutupInformation>> _audioDevicesStatuses = new([]);
    private readonly Lock _deviceEventsSync = new();
    private readonly Lock _enumerationSync = new();
    private readonly ILogger<ShutupService> _logger;
    private string _defaultOutputDeviceId = string.Empty;
    private IDisposable? _deviceEventsDisposable;
    private string? _discordOutputDeviceId = string.Empty;

    public ShutupService(AudioDeviceService audioDeviceService, ILogger<ShutupService> logger)
    {
        _logger = logger;
        _audioDeviceService = audioDeviceService;
        _audioDeviceService.DeviceAdded += _ => ReinitializeDeviceSubscriptions();
        _audioDeviceService.DeviceRemoved += _ => ReinitializeDeviceSubscriptions();
        _audioDeviceService.DeviceStateChanged += (_, _) => ReinitializeDeviceSubscriptions();
        _audioDeviceService.PropertyValueChanged += _ => SafeEnumerateAndShutup();
        _audioDeviceService.DefaultDeviceChanged += (dataFlow, deviceRole, defaultDeviceId) =>
        {
            if (dataFlow == DataFlow.Render && deviceRole == Role.Console) SetDefaultOutputDevice(defaultDeviceId);
        };

        var defaultAudioEndpoint =
            _audioDeviceService.DeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
        SetDefaultOutputDevice(defaultAudioEndpoint.ID);
        ReinitializeDeviceSubscriptions();
    }

    public IObservable<IReadOnlyList<AudioDeviceShutupInformation>> AudioDevicesStatuses => _audioDevicesStatuses;

    private void ReinitializeDeviceSubscriptions()
    {
        lock (_deviceEventsSync)
        {
            _logger.LogInformation("Device list changed. Reinitializing devices sessions event listening");

            try
            {
                var newSubscription = _audioDeviceService.DeviceEnumerator
                    .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active | DeviceState.Disabled)
                    .Select(device =>
                        Observable.FromEvent<AudioSessionManager.SessionCreatedDelegate, IAudioSessionControl>(
                            h => (_, session) => h(session),
                            action => device.AudioSessionManager.OnSessionCreated += action,
                            action => device.AudioSessionManager.OnSessionCreated -= action))
                    .Merge()
                    .Subscribe(SafeOnSessionCreated, OnDeviceEventsError);

                // Subscribe first, then dispose the old listeners. This avoids a period
                // where a newly created audio session cannot be observed.
                var oldSubscription = _deviceEventsDisposable;
                _deviceEventsDisposable = newSubscription;
                oldSubscription?.Dispose();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to initialize audio session event listening");
            }
        }

        SafeEnumerateAndShutup();
    }

    private void OnDeviceEventsError(Exception exception)
    {
        _logger.LogError(exception, "Audio session event subscription failed; rebuilding subscriptions");
        ReinitializeDeviceSubscriptions();
    }

    private void SafeOnSessionCreated(IAudioSessionControl session)
    {
        try
        {
            _logger.LogInformation("New audio session created, checking for discord");
            EnumerateAndShutup();
        }
        catch (Exception exception)
        {
            // A stale MMDevice/IAudioSessionControl must not terminate the Rx subscription.
            _logger.LogError(exception, "Failed to process newly created audio session");
        }
    }

    private void SafeEnumerateAndShutup()
    {
        try
        {
            EnumerateAndShutup();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to enumerate audio devices and sessions");
        }
    }

    private void SetDefaultOutputDevice(string deviceId)
    {
        if (deviceId == _defaultOutputDeviceId) return;
        var device = _audioDeviceService.DeviceEnumerator.GetDevice(deviceId);
        _logger.LogInformation("Default output device changed to {DeviceName} ({DeviceId})",
            device.FriendlyName, deviceId);
        _defaultOutputDeviceId = deviceId;
        SafeEnumerateAndShutup();
    }

    public void SetDiscordOutputDevice(string? deviceId)
    {
        if (deviceId == _discordOutputDeviceId) return;
        var device = deviceId != null ? _audioDeviceService.DeviceEnumerator.GetDevice(deviceId) : null;
        _logger.LogInformation("Discord output device set to {DeviceName} ({DeviceId})",
            device?.FriendlyName, deviceId);
        _discordOutputDeviceId = deviceId;
        SafeEnumerateAndShutup();
    }

    private void EnumerateAndShutup()
    {
        lock (_enumerationSync)
        {
            _logger.LogInformation("Enumerating devices, finding discord");
            var endpoints = _audioDeviceService.DeviceEnumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            var information = new List<AudioDeviceShutupInformation>(endpoints.Count);
            foreach (var endpoint in endpoints)
                try
                {
                    var status = ProcessEndpoint(endpoint);
                    information.Add(new AudioDeviceShutupInformation(endpoint.ID, endpoint.FriendlyName, status));
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Failed to process audio endpoint {DeviceId}", endpoint.ID);
                }

            _audioDevicesStatuses.OnNext(information);
        }

        ShutupStatus ProcessEndpoint(MMDevice endpoint)
        {
            var isAllowed = endpoint.ID == _discordOutputDeviceId || endpoint.ID == _defaultOutputDeviceId;

            var discordFound = false;
            var sessions = endpoint.AudioSessionManager.Sessions;
            for (var i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];
                    var name = session.DisplayName;
                    if (string.IsNullOrEmpty(name) && session.GetProcessID > 0)
                        try
                        {
                            using var process = Process.GetProcessById((int)session.GetProcessID);
                            name = process.ProcessName;
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                        catch (InvalidOperationException)
                        {
                            continue;
                        }

                    // ReSharper disable once InvertIf
                    if (name.StartsWith("Discord", StringComparison.OrdinalIgnoreCase))
                    {
                        if (session.SimpleAudioVolume.Mute != !isAllowed)
                        {
                            _logger.LogInformation(
                                "New Discord session detected on {DeviceName} ({DeviceId}), muted: {IsMuted}",
                                endpoint.FriendlyName, endpoint.ID, !isAllowed);
                            session.SimpleAudioVolume.Mute = !isAllowed;
                        }

                        discordFound = true;
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogDebug(exception, "Failed to inspect an audio session on {DeviceId}", endpoint.ID);
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