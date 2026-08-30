using System.Text.Json;

namespace SparkDash.DesktopTile.Core;

public static class TileSummaryParser
{
    private const int MaximumUnits = 2;

    public static TileSummary Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out var version) ||
            !version.TryGetInt32(out var schemaVersion) ||
            schemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported sparkDash tile summary contract.");
        }

        var generatedAt = root.GetProperty("generatedAt").GetDateTimeOffset();
        var units = root.GetProperty("units")
            .EnumerateArray()
            .Take(MaximumUnits)
            .Select(ParseUnit)
            .ToArray();

        return new TileSummary(
            root.GetProperty("title").GetString() ?? "sparkDash",
            GetRequiredString(root, "headline"),
            GetRequiredString(root, "statusText"),
            GetRequiredString(root, "state"),
            generatedAt,
            units);
    }

    private static TileUnit ParseUnit(JsonElement unit)
    {
        var metrics = new[]
        {
            GetRequiredString(unit, "gpuUsageText"),
            GetRequiredString(unit, "temperatureText"),
            GetRequiredString(unit, "memoryText"),
        };
        return new TileUnit(
            GetRequiredString(unit, "id"),
            GetRequiredString(unit, "name"),
            unit.GetProperty("online").GetBoolean(),
            GetRequiredString(unit, "statusText"),
            string.Join(" · ", metrics),
            GetRequiredString(unit, "llmText"),
            GetGenerationTps(unit),
            GetOptionalString(unit, "llmModel"),
            GetOptionalBoolean(unit, "thermalThrottle"));
    }

    private static double? GetGenerationTps(JsonElement unit)
    {
        if (!unit.TryGetProperty("generationTps", out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out var value) &&
            double.IsFinite(value) &&
            value >= 0)
        {
            return value;
        }

        throw new InvalidDataException("sparkDash tile summary contains an invalid generationTps value.");
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString()?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        throw new InvalidDataException($"sparkDash tile summary contains an invalid {propertyName} value.");
    }

    private static bool GetOptionalBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }
        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return property.GetBoolean();
        }

        throw new InvalidDataException($"sparkDash tile summary contains an invalid {propertyName} value.");
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException($"sparkDash tile summary is missing {propertyName}.");
        }

        return property.GetString()!;
    }
}
