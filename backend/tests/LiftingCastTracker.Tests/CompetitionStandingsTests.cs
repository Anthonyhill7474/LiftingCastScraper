using System.Text.Json;
using LiftingCastTracker.Services.Calculations;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Parsing;

namespace LiftingCastTracker.Tests;

public sealed class CompetitionStandingsTests
{
    [Fact]
    public void CreatesSeparateCategoriesForEveryDivisionAndWeightClassMembership()
    {
        var documents = new[]
        {
            Division("division:open", "Open", "MALE", "RAW", "TOTAL", Powerlifting, ("class:83", "83 kg")),
            Division("division:masters", "Masters", "MALE", "RAW", "TOTAL", Powerlifting, ("class:83", "83 kg")),
            Division("division:bench", "Bench only", "MALE", "RAW", "TOTAL", BenchOnly, ("class:83", "83 kg")),
            Division("division:female", "Female Open", "FEMALE", "EQUIPPED", "TOTAL", Powerlifting, ("class:83", "83 kg")),
            Lifter("lifter:multi", "Multi-division athlete", 80,
                ("division:open", "class:83"),
                ("division:masters", "class:83"),
                ("division:bench", "class:83")),
            Lifter("lifter:open-opponent", "Open opponent", 81, ("division:open", "class:83")),
            Lifter("lifter:female", "Female athlete", 82, ("division:female", "class:83")),
            Attempt("attempt:multi-squat", "lifter:multi", "squat", 200, "good"),
            Attempt("attempt:multi-bench", "lifter:multi", "bench", 120, "good"),
            Attempt("attempt:multi-dead", "lifter:multi", "dead", 220, "good"),
            Attempt("attempt:opponent-squat", "lifter:open-opponent", "squat", 180, "good"),
            Attempt("attempt:opponent-bench", "lifter:open-opponent", "bench", 110, "good"),
            Attempt("attempt:opponent-dead", "lifter:open-opponent", "dead", 200, "good"),
            Attempt("attempt:female-squat", "lifter:female", "squat", 120, "good"),
            Attempt("attempt:female-bench", "lifter:female", "bench", 70, "good"),
            Attempt("attempt:female-dead", "lifter:female", "dead", 130, "good"),
            Metadata
        };

        var result = CreateStandings(documents);

        Assert.Equal(4, result.WeightClasses.Count);
        var open = Assert.Single(result.WeightClasses, category => category.DivisionId == "division:open");
        var masters = Assert.Single(result.WeightClasses, category => category.DivisionId == "division:masters");
        var bench = Assert.Single(result.WeightClasses, category => category.EventType == "bench-only");
        var female = Assert.Single(result.WeightClasses, category =>
            string.Equals(category.Gender, "female", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("83 kg", open.WeightClassName);
        Assert.Single(open.Standings, standing => standing.AthleteId == "lifter:multi");
        Assert.Single(masters.Standings, standing => standing.AthleteId == "lifter:multi");
        Assert.Single(bench.Standings, standing => standing.AthleteId == "lifter:multi");
        Assert.Equal("classic", open.EquipmentCategory?.ToLowerInvariant());
        Assert.Equal("equipped", female.EquipmentCategory?.ToLowerInvariant());

        var overall = Assert.Single(result.OverallCategories, category =>
            category.Gender.Equals("male", StringComparison.OrdinalIgnoreCase)
            && category.EventType == "full-power"
            && string.Equals(category.EquipmentCategory, "classic", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, overall.Standings.Count);
        Assert.Single(overall.Standings, standing => standing.AthleteId == "lifter:multi");
        Assert.Contains(result.OverallCategories, category => category.EventType == "bench-only");
        Assert.Contains(result.OverallCategories, category =>
            string.Equals(category.Gender, "female", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RanksOnlyCompleteSuccessfulTotalsAndKeepsProjectionSeparate()
    {
        var documents = new[]
        {
            Division("division:open", "Open", "MALE", "RAW", "TOTAL", Powerlifting, ("class:83", "83 kg")),
            Lifter("lifter:tie-a", "Tie A", 80, ("division:open", "class:83")),
            Lifter("lifter:tie-b", "Tie B", 81, ("division:open", "class:83")),
            Lifter("lifter:pending", "Pending lifter", 82, ("division:open", "class:83")),
            Lifter("lifter:failed", "Failed lifter", 83, ("division:open", "class:83")),
            Attempt("attempt:tie-a-squat", "lifter:tie-a", "squat", 200, "good"),
            Attempt("attempt:tie-a-bench", "lifter:tie-a", "bench", 100, "good"),
            Attempt("attempt:tie-a-dead", "lifter:tie-a", "dead", 100, "good"),
            Attempt("attempt:tie-b-squat", "lifter:tie-b", "squat", 190, "good"),
            Attempt("attempt:tie-b-bench", "lifter:tie-b", "bench", 110, "good"),
            Attempt("attempt:tie-b-dead", "lifter:tie-b", "dead", 100, "good"),
            Attempt("attempt:pending-squat", "lifter:pending", "squat", 200, "good"),
            Attempt("attempt:pending-bench", "lifter:pending", "bench", 100, "good"),
            Attempt("attempt:pending-dead", "lifter:pending", "dead", 150, null),
            Attempt("attempt:failed-squat", "lifter:failed", "squat", 250, "bad"),
            Attempt("attempt:failed-bench", "lifter:failed", "bench", 150, "bad"),
            Attempt("attempt:failed-dead", "lifter:failed", "dead", 250, "bad"),
            Metadata
        };

        var result = CreateStandings(documents);
        var leaderboard = Assert.Single(result.WeightClasses);
        var tieA = Standing(leaderboard, "lifter:tie-a");
        var tieB = Standing(leaderboard, "lifter:tie-b");
        var pending = Standing(leaderboard, "lifter:pending");
        var failed = Standing(leaderboard, "lifter:failed");

        Assert.Equal(400, tieA.ConfirmedResultKg);
        Assert.Equal(400, tieB.ConfirmedResultKg);
        Assert.Null(tieA.ConfirmedRank);
        Assert.Null(tieB.ConfirmedRank);
        Assert.Equal("tied", tieA.ConfirmedStatus);
        Assert.Equal("tied", tieB.ConfirmedStatus);
        Assert.Equal(300, pending.SuccessfulLiftSumKg);
        Assert.Null(pending.ConfirmedResultKg);
        Assert.Null(pending.ConfirmedRank);
        Assert.Equal("incomplete", pending.ConfirmedStatus);
        Assert.True(pending.HasPendingAttempts);
        Assert.Equal(450, pending.ProjectedResultKg);
        Assert.Equal(1, pending.ProjectedRank);
        Assert.Equal("ranked", pending.ProjectedStatus);
        Assert.Null(failed.SuccessfulLiftSumKg);
        Assert.Null(failed.ConfirmedRank);
        Assert.Equal("unavailable", failed.ConfirmedStatus);
        Assert.Equal(2, leaderboard.Standings.Count(standing => standing.ConfirmedStatus == "tied"));
    }

    [Fact]
    public void KeepsUnresolvedMembershipsVisibleButDoesNotInventRankingRules()
    {
        var documents = new[]
        {
            Division("division:points", "Points division", "MALE", "RAW", "IPF_POINTS", Powerlifting, ("class:83", "83 kg")),
            Division("division:unresolved", "Unresolved division", "MALE", "RAW", "TOTAL", Powerlifting),
            Lifter(
                "lifter:one",
                "Athlete",
                80,
                ("division:points", "class:83"),
                ("division:unresolved", "class:missing"),
                (null, null)),
            Attempt("attempt:one-squat", "lifter:one", "squat", 200, "good"),
            Attempt("attempt:one-bench", "lifter:one", "bench", 100, "good"),
            Attempt("attempt:one-dead", "lifter:one", "dead", 200, "good"),
            Metadata
        };

        var result = CreateStandings(documents);

        Assert.Equal(1, result.UnresolvedMembershipCount);
        var pointsCategory = Assert.Single(
            result.WeightClasses,
            category => category.DivisionId == "division:points");
        Assert.Equal("IPF_POINTS", pointsCategory.ScoreBy);
        Assert.Equal("The configured scoreBy is not TOTAL; the award scoring basis is not verified.", pointsCategory.RankingLimitation);
        Assert.Null(Assert.Single(pointsCategory.Standings).ConfirmedRank);
        Assert.Equal("unrankable", Assert.Single(pointsCategory.Standings).ConfirmedStatus);

        var unresolvedClass = Assert.Single(
            result.WeightClasses,
            category => category.DivisionId == "division:unresolved");
        Assert.Equal("class:missing", unresolvedClass.WeightClassId);
        Assert.Null(unresolvedClass.WeightClassName);
        Assert.NotNull(unresolvedClass.RankingLimitation);
    }

    [Fact]
    public void OverallRankingsUseEveryExistingPointsSystemWithoutDuplicatingMemberships()
    {
        var documents = new[]
        {
            Division("division:light", "Light", "MALE", "RAW", "TOTAL", Powerlifting, ("class:47", "47 kg")),
            Division("division:light-masters", "Light Masters", "MALE", "CLASSIC", "TOTAL", Powerlifting, ("class:47-m", "47 kg")),
            Division("division:heavy", "Heavy", "MALE", "RAW", "TOTAL", Powerlifting, ("class:52", "52 kg")),
            Division("division:female", "Female", "FEMALE", "RAW", "TOTAL", Powerlifting, ("class:f", "57 kg")),
            Lifter("lifter:light", "Lighter", 45,
                ("division:light", "class:47"),
                ("division:light-masters", "class:47-m")),
            Lifter("lifter:heavy", "Heavier", 50, ("division:heavy", "class:52")),
            Lifter("lifter:female", "Female", 55, ("division:female", "class:f")),
            Attempt("attempt:light-squat", "lifter:light", "squat", 100, "good"),
            Attempt("attempt:light-bench", "lifter:light", "bench", 50, "good"),
            Attempt("attempt:light-dead", "lifter:light", "dead", 100, "good"),
            Attempt("attempt:heavy-squat", "lifter:heavy", "squat", 110, "good"),
            Attempt("attempt:heavy-bench", "lifter:heavy", "bench", 55, "good"),
            Attempt("attempt:heavy-dead", "lifter:heavy", "dead", 110, "good"),
            Attempt("attempt:female-squat", "lifter:female", "squat", 100, "good"),
            Attempt("attempt:female-bench", "lifter:female", "bench", 50, "good"),
            Attempt("attempt:female-dead", "lifter:female", "dead", 100, "good"),
            Metadata
        };

        var result = CreateStandings(documents);
        var male = Assert.Single(result.OverallCategories, category =>
            category.Gender.Equals("male", StringComparison.OrdinalIgnoreCase)
            && category.EquipmentCategory.Equals("classic", StringComparison.OrdinalIgnoreCase)
            && category.EventType == "full-power");
        var lighter = OverallStanding(male, "lifter:light");
        var heavier = OverallStanding(male, "lifter:heavy");

        Assert.Equal(2, male.Standings.Count);
        Assert.Equal(2, lighter.Rankings["ipf-gl"].ConfirmedRank);
        Assert.Equal(1, heavier.Rankings["ipf-gl"].ConfirmedRank);
        Assert.Equal(1, lighter.Rankings["wilks-1"].ConfirmedRank);
        Assert.Equal(2, heavier.Rankings["wilks-1"].ConfirmedRank);
        Assert.Equal("no-projection", lighter.Rankings["ipf-gl"].ProjectedStatus);
        Assert.Contains(result.OverallCategories, category =>
            category.Gender.Equals("female", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BenchOnlyOverallKeepsUnsupportedFormulaScoresUnavailable()
    {
        var documents = new[]
        {
            Division("division:bench", "Bench only", "MALE", "EQUIPPED", "TOTAL", BenchOnly, ("class:120", "120 kg")),
            Lifter("lifter:bench", "Bench athlete", 100, ("division:bench", "class:120")),
            Attempt("attempt:bench-good", "lifter:bench", "bench", 200, "good"),
            Attempt("attempt:bench-pending", "lifter:bench", "bench", 210, null),
            Metadata
        };

        var result = CreateStandings(documents);
        var category = Assert.Single(result.OverallCategories, item => item.EventType == "bench-only");
        var athlete = Assert.Single(category.Standings);

        Assert.NotNull(athlete.PointsScores["ipf-gl"].ConfirmedScore);
        Assert.Null(athlete.PointsScores["dots"].ConfirmedScore);
        Assert.Equal("ranked", athlete.Rankings["ipf-gl"].ConfirmedStatus);
        Assert.Equal("unavailable", athlete.Rankings["dots"].ConfirmedStatus);
        Assert.True(athlete.HasPendingAttempts);
    }

    [Fact]
    public void SerializedStandingsContainOnlyExplicitPrivacySafeFields()
    {
        var documents = new[]
        {
            Division("division:open", "Open", "MALE", "RAW", "TOTAL", Powerlifting, ("class:83", "83 kg")),
            """
            {"_id":"lifter:private","name":"Athlete","bodyWeight":80,"birthDate":"private-birth","paymentReference":"private-payment","contactEmail":"private@example.test","changes":[{"value":"private-history"}],"divisions":[{"divisionId":"division:open","declaredAwardsWeightClassId":"class:83"}]}
            """,
            Attempt("attempt:private-squat", "lifter:private", "squat", 200, "good"),
            Attempt("attempt:private-bench", "lifter:private", "bench", 100, "good"),
            Attempt("attempt:private-dead", "lifter:private", "dead", 200, "good"),
            Metadata
        };

        var response = JsonSerializer.Serialize(
            CreateStandings(documents),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("Athlete", response);
        Assert.Contains("completeThreeLiftTotalKg", response);
        Assert.DoesNotContain("birthDate", response);
        Assert.DoesNotContain("private-birth", response);
        Assert.DoesNotContain("paymentReference", response);
        Assert.DoesNotContain("private-payment", response);
        Assert.DoesNotContain("contactEmail", response);
        Assert.DoesNotContain("private@example.test", response);
        Assert.DoesNotContain("changes", response);
        Assert.DoesNotContain("private-history", response);
    }

    [Fact]
    public void CompletedFixtureRetainsAllResolvedDivisionMembershipsAndSeparatesOverallCategories()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "completed-meet.json");
        using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var rows = fixture.RootElement.TryGetProperty("rows", out var rowArray)
            ? rowArray
            : fixture.RootElement.GetProperty("docs");
        var documents = rows.EnumerateArray()
            .Select(row => row.TryGetProperty("doc", out var sourceDocument)
                ? sourceDocument
                : row)
            .Select(sourceDocument =>
            {
                var json = sourceDocument.GetRawText();
                return new CompetitionDocument
                {
                    CompetitionId = "fixture-meet",
                    DocumentId = sourceDocument.GetProperty("_id").GetString()!,
                    Revision = sourceDocument.TryGetProperty("_rev", out var revision)
                        ? revision.GetString() ?? string.Empty
                        : string.Empty,
                    DocumentDataJson = json,
                    LastSyncedAt = DateTimeOffset.Parse("2026-10-22T00:00:00Z")
                };
            })
            .ToArray();
        var details = CompetitionDetailsMapper.Create(
            new Competition { Id = "fixture-meet", Name = "Completed fixture" },
            documents,
            DateTimeOffset.Parse("2026-10-22T00:00:00Z"));

        var standings = CompetitionStandingsCalculator.Calculate(details);

        Assert.Equal(28, details.Lifters.Count);
        Assert.Equal(55, details.Lifters.Sum(lifter => lifter.Divisions.Count));
        Assert.Equal(21, standings.WeightClasses.Count);
        Assert.Equal(55, standings.WeightClasses.Sum(category => category.Standings.Count));
        Assert.Equal(0, standings.UnresolvedMembershipCount);
        Assert.Equal(4, standings.OverallCategories.Count);
        Assert.All(
            standings.WeightClasses,
            category => Assert.Equal(
                category.Standings.Count,
                category.Standings.Select(standing => standing.AthleteId).Distinct(StringComparer.Ordinal).Count()));
        Assert.All(
            standings.OverallCategories,
            category => Assert.Equal(
                category.Standings.Count,
                category.Standings.Select(standing => standing.AthleteId).Distinct(StringComparer.Ordinal).Count()));
        Assert.Equal(
            2,
            standings.WeightClasses.Count(category => category.ScoreBy == "IPF_POINTS"));
        Assert.All(
            standings.WeightClasses.Where(category => category.ScoreBy == "IPF_POINTS"),
            category => Assert.NotNull(category.RankingLimitation));
    }

    private static CompetitionStandingsDto CreateStandings(params string[] jsonDocuments)
    {
        var documents = jsonDocuments
            .Select(json =>
            {
                using var parsed = JsonDocument.Parse(json);
                var id = parsed.RootElement.GetProperty("_id").GetString()!;
                return new CompetitionDocument
                {
                    CompetitionId = "meet-1",
                    DocumentId = id,
                    Revision = "1-revision",
                    DocumentDataJson = json,
                    LastSyncedAt = DateTimeOffset.Parse("2026-10-22T00:00:00Z")
                };
            })
            .ToArray();
        var details = CompetitionDetailsMapper.Create(
            new Competition
            {
                Id = "meet-1",
                Name = "Standings test",
                Date = "22/10/2026",
                DateFormat = "DD/MM/YYYY"
            },
            documents,
            DateTimeOffset.Parse("2026-10-22T00:00:00Z"));
        return CompetitionStandingsCalculator.Calculate(details);
    }

    private static CompetitionWeightClassStandingDto Standing(
        CompetitionWeightClassLeaderboardDto leaderboard,
        string athleteId) =>
        Assert.Single(leaderboard.Standings, standing => standing.AthleteId == athleteId);

    private static CompetitionOverallStandingDto OverallStanding(
        CompetitionOverallLeaderboardDto leaderboard,
        string athleteId) =>
        Assert.Single(leaderboard.Standings, standing => standing.AthleteId == athleteId);

    private static string Division(
        string id,
        string name,
        string gender,
        string equipment,
        string scoreBy,
        (bool? Squat, bool? Bench, bool? Dead) lifts,
        params (string Id, string Name)[] weightClasses)
    {
        var classes = string.Join(
            ",",
            weightClasses.Select(item => $"\"{item.Id}\":{{\"_id\":\"{item.Id}\",\"name\":\"{item.Name}\"}}"));
        return $$"""
            {
              "_id":"{{id}}",
              "name":"{{name}}",
              "gender":"{{gender}}",
              "rawOrEquipped":"{{equipment}}",
              "scoreBy":"{{scoreBy}}",
              "lifts":{"squat":{{Json(lifts.Squat)}},"bench":{{Json(lifts.Bench)}},"dead":{{Json(lifts.Dead)}}},
              "weightClasses":{ {{classes}} }
            }
            """;
    }

    private static string Lifter(
        string id,
        string name,
        double bodyWeight,
        params (string? DivisionId, string? WeightClassId)[] memberships)
    {
        var divisions = string.Join(
            ",",
            memberships.Select(membership => $$"""
                {"divisionId":{{Json(membership.DivisionId)}},"declaredAwardsWeightClassId":{{Json(membership.WeightClassId)}}}
                """));
        return $$"""
            {"_id":"{{id}}","name":"{{name}}","bodyWeight":{{bodyWeight}},"divisions":[{{divisions}}]}
            """;
    }

    private static string Attempt(
        string id,
        string lifterId,
        string lift,
        double weight,
        string? result) =>
        $$"""
          {"_id":"{{id}}","lifterId":"{{lifterId}}","liftName":"{{lift}}","attemptNumber":"1","weight":{{weight}}{{(result is null ? "" : $",\"result\":\"{result}\"")}}}
          """;

    private static string Json(bool? value) =>
        value is null ? "null" : value.Value ? "true" : "false";

    private static string Json(string? value) =>
        value is null ? "null" : JsonSerializer.Serialize(value);

    private static string Metadata =>
        """{"_id":"meet:metadata","units":"KG"}""";

    private static (bool? Squat, bool? Bench, bool? Dead) Powerlifting => (true, true, true);
    private static (bool? Squat, bool? Bench, bool? Dead) BenchOnly => (false, true, false);
}
