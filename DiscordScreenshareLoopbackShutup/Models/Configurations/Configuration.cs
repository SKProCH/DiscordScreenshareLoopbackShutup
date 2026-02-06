namespace DiscordScreenshareLoopbackShutup.Models.Configurations;

public class Configuration : IConfiguration
{
    public string? DiscordOutputDeviceId { get; set; }
    public string? LogPath { get; set; }
}