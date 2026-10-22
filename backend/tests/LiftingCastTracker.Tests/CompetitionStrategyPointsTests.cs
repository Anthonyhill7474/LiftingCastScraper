using System.Text.Json;
using LiftingCastTracker.Services.Calculations;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Tests;

public sealed class CompetitionStrategyPointsTests
{
    [Fact]
    public void BestOverallRankingChangesWithTheSelectedPointsSystemAcrossWeightClasses()
    {
        var lighter = Lifter(
            "lifter-light",
            "Lighter lifter",
            45,
            [
                Division("female-equipped", "female-47", "FEMALE", "EQUIPPED"),
                Division("male-classic", "male-47", "MALE", "RAW")
            ],
            (100, 50, 100));
        var heavier = Lifter(
            "lifter-heavy",
            "Heavier lifter",
            50,
            [
                Division("male-equipped", "male-52-equipped", "MALE", "EQUIPPED"),
                Division("male-classic-open", "male-52", "MALE", "CLASSIC"),
                Division("female-classic", "female-52", "FEMALE", "RAW")
            ],
            (110, 55, 110));

        var ipfGl = CalculateBestOverall("ipf-gl", lighter, heavier);
        var wilks1 = CalculateBestOverall("wilks-1", lighter, heavier);

        Assert.Equal(2, Standing(ipfGl, lighter).ProvisionalPlace);
        Assert.Equal(1, Standing(ipfGl, heavier).ProvisionalPlace);
        Assert.Equal(1, Standing(wilks1, lighter).ProvisionalPlace);
        Assert.Equal(2, Standing(wilks1, heavier).ProvisionalPlace);
        Assert.Equal("male-47", Standing(ipfGl, lighter).WeightClassId);
        Assert.Equal("male-52", Standing(ipfGl, heavier).WeightClassId);
        Assert.Null(Standing(ipfGl, lighter).ProjectedPoints);
        Assert.NotNull(Standing(ipfGl, lighter).CurrentConfirmedPoints);
    }

    [Fact]
    public void BestOverallAcceptsCompatibleMultipleMembershipsAndRecalculatesScenarioPoints()
    {
        var athlete = Lifter(
            "athlete",
            "Athlete",
            45,
            [
                Division("male-classic", "male-47", "MALE", "RAW"),
                Division("female-classic", "female-47", "FEMALE", "RAW")
            ],
            (100, 50, 100),
            pendingSquat: 150);
        var opponent = Lifter(
            "opponent",
            "Opponent",
            50,
            [
                Division("male-equipped", "male-52-e", "MALE", "EQUIPPED"),
                Division("male-classic-open", "male-52", "MALE", "CLASSIC")
            ],
            (110, 55, 110));

        var result = CompetitionStrategyCalculator.Calculate(
            Details(athlete, opponent),
            new CompetitionStrategyRequest(
                "male-classic",
                "male-47",
                [
                    new CompetitionStrategyLifterScenario(
                        athlete.DocumentId,
                        [new CompetitionAttemptScenario("athlete-pending-squat", 160, "good")],
                        "male-classic",
                        "male-47"),
                    new CompetitionStrategyLifterScenario(
                        opponent.DocumentId,
                        [],
                        "male-classic-open",
                        "male-52")
                ],
                "ipf-gl",
                "best-overall"));

        var athleteStanding = Standing(result, athlete);
        Assert.Equal(250, athleteStanding.CurrentPerformance.EligibleCompleteTotalKg);
        Assert.Equal(310, athleteStanding.ScenarioPerformance.BaselineProjectedEligibleTotalKg);
        Assert.Equal(47.857218311990046, athleteStanding.CurrentConfirmedPoints!.Value, 9);
        Assert.Equal(59.342950706867654, athleteStanding.ProjectedPoints!.Value, 9);
        Assert.NotEqual(athleteStanding.CurrentConfirmedPoints, athleteStanding.ProjectedPoints);
        Assert.Equal(2, result.Standings.Count);
    }

    [Theory]
    [InlineData("FEMALE", "RAW", true, true, true)]
    [InlineData("MALE", "EQUIPPED", true, true, true)]
    [InlineData("MALE", "RAW", false, true, false)]
    public void BestOverallRejectsIncompatibleGenderEquipmentOrEvent(
        string gender,
        string equipment,
        bool squat,
        bool bench,
        bool dead)
    {
        var primary = Lifter(
            "primary",
            "Primary",
            80,
            [Division("primary-division", "class-a", "MALE", "RAW")],
            (200, 100, 200));
        var incompatible = Lifter(
            "incompatible",
            "Incompatible",
            80,
            [Division(
                "other-division",
                "class-b",
                gender,
                equipment,
                new CompetitionLiftConfigurationDto(squat, bench, dead))],
            (200, 100, 200));

        var primaryDivision = primary.Divisions[0];
        var incompatibleDivision = incompatible.Divisions[0];
        var request = new CompetitionStrategyRequest(
            primaryDivision.DivisionId!,
            primaryDivision.WeightClassId,
            [
                new CompetitionStrategyLifterScenario(
                    primary.DocumentId,
                    [],
                    primaryDivision.DivisionId,
                    primaryDivision.WeightClassId),
                new CompetitionStrategyLifterScenario(
                    incompatible.DocumentId,
                    [],
                    incompatibleDivision.DivisionId,
                    incompatibleDivision.WeightClassId)
            ],
            "ipf-gl",
            "best-overall");
        Assert.Throws<ArgumentException>(() =>
            CompetitionStrategyCalculator.Calculate(Details(primary, incompatible), request));
    }

    private static CompetitionStrategyResult CalculateBestOverall(
        string pointsSystem,
        CompetitionLifterDto primary,
        CompetitionLifterDto opponent)
    {
        var primaryDivision = primary.Divisions[0].Gender == "FEMALE"
            ? primary.Divisions[1]
            : primary.Divisions.First(division => division.Gender == "MALE" && division.RawOrEquipped is "RAW" or "CLASSIC");
        var opponentDivision = opponent.Divisions.First(division =>
            division.Gender == "MALE"
            && division.RawOrEquipped is "RAW" or "CLASSIC");
        return CompetitionStrategyCalculator.Calculate(
            Details(primary, opponent),
            new CompetitionStrategyRequest(
                primaryDivision.DivisionId!,
                primaryDivision.WeightClassId,
                [
                    new CompetitionStrategyLifterScenario(
                        primary.DocumentId,
                        [],
                        primaryDivision.DivisionId,
                        primaryDivision.WeightClassId),
                    new CompetitionStrategyLifterScenario(
                        opponent.DocumentId,
                        [],
                        opponentDivision.DivisionId,
                        opponentDivision.WeightClassId)
                ],
                pointsSystem,
                "best-overall"));
    }

    private static CompetitionStrategyStandingDto Standing(
        CompetitionStrategyResult result,
        CompetitionLifterDto lifter) =>
        Assert.Single(result.Standings, standing => standing.LifterId == lifter.DocumentId);

    private static CompetitionDetailsDto Details(params CompetitionLifterDto[] lifters) =>
        new("meet-1", "Meet", null, null, "KG", DateTimeOffset.UtcNow, [], lifters);

    private static CompetitionLifterDto Lifter(
        string id,
        string name,
        double bodyWeight,
        IReadOnlyList<CompetitionDivisionDto> divisions,
        (double Squat, double Bench, double Dead) weights,
        double? pendingSquat = null)
    {
        var attempts = new List<CompetitionAttemptDto>
        {
            Attempt($"{id}-squat", "squat", weights.Squat, "good"),
            Attempt($"{id}-bench", "bench", weights.Bench, "good"),
            Attempt($"{id}-dead", "dead", weights.Dead, "good")
        };
        if (pendingSquat is { } pending)
        {
            attempts.Add(Attempt($"{id}-pending-squat", "squat", pending, null));
        }

        return new CompetitionLifterDto(
            id,
            name,
            divisions,
            null,
            null,
            null,
            JsonValue(bodyWeight),
            null,
            CompetitionPerformanceCalculator.Calculate(attempts, "KG"),
            attempts);
    }

    private static CompetitionDivisionDto Division(
        string divisionId,
        string weightClassId,
        string gender,
        string equipment,
        CompetitionLiftConfigurationDto? lifts = null) =>
        new(
            divisionId,
            divisionId,
            weightClassId,
            weightClassId,
            null,
            gender,
            equipment,
            "IPF_POINTS",
            lifts ?? new CompetitionLiftConfigurationDto(true, true, true));

    private static CompetitionAttemptDto Attempt(
        string id,
        string lift,
        double weight,
        string? result) =>
        new(
            id,
            lift,
            JsonValue("1"),
            JsonValue(weight),
            result is null ? null : JsonValue(result));

    private static JsonElement JsonValue<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }
}
