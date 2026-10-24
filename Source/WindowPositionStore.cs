using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Orbit;

public static class WindowPositionStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Orbit",
        "window-positions.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object Sync = new();
    private static readonly HashSet<Window> PausedWindows = new();

    public static bool Track(Window window)
    {
        var key = window.GetType().FullName ?? window.GetType().Name;
        var restored = Restore(window, key);
        window.PositionChanged += (_, _) =>
        {
            lock (Sync)
            {
                if (PausedWindows.Contains(window))
                    return;
            }
            Save(window, key);
        };
        window.Closed += (_, _) => Save(window, key);
        return restored;
    }

    public static void SetTrackingPaused(Window window, bool paused)
    {
        lock (Sync)
        {
            if (paused)
                PausedWindows.Add(window);
            else
                PausedWindows.Remove(window);
        }
    }

    private static bool Restore(Window window, string key)
    {
        try
        {
            Dictionary<string, SavedPosition>? positions;
            lock (Sync)
                positions = File.Exists(SettingsPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, SavedPosition>>(File.ReadAllText(SettingsPath))
                    : null;
            if (positions is null || !positions.TryGetValue(key, out var saved))
                return false;
            if (saved.X == 0 && saved.Y == 0)
                return false;

            var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
            if (screen is null)
                return false;

            var scale = screen.Scaling;
            var width = (int)(window.Width * scale);
            var height = (int)(window.Height * scale);
            var area = screen.WorkingArea;
            var x = Math.Clamp(saved.X, area.X, Math.Max(area.X, area.Right - width));
            var y = Math.Clamp(saved.Y, area.Y, Math.Max(area.Y, area.Bottom - height));
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = new PixelPoint(x, y);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void Save(Window window, string key)
    {
        try
        {
            lock (Sync)
            {
                if (window.Position == default)
                    return;
                var positions = File.Exists(SettingsPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, SavedPosition>>(File.ReadAllText(SettingsPath)) ?? new()
                    : new Dictionary<string, SavedPosition>();
                positions[key] = new(window.Position.X, window.Position.Y);
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(positions, JsonOptions));
            }
        }
        catch (Exception)
        {
        }
    }

    private sealed record SavedPosition(int X, int Y);
}
