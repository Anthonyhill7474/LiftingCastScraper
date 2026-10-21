using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Repositories;

public interface ICompetitionRepository
{
    Task<List<Competition>> GetAllAsync();
    Task<Competition?> GetByIdAsync(string id);
}
