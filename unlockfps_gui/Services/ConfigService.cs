using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnlockFps.Gui.Model;

namespace UnlockFps.Gui.Services;

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
        if (!string.IsNullOrWhiteSpace(Config.GamePath))
        {
            Config.GamePath = File.Exists(Config.GamePath) ? Path.GetFullPath(Config.GamePath) : null;
        }

        Config.FPSTarget = Math.Clamp(Config.FPSTarget, 1, 420);
        Config.Priority = Math.Clamp(Config.Priority, 0, 5);
        Config.CustomResX = Math.Clamp(Config.CustomResX, 200, 7680);
        Config.CustomResY = Math.Clamp(Config.CustomResY, 200, 4320);
        Config.MonitorNum = Math.Clamp(Config.MonitorNum, 1, 100);

        if (Config.DllList == null) Config.DllList = new ObservableCollection<string>();
        else
        {
            Config.DllList = new ObservableCollection<string>(
                Config.DllList
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

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config))]
internal partial class ConfigJsonContext : JsonSerializerContext;