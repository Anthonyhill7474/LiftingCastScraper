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
}
