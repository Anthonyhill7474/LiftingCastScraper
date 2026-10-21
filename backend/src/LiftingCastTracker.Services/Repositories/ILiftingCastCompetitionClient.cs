using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Repositories;

public interface ILiftingCastCompetitionClient
{
    Task<CouchDbHttpResponse> GetSnapshotAsync(
        string competitionId,
        CancellationToken cancellationToken = default);

    Task<CouchDbHttpResponse> CheckChangesAsync(
        string competitionId,
        CancellationToken cancellationToken = default);
}
