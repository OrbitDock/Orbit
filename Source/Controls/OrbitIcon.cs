using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Orbit.Controls;

public enum OrbitIconKind
{
    Clip,
    Screenshot,
    Performance,
    Marketplace,
    Music,
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
        [OrbitIconKind.Marketplace] = "M3 9 L5 4 H19 L21 9 M4 9 V20 H20 V9 M4 9 C4 12 8 12 8 9 C8 12 12 12 12 9 C12 12 16 12 16 9 C16 12 20 12 20 9 M9 20 V15 H15 V20",
        [OrbitIconKind.Music] = "M12 5 V15.5 M21 3 V13.5 M12 5 L21 3 M12 15.5 C12 17.2 10.4 18.6 8.2 18.6 C6 18.6 4.5 17.4 4.5 15.8 C4.5 14.2 6.2 12.8 8.3 12.8 C10.4 12.8 12 14 12 15.5 Z M21 13.5 C21 15.2 19.4 16.6 17.2 16.6 C15 16.6 13.5 15.4 13.5 13.8 C13.5 12.2 15.2 10.8 17.3 10.8 C19.4 10.8 21 12 21 13.5 Z",
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
