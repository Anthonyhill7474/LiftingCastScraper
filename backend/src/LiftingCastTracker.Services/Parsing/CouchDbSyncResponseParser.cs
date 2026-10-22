using System.Text;
using System.Text.Json;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Parsing;

public static class CouchDbSyncResponseParser
{
    private const int MaximumDivisionEntries = 100;
    private const int MaximumWeightClasses = 100;
    private const int MaximumStringLength = 512;

    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
    {
        "barAndCollarsWeight",
        "clockState",
        "clockTimerLength",
        "currentAttemptId",
        "date",
        "dateFormat",
        "decisions",
        "divisions",
        "federation",
        "flight",
        "gender",
        "lifterId",
        "liftName",
        "lifts",
        "lot",
        "name",
        "platformId",
        "rawOrEquipped",
        "result",
        "scoreBy",
        "session",
        "showOnHomePage",
        "units",
        "usaplDivisionCode",
        "weight",
        "weightClasses"
    };

    public static CouchDbSyncSnapshot ParseSnapshot(string json)
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
            || !totalRows.TryGetInt64(out var totalDocumentCount)
            || totalDocumentCount != rows.GetArrayLength())
        {
            throw new JsonException("The CouchDB snapshot is incomplete or has an invalid document count.");
        }

        if (!root.TryGetProperty("update_seq", out var updateSequence)
            || !TryReadSequence(updateSequence, out var lastSequence))
        {
            throw new JsonException("The CouchDB snapshot must contain a valid update sequence.");
        }

        var documents = new List<CouchDbSyncDocument>();
        var documentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object
                || !row.TryGetProperty("id", out var rowIdElement)
                || rowIdElement.ValueKind != JsonValueKind.String
                || !row.TryGetProperty("doc", out var couchDocument)
                || couchDocument.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("A CouchDB snapshot row is missing its document.");
            }

            var rowId = rowIdElement.GetString();
            var documentId = GetRequiredString(couchDocument, "_id");
            if (rowId != documentId)
            {
                throw new JsonException("A CouchDB snapshot row ID does not match its document.");
            }

            if (IsDesignDocument(documentId))
            {
                continue;
            }

            if (!CouchDbDocumentIdValidator.IsValid(documentId)
                || !documentIds.Add(documentId))
            {
                throw new JsonException("The CouchDB snapshot contains an invalid or duplicate document ID.");
            }

            if (IsDeleted(couchDocument))
            {
                continue;
            }

            var revision = GetRequiredString(couchDocument, "_rev");
            documents.Add(new CouchDbSyncDocument(
                documentId,
                revision,
                SanitizeDocument(couchDocument)));
        }

        return new CouchDbSyncSnapshot(lastSequence, documents);
    }

    public static CouchDbSyncBatch ParseChanges(string json, int maximumResults)
    {
        if (maximumResults is < 1 or > CouchDbChangesRequestValidator.MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array
            || !root.TryGetProperty("last_seq", out var lastSequenceElement)
            || !TryReadSequence(lastSequenceElement, out var lastSequence))
        {
            throw new JsonException("The CouchDB changes response is incomplete.");
        }

        if (results.GetArrayLength() > maximumResults)
        {
            throw new JsonException("The CouchDB changes response exceeded the requested batch limit.");
        }

        long? pending = null;
        if (root.TryGetProperty("pending", out var pendingElement))
        {
            if (!pendingElement.TryGetInt64(out var pendingCount) || pendingCount < 0)
            {
                throw new JsonException("The CouchDB changes response has an invalid pending count.");
            }

            pending = pendingCount;
        }

        var changes = new List<CouchDbSyncChange>();
        foreach (var result in results.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.Object
                || !result.TryGetProperty("id", out var idElement)
                || idElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("A CouchDB changes result is missing its document ID.");
            }

            var id = idElement.GetString();
            if (string.IsNullOrEmpty(id) || !CouchDbDocumentIdValidator.IsValid(id))
            {
                throw new JsonException("A CouchDB changes result contains an invalid document ID.");
            }

            if (IsDesignDocument(id))
            {
                continue;
            }

            var isDeleted = result.TryGetProperty("deleted", out var deletedElement)
                && deletedElement.ValueKind == JsonValueKind.True;
            if (isDeleted)
            {
                changes.Add(new CouchDbSyncChange(id, true, null));
                continue;
            }

            if (!result.TryGetProperty("doc", out var couchDocument)
                || couchDocument.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("A CouchDB changes result is missing its document.");
            }

            var documentId = GetRequiredString(couchDocument, "_id");
            if (documentId != id)
            {
                throw new JsonException("A CouchDB changes result ID does not match its document.");
            }

            if (IsDeleted(couchDocument))
            {
                changes.Add(new CouchDbSyncChange(id, true, null));
                continue;
            }

            changes.Add(new CouchDbSyncChange(
                id,
                false,
                new CouchDbSyncDocument(
                    id,
                    GetRequiredString(couchDocument, "_rev"),
                    SanitizeDocument(couchDocument))));
        }

        return new CouchDbSyncBatch(lastSequence, changes, results.GetArrayLength(), pending);
    }

    public static IReadOnlyList<CouchDbPropertySummary> GetFieldSummary(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Stored competition document data must be an object.");
        }

        return document.RootElement.EnumerateObject()
            .Take(25)
            .Select(property => new CouchDbPropertySummary(
                property.Name,
                GetJsonType(property.Value.ValueKind)))
            .ToArray();
    }

    private static string SanitizeDocument(JsonElement document)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in document.EnumerateObject()
                         .Where(property => AllowedFields.Contains(property.Name))
                         .OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                if (WriteAllowedValue(writer, property.Name, property.Value))
                {
                    continue;
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static bool WriteAllowedValue(
        Utf8JsonWriter writer,
        string name,
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            return name switch
            {
                "decisions" => WriteDecisions(writer, name, value),
                "lifts" => WriteLifts(writer, name, value),
                "weightClasses" => WriteWeightClasses(writer, name, value),
                _ => false
            };
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return name switch
            {
                "divisions" => WriteDivisions(writer, name, value),
                _ => false
            };
        }

        return WriteScalarProperty(writer, name, value);
    }

    private static bool WriteDecisions(
        Utf8JsonWriter writer,
        string name,
        JsonElement value)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        foreach (var decision in value.EnumerateObject().Where(property =>
                     property.Name is "head" or "left" or "right"))
        {
            if (decision.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            writer.WritePropertyName(decision.Name);
            writer.WriteStartObject();
            foreach (var property in decision.Value.EnumerateObject())
            {
                if (property.Name == "decision")
                {
                    WriteScalarProperty(writer, property.Name, property.Value);
                }
                else if (property.Name == "cards"
                    && property.Value.ValueKind == JsonValueKind.Object)
                {
                    writer.WritePropertyName("cards");
                    writer.WriteStartObject();
                    foreach (var card in property.Value.EnumerateObject().Where(card =>
                                 card.Name is "blue" or "red" or "yellow"))
                    {
                        if (card.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        {
                            writer.WritePropertyName(card.Name);
                            card.Value.WriteTo(writer);
                        }
                    }

                    writer.WriteEndObject();
                }
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        return true;
    }

    private static bool WriteDivisions(
        Utf8JsonWriter writer,
        string name,
        JsonElement value)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (var division in value.EnumerateArray().Take(MaximumDivisionEntries))
        {
            if (division.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            writer.WriteStartObject();
            foreach (var property in division.EnumerateObject().Where(property =>
                         property.Name is "divisionId"
                             or "declaredAwardsWeightClassId"
                             or "rawOrEquipped"))
            {
                WriteScalarProperty(writer, property.Name, property.Value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        return true;
    }

    private static bool WriteWeightClasses(
        Utf8JsonWriter writer,
        string name,
        JsonElement value)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        foreach (var weightClass in value.EnumerateObject().Take(MaximumWeightClasses))
        {
            if (weightClass.Value.ValueKind != JsonValueKind.Object
                || weightClass.Name.Length > 100)
            {
                continue;
            }

            writer.WritePropertyName(weightClass.Name);
            writer.WriteStartObject();
            foreach (var property in weightClass.Value.EnumerateObject().Where(property =>
                         property.Name is "_id" or "maxWeight" or "name"))
            {
                WriteScalarProperty(writer, property.Name, property.Value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        return true;
    }

    private static bool WriteLifts(
        Utf8JsonWriter writer,
        string name,
        JsonElement value)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        foreach (var lift in value.EnumerateObject().Where(property =>
                     property.Name is "squat" or "bench" or "dead"))
        {
            if (lift.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                WriteScalarProperty(writer, lift.Name, lift.Value);
            }
        }

        writer.WriteEndObject();
        return true;
    }

    private static bool WriteScalarProperty(
        Utf8JsonWriter writer,
        string name,
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String
            && value.GetString() is not { Length: <= MaximumStringLength })
        {
            return false;
        }

        if (value.ValueKind is not (JsonValueKind.String
            or JsonValueKind.Number
            or JsonValueKind.True
            or JsonValueKind.False
            or JsonValueKind.Null))
        {
            return false;
        }

        writer.WritePropertyName(name);
        value.WriteTo(writer);
        return true;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(property.GetString()))
        {
            throw new JsonException($"A CouchDB document is missing '{propertyName}'.");
        }

        return property.GetString()!;
    }

    private static bool TryReadSequence(JsonElement element, out string sequence)
    {
        sequence = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            _ => string.Empty
        };

        return !string.IsNullOrEmpty(sequence)
            && sequence.Length <= CouchDbChangesRequestValidator.MaximumCheckpointLength;
    }

    private static bool IsDeleted(JsonElement document) =>
        document.TryGetProperty("_deleted", out var deleted)
        && deleted.ValueKind == JsonValueKind.True;

    private static bool IsDesignDocument(string documentId) =>
        documentId.StartsWith("_design/", StringComparison.Ordinal);

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

}
