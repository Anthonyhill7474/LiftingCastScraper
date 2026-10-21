using System.Text.Json;

namespace LiftingCastTracker.Services.Models;

public sealed record CouchDbHttpResponse(int StatusCode, string Body);

public sealed record CouchDbSnapshotSummary(
    string CompetitionId,
    long TotalDocuments,
    JsonElement? UpdateSequence,
    IReadOnlyList<CouchDbDocumentSample> SampleDocuments,
    DateTimeOffset RetrievedAtUtc);

public sealed record CouchDbDocumentSample(
    string? Id,
    IReadOnlyList<CouchDbPropertySummary> Properties);

public sealed record CouchDbPropertySummary(string Name, string Type);

public sealed record CouchDbChangesDiagnostic(
    string CompetitionId,
    int HttpStatusCode,
    bool IsValidChangesFeed,
    int? ResultCount,
    JsonElement? LastSequence,
    DateTimeOffset CheckedAtUtc);
