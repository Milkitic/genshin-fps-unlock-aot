using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnlockFps.Gui.Model;

namespace UnlockFps.Gui.Service;

public class ConfigService
{
    private const string ConfigName = "fps_config.json";

    public Config Config { get; private set; } = new();

    public ConfigService()
    {
        Load();
        ClampValues();
    }

    private void Load()
    {
        if (!File.Exists(ConfigName))
            return;

        var json = File.ReadAllText(ConfigName);
        Config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.Config)!;
    }

    private void ClampValues()
    {
        Config.FPSTarget = Math.Clamp(Config.FPSTarget, 1, 420);
        Config.Priority = Math.Clamp(Config.Priority, 0, 5);
        Config.CustomResX = Math.Clamp(Config.CustomResX, 200, 7680);
        Config.CustomResY = Math.Clamp(Config.CustomResY, 200, 4320);
        Config.MonitorNum = Math.Clamp(Config.MonitorNum, 1, 100);
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