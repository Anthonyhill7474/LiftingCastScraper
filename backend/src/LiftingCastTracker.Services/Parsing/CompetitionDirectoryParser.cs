using System.Globalization;
using System.Text.Json;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Parsing;

public sealed record ParsedCompetitionDirectory(
    IReadOnlyList<Competition> Competitions,
    int InvalidRecords);

public static class CompetitionDirectoryParser
{
    public static ParsedCompetitionDirectory Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("docs", out var docs)
            || docs.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The competition directory must contain a 'docs' array.");
        }

        var competitions = new List<Competition>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var invalidRecords = 0;

        foreach (var doc in docs.EnumerateArray())
        {
            if (doc.ValueKind != JsonValueKind.Object
                || !TryGetOptionalString(doc, "_id", out var id)
                || string.IsNullOrWhiteSpace(id)
                || !TryGetOptionalString(doc, "name", out var name)
                || !TryGetOptionalString(doc, "date", out var date)
                || !TryGetOptionalString(doc, "dateFormat", out var dateFormat)
                || !TryGetOptionalString(doc, "createDate", out var createDate)
                || !TryGetOptionalBoolean(doc, "showOnHomePage", out var showOnHomePage)
                || !ids.Add(id))
            {
                invalidRecords++;
                continue;
            }

            DateTime? parsedCreateDate = null;
            if (DateTimeOffset.TryParse(
                createDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var createDateWithOffset))
            {
                parsedCreateDate = createDateWithOffset.UtcDateTime;
            }

            competitions.Add(new Competition
            {
                Id = id,
                Name = name ?? string.Empty,
                Date = date,
                DateFormat = dateFormat,
                CreateDate = parsedCreateDate,
                ShowOnHomePage = showOnHomePage
            });
        }

        return new ParsedCompetitionDirectory(competitions, invalidRecords);
    }

    private static bool TryGetOptionalString(JsonElement element, string propertyName, out string? value)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            value = null;
            return true;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetOptionalBoolean(JsonElement element, string propertyName, out bool value)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            value = false;
            return true;
        }

        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }
}
