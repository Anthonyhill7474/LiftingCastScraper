using LiftingCastTracker.Services.Import;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Parsing;
using LiftingCastTracker.Services.Repositories;

namespace LiftingCastTracker.Tests;

public class CompetitionImportTests
{
    [Fact]
    public void ParsesCompetitionDirectoryMetadata()
    {
        const string json = """
            {
              "docs": [
                {
                  "_id": "mnk13ga524h1",
                  "name": "Leviathan Unchained",
                  "date": "24/10/2026",
                  "showOnHomePage": true,
                  "createDate": "2026-10-21T10:45:13+11:00",
                  "dateFormat": "DD/MM/YYYY"
                }
              ]
            }
            """;

        var result = CompetitionDirectoryParser.Parse(json);
        var competition = Assert.Single(result.Competitions);

        Assert.Equal(0, result.InvalidRecords);
        Assert.Equal("mnk13ga524h1", competition.Id);
        Assert.Equal("Leviathan Unchained", competition.Name);
        Assert.Equal("24/10/2026", competition.Date);
        Assert.Equal("DD/MM/YYYY", competition.DateFormat);
        Assert.True(competition.ShowOnHomePage);
        Assert.Equal(new DateTime(2026, 10, 20, 23, 45, 13, DateTimeKind.Utc), competition.CreateDate);
        Assert.Equal(new DateOnly(2026, 10, 24), competition.ParsedDate);
    }

    [Theory]
    [InlineData("24/10/2026", "DD/MM/YYYY", 2026, 10, 24)]
    [InlineData("10/24/2026", "MM/DD/YYYY", 2026, 10, 24)]
    [InlineData("2026/10/24", "YYYY/MM/DD", 2026, 10, 24)]
    public void ParsesDateUsingItsDeclaredFormat(
        string date,
        string format,
        int year,
        int month,
        int day)
    {
        Assert.Equal(
            new DateOnly(year, month, day),
            CompetitionDateParser.Parse(date, format));
    }

    [Fact]
    public void ReturnsNullForAnInvalidCompetitionDate()
    {
        Assert.Null(CompetitionDateParser.Parse("31/02/2026", "DD/MM/YYYY"));
        Assert.Null(CompetitionDateParser.Parse("10/24/2026", "DD/MM/YYYY"));
    }

    [Fact]
    public void AllowsEmptyNameAndDateAndMissingOptionalFields()
    {
        const string json = """{"docs":[{"_id":"empty","name":"","date":""}]}""";

        var result = CompetitionDirectoryParser.Parse(json);
        var competition = Assert.Single(result.Competitions);

        Assert.Equal(0, result.InvalidRecords);
        Assert.Equal(string.Empty, competition.Name);
        Assert.Equal(string.Empty, competition.Date);
        Assert.Null(competition.DateFormat);
        Assert.Null(competition.CreateDate);
        Assert.False(competition.ShowOnHomePage);
        Assert.Null(competition.ParsedDate);
    }

    [Fact]
    public async Task RepeatedImportsLeaveOneUnchangedCompetition()
    {
        var repository = new InMemoryCompetitionRepository();
        var importer = new CompetitionImportService(repository);
        const string json = """{"docs":[{"_id":"meet-1","name":"Open","date":"24/10/2026","dateFormat":"DD/MM/YYYY"}]}""";

        var firstImport = await importer.ImportAsync(json);
        var secondImport = await importer.ImportAsync(json);

        Assert.Equal(new CompetitionImportSummary(1, 0, 0, 0), firstImport);
        Assert.Equal(new CompetitionImportSummary(0, 0, 1, 0), secondImport);
        Assert.Single(repository.Competitions);
    }

    [Fact]
    public async Task UpdatesChangedMetadataAndCountsInvalidRecords()
    {
        var repository = new InMemoryCompetitionRepository();
        var importer = new CompetitionImportService(repository);

        await importer.ImportAsync("""{"docs":[{"_id":"meet-1","name":"Old name"}]}""");
        repository.Competitions[0].AutomaticSyncEnabled = true;
        var result = await importer.ImportAsync(
            """{"docs":[{"_id":"meet-1","name":"New name","showOnHomePage":true},{"name":"No identifier"}]}""");

        Assert.Equal(new CompetitionImportSummary(0, 1, 0, 1), result);
        Assert.Equal("New name", Assert.Single(repository.Competitions).Name);
        Assert.True(repository.Competitions[0].ShowOnHomePage);
        Assert.True(repository.Competitions[0].AutomaticSyncEnabled);
    }

    private sealed class InMemoryCompetitionRepository : ICompetitionRepository
    {
        private readonly Dictionary<string, Competition> _competitions = new(StringComparer.Ordinal);

        public List<Competition> Competitions => _competitions.Values.ToList();

        public Task<List<Competition>> GetAllAsync() =>
            Task.FromResult(_competitions.Values.ToList());

        public Task<Competition?> GetByIdAsync(string id) =>
            Task.FromResult(_competitions.GetValueOrDefault(id));

        public Task<List<Competition>> GetByIdsAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ids
                .Where(_competitions.ContainsKey)
                .Select(id => _competitions[id])
                .ToList());

        public Task SaveImportedAsync(
            IReadOnlyCollection<Competition> inserted,
            IReadOnlyCollection<Competition> updated,
            CancellationToken cancellationToken = default)
        {
            foreach (var competition in inserted.Concat(updated))
            {
                _competitions[competition.Id] = competition;
            }

            return Task.CompletedTask;
        }
    }
}
