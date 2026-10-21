using System.Text.Json;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Parsing;

public static class CouchDbResponseParser
{
    private const int MaximumSampleDocuments = 3;
    private const int MaximumPropertiesPerDocument = 10;
    private const int MaximumPropertyNameLength = 100;

    public static CouchDbSnapshotSummary ParseSnapshot(
        string competitionId,
        string json,
        DateTimeOffset retrievedAtUtc)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("rows", out var rows)
            || rows.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The CouchDB snapshot must contain a 'rows' array.");
        }

        if (!root.TryGetProperty("total_rows", out var totalRows)
            || !totalRows.TryGetInt64(out var totalDocuments)
            || totalDocuments < 0)
        {
            throw new JsonException("The CouchDB snapshot must contain a valid 'total_rows' count.");
        }

        var updateSequence = root.TryGetProperty("update_seq", out var sequence)
            ? sequence.Clone()
            : (JsonElement?)null;
        var samples = new List<CouchDbDocumentSample>(MaximumSampleDocuments);

        foreach (var row in rows.EnumerateArray())
        {
            if (samples.Count == MaximumSampleDocuments)
            {
                break;
            }

            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = row.TryGetProperty("id", out var idElement)
                && idElement.ValueKind == JsonValueKind.String
                    ? Truncate(idElement.GetString(), MaximumPropertyNameLength)
                    : null;
            var properties = new List<CouchDbPropertySummary>(MaximumPropertiesPerDocument);

            if (row.TryGetProperty("doc", out var doc) && doc.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in doc.EnumerateObject())
                {
                    if (properties.Count == MaximumPropertiesPerDocument)
                    {
                        break;
                    }

                    properties.Add(new CouchDbPropertySummary(
                        Truncate(property.Name, MaximumPropertyNameLength) ?? string.Empty,
                        GetJsonType(property.Value.ValueKind)));
                }
            }

            samples.Add(new CouchDbDocumentSample(id, properties));
        }

        return new CouchDbSnapshotSummary(
            competitionId,
            totalDocuments,
            updateSequence,
            samples,
            retrievedAtUtc);
    }

    public static CouchDbChangesDiagnostic ParseChanges(
        string competitionId,
        int httpStatusCode,
        string json,
        DateTimeOffset checkedAtUtc)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var results = default(JsonElement);
        var resultsAreValid = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("results", out results)
            && results.ValueKind == JsonValueKind.Array;
        var hasLastSequence = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("last_seq", out _);
        var isSuccessful = httpStatusCode is >= 200 and < 300;

        JsonElement? lastSequence = null;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("last_seq", out var lastSequenceElement))
        {
            lastSequence = lastSequenceElement.Clone();
        }

        var resultCount = resultsAreValid ? results.GetArrayLength() : (int?)null;

        return new CouchDbChangesDiagnostic(
            competitionId,
            httpStatusCode,
            isSuccessful && resultsAreValid && hasLastSequence,
            resultCount,
            lastSequence,
            checkedAtUtc);
    }

    private static string GetJsonType(JsonValueKind kind) =>
        kind switch
        {
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Null => "null",
            _ => "undefined"
        };

    private static string? Truncate(string? value, int maximumLength) =>
        value is not null && value.Length > maximumLength
            ? value[..maximumLength]
            : value;
}
