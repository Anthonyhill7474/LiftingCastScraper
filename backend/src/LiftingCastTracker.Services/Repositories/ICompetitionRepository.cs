using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Repositories;

public interface ICompetitionRepository
{
    Task<List<Competition>> GetAllAsync();
    Task<Competition?> GetByIdAsync(string id);
    Task<List<Competition>> GetByIdsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default);
    Task SaveImportedAsync(
        IReadOnlyCollection<Competition> inserted,
        IReadOnlyCollection<Competition> updated,
        CancellationToken cancellationToken = default);
}
