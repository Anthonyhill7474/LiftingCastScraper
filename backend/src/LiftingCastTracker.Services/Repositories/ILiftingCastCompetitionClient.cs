using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Repositories;

public interface ILiftingCastCompetitionClient
{
    Task<CouchDbHttpResponse> GetSnapshotAsync(
        string competitionId,
        CancellationToken cancellationToken = default);

    Task<CouchDbHttpResponse> GetSyncSnapshotAsync(
        string competitionId,
        CancellationToken cancellationToken = default);

    Task<CouchDbHttpResponse> GetDocumentAsync(
        string competitionId,
        string documentId,
        CancellationToken cancellationToken = default);

    Task<CouchDbHttpResponse> CheckChangesAsync(
        string competitionId,
        CancellationToken cancellationToken = default);

    Task<CouchDbHttpResponse> GetChangesSinceAsync(
        string competitionId,
        string checkpoint,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CouchDbHttpResponse> GetSyncChangesAsync(
        string competitionId,
        string checkpoint,
        int limit,
        CancellationToken cancellationToken = default);
}
