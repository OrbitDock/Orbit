using System.Text.Json;

namespace Orbit;

public sealed record OrbitHotkeyBinding(uint Modifiers, uint Key, string Display, uint[]? ExtraKeys = null);

public static class OrbitHotkey
{
    private static readonly string BindingPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Orbit",
        "hotkey.json");

    public static event Action? Changed;

    public static OrbitHotkeyBinding Current
    {
        get
        {
            try
            {
                if (File.Exists(BindingPath) && JsonSerializer.Deserialize<OrbitHotkeyBinding>(File.ReadAllText(BindingPath)) is { Key: > 0 } binding)
                    return binding;
            }
            catch (Exception)
            {
            }

            return new OrbitHotkeyBinding(3, 0x4F, "Ctrl + Alt + O");
        }
    }

    public static void Save(OrbitHotkeyBinding binding)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BindingPath)!);
            File.WriteAllText(BindingPath, JsonSerializer.Serialize(binding));
            Changed?.Invoke();
        }
        catch (Exception)
        {
        }
    }

    public static uint[] GetKeys(OrbitHotkeyBinding binding) =>
        new[] { binding.Key }.Concat(binding.ExtraKeys ?? Array.Empty<uint>()).Distinct().ToArray();
}
