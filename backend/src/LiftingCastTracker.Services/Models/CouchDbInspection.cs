using System.Text.Json;

namespace LiftingCastTracker.Services.Models;

public sealed record CouchDbHttpResponse(
    int StatusCode,
    string Body,
    DateTimeOffset? RetryAfterUtc = null);

public sealed record AutomaticSyncCandidate(
    string CompetitionId,
    string CompetitionName,
    DateOnly? CompetitionDate,
    DateTimeOffset? LastSyncedAtUtc);

public sealed record CouchDbSnapshotSummary(
    string CompetitionId,
    long TotalDocuments,
    JsonElement? UpdateSequence,
    int InspectedDocuments,
    IReadOnlyList<CouchDbDocumentStructure> Structures,
    DateTimeOffset RetrievedAtUtc);

public sealed record CouchDbDocumentStructure(
    int Occurrences,
    IReadOnlyList<string> SampleDocumentIds,
    IReadOnlyList<CouchDbPropertySummary> Fields,
    bool FieldsTruncated);

public sealed record CouchDbPropertySummary(string Name, string Type);

public sealed record CouchDbJsonNodePreview(
    string Name,
    string Type,
    IReadOnlyList<CouchDbJsonNodePreview> Children,
    bool IsTruncated);

public sealed record CouchDbDocumentPreview(
    string CompetitionId,
    string DocumentId,
    IReadOnlyList<CouchDbJsonNodePreview> Fields,
    DateTimeOffset RetrievedAtUtc,
    bool FieldsTruncated);

public sealed record CouchDbChangesRequest(string? Since, int Limit = 10);

public sealed record CouchDbChangedDocument(
    string Id,
    IReadOnlyList<string> Revisions,
    bool Deleted);

public sealed record CouchDbChangesDiagnostic(
    string CompetitionId,
    string SinceSequence,
    int HttpStatusCode,
    bool IsValidChangesFeed,
    int? ResultCount,
    IReadOnlyList<CouchDbChangedDocument> Changes,
    JsonElement? LastSequence,
    DateTimeOffset CheckedAtUtc);

public sealed class CompetitionDocument
{
    public string CompetitionId { get; set; } = null!;
    public string DocumentId { get; set; } = null!;
    public string Revision { get; set; } = null!;
    public string DocumentDataJson { get; set; } = null!;
    public DateTimeOffset LastSyncedAt { get; set; }
}

public sealed class CompetitionSyncState
{
    public string CompetitionId { get; set; } = null!;
    public string LastSequence { get; set; } = null!;
    public DateTimeOffset LastSyncedAt { get; set; }
}

public sealed record CouchDbSyncDocument(string Id, string Revision, string DataJson);

public sealed record CouchDbSyncChange(string Id, bool Deleted, CouchDbSyncDocument? Document);

public sealed record CouchDbSyncSnapshot(
    string LastSequence,
    IReadOnlyList<CouchDbSyncDocument> Documents);

public sealed record CouchDbSyncBatch(
    string LastSequence,
    IReadOnlyList<CouchDbSyncChange> Changes,
    int ResultCount,
    long? Pending);

public sealed record CompetitionSyncCounts(int Inserted, int Updated, int Unchanged, int Deleted)
{
    public static CompetitionSyncCounts operator +(
        CompetitionSyncCounts left,
        CompetitionSyncCounts right) =>
        new(
            left.Inserted + right.Inserted,
            left.Updated + right.Updated,
            left.Unchanged + right.Unchanged,
            left.Deleted + right.Deleted);
}

public sealed record CompetitionSyncResult(
    string CompetitionId,
    bool InitialSync,
    int Inserted,
    int Updated,
    int Unchanged,
    int Deleted,
    int Batches,
    string LastSequence,
    bool HasMore,
    DateTimeOffset SyncedAtUtc,
    bool FullRefresh);

public sealed record SynchronizedDocumentSummary(
    string DocumentId,
    string Revision,
    IReadOnlyList<CouchDbPropertySummary> Fields,
    DateTimeOffset LastSyncedAt);

public sealed record SynchronizedDocumentsPage(
    string CompetitionId,
    int TotalDocuments,
    int Offset,
    int Limit,
    IReadOnlyList<SynchronizedDocumentSummary> Documents);

public sealed record CompetitionDetailsDto(
    string CompetitionId,
    string Name,
    string? Date,
    string? DateFormat,
    DateTimeOffset? LastSyncedAtUtc,
    IReadOnlyList<CompetitionPlatformDto> Platforms,
    IReadOnlyList<CompetitionLifterDto> Lifters);

public sealed record CompetitionPlatformDto(
    string DocumentId,
    string? Name,
    string? ClockState,
    JsonElement? ClockTimerLength,
    string? CurrentAttemptId);

public sealed record CompetitionPlatformsDto(
    string CompetitionId,
    DateTimeOffset LastSyncedAtUtc,
    IReadOnlyList<CompetitionPlatformDashboardDto> Platforms);

public sealed record CompetitionPlatformDashboardDto(
    string PlatformId,
    string? Name,
    JsonElement? ClockState,
    JsonElement? ClockTimerLength,
    string? CurrentAttemptId,
    CompetitionPlatformCurrentAttemptDto? CurrentAttempt);

public sealed record CompetitionPlatformCurrentAttemptDto(
    string AttemptId,
    string? LiftName,
    JsonElement? AttemptNumber,
    JsonElement? Weight,
    CompetitionPlatformLifterDto? Lifter);

public sealed record CompetitionPlatformLifterDto(
    string LifterId,
    string? Name,
    string? Flight,
    JsonElement? Session,
    JsonElement? Lot,
    JsonElement? BodyWeight);

public sealed record CompetitionLifterDto(
    string DocumentId,
    string? Name,
    IReadOnlyList<CompetitionDivisionDto> Divisions,
    string? Flight,
    JsonElement? Session,
    JsonElement? Lot,
    JsonElement? BodyWeight,
    IReadOnlyList<CompetitionAttemptDto> Attempts);

public sealed record CompetitionDivisionDto(
    string? DivisionId,
    string? Name,
    string? WeightClassId,
    string? WeightClassName,
    string? RawOrEquipped);

public sealed record CompetitionAttemptDto(
    string DocumentId,
    string? LiftName,
    JsonElement? AttemptNumber,
    JsonElement? Weight,
    JsonElement? Result);
