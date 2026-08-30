namespace SparkDash.DesktopTile.Core;

public sealed record TileSummary(
    string Title,
    string Headline,
    string StatusText,
    string State,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<TileUnit> Units);

public sealed record TileUnit(
    string Id,
    string Name,
    bool Online,
    string StatusText,
    string MetricsText,
    string LlmText,
    double? GenerationTps,
    string? LlmModel,
    bool ThermalThrottle);
