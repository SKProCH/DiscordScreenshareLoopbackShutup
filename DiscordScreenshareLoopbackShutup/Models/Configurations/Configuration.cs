using System;
using System.IO;
using Tomlyn;
using TruePath;

namespace DiscordScreenshareLoopbackShutup.Models.Configurations;

public class Configuration : IConfiguration
{
    public static IConfiguration Current { get; } = Load() ?? new Configuration();

    private static AbsolutePath ConfigurationPath => Program.GetAppropriateProgramFolderPath() / "config.toml";
    public string? DiscordOutputDeviceId { get; set; }

    public static void Edit(Action<Configuration> editAction)
    {
        editAction((Configuration)Current);
        var tomlText = Toml.FromModel(Current);
        File.WriteAllText(ConfigurationPath.ToString(), tomlText);
    }

    private static Configuration? Load()
    {
        try
        {
            var configText = File.ReadAllText(ConfigurationPath.ToString());
            return Toml.ToModel<Configuration>(configText);
        }
        catch (Exception)
        {
            return null;
        }
    }
}