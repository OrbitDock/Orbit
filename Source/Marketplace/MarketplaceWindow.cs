using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orbit.Controls;

namespace Orbit.Marketplace;

public sealed class MarketplaceWindow : SettingsWindowBase
{
    private readonly WidgetMarketplace _marketplace = new();
    private readonly Func<MarketplaceWidget, Task> _openWidget;
    private readonly Action _widgetsChanged;
    private readonly StackPanel _widgets = new() { Spacing = 8 };
    private readonly TextBlock _status = new() { Text = "Loading marketplace...", FontSize = 12, Foreground = OrbitTheme.Muted };
    private readonly TextBox _search = new()
    {
        PlaceholderText = "Search widgets",
        MinHeight = 36,
        Padding = new Avalonia.Thickness(10, 7),
        Background = OrbitTheme.Raised,
        BorderBrush = OrbitTheme.Line,
        BorderThickness = new Avalonia.Thickness(1)
    };
    private MarketplaceIndex? _index;
    private Uri? _source;

    public MarketplaceWindow(Func<MarketplaceWidget, Task> openWidget, Action widgetsChanged)
    {
        _openWidget = openWidget;
        _widgetsChanged = widgetsChanged;
        SetContent("Marketplace", new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new StackPanel { Spacing = 12, Children = { _status, _search, _widgets } }
        });
        _search.TextChanged += (_, _) => RenderWidgets();
        Opened += async (_, _) => await LoadMarketplaceAsync();
    }

    private async Task LoadMarketplaceAsync()
    {
        try
        {
            var (index, source) = await _marketplace.LoadIndexAsync();
            _index = index;
            _source = source;
            RenderWidgets();
            _status.Text = index.Widgets.Count == 0 ? "No widgets are listed in this index." : "Install a widget or open one that is already installed.";
        }
        catch (Exception ex)
        {
            _status.Text = $"Marketplace unavailable: {ex.Message}";
        }
    }

    private void RenderWidgets()
    {
        _widgets.Children.Clear();
        if (_index is null || _source is null)
            return;

        var query = _search.Text?.Trim();
        var results = _index.Widgets.Where(widget => string.IsNullOrEmpty(query)
            || widget.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || widget.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || widget.Author.Contains(query, StringComparison.OrdinalIgnoreCase)
            || widget.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
            || widget.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();

        for (var i = 0; i < results.Length; i++)
        {
            if (i > 0)
                _widgets.Children.Add(new Border { Height = 1, Background = OrbitTheme.Line, Margin = new Avalonia.Thickness(42, 0, 0, 0) });
            _widgets.Children.Add(CreateWidgetCard(results[i], _index, _source));
        }

        if (results.Length == 0)
            _widgets.Children.Add(new TextBlock { Text = "No widgets match your search.", FontSize = 12, Foreground = OrbitTheme.Muted });
    }

    private Control CreateWidgetCard(MarketplaceWidget widget, MarketplaceIndex index, Uri source)
    {
        var button = new Button
        {
            Content = _marketplace.IsInstalled(widget) ? "Open" : "Install",
            MinWidth = 76,
            Padding = new Avalonia.Thickness(12, 7),
            Background = OrbitTheme.Raised,
            BorderBrush = OrbitTheme.Line,
            BorderThickness = new Avalonia.Thickness(1),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Click += async (_, _) =>
        {
            if (_marketplace.IsInstalled(widget))
            {
                await _openWidget(widget);
                return;
            }

            button.IsEnabled = false;
            button.Content = "Installing";
            try
            {
                await _marketplace.InstallAsync(widget, index, source);
                _widgetsChanged();
                button.Content = "Open";
                _status.Text = $"{widget.Name} installed.";
                await _openWidget(widget);
            }
            catch (Exception ex)
            {
                button.Content = "Retry";
                _status.Text = $"Could not install {widget.Name}: {ex.Message}";
            }
            finally
            {
                button.IsEnabled = true;
            }
        };

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("42,*,Auto"),
            MinHeight = 64,
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
                    Child = new OrbitIcon(OrbitIconKind.Marketplace) { Width = 17, Height = 17, Stroke = OrbitTheme.Icon, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                },
                InColumn(new StackPanel
                {
                    Spacing = 3,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Avalonia.Thickness(10, 0, 8, 0),
                    Children =
                    {
                        new TextBlock { Text = widget.Name, FontSize = 13, FontWeight = FontWeight.Medium },
                        new TextBlock { Text = $"{widget.Author} · v{widget.Version}", FontSize = 11, Foreground = OrbitTheme.Muted },
                        new TextBlock { Text = widget.Description, FontSize = 11, Foreground = OrbitTheme.Muted, TextWrapping = TextWrapping.Wrap }
                    }
                }, 1),
                InColumn(button, 2)
            }
        };
    }

    private static Control InColumn(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
