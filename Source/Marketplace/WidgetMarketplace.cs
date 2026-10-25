using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Orbit.Marketplace;

public sealed class WidgetMarketplace
{
    private static readonly HttpClient Client = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _widgetsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orbit", "Widgets");

    public const string DefaultIndexLocation = "https://raw.githubusercontent.com/orbitdock/Orbit-Widgets/main/Indexes/Widget_Index.json";

    public async Task<(MarketplaceIndex Index, Uri Source)> LoadIndexAsync()
    {
        var sourceText = DefaultIndexLocation;
        var source = Uri.TryCreate(sourceText, UriKind.Absolute, out var parsed) && parsed.IsAbsoluteUri
            ? parsed
            : new Uri(Path.GetFullPath(sourceText));
        var json = source.IsFile
            ? await File.ReadAllTextAsync(source.LocalPath)
            : await Client.GetStringAsync(source);
        var index = JsonSerializer.Deserialize<MarketplaceIndex>(json, JsonOptions)
            ?? throw new InvalidDataException("The marketplace index is empty or invalid.");
        return (index, source);
    }

    public bool IsInstalled(MarketplaceWidget widget)
    {
        MigrateLegacyInstall(widget.Id);
        var manifestPath = Path.Combine(GetInstallDirectory(widget.Id, widget.Version), "manifest.json");
        if (!File.Exists(manifestPath))
            return false;

        var manifest = JsonSerializer.Deserialize<WidgetManifest>(File.ReadAllText(manifestPath), JsonOptions);
        return manifest?.Version == widget.Version;
    }

    public void Uninstall(MarketplaceWidget widget)
    {
        if (IsInstalled(widget))
            Directory.Delete(GetInstallDirectory(widget.Id, widget.Version), true);
    }

    public IReadOnlyList<MarketplaceWidget> GetInstalledWidgets()
    {
        if (!Directory.Exists(_widgetsDirectory))
            return Array.Empty<MarketplaceWidget>();

        MigrateLegacyInstalls();
        var widgets = new List<MarketplaceWidget>();
        foreach (var packageDirectory in Directory.EnumerateDirectories(_widgetsDirectory))
        {
            try
            {
                var manifestPath = Path.Combine(packageDirectory, "manifest.json");
                if (!File.Exists(manifestPath))
                    continue;
                var manifest = JsonSerializer.Deserialize<WidgetManifest>(File.ReadAllText(manifestPath), JsonOptions);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Version))
                    continue;
                widgets.Add(new MarketplaceWidget
                {
                    Id = manifest.Id,
                    Name = manifest.Name,
                    Author = manifest.Author,
                    Description = manifest.Description,
                    Version = manifest.Version,
                    Category = manifest.Category,
                    Icon = manifest.Icon
                });
            }
            catch (Exception)
            {
            }
        }

        return widgets;
    }

    public async Task InstallAsync(MarketplaceWidget widget, MarketplaceIndex index, Uri source)
    {
        await InstallAsync(widget, index, source, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private async Task InstallAsync(MarketplaceWidget widget, MarketplaceIndex index, Uri source, HashSet<string> installing)
    {
        var packageKey = $"{widget.Id}@{widget.Version}";
        if (IsInstalled(widget))
            return;
        if (!installing.Add(packageKey))
            throw new InvalidDataException($"The widget dependency list contains a cycle at {widget.Name}.");

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "OrbitWidget-" + Guid.NewGuid().ToString("N"));
        try
        {
            var packageUri = new Uri(source, widget.DownloadUrl);
            if (packageUri.Scheme != Uri.UriSchemeHttps && !(source.IsFile && packageUri.IsFile))
                throw new InvalidDataException("Widget packages must be downloaded over HTTPS.");
            var archivePath = Path.Combine(temporaryDirectory, "package.zip");
            Directory.CreateDirectory(temporaryDirectory);
            if (packageUri.IsFile)
                File.Copy(packageUri.LocalPath, archivePath);
            else
                await File.WriteAllBytesAsync(archivePath, await Client.GetByteArrayAsync(packageUri));

            var extractedDirectory = Path.Combine(temporaryDirectory, "package");
            ZipFile.ExtractToDirectory(archivePath, extractedDirectory);
            var manifestPath = Path.Combine(extractedDirectory, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new InvalidDataException($"{widget.Name} package has no manifest.json.");

            var manifest = JsonSerializer.Deserialize<WidgetManifest>(await File.ReadAllTextAsync(manifestPath), JsonOptions)
                ?? throw new InvalidDataException($"{widget.Name} manifest is invalid.");
            if (manifest.Id != widget.Id || manifest.Version != widget.Version)
                throw new InvalidDataException($"{widget.Name} package identity does not match the marketplace index.");
            ValidatePackageFile(extractedDirectory, manifest.Assembly);

            foreach (var dependency in manifest.Dependencies)
            {
                var match = index.Widgets.FirstOrDefault(item => item.Id == dependency.Id
                    && (string.IsNullOrWhiteSpace(dependency.Version) || item.Version == dependency.Version));
                if (match is null)
                    throw new InvalidDataException($"Required widget dependency '{dependency.Id}' is missing from the marketplace index.");
                await InstallAsync(match, index, source, installing);
            }

            var installDirectory = GetInstallDirectory(widget.Id, widget.Version);
            Directory.CreateDirectory(Path.GetDirectoryName(installDirectory)!);
            if (Directory.Exists(installDirectory))
                Directory.Delete(installDirectory, true);
            Directory.Move(extractedDirectory, installDirectory);
        }
        finally
        {
            installing.Remove(packageKey);
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }
    }

    public LoadedWidget Load(MarketplaceWidget widget)
    {
        var directory = GetInstallDirectory(widget.Id, widget.Version);
        var manifest = JsonSerializer.Deserialize<WidgetManifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")), JsonOptions)
            ?? throw new InvalidDataException($"{widget.Name} manifest is invalid.");
        var assemblyPath = ValidatePackageFile(directory, manifest.Assembly);
        var loadContext = new WidgetLoadContext(_widgetsDirectory);
        using var assemblyStream = File.OpenRead(assemblyPath);
        var assembly = loadContext.LoadFromStream(assemblyStream);
        var type = assembly.GetType(manifest.EntryPoint, throwOnError: true)!;
        if (Activator.CreateInstance(type) is not IOrbitWidget instance)
            throw new InvalidDataException($"{manifest.EntryPoint} must implement IOrbitWidget.");
        return new LoadedWidget(instance, loadContext);
    }

    private string GetInstallDirectory(string id, string version)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version)
            || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || id is "." or ".." || version is "." or "..")
            throw new InvalidDataException("The widget index contains an invalid package identifier or version.");

        return Path.Combine(_widgetsDirectory, id);
    }

    private void MigrateLegacyInstalls()
    {
        foreach (var packageDirectory in Directory.EnumerateDirectories(_widgetsDirectory))
            MigrateLegacyInstall(Path.GetFileName(packageDirectory));
    }

    private void MigrateLegacyInstall(string id)
    {
        var packageDirectory = GetInstallDirectory(id, "legacy");
        if (!Directory.Exists(packageDirectory) || File.Exists(Path.Combine(packageDirectory, "manifest.json")))
            return;

        var legacyDirectory = Directory.EnumerateDirectories(packageDirectory)
            .FirstOrDefault(path => File.Exists(Path.Combine(path, "manifest.json")));
        if (legacyDirectory is null)
            return;

        foreach (var file in Directory.EnumerateFiles(legacyDirectory))
            File.Move(file, Path.Combine(packageDirectory, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(legacyDirectory))
        {
            var destination = Path.Combine(packageDirectory, Path.GetFileName(directory));
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);
            Directory.Move(directory, destination);
        }
        Directory.Delete(legacyDirectory, true);
    }

    private static string ValidatePackageFile(string directory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidDataException("The widget manifest has an invalid assembly path.");
        var fullPath = Path.GetFullPath(Path.Combine(directory, relativePath));
        if (!fullPath.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(fullPath))
            throw new InvalidDataException("The widget assembly was not found inside its package.");
        return fullPath;
    }

    public sealed record LoadedWidget(IOrbitWidget Instance, AssemblyLoadContext LoadContext);

    private sealed class WidgetLoadContext(string widgetsDirectory) : AssemblyLoadContext(isCollectible: false)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == typeof(IOrbitWidget).Assembly.GetName().Name
                || assemblyName.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true)
                return null;

            foreach (var path in Directory.Exists(widgetsDirectory)
                ? Directory.EnumerateFiles(widgetsDirectory, assemblyName.Name + ".dll", SearchOption.AllDirectories)
                : Array.Empty<string>())
            {
                using var stream = File.OpenRead(path);
                return LoadFromStream(stream);
            }

            return null;
        }
    }
}
