using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Parsing;
using LiftingCastTracker.Services.Repositories;

namespace LiftingCastTracker.Services.Import;

public sealed class CompetitionImportService
{
    private readonly ICompetitionRepository _repository;

    public CompetitionImportService(ICompetitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<CompetitionImportSummary> ImportAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        var directory = CompetitionDirectoryParser.Parse(json);
        var existingCompetitions = await _repository.GetByIdsAsync(
            directory.Competitions.Select(competition => competition.Id).ToArray(),
            cancellationToken);
        var existingById = existingCompetitions.ToDictionary(
            competition => competition.Id,
            StringComparer.Ordinal);
        var inserted = new List<Competition>();
        var updated = new List<Competition>();
        var unchanged = 0;

        foreach (var competition in directory.Competitions)
        {
            if (!existingById.TryGetValue(competition.Id, out var existing))
            {
                inserted.Add(competition);
                continue;
            }

            if (HasSameMetadata(existing, competition))
            {
                unchanged++;
                continue;
            }

            CopyMetadata(competition, existing);
            updated.Add(existing);
        }

        await _repository.SaveImportedAsync(inserted, updated, cancellationToken);

        return new CompetitionImportSummary(
            inserted.Count,
            updated.Count,
            unchanged,
            directory.InvalidRecords);
    }

    private static bool HasSameMetadata(Competition existing, Competition incoming) =>
        existing.Name == incoming.Name
        && existing.Date == incoming.Date
        && existing.DateFormat == incoming.DateFormat
        && existing.CreateDate == incoming.CreateDate
        && existing.ShowOnHomePage == incoming.ShowOnHomePage;

    private static void CopyMetadata(Competition source, Competition destination)
    {
        destination.Name = source.Name;
        destination.Date = source.Date;
        destination.DateFormat = source.DateFormat;
        destination.CreateDate = source.CreateDate;
        destination.ShowOnHomePage = source.ShowOnHomePage;
    }
}
