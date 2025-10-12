using System;
using Tomlyn;

namespace DiscordScreenshareLoopbackShutup.Models.Configurations;

public interface IConfiguration
{
    string? DiscordOutputDeviceId { get; set; }
}