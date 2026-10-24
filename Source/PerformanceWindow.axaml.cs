using Avalonia.Controls;
using Avalonia.Media;
using Orbit.Controls;

namespace Orbit;

public sealed class PerformanceWindow : SettingsWindowBase
{
    public PerformanceWindow()
    {
        SetContent("PC performance", new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                new Border
                {
                    Width = 52,
                    Height = 52,
                    CornerRadius = new Avalonia.CornerRadius(14),
                    Background = OrbitTheme.Surface,
                    BorderBrush = OrbitTheme.Line,
                    BorderThickness = new Avalonia.Thickness(1),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Child = new OrbitIcon(OrbitIconKind.Performance) { Width = 23, Height = 23, Stroke = OrbitTheme.Icon, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }
                },
                new TextBlock { Text = "System readings aren't connected yet.", FontSize = 14, Foreground = OrbitTheme.Muted, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center }
            }
        });
    }
}
