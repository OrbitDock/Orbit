using Avalonia.Controls;

namespace Orbit.Marketplace;

public sealed class MarketplaceWidgetWindow : SettingsWindowBase
{
    public MarketplaceWidgetWindow(string title, Control content) => SetContent(title, content);
}
