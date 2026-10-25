using Avalonia.Controls;

namespace Orbit;

public interface IOrbitWidget
{
    Control CreateView();
}
