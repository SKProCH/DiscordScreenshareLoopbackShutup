using System;
using System.IO;
using Tomlyn;
using TruePath;

namespace DiscordScreenshareLoopbackShutup.Models.Configurations;

public class ConfigurationManager
{
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
        var tomlText = Toml.FromModel(_currentConfiguration);
        File.WriteAllText(_configurationPath.ToString(), tomlText);
    }

    private Configuration? Load()
    {
        try
        {
            if (!File.Exists(_configurationPath.ToString())) return null;

            var configText = File.ReadAllText(_configurationPath.ToString());
            return Toml.ToModel<Configuration>(configText);
        }
        catch (Exception)
        {
            return null;
        }
    }
}