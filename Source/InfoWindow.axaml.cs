using Avalonia.Controls;
using Avalonia.Media;

namespace Orbit;

public sealed class InfoWindow : SettingsWindowBase
{
    public InfoWindow(string title, string message)
    {
        SetContent(title, new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = message, FontSize = 15, TextWrapping = TextWrapping.Wrap }
            }
        });
    }
}
