using System.Text.Json;

namespace SparkDash.DesktopTile.Core;

public sealed record TileSettings(
    double? Left,
    double? Top,
    double Width,
    double Height,
    bool Topmost,
    bool StartWithWindows)
{
    public static TileSettings Default { get; } = new(
        Left: null,
        Top: null,
        Width: 420,
        Height: 280,
        Topmost: true,
        StartWithWindows: false);
}

public sealed class TileSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };
    private readonly string path;

    public TileSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = path;
    }

    public TileSettings Load()
    {
        if (!File.Exists(path))
        {
            return TileSettings.Default;
        }

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<TileSettings>(json, JsonOptions);
            return settings is null ? TileSettings.Default : Normalize(settings);
        }
        catch (Exception error) when (
            error is JsonException or
            IOException or
            UnauthorizedAccessException)
        {
            return TileSettings.Default;
        }
    }

    public void Save(TileSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static TileSettings Normalize(TileSettings settings)
    {
        const double MinimumWidth = 320;
        const double MaximumWidth = 900;
        const double MinimumHeight = 180;
        const double MaximumHeight = 700;
        var width = settings.Width is >= MinimumWidth and <= MaximumWidth
            ? settings.Width
            : TileSettings.Default.Width;
        var height = settings.Height is >= MinimumHeight and <= MaximumHeight
            ? settings.Height
            : TileSettings.Default.Height;
        return settings with { Width = width, Height = height };
    }
}
