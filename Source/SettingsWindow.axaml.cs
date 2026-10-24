using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Orbit.Controls;

namespace Orbit;

public sealed class SettingsWindow : SettingsWindowBase
{
    private const string BarAnimationKey = "BarDropAnimationEnabled";
    private readonly Button _hotkeyButton;
    private readonly HashSet<Key> _pressedKeys = new();
    private readonly HashSet<Key> _capturedKeys = new();
    private bool _capturingHotkey;

    public SettingsWindow()
    {
        AddHandler(KeyDownEvent, OnHotkeyKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnHotkeyKeyUp, RoutingStrategies.Tunnel);
        _hotkeyButton = new Button
        {
            Content = OrbitHotkey.Current.Display,
            Background = OrbitTheme.Raised,
            BorderBrush = OrbitTheme.Line,
            BorderThickness = new Avalonia.Thickness(1),
            Padding = new Avalonia.Thickness(10, 7),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = OrbitTheme.Text,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        _hotkeyButton.Click += (_, _) => BeginHotkeyCapture();

        var barAnimationToggle = new CheckBox
        {
            Content = "Animate the Orbit bar when it opens",
            IsChecked = OrbitSettings.GetBool(BarAnimationKey, true),
            FontSize = 13
        };
        barAnimationToggle.IsCheckedChanged += (_, _) => OrbitSettings.SetBool(BarAnimationKey, barAnimationToggle.IsChecked == true);

        var settings = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = "General", FontSize = 15, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "Shortcut", FontSize = 13, FontWeight = FontWeight.Medium },
                new Border
                {
                    CornerRadius = new Avalonia.CornerRadius(10),
                    BorderBrush = OrbitTheme.Line,
                    BorderThickness = new Avalonia.Thickness(1),
                    Background = OrbitTheme.Surface,
                    Padding = new Avalonia.Thickness(14, 12),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new StackPanel
                            {
                                Spacing = 4,
                                Children =
                                {
                                    new TextBlock { Text = "Toggle overlay", FontSize = 14, FontWeight = FontWeight.Medium },
                                    new TextBlock { Text = "Show or hide Orbit from any app", FontSize = 12, Foreground = OrbitTheme.Muted }
                                }
                            },
                            InColumn(_hotkeyButton, 1)
                        }
                    }
                },
                new Border { Height = 1, Background = OrbitTheme.Line },
                new StackPanel
                {
                    Spacing = 5,
                    Children =
                    {
                        new TextBlock { Text = "Widgets", FontSize = 14, FontWeight = FontWeight.Medium },
                        new TextBlock { Text = "Widget controls are not active yet.", FontSize = 12, Foreground = OrbitTheme.Muted }
                    }
                },
                new Border { Height = 1, Background = OrbitTheme.Line },
                new TextBlock { Text = "Performance", FontSize = 15, FontWeight = FontWeight.SemiBold },
                barAnimationToggle,
                new TextBlock
                {
                    Text = "Play a short drop-down animation when the Orbit bar opens.",
                    FontSize = 12,
                    Foreground = OrbitTheme.Muted,
                    Margin = new Avalonia.Thickness(24, -10, 0, 0)
                }
            }
        };

        SetContent("Settings", new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = settings
        });
    }

    private void BeginHotkeyCapture()
    {
        _capturingHotkey = true;
        _pressedKeys.Clear();
        _capturedKeys.Clear();
        _hotkeyButton.Content = "Waiting For Key Input";
        Focus();
    }

    private void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_capturingHotkey)
            return;

        if (e.Key == Key.Escape)
        {
            _capturingHotkey = false;
            _hotkeyButton.Content = OrbitHotkey.Current.Display;
            e.Handled = true;
            return;
        }

        _pressedKeys.Add(e.Key);
        _capturedKeys.Add(e.Key);
        e.Handled = true;
    }

    private void OnHotkeyKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_capturingHotkey)
            return;

        _pressedKeys.Remove(e.Key);
        e.Handled = true;
        if (_pressedKeys.Count > 0)
            return;

        var keys = _capturedKeys.Where(key => !IsModifier(key)).Distinct().ToArray();
        if (keys.Length == 0 || keys.Any(key => !TryGetVirtualKey(key, out _)))
        {
            _capturingHotkey = false;
            _hotkeyButton.Content = OrbitHotkey.Current.Display;
            return;
        }

        uint modifiers = 0;
        var names = new List<string>();
        if (_capturedKeys.Any(IsControl)) { modifiers |= 2; names.Add("Ctrl"); }
        if (_capturedKeys.Any(IsAlt)) { modifiers |= 1; names.Add("Alt"); }
        if (_capturedKeys.Any(IsShift)) { modifiers |= 4; names.Add("Shift"); }
        if (_capturedKeys.Any(IsWindows)) { modifiers |= 8; names.Add("Win"); }
        foreach (var key in keys)
            names.Add(GetKeyDisplay(key));

        var virtualKeys = keys.Select(key => { TryGetVirtualKey(key, out var value); return value; }).ToArray();
        var binding = new OrbitHotkeyBinding(modifiers, virtualKeys[0], string.Join(" + ", names), virtualKeys.Skip(1).ToArray());
        OrbitHotkey.Save(binding);
        _hotkeyButton.Content = binding.Display;
        _capturingHotkey = false;
    }

    private static bool IsModifier(Key key) => IsControl(key) || IsAlt(key) || IsShift(key) || IsWindows(key);
    private static bool IsControl(Key key) => key is Key.LeftCtrl or Key.RightCtrl;
    private static bool IsAlt(Key key) => key is Key.LeftAlt or Key.RightAlt;
    private static bool IsShift(Key key) => key is Key.LeftShift or Key.RightShift;
    private static bool IsWindows(Key key) => key is Key.LWin or Key.RWin;

    private static bool TryGetVirtualKey(Key key, out uint value)
    {
        var name = key.ToString();
        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0]))
        {
            value = char.ToUpperInvariant(name[0]);
            return true;
        }
        if (name.StartsWith("D", StringComparison.Ordinal) && name.Length == 2 && char.IsAsciiDigit(name[1]))
        {
            value = name[1];
            return true;
        }
        if (name.StartsWith("NumPad", StringComparison.Ordinal) && name.Length == 7 && char.IsAsciiDigit(name[6]))
        {
            value = (uint)(0x60 + name[6] - '0');
            return true;
        }
        if (name.Length is >= 2 and <= 3 && name[0] == 'F' && int.TryParse(name.AsSpan(1), out var functionKey) && functionKey is >= 1 and <= 24)
        {
            value = (uint)(0x70 + functionKey - 1);
            return true;
        }

        value = key switch
        {
            Key.Space => 0x20, Key.Enter => 0x0D, Key.Tab => 0x09, Key.Back => 0x08,
            Key.Delete => 0x2E, Key.Insert => 0x2D, Key.Home => 0x24, Key.End => 0x23,
            Key.PageUp => 0x21, Key.PageDown => 0x22, Key.Left => 0x25, Key.Up => 0x26,
            Key.Right => 0x27, Key.Down => 0x28, Key.OemMinus => 0xBD, Key.OemPlus => 0xBB,
            _ => 0
        };
        return value != 0;
    }

    private static string GetKeyDisplay(Key key)
    {
        var name = key.ToString();
        if (name.Length == 2 && name[0] == 'D' && char.IsAsciiDigit(name[1]))
            return name[1].ToString();
        return name switch { "OemMinus" => "-", "OemPlus" => "=", _ => name };
    }

    private static Control InColumn(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
