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
        [OrbitIconKind.Clip] = "M3 6 H14 V18 H3 Z M14 9 L21 6 V18 L14 15 Z M6 9 L10 12 L6 15 Z",
        [OrbitIconKind.Screenshot] = "M3 7 H21 V19 H3 Z M7 7 L9 4 H15 L17 7 M6 16 L10 12 L13 15 L16 11 L19 16",
        [OrbitIconKind.Performance] = "M3 18 V6 M3 18 H21 M6 14 L10 10 L13 13 L19 6",
        [OrbitIconKind.Add] = "M12 4 V20 M4 12 H20",
        [OrbitIconKind.Settings] = "M4 6 H10 M14 6 H20 M4 12 H6 M10 12 H20 M4 18 H14 M18 18 H20 M12 4 V8 M8 10 V14 M16 16 V20",
        [OrbitIconKind.Close] = "M6 6 L18 18 M18 6 L6 18"
    };

    private OrbitIconKind _kind;

    public OrbitIcon()
    {
        StrokeThickness = 1.8;
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
