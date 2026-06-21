using System.IO;
using System.Text.Json;
using ScreenGuides.Models;

namespace ScreenGuides.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScreenGuides",
        "settings.json");

    public GuideSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new GuideSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<GuideSettings>(json, JsonOptions) ?? new GuideSettings();
        }
        catch
        {
            return new GuideSettings();
        }
    }

    public void Save(GuideSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
