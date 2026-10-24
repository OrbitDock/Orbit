using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orbit.Controls;

namespace Orbit;

public sealed class WidgetsWindow : SettingsWindowBase
{
    public WidgetsWindow()
    {
        var widgets = new StackPanel { Spacing = 0 };
        AddWidget(widgets, OrbitIconKind.Clip, "Video clipping", "Capture isn't available yet");
        AddDivider(widgets);
        AddWidget(widgets, OrbitIconKind.Screenshot, "Screenshots", "Capture isn't available yet");
        AddDivider(widgets);
        AddWidget(widgets, OrbitIconKind.Performance, "PC performance", "Monitoring isn't active yet");
        AddDivider(widgets);
        AddWidget(widgets, OrbitIconKind.Music, "Music", "Control your music apps");
        widgets.Children.Add(new TextBlock
        {
            Text = "Widgets can't be added yet.",
            FontSize = 12,
            Foreground = OrbitTheme.Muted,
            Margin = new Avalonia.Thickness(2, 14, 0, 0)
        });
        SetContent("New widgets", widgets);
    }

    private static void AddWidget(StackPanel panel, OrbitIconKind icon, string name, string description)
    {
        panel.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("42,*"),
            MinHeight = 59,
            Children =
            {
                new Border
                {
                    Width = 34,
                    Height = 34,
                    CornerRadius = new Avalonia.CornerRadius(10),
                    Background = OrbitTheme.Surface,
                    BorderBrush = OrbitTheme.Line,
                    BorderThickness = new Avalonia.Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new OrbitIcon(icon) { Width = 17, Height = 17, Stroke = OrbitTheme.Icon, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                },
                InColumn(new StackPanel
                {
                    Spacing = 3,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Avalonia.Thickness(10, 0, 0, 0),
                    Children =
                    {
                        new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeight.Medium },
                        new TextBlock { Text = description, FontSize = 11, Foreground = OrbitTheme.Muted }
                    }
                }, 1),
            }
        });
    }

    private static void AddDivider(StackPanel panel) => panel.Children.Add(new Border
    {
        Height = 1,
        Background = OrbitTheme.Line,
        Margin = new Avalonia.Thickness(42, 0, 0, 0)
    });

    private static Control InColumn(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
