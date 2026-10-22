using System.Text.Json;
using System.Globalization;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Parsing;

public static class CompetitionDetailsMapper
{
    public static CompetitionPlatformsDto CreatePlatformDashboard(
        string competitionId,
        IReadOnlyCollection<CompetitionDocument> documents,
        DateTimeOffset lastSyncedAtUtc)
    {
        var parsedDocuments = new List<ParsedDocument>(documents.Count);
        try
        {
            foreach (var document in documents)
            {
                parsedDocuments.Add(new ParsedDocument(
                    document,
                    JsonDocument.Parse(document.DocumentDataJson)));
            }

            var lifterDocuments = parsedDocuments
                .Where(item => IsLifter(item.Root))
                .ToDictionary(item => item.Document.DocumentId, StringComparer.Ordinal);
            var attemptsById = new Dictionary<string, (string LifterId, CompetitionAttemptDto Attempt)>(
                StringComparer.Ordinal);
            foreach (var parsed in parsedDocuments)
            {
                if (TryMapAttempt(parsed, out var lifterId, out var attempt))
                {
                    attemptsById[parsed.Document.DocumentId] = (lifterId, attempt);
                }
            }

            var platforms = new List<CompetitionPlatformDashboardDto>();
            foreach (var parsed in parsedDocuments)
            {
                if (!TryMapPlatform(parsed, out var platform))
                {
                    continue;
                }

                CompetitionPlatformCurrentAttemptDto? currentAttempt = null;
                if (platform.CurrentAttemptId is { } attemptId
                    && attemptsById.TryGetValue(attemptId, out var attemptRecord))
                {
                    var lifter = lifterDocuments.TryGetValue(attemptRecord.LifterId, out var lifterDocument)
                        ? MapPlatformLifter(lifterDocument)
                        : null;
                    currentAttempt = new CompetitionPlatformCurrentAttemptDto(
                        attemptRecord.Attempt.DocumentId,
                        attemptRecord.Attempt.LiftName,
                        attemptRecord.Attempt.AttemptNumber,
                        attemptRecord.Attempt.Weight,
                        lifter);
                }

                platforms.Add(new CompetitionPlatformDashboardDto(
                    platform.DocumentId,
                    platform.Name,
                    GetValue(parsed.Root, "clockState"),
                    platform.ClockTimerLength,
                    platform.CurrentAttemptId,
                    currentAttempt));
            }

            return new CompetitionPlatformsDto(
                competitionId,
                lastSyncedAtUtc,
                platforms.OrderBy(platform => platform.PlatformId, StringComparer.Ordinal).ToArray());
        }
        finally
        {
            foreach (var document in parsedDocuments)
            {
                document.Json.Dispose();
            }
        }
    }

    public static CompetitionDetailsDto Create(
        Competition competition,
        IReadOnlyCollection<CompetitionDocument> documents,
        DateTimeOffset? lastSyncedAtUtc)
    {
        var parsedDocuments = new List<ParsedDocument>(documents.Count);
        try
        {
            foreach (var document in documents)
            {
                parsedDocuments.Add(new ParsedDocument(
                    document,
                    JsonDocument.Parse(document.DocumentDataJson)));
            }

            var divisionConfigurations = parsedDocuments
                .Where(item => item.Root.TryGetProperty("weightClasses", out _))
                .ToDictionary(
                    item => item.Document.DocumentId,
                    item => item.Root,
                    StringComparer.Ordinal);

            var lifterDocuments = parsedDocuments
                .Where(item => IsLifter(item.Root))
                .ToDictionary(item => item.Document.DocumentId, StringComparer.Ordinal);
            var attemptsByLifter = new Dictionary<string, List<CompetitionAttemptDto>>(StringComparer.Ordinal);
            var platforms = new List<CompetitionPlatformDto>();

            foreach (var parsed in parsedDocuments)
            {
                if (TryMapPlatform(parsed, out var platform))
                {
                    platforms.Add(platform);
                }

                if (TryMapAttempt(parsed, out var lifterId, out var attempt)
                    && lifterDocuments.ContainsKey(lifterId))
                {
                    if (!attemptsByLifter.TryGetValue(lifterId, out var attempts))
                    {
                        attempts = [];
                        attemptsByLifter.Add(lifterId, attempts);
                    }

                    attempts.Add(attempt);
                }
            }

            var lifters = lifterDocuments.Values
                .Select(item => MapLifter(
                    item,
                    attemptsByLifter.GetValueOrDefault(item.Document.DocumentId, []),
                    divisionConfigurations))
                .OrderBy(lifter => lifter.Flight, StringComparer.OrdinalIgnoreCase)
                .ThenBy(lifter => lifter.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(lifter => lifter.DocumentId, StringComparer.Ordinal)
                .ToArray();

            return new CompetitionDetailsDto(
                competition.Id,
                competition.Name,
                competition.Date,
                competition.DateFormat,
                lastSyncedAtUtc,
                platforms.OrderBy(platform => platform.DocumentId, StringComparer.Ordinal).ToArray(),
                lifters);
        }
        finally
        {
            foreach (var document in parsedDocuments)
            {
                document.Json.Dispose();
            }
        }
    }

    private static CompetitionLifterDto MapLifter(
        ParsedDocument lifter,
        IReadOnlyCollection<CompetitionAttemptDto> attempts,
        IReadOnlyDictionary<string, JsonElement> divisionConfigurations)
    {
        var root = lifter.Root;
        var divisions = new List<CompetitionDivisionDto>();
        if (root.TryGetProperty("divisions", out var divisionList)
            && divisionList.ValueKind == JsonValueKind.Array)
        {
            foreach (var division in divisionList.EnumerateArray())
            {
                if (division.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var divisionId = GetString(division, "divisionId");
                var weightClassId = GetString(division, "declaredAwardsWeightClassId");
                var config = divisionId is not null
                    && divisionConfigurations.TryGetValue(divisionId, out var configuration)
                        ? configuration
                        : default;
                divisions.Add(new CompetitionDivisionDto(
                    divisionId,
                    GetString(config, "name"),
                    weightClassId,
                    GetWeightClassName(config, weightClassId),
                    GetString(division, "rawOrEquipped")));
            }
        }

        return new CompetitionLifterDto(
            lifter.Document.DocumentId,
            GetString(root, "name"),
            divisions,
            GetString(root, "flight"),
            GetValue(root, "session"),
            GetValue(root, "lot"),
            GetValue(root, "bodyWeight"),
            attempts
                .OrderBy(attempt => GetNumericValue(attempt.AttemptNumber))
                .ThenBy(attempt => attempt.DocumentId, StringComparer.Ordinal)
                .ToArray());
    }

    private static bool IsLifter(JsonElement document) =>
        !document.TryGetProperty("lifterId", out _)
        && (document.TryGetProperty("divisions", out _)
            || document.TryGetProperty("flight", out _)
            || document.TryGetProperty("session", out _)
            || document.TryGetProperty("lot", out _)
            || document.TryGetProperty("bodyWeight", out _));

    private static bool TryMapAttempt(
        ParsedDocument parsed,
        out string lifterId,
        out CompetitionAttemptDto attempt)
    {
        var root = parsed.Root;
        if (GetString(root, "lifterId") is { } id
            && root.TryGetProperty("liftName", out var liftName)
            && liftName.ValueKind == JsonValueKind.String)
        {
            lifterId = id;
            attempt = new CompetitionAttemptDto(
                parsed.Document.DocumentId,
                liftName.GetString(),
                GetValue(root, "attemptNumber"),
                GetValue(root, "weight"),
                GetValue(root, "result"));
            return true;
        }

        lifterId = string.Empty;
        attempt = null!;
        return false;
    }

    private static bool TryMapPlatform(ParsedDocument parsed, out CompetitionPlatformDto platform)
    {
        var root = parsed.Root;
        if (IsPlatformRuntime(root))
        {
            platform = new CompetitionPlatformDto(
                parsed.Document.DocumentId,
                GetString(root, "name"),
                GetString(root, "clockState"),
                GetValue(root, "clockTimerLength"),
                GetString(root, "currentAttemptId"));
            return true;
        }

        platform = null!;
        return false;
    }

    private static bool IsPlatformRuntime(JsonElement root) =>
        root.TryGetProperty("clockState", out _)
        || root.TryGetProperty("clockTimerLength", out _)
        || root.TryGetProperty("currentAttemptId", out _)
        || root.TryGetProperty("barAndCollarsWeight", out _)
        || root.TryGetProperty("liveStreamLink", out _);

    private static CompetitionPlatformLifterDto MapPlatformLifter(ParsedDocument lifter) =>
        new(
            lifter.Document.DocumentId,
            GetString(lifter.Root, "name"),
            GetString(lifter.Root, "flight"),
            GetValue(lifter.Root, "session"),
            GetValue(lifter.Root, "lot"),
            GetValue(lifter.Root, "bodyWeight"));

    private static string? GetWeightClassName(JsonElement configuration, string? weightClassId)
    {
        if (weightClassId is null
            || configuration.ValueKind != JsonValueKind.Object
            || !configuration.TryGetProperty("weightClasses", out var weightClasses)
            || weightClasses.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var item in weightClasses.EnumerateObject())
        {
            if ((item.Name == weightClassId
                    || GetString(item.Value, "_id") == weightClassId)
                && GetString(item.Value, "name") is { } name)
            {
                return name;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static JsonElement? GetValue(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
                ? value.Clone()
                : null;

    private static double GetNumericValue(JsonElement? value)
    {
        if (value is not { } element)
        {
            return double.MaxValue;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number))
        {
            return number;
        }

        if (element.ValueKind == JsonValueKind.String
            && double.TryParse(
                element.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out number))
        {
            return number;
        }

        return double.MaxValue;
    }

    private sealed record ParsedDocument(CompetitionDocument Document, JsonDocument Json)
    {
        public JsonElement Root => Json.RootElement;
    }
}
