using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnlockFps.Services;

public class ConfigService
{
    private const string ConfigName = "fps_config.json";

    public Config Config { get; private set; } = new();

    public ConfigService()
    {
        Load();
        StandardizeValues();
    }

    private void Load()
    {
        if (!File.Exists(ConfigName))
            return;

        var json = File.ReadAllText(ConfigName);
        Config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.Config)!;
    }

    private void StandardizeValues()
    {
        if (Config.LaunchOptions == null!)
        {
            Config.LaunchOptions = new LaunchOptions();
        }

        if (!string.IsNullOrWhiteSpace(Config.LaunchOptions.GamePath))
        {
            Config.LaunchOptions.GamePath = File.Exists(Config.LaunchOptions.GamePath)
                ? Path.GetFullPath(Config.LaunchOptions.GamePath)
                : null;
        }

        Config.FpsTarget = Math.Clamp(Config.FpsTarget, 1, 420);
        Config.ProcessPriority = Math.Clamp(Config.ProcessPriority, 0, 5);
        Config.LaunchOptions.CustomResolutionX = Math.Clamp(Config.LaunchOptions.CustomResolutionX, 200, 7680);
        Config.LaunchOptions.CustomResolutionY = Math.Clamp(Config.LaunchOptions.CustomResolutionY, 200, 4320);
        Config.LaunchOptions.MonitorId = Math.Clamp(Config.LaunchOptions.MonitorId, 1, 100);
        Config.FpsPowerSave = Math.Clamp(Config.FpsPowerSave, 1, 30);

        if (Config.LaunchOptions.DllList == null!)
        {
            Config.LaunchOptions.DllList = new ObservableCollection<string>();
        }
        else
        {
            Config.LaunchOptions.DllList = new ObservableCollection<string>(
                Config.LaunchOptions.DllList
                    .Where(k => !string.IsNullOrWhiteSpace(k) && File.Exists(k))
                    .Select(Path.GetFullPath)
            );
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Config, ConfigJsonContext.Default.Config);
        File.WriteAllText(ConfigName, json);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(Config))]
internal partial class ConfigJsonContext : JsonSerializerContext;