namespace DiscordScreenshareLoopbackShutup.Models;

public record AudioDeviceShutupInformation(string DeviceId, string DeviceName, ShutupStatus Status)
{
    public virtual bool Equals(AudioDeviceShutupInformation? other)
    {
        return DeviceId == other?.DeviceId;
    }
}