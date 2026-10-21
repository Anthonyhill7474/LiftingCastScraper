using Microsoft.EntityFrameworkCore;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Repositories;
using LiftingCastTracker.Repositories.Data;

namespace LiftingCastTracker.Repositories.Implementations;

public class CompetitionRepository : ICompetitionRepository
{
    private readonly ApplicationDbContext _context;

    public CompetitionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Competition>> GetAllAsync()
    {
        return await _context.Competitions
            .OrderByDescending(c => c.CreateDate)
            .ToListAsync();
    }

    public async Task<Competition?> GetByIdAsync(string id)
    {
        return await _context.Competitions.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Competition>> GetByIdsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.Competitions
            .Where(competition => ids.Contains(competition.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveImportedAsync(
        IReadOnlyCollection<Competition> inserted,
        IReadOnlyCollection<Competition> updated,
        CancellationToken cancellationToken = default)
    {
        _context.Competitions.AddRange(inserted);
        _context.Competitions.UpdateRange(updated);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
