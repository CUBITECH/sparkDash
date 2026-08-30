using SparkDash.DesktopTile.Core;
using Xunit;

namespace SparkDash.DesktopTile.Tests;

public sealed class TileSettingsStoreTests
{
    [Fact]
    public void Load_ReturnsDefaultsWhenTheSettingsFileDoesNotExist()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"sparkdash-tile-{Guid.NewGuid():N}",
            "settings.json");
        var store = new TileSettingsStore(missingPath);

        var settings = store.Load();

        Assert.Equal(TileSettings.Default, settings);
    }

    [Fact]
    public void Load_ReturnsDefaultsWhenTheSettingsFileIsMalformed()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sparkdash-tile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{not-json");
            var store = new TileSettingsStore(path);

            var settings = store.Load();

            Assert.Equal(TileSettings.Default, settings);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_NormalizesAnUnusableWindowSize()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sparkdash-tile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            File.WriteAllText(
                path,
                """
                {
                  "left": 10,
                  "top": 20,
                  "width": -1,
                  "height": 40,
                  "topmost": false,
                  "startWithWindows": true
                }
                """);
            var store = new TileSettingsStore(path);

            var settings = store.Load();

            Assert.Equal(TileSettings.Default.Width, settings.Width);
            Assert.Equal(TileSettings.Default.Height, settings.Height);
            Assert.False(settings.Topmost);
            Assert.True(settings.StartWithWindows);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsDefaultsWhenTheSettingsFileIsTemporarilyLocked()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sparkdash-tile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{}");
            var store = new TileSettingsStore(path);
            using var lockStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            var settings = store.Load();

            Assert.Equal(TileSettings.Default, settings);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsWindowPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sparkdash-tile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new TileSettingsStore(Path.Combine(directory, "settings.json"));
            var expected = new TileSettings(
                Left: 110,
                Top: 90,
                Width: 510,
                Height: 310,
                Topmost: false,
                StartWithWindows: true);

            store.Save(expected);
            var actual = store.Load();

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
