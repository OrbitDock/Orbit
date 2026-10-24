using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Orbit;

public partial class SettingsWindowBase : Window
{
    public SettingsWindowBase()
    {
        AvaloniaXamlLoader.Load(this);
        WindowPositionStore.Track(this);
    }

    protected void SetContent(string title, Control content)
    {
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<ContentControl>("Body")!.Content = content;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
