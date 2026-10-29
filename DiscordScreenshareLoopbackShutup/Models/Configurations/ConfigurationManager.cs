using System;
using System.IO;
using System.Text.Json;
using Tomlyn;
using TruePath;

namespace DiscordScreenshareLoopbackShutup.Models.Configurations;

public class ConfigurationManager
{
    private static readonly TomlSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly AbsolutePath _configurationPath;
    private readonly Configuration _currentConfiguration;

    public ConfigurationManager(AbsolutePath configurationPath)
    {
        _configurationPath = configurationPath;
        _currentConfiguration = Load() ?? new Configuration();
    }

    public IConfiguration Configuration => _currentConfiguration;

    public void Edit(Action<Configuration> editAction)
    {
        editAction(_currentConfiguration);
        Save();
    }

    private void Save()
    {
        var tomlText = TomlSerializer.Serialize(_currentConfiguration, SerializerOptions);
        File.WriteAllText(_configurationPath.ToString(), tomlText);
    }

    private Configuration? Load()
    {
        try
        {
            if (!File.Exists(_configurationPath.ToString())) return null;

            var configText = File.ReadAllText(_configurationPath.ToString());
            return TomlSerializer.Deserialize<Configuration>(configText, SerializerOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }
}