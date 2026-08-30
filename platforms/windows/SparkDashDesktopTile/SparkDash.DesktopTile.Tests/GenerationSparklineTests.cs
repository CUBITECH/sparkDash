using SparkDash.DesktopTile.Core;
using Xunit;

namespace SparkDash.DesktopTile.Tests;

public sealed class GenerationSparklineTests
{
    [Fact]
    public void Append_KeepsOnlyTheConfiguredRollingWindow()
    {
        var subject = new GenerationSparkline(capacity: 3);

        subject.Append(1);
        subject.Append(2);
        subject.Append(3);
        subject.Append(4);

        Assert.Equal(3, subject.Count);
        Assert.Equal(
            [
                new SparklinePoint(0, 13),
                new SparklinePoint(50, 7),
                new SparklinePoint(100, 1),
            ],
            subject.CreatePoints(width: 100, height: 25));
    }

    [Fact]
    public void CreatePoints_NormalizesGenerationRatesAndTreatsUnavailableAsZero()
    {
        var subject = new GenerationSparkline();
        subject.Append(null);
        subject.Append(10);
        subject.Append(5);

        Assert.Equal(
            [
                new SparklinePoint(0, 19),
                new SparklinePoint(50, 1),
                new SparklinePoint(100, 10),
            ],
            subject.CreatePoints(width: 100, height: 20));
    }
}
