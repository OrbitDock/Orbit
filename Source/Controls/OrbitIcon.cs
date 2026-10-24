using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Orbit.Controls;

public enum OrbitIconKind
{
    Clip,
    Screenshot,
    Performance,
    Add,
    Settings,
    Close
}

public sealed class OrbitIcon : Avalonia.Controls.Shapes.Path
{
    private static readonly IReadOnlyDictionary<OrbitIconKind, string> GeometryByKind = new Dictionary<OrbitIconKind, string>
    {
        [OrbitIconKind.Clip] = "M4 4 H14 A2 2 0 0 1 16 6 V18 A2 2 0 0 1 14 20 H4 A2 2 0 0 1 2 18 V6 A2 2 0 0 1 4 4 Z M16 9 L22 6 V18 L16 15 Z",
        [OrbitIconKind.Screenshot] = "M3 7 H7 L9 4 H15 L17 7 H21 V19 H3 Z M12 10 A3.5 3.5 0 1 0 12 17 A3.5 3.5 0 1 0 12 10 Z",
        [OrbitIconKind.Performance] = "M3 4 V20 H21 M6 15 L10 11 L13 14 L19 7 M16 7 H19 V10",
        [OrbitIconKind.Add] = "M12 5 V19 M5 12 H19",
        [OrbitIconKind.Settings] = "M4 6 H20 M4 12 H20 M4 18 H20 M9 4 V8 M15 10 V14 M8 16 V20",
        [OrbitIconKind.Close] = "M6 6 L18 18 M18 6 L6 18"
    };

    private OrbitIconKind _kind;

    public OrbitIcon()
    {
        StrokeThickness = 1.7;
        StrokeLineCap = PenLineCap.Round;
        StrokeJoin = PenLineJoin.Round;
        Stretch = Stretch.Uniform;
        Fill = Brushes.Transparent;
    }

    public OrbitIcon(OrbitIconKind kind) : this()
    {
        Kind = kind;
    }

    public OrbitIconKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Data = Geometry.Parse(GeometryByKind[value]);
        }
    }
}
