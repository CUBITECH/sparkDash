using SparkDash.DesktopTile.Core;
using Xunit;

namespace SparkDash.DesktopTile.Tests;

public sealed class TileSummaryParserTests
{
    [Fact]
    public void Parse_MapsTheGlanceableSummaryAndLimitsUnits()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "generatedAt": "2026-08-30T12:00:00Z",
              "state": "healthy",
              "title": "sparkDash",
              "headline": "All 3 systems online",
              "statusText": "Live status",
              "units": [
                {
                  "id": "spark-1",
                  "name": "Spark 1",
                  "online": true,
                  "statusText": "Online",
                  "gpuUsageText": "GPU 61%",
                  "temperatureText": "70 °C",
                  "memoryText": "Memory 55%",
                  "llmText": "LLM 12.3 tok/s",
                  "generationTps": 12.3,
                  "llmModel": "deepseek-v4-flash-0731",
                  "thermalThrottle": true
                },
                {
                  "id": "spark-2",
                  "name": "Spark 2",
                  "online": false,
                  "statusText": "Offline",
                  "gpuUsageText": "GPU —",
                  "temperatureText": "—",
                  "memoryText": "Memory —",
                  "llmText": "LLM unavailable",
                  "generationTps": null,
                  "llmModel": null,
                  "thermalThrottle": false
                },
                {
                  "id": "spark-3",
                  "name": "Spark 3",
                  "online": true,
                  "statusText": "Online",
                  "gpuUsageText": "GPU 10%",
                  "temperatureText": "50 °C",
                  "memoryText": "Memory 20%",
                  "llmText": "LLM idle",
                  "generationTps": 0
                }
              ]
            }
            """;

        var summary = TileSummaryParser.Parse(json);

        Assert.Equal("sparkDash", summary.Title);
        Assert.Equal("All 3 systems online", summary.Headline);
        Assert.Equal("healthy", summary.State);
        Assert.Equal(new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero), summary.GeneratedAt);
        Assert.Collection(
            summary.Units,
            first =>
            {
                Assert.Equal("spark-1", first.Id);
                Assert.Equal("Spark 1", first.Name);
                Assert.True(first.Online);
                Assert.Equal("GPU 61% · 70 °C · Memory 55%", first.MetricsText);
                Assert.Equal("LLM 12.3 tok/s", first.LlmText);
                Assert.Equal(12.3, first.GenerationTps);
                Assert.Equal("deepseek-v4-flash-0731", first.LlmModel);
                Assert.True(first.ThermalThrottle);
            },
            second =>
            {
                Assert.Equal("Spark 2", second.Name);
                Assert.False(second.Online);
                Assert.Equal("Offline", second.StatusText);
                Assert.Null(second.GenerationTps);
                Assert.Null(second.LlmModel);
                Assert.False(second.ThermalThrottle);
            });
    }

    [Fact]
    public void Parse_RejectsAnUnsupportedSchema()
    {
        const string json = """
            {
              "schemaVersion": 2,
              "generatedAt": "2026-08-30T12:00:00Z",
              "state": "healthy",
              "title": "sparkDash",
              "headline": "Wrong contract",
              "statusText": "Live status",
              "units": []
            }
            """;

        var error = Assert.Throws<InvalidDataException>(() => TileSummaryParser.Parse(json));

        Assert.Contains("contract", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
