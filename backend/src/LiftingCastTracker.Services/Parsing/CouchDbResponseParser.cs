using System.Text.Json;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Parsing;

public static class CouchDbResponseParser
{
    private const int MaximumInspectedDocuments = 25;
    private const int MaximumStructures = 5;
    private const int MaximumSampleIdsPerStructure = 3;
    private const int MaximumProperties = 10;
    private const int MaximumArrayItems = 5;
    private const int MaximumPreviewDepth = 4;
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
        var structures = new Dictionary<string, StructureAccumulator>(StringComparer.Ordinal);
        var inspectedDocuments = 0;
        var inspectedRows = 0;

        foreach (var row in rows.EnumerateArray())
        {
            if (inspectedRows == MaximumInspectedDocuments)
            {
                break;
            }

            inspectedRows++;
            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = row.TryGetProperty("id", out var idElement)
                && idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString()
                    : null;
            if (!row.TryGetProperty("doc", out var doc)
                || doc.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            id ??= doc.TryGetProperty("_id", out var documentId)
                && documentId.ValueKind == JsonValueKind.String
                    ? documentId.GetString()
                    : null;
            if (id?.StartsWith("_design/", StringComparison.Ordinal) == true)
            {
                continue;
            }

            inspectedDocuments++;
            var allFields = doc.EnumerateObject()
                .Select(property => new CouchDbPropertySummary(
                    property.Name,
                    GetJsonType(property.Value.ValueKind)))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ThenBy(property => property.Type, StringComparer.Ordinal)
                .ToList();
            var signature = string.Join(
                "\n",
                allFields.Select(property => $"{property.Name}\0{property.Type}"));
            if (!structures.TryGetValue(signature, out var structure))
            {
                structure = new StructureAccumulator(
                    allFields
                        .Take(MaximumProperties)
                        .Select(property => property with
                        {
                            Name = Truncate(property.Name, MaximumPropertyNameLength) ?? string.Empty
                        })
                        .ToArray(),
                    allFields.Count > MaximumProperties);
                structures.Add(signature, structure);
            }

            structure.Occurrences++;
            if (!string.IsNullOrEmpty(id)
                && structure.SampleDocumentIds.Count < MaximumSampleIdsPerStructure)
            {
                structure.SampleDocumentIds.Add(id);
            }
        }

        var sampledStructures = structures
            .OrderByDescending(pair => pair.Value.Occurrences)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(MaximumStructures)
            .Select(pair => new CouchDbDocumentStructure(
                pair.Value.Occurrences,
                pair.Value.SampleDocumentIds,
                pair.Value.Fields,
                pair.Value.FieldsTruncated))
            .ToArray();

        return new CouchDbSnapshotSummary(
            competitionId,
            totalDocuments,
            updateSequence,
            inspectedDocuments,
            sampledStructures,
            retrievedAtUtc);
    }

    public static CouchDbDocumentPreview ParseDocumentPreview(
        string competitionId,
        string documentId,
        string json,
        DateTimeOffset retrievedAtUtc)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The CouchDB document must be a JSON object.");
        }

        var fields = root.EnumerateObject()
            .Take(MaximumProperties)
            .Select(property => CreatePreview(property.Name, property.Value, 1))
            .ToArray();
        var hasMoreFields = root.EnumerateObject().Skip(MaximumProperties).Any();
        return new CouchDbDocumentPreview(
            competitionId,
            documentId,
            fields,
            retrievedAtUtc,
            hasMoreFields);
    }

    public static CouchDbChangesDiagnostic ParseChanges(
        string competitionId,
        int httpStatusCode,
        string sinceSequence,
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
        var changes = new List<CouchDbChangedDocument>();
        if (resultsAreValid)
        {
            foreach (var result in results.EnumerateArray())
            {
                if (changes.Count == CouchDbChangesRequestValidator.MaximumLimit)
                {
                    break;
                }

                if (result.ValueKind != JsonValueKind.Object
                    || !result.TryGetProperty("id", out var idElement)
                    || idElement.ValueKind != JsonValueKind.String
                    || string.IsNullOrEmpty(idElement.GetString()))
                {
                    continue;
                }

                var revisions = new List<string>();
                if (result.TryGetProperty("changes", out var revisionList)
                    && revisionList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var revision in revisionList.EnumerateArray())
                    {
                        if (revisions.Count == 10)
                        {
                            break;
                        }

                        if (revision.ValueKind == JsonValueKind.Object
                            && revision.TryGetProperty("rev", out var revisionId)
                            && revisionId.ValueKind == JsonValueKind.String
                            && revisionId.GetString() is { } value)
                        {
                            revisions.Add(value);
                        }
                    }
                }

                changes.Add(new CouchDbChangedDocument(
                    idElement.GetString()!,
                    revisions,
                    result.TryGetProperty("deleted", out var deleted)
                        && deleted.ValueKind == JsonValueKind.True));
            }
        }

        return new CouchDbChangesDiagnostic(
            competitionId,
            sinceSequence,
            httpStatusCode,
            isSuccessful && resultsAreValid && hasLastSequence,
            resultCount,
            changes,
            lastSequence,
            checkedAtUtc);
    }

    private static CouchDbJsonNodePreview CreatePreview(
        string name,
        JsonElement element,
        int depth)
    {
        var type = GetJsonType(element.ValueKind);
        var children = new List<CouchDbJsonNodePreview>();
        var truncated = false;

        if (depth >= MaximumPreviewDepth)
        {
            truncated = element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject().Any(),
                JsonValueKind.Array => element.GetArrayLength() > 0,
                _ => false
            };
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (children.Count == MaximumProperties)
                {
                    truncated = true;
                    break;
                }

                children.Add(CreatePreview(property.Name, property.Value, depth + 1));
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                if (children.Count == MaximumArrayItems)
                {
                    truncated = true;
                    break;
                }

                children.Add(CreatePreview($"[{index++}]", item, depth + 1));
            }
        }

        return new CouchDbJsonNodePreview(
            Truncate(name, MaximumPropertyNameLength) ?? string.Empty,
            type,
            children,
            truncated);
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

    private sealed class StructureAccumulator(
        IReadOnlyList<CouchDbPropertySummary> fields,
        bool fieldsTruncated)
    {
        public int Occurrences { get; set; }
        public List<string> SampleDocumentIds { get; } = [];
        public IReadOnlyList<CouchDbPropertySummary> Fields { get; } = fields;
        public bool FieldsTruncated { get; } = fieldsTruncated;
    }
}
