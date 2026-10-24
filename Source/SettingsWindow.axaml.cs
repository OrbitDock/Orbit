using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orbit.Controls;

namespace Orbit;

public sealed class SettingsWindow : SettingsWindowBase
{
    private const string BarAnimationKey = "BarDropAnimationEnabled";

    public SettingsWindow()
    {
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
                            InColumn(new Border
                            {
                                CornerRadius = new Avalonia.CornerRadius(7),
                                Background = OrbitTheme.Raised,
                                BorderBrush = OrbitTheme.Line,
                                BorderThickness = new Avalonia.Thickness(1),
                                Padding = new Avalonia.Thickness(10, 7),
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                Child = new TextBlock { Text = "CTRL  +  ALT  +  O", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = OrbitTheme.Text }
                            }, 1)
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

    private static Control InColumn(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
