namespace SparkDash.DesktopTile.Core;

public sealed record SparklinePoint(double X, double Y);

public sealed class GenerationSparkline
{
    private const double Padding = 1;
    private readonly int capacity;
    private readonly Queue<double> samples = new();

    public GenerationSparkline(int capacity = 60)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 2);
        this.capacity = capacity;
    }

    public int Count => samples.Count;

    public void Append(double? generationTps)
    {
        var sample = generationTps is { } value && double.IsFinite(value) && value >= 0
            ? value
            : 0;
        samples.Enqueue(sample);
        while (samples.Count > capacity)
        {
            samples.Dequeue();
        }
    }

    public IReadOnlyList<SparklinePoint> CreatePoints(double width, double height)
    {
        if (!double.IsFinite(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        if (!double.IsFinite(height) || height <= Padding * 2)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        var values = samples.ToArray();
        if (values.Length == 0)
        {
            return Array.Empty<SparklinePoint>();
        }

        var maximum = values.Max();
        var usableHeight = height - (Padding * 2);
        double MapY(double value)
        {
            var normalized = maximum > 0 ? value / maximum : 0;
            var y = height - Padding - (normalized * usableHeight);
            return Math.Round(y, MidpointRounding.AwayFromZero);
        }

        if (values.Length == 1)
        {
            var y = MapY(values[0]);
            return [new SparklinePoint(0, y), new SparklinePoint(width, y)];
        }

        var points = new SparklinePoint[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var x = width * index / (values.Length - 1);
            points[index] = new SparklinePoint(x, MapY(values[index]));
        }
        return points;
    }
}
