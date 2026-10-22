using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Repositories;

public interface ICompetitionSyncRepository
{
    Task<CompetitionSyncState?> GetStateAsync(
        string competitionId,
        CancellationToken cancellationToken = default);

    Task<CompetitionSyncCounts> SaveSnapshotAsync(
        string competitionId,
        IReadOnlyCollection<CouchDbSyncDocument> documents,
        string lastSequence,
        DateTimeOffset syncedAtUtc,
        CancellationToken cancellationToken = default);

    Task<CompetitionSyncCounts> ApplyChangesAsync(
        string competitionId,
        IReadOnlyCollection<CouchDbSyncChange> changes,
        string lastSequence,
        DateTimeOffset syncedAtUtc,
        CancellationToken cancellationToken = default);

    Task<(int TotalDocuments, IReadOnlyList<CompetitionDocument> Documents)> GetDocumentsAsync(
        string competitionId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);
}
