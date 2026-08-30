using Media = System.Windows.Media;
using SparkDash.DesktopTile.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SparkDash.DesktopTile;

internal sealed class TileViewModel : INotifyPropertyChanged
{
    private const double SparklineWidth = 120;
    private const double SparklineHeight = 32;
    private readonly Dictionary<string, GenerationSparkline> generationHistories = new(StringComparer.Ordinal);
    private string title = "sparkDash";
    private string headline = "Connecting…";
    private string statusText = "Waiting for the local dashboard";
    private string lastUpdatedText = "No live data yet";
    private Media.Brush stateBrush = new Media.SolidColorBrush(Media.Color.FromRgb(148, 163, 184));

    public ObservableCollection<TileUnitViewModel> Units { get; } = [];

    public string Title
    {
        get => title;
        private set => SetField(ref title, value);
    }

    public string Headline
    {
        get => headline;
        private set => SetField(ref headline, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public string LastUpdatedText
    {
        get => lastUpdatedText;
        private set => SetField(ref lastUpdatedText, value);
    }

    public Media.Brush StateBrush
    {
        get => stateBrush;
        private set => SetField(ref stateBrush, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Apply(TileSummary summary)
    {
        Title = summary.Title;
        Headline = summary.Headline;
        StatusText = summary.StatusText;
        LastUpdatedText = $"Updated {summary.GeneratedAt.ToLocalTime():HH:mm:ss}";
        StateBrush = summary.State switch
        {
            "healthy" => new Media.SolidColorBrush(Media.Color.FromRgb(74, 222, 128)),
            "degraded" => new Media.SolidColorBrush(Media.Color.FromRgb(251, 191, 36)),
            _ => new Media.SolidColorBrush(Media.Color.FromRgb(248, 113, 113)),
        };
        ApplyUnits(summary.Units);
    }

    internal void ApplyUnavailable()
    {
        Title = "sparkDash";
        Headline = "Dashboard unavailable";
        StatusText = "Start the local sparkDash service";
        LastUpdatedText = "No live data";
        StateBrush = new Media.SolidColorBrush(Media.Color.FromRgb(248, 113, 113));
        Units.Clear();
    }

    private void ApplyUnits(IReadOnlyList<TileUnit> units)
    {
        var activeIds = units.Select(unit => unit.Id).ToHashSet(StringComparer.Ordinal);
        for (var index = Units.Count - 1; index >= 0; index--)
        {
            if (!activeIds.Contains(Units[index].Id))
            {
                Units.RemoveAt(index);
            }
        }

        foreach (var staleId in generationHistories.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            generationHistories.Remove(staleId);
        }

        for (var targetIndex = 0; targetIndex < units.Count; targetIndex++)
        {
            var unit = units[targetIndex];
            if (!generationHistories.TryGetValue(unit.Id, out var history))
            {
                history = new GenerationSparkline();
                generationHistories.Add(unit.Id, history);
            }
            history.Append(unit.GenerationTps);
            var points = CreatePointCollection(history);

            var existingIndex = IndexOfUnit(unit.Id);
            if (existingIndex < 0)
            {
                Units.Insert(targetIndex, new TileUnitViewModel(unit, points));
            }
            else
            {
                if (existingIndex != targetIndex)
                {
                    Units.Move(existingIndex, targetIndex);
                }
                Units[targetIndex].Apply(unit, points);
            }
        }
    }

    private int IndexOfUnit(string id)
    {
        for (var index = 0; index < Units.Count; index++)
        {
            if (string.Equals(Units[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }

    private static Media.PointCollection CreatePointCollection(GenerationSparkline history)
    {
        var points = new Media.PointCollection(
            history.CreatePoints(SparklineWidth, SparklineHeight)
                .Select(point => new System.Windows.Point(point.X, point.Y)));
        if (points.CanFreeze)
        {
            points.Freeze();
        }
        return points;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class TileUnitViewModel : INotifyPropertyChanged
{
    private string id = string.Empty;
    private string name = string.Empty;
    private bool online;
    private string statusText = string.Empty;
    private string metricsText = string.Empty;
    private string llmText = string.Empty;
    private string modelText = string.Empty;
    private bool thermalThrottle;
    private Media.PointCollection generationPoints = [];
    private double generationOpacity = 0.25;

    internal TileUnitViewModel(TileUnit unit, Media.PointCollection points)
    {
        Apply(unit, points);
    }

    public string Id
    {
        get => id;
        private set => SetField(ref id, value);
    }

    public string Name
    {
        get => name;
        private set => SetField(ref name, value);
    }

    public bool Online
    {
        get => online;
        private set => SetField(ref online, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public string MetricsText
    {
        get => metricsText;
        private set => SetField(ref metricsText, value);
    }

    public string LlmText
    {
        get => llmText;
        private set => SetField(ref llmText, value);
    }

    public string ModelText
    {
        get => modelText;
        private set => SetField(ref modelText, value);
    }

    public bool ThermalThrottle
    {
        get => thermalThrottle;
        private set => SetField(ref thermalThrottle, value);
    }

    public Media.PointCollection GenerationPoints
    {
        get => generationPoints;
        private set => SetField(ref generationPoints, value);
    }

    public double GenerationOpacity
    {
        get => generationOpacity;
        private set => SetField(ref generationOpacity, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Apply(TileUnit unit, Media.PointCollection points)
    {
        Id = unit.Id;
        Name = unit.Name;
        Online = unit.Online;
        StatusText = unit.StatusText;
        MetricsText = unit.MetricsText;
        LlmText = unit.LlmText;
        ModelText = string.IsNullOrWhiteSpace(unit.LlmModel) ? string.Empty : $"· {unit.LlmModel}";
        ThermalThrottle = unit.ThermalThrottle;
        GenerationPoints = points;
        GenerationOpacity = unit.GenerationTps.HasValue ? 1 : 0.25;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
