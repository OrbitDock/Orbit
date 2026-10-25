namespace Orbit.Marketplace;

public sealed class WidgetManifest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string Category { get; set; } = "";
    public string Assembly { get; set; } = "";
    public string EntryPoint { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<WidgetDependency> Dependencies { get; set; } = new();
}

public sealed class WidgetDependency
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
}

public sealed class MarketplaceIndex
{
    public int SchemaVersion { get; set; }
    public List<MarketplaceWidget> Widgets { get; set; } = new();
}

public sealed class MarketplaceWidget
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string Category { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<string> Tags { get; set; } = new();
}
