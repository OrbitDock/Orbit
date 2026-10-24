using System.Text.Json;

namespace Orbit;

public static class OrbitSettings
{
    private static readonly object Sync = new();
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Orbit",
        "settings.json");

    public static bool GetBool(string key, bool fallback)
    {
        lock (Sync)
        {
            try
            {
                var settings = File.Exists(SettingsPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(SettingsPath))
                    : null;
                return settings is not null && settings.TryGetValue(key, out var value) ? value : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }

    public static void SetBool(string key, bool value)
    {
        lock (Sync)
        {
            try
            {
                var settings = File.Exists(SettingsPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(SettingsPath)) ?? new()
                    : new Dictionary<string, bool>();
                settings[key] = value;
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
            }
            catch (Exception)
            {
            }
        }
    }
}
