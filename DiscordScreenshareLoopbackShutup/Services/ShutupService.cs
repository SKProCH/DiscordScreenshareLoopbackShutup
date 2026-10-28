using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Threading;
using DiscordScreenshareLoopbackShutup.Models;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DiscordScreenshareLoopbackShutup.Services;

public sealed class ShutupService : IDisposable
{
    private readonly AudioDeviceService _audioDeviceService;
    private readonly BehaviorSubject<IReadOnlyList<AudioDeviceShutupInformation>> _audioDevicesStatuses = new([]);
    private readonly Lock _deviceEventsSync = new();
    private readonly Lock _enumerationSync = new();
    private readonly ILogger<ShutupService> _logger;
    private string _defaultOutputDeviceId = string.Empty;
    private string? _discordOutputDeviceId;
    private AudioSessionSubscriptionSet? _sessionSubscriptions;

    public ShutupService(AudioDeviceService audioDeviceService, ILogger<ShutupService> logger)
    {
        _audioDeviceService = audioDeviceService;
        _logger = logger;

        SubscribeToDeviceEvents();

        RefreshDefaultOutputDevice();
        RebuildSessionSubscriptions();
    }

    public IObservable<IReadOnlyList<AudioDeviceShutupInformation>> AudioDevicesStatuses => _audioDevicesStatuses;

    public void Dispose()
    {
        UnsubscribeFromDeviceEvents();

        lock (_deviceEventsSync)
        {
            _sessionSubscriptions?.Dispose();
            _sessionSubscriptions = null;
        }

        _audioDevicesStatuses.Dispose();
    }

    public void SetDiscordOutputDevice(string? deviceId)
    {
        if (deviceId == _discordOutputDeviceId)
            return;

        _logger.LogInformation("Discord output device set to {DeviceId}", deviceId);

        _discordOutputDeviceId = deviceId;
        RefreshStatusesSafely();
    }

    private void SubscribeToDeviceEvents()
    {
        _audioDeviceService.DeviceAdded += OnDeviceAdded;
        _audioDeviceService.DeviceRemoved += OnDeviceRemoved;
        _audioDeviceService.DeviceStateChanged += OnDeviceStateChanged;
        _audioDeviceService.PropertyValueChanged += OnPropertyValueChanged;
        _audioDeviceService.DefaultDeviceChanged += OnDefaultDeviceChanged;
    }

    private void UnsubscribeFromDeviceEvents()
    {
        _audioDeviceService.DeviceAdded -= OnDeviceAdded;
        _audioDeviceService.DeviceRemoved -= OnDeviceRemoved;
        _audioDeviceService.DeviceStateChanged -= OnDeviceStateChanged;
        _audioDeviceService.PropertyValueChanged -= OnPropertyValueChanged;
        _audioDeviceService.DefaultDeviceChanged -= OnDefaultDeviceChanged;
    }

    private void OnDeviceAdded(string _)
    {
        RebuildSessionSubscriptions();
    }

    private void OnDeviceRemoved(string _)
    {
        RebuildSessionSubscriptions();
    }

    private void OnDeviceStateChanged(string _, DeviceState __)
    {
        RebuildSessionSubscriptions();
    }

    private void OnPropertyValueChanged(string _)
    {
        RefreshStatusesSafely();
    }

    private void OnDefaultDeviceChanged(DataFlow dataFlow, Role deviceRole, string deviceId)
    {
        if (dataFlow == DataFlow.Render && deviceRole == Role.Console)
            RefreshDefaultOutputDevice();
    }

    private void RebuildSessionSubscriptions()
    {
        lock (_deviceEventsSync)
        {
            _logger.LogInformation("Device list changed. Rebuilding audio session subscriptions");

            try
            {
                var replacement = AudioSessionSubscriptionSet.Create(
                    _audioDeviceService.DeviceEnumerator,
                    OnSessionCreated,
                    OnSessionSubscriptionError);

                var previous = _sessionSubscriptions;
                _sessionSubscriptions = replacement;
                previous?.Dispose();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to initialize audio session event listening");
            }
        }

        RefreshStatusesSafely();
    }

    private void OnSessionSubscriptionError(Exception exception)
    {
        _logger.LogError(exception, "Audio session subscription failed; rebuilding subscriptions");
        RebuildSessionSubscriptions();
    }

    private void OnSessionCreated(IAudioSessionControl _)
    {
        _logger.LogInformation("New audio session created, checking for Discord");
        RefreshStatusesSafely();
    }

    private void RefreshDefaultOutputDevice()
    {
        try
        {
            using var device = _audioDeviceService.DeviceEnumerator
                .GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            SetDefaultOutputDevice(device.ID);
        }
        catch (COMException exception) when (exception.HResult == unchecked((int)0x80070490))
        {
            _logger.LogInformation("No default output device is available; waiting for device changes");
            SetDefaultOutputDevice(string.Empty);
        }
    }

    private void SetDefaultOutputDevice(string deviceId)
    {
        if (deviceId == _defaultOutputDeviceId)
            return;

        _logger.LogInformation("Default output device changed to {DeviceId}", deviceId);

        _defaultOutputDeviceId = deviceId;
        RefreshStatusesSafely();
    }

    private void RefreshStatusesSafely()
    {
        try
        {
            lock (_enumerationSync)
            {
                _logger.LogInformation("Enumerating devices, finding Discord");

                var endpoints = _audioDeviceService.DeviceEnumerator
                    .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                var statuses = new List<AudioDeviceShutupInformation>(endpoints.Count);

                foreach (var endpoint in endpoints)
                    using (endpoint)
                    {
                        try
                        {
                            statuses.Add(InspectDevice(endpoint));
                        }
                        catch (Exception exception)
                        {
                            _logger.LogWarning(exception, "Failed to process audio endpoint {DeviceId}", endpoint.ID);
                        }
                    }

                _audioDevicesStatuses.OnNext(statuses);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to enumerate audio devices and sessions");
        }
    }

    private AudioDeviceShutupInformation InspectDevice(MMDevice device)
    {
        var isAllowed = IsAllowedDevice(device);
        var discordFound = false;
        var sessions = device.AudioSessionManager.Sessions;

        for (var i = 0; i < sessions.Count; i++)
            try
            {
                using var session = sessions[i];
                if (!TryGetSessionName(session, out var name) ||
                    !name.StartsWith("Discord", StringComparison.OrdinalIgnoreCase))
                    continue;

                SetMuteState(session, device, isAllowed);
                discordFound = true;
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Failed to inspect an audio session on {DeviceId}", device.ID);
            }

        var status = (isAllowed, discordFound) switch
        {
            (true, _) => ShutupStatus.ValidOutput,
            (_, true) => ShutupStatus.Muted,
            (_, false) => ShutupStatus.None
        };

        return new AudioDeviceShutupInformation(device.ID, device.FriendlyName, status);
    }

    private bool IsAllowedDevice(MMDevice device)
    {
        return device.ID == _discordOutputDeviceId || device.ID == _defaultOutputDeviceId;
    }

    private bool TryGetSessionName(AudioSessionControl session, out string name)
    {
        name = session.DisplayName;
        if (!string.IsNullOrEmpty(name))
            return true;

        if (session.GetProcessID == 0)
            return false;

        try
        {
            using var process = Process.GetProcessById((int)session.GetProcessID);
            name = process.ProcessName;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void SetMuteState(AudioSessionControl session, MMDevice device, bool isAllowed)
    {
        var shouldMute = !isAllowed;
        if (session.SimpleAudioVolume.Mute == shouldMute)
            return;

        _logger.LogInformation(
            "New Discord session detected on {DeviceName} ({DeviceId}), muted: {IsMuted}",
            device.FriendlyName, device.ID, shouldMute);
        session.SimpleAudioVolume.Mute = shouldMute;
    }

    private sealed class AudioSessionSubscriptionSet : IDisposable
    {
        private readonly IReadOnlyList<MMDevice> _devices;
        private readonly IDisposable _subscription;

        private AudioSessionSubscriptionSet(IReadOnlyList<MMDevice> devices, IDisposable subscription)
        {
            _devices = devices;
            _subscription = subscription;
        }

        public void Dispose()
        {
            _subscription.Dispose();
            DisposeDevices(_devices);
        }

        public static AudioSessionSubscriptionSet Create(
            MMDeviceEnumerator deviceEnumerator,
            Action<IAudioSessionControl> onSessionCreated,
            Action<Exception> onError)
        {
            var devices = deviceEnumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active | DeviceState.Disabled)
                .ToList();

            try
            {
                var subscription = devices
                    .Select(device =>
                        Observable.FromEvent<AudioSessionManager.SessionCreatedDelegate, IAudioSessionControl>(
                            handler => (_, session) => handler(session),
                            handler => device.AudioSessionManager.OnSessionCreated += handler,
                            handler => device.AudioSessionManager.OnSessionCreated -= handler))
                    .Merge()
                    .Subscribe(onSessionCreated, onError);

                return new AudioSessionSubscriptionSet(devices, subscription);
            }
            catch
            {
                DisposeDevices(devices);
                throw;
            }
        }

        private static void DisposeDevices(IEnumerable<MMDevice> devices)
        {
            foreach (var device in devices)
                device.Dispose();
        }
    }
}