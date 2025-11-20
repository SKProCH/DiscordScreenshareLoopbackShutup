namespace DiscordScreenshareLoopbackShutup.Models.Configurations;

public interface IConfiguration
{
    string? DiscordOutputDeviceId { get; }
    string? LogPath { get; }
}