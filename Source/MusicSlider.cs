using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Orbit;

public sealed class MusicSlider : Control
{
    private bool _dragging;
    private double _value;
    private double _minimum;
    private double _maximum = 1;

    public event EventHandler? ValueChanged;

    public double Minimum
    {
        get => _minimum;
        set { _minimum = value; SetValue(_value); InvalidateVisual(); }
    }

    public double Maximum
    {
        get => _maximum;
        set { _maximum = value; SetValue(_value); InvalidateVisual(); }
    }

    public double Value
    {
        get => _value;
        set => SetValue(value);
    }

    public MusicSlider()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var radius = 5d;
        var left = radius;
        var right = Math.Max(left, Bounds.Width - radius);
        var centerY = Bounds.Height / 2;
        var progress = GetProgressX(left, right);
        context.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#5A5A5A")), 2), new Point(left, centerY), new Point(right, centerY));
        context.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#2864F0")), 2), new Point(left, centerY), new Point(progress, centerY));
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#2864F0")), null, new Point(progress, centerY), radius, radius);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragging = true;
        e.Pointer.Capture(this);
        SetFromPointer(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        SetFromPointer(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
        SetFromPointer(e.GetPosition(this).X);
        e.Handled = true;
    }

    private void SetFromPointer(double x)
    {
        var left = 5d;
        var width = Math.Max(1, Bounds.Width - 10);
        var ratio = Math.Clamp((x - left) / width, 0, 1);
        SetValue(_minimum + (_maximum - _minimum) * ratio);
    }

    private double GetProgressX(double left, double right)
    {
        var ratio = _maximum <= _minimum ? 0 : Math.Clamp((_value - _minimum) / (_maximum - _minimum), 0, 1);
        return left + (right - left) * ratio;
    }

    private void SetValue(double value)
    {
        var next = Math.Clamp(value, _minimum, Math.Max(_minimum, _maximum));
        if (Math.Abs(next - _value) < 0.01) return;
        _value = next;
        InvalidateVisual();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }
}
