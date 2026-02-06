using System;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DiscordScreenshareLoopbackShutup;

public class AudioDeviceService : IMMNotificationClient, IDisposable
{
    public delegate void DefaultDeviceChangedHandler(DataFlow dataFlow, Role deviceRole, string defaultDeviceId);

    public delegate void DeviceAddedHandler(string deviceId);

    public delegate void DeviceRemovedHandler(string deviceId);

    public delegate void DeviceStateChangedHandler(string deviceId, DeviceState newState);

    public delegate void PropertyValueChangedHandler(string deviceId);

    private readonly ILogger<AudioDeviceService> _logger;

    public AudioDeviceService(ILogger<AudioDeviceService> logger)
    {
        _logger = logger;
        DeviceEnumerator.RegisterEndpointNotificationCallback(this);
    }

    public MMDeviceEnumerator DeviceEnumerator { get; } = new();

    public void Dispose()
    {
        DeviceEnumerator.UnregisterEndpointNotificationCallback(this);
    }

    /// <summary>
    ///     Triggered by NAudio.CoreAudioApi.MMDeviceEnumerator when the default device changes.
    /// </summary>
    /// <param name="dataFlow"></param>
    /// <param name="deviceRole"></param>
    /// <param name="defaultDeviceId"></param>
    public void OnDefaultDeviceChanged(DataFlow dataFlow, Role deviceRole, string defaultDeviceId)
    {
        _logger.LogDebug(
            "AudioDeviceChangeNotifier::OnDefaultDeviceChanged - dataFlow: {DataFlow}, deviceRole: {DeviceRole}, defaultDeviceId: {DefaultDeviceId}",
            dataFlow, deviceRole, defaultDeviceId);

        if (DefaultDeviceChanged != null)
            DefaultDeviceChanged(dataFlow, deviceRole, defaultDeviceId);
    }

    /// <summary>
    ///     Triggered by NAudio.CoreAudioApi.MMDeviceEnumerator when an audio device is added.
    /// </summary>
    /// <param name="deviceId"></param>
    public void OnDeviceAdded(string deviceId)
    {
        _logger.LogDebug("AudioDeviceChangeNotifier::OnDeviceAdded - deviceId: {DeviceId}", deviceId);

        if (DeviceAdded != null)
            DeviceAdded(deviceId);
    }

    /// <summary>
    ///     Triggered by NAudio.CoreAudioApi.MMDeviceEnumerator when an audio device is removed.
    /// </summary>
    /// <param name="deviceId"></param>
    public void OnDeviceRemoved(string deviceId)
    {
        _logger.LogDebug("AudioDeviceChangeNotifier::OnDeviceRemoved - deviceId: {DeviceId}", deviceId);

        if (DeviceRemoved != null)
            DeviceRemoved(deviceId);
    }

    /// <summary>
    ///     Triggered by NAudio.CoreAudioApi.MMDeviceEnumerator when an audio device's state is changed.
    /// </summary>
    /// <param name="deviceId"></param>
    /// <param name="newState"></param>
    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        _logger.LogDebug("AudioDeviceChangeNotifier::OnDeviceStateChanged - deviceId: {DeviceId}, newState: {NewState}",
            deviceId, newState);

        if (DeviceStateChanged != null)
            DeviceStateChanged(deviceId, newState);
    }

    /// <summary>
    ///     Triggered by NAudio.CoreAudioApi.MMDeviceEnumerator when an audio device's property is changed.
    /// </summary>
    /// <param name="deviceId"></param>
    /// <param name="propertyKey"></param>
    public void OnPropertyValueChanged(string deviceId, PropertyKey propertyKey)
    {
        _logger.LogDebug(
            "AudioDeviceChangeNotifier::OnPropertyValueChanged - deviceId: {DeviceId}, propertyKey: {PropertyKey}",
            deviceId, propertyKey);

        if (PropertyValueChanged != null)
            PropertyValueChanged(deviceId);
    }

    /// <summary>
    ///     Raised when the default audio device is changed.
    /// </summary>
    public event DefaultDeviceChangedHandler? DefaultDeviceChanged;

    /// <summary>
    ///     Raised when a new audio device is added.
    /// </summary>
    public event DeviceAddedHandler? DeviceAdded;

    /// <summary>
    ///     Raised when an audio device is removed.
    /// </summary>
    public event DeviceRemovedHandler? DeviceRemoved;

    /// <summary>
    ///     Raised when an audio device's state is changed.
    /// </summary>
    public event DeviceStateChangedHandler? DeviceStateChanged;

    /// <summary>
    ///     Raised when a property value is changed.
    /// </summary>
    public event PropertyValueChangedHandler? PropertyValueChanged;
}