using System.Text.Json;
using LiftingCastTracker.Services.Calculations;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Tests;

public sealed class CompetitionPerformanceTests
{
    [Fact]
    public void CalculatesConfirmedAndBaselineProjectionWithoutCountingFailedAttempts()
    {
        var attempts = new[]
        {
            Attempt("squat-1", "squat", "1", 100, "good"),
            Attempt("squat-2", "squat", "2", 110, "good"),
            Attempt("squat-3", "squat", "3", 130, "bad"),
            Attempt("bench-1", "bench", "1", 80, "good"),
            Attempt("bench-2", "bench", "2", 90, null)
        };

        var performance = CompetitionPerformanceCalculator.Calculate(attempts, "KG");

        Assert.Equal(110, performance.BestSquatKg);
        Assert.Equal(80, performance.BestBenchKg);
        Assert.Null(performance.BestDeadliftKg);
        Assert.Equal(190, performance.ConfirmedSuccessfulLiftSumKg);
        Assert.Null(performance.CompleteThreeLiftTotalKg);
        Assert.Equal(200, performance.BaselineProjectedLiftSumKg);
        Assert.Null(performance.BaselineProjectedThreeLiftTotalKg);
    }

    [Fact]
    public void CalculatesCompleteThreeLiftAndBenchOnlyResultsOnlyWhenEligibleLiftsAreSuccessful()
    {
        var attempts = new[]
        {
            Attempt("squat-1", "squat", "1", 100, "good"),
            Attempt("bench-1", "bench", "1", 80, "good"),
            Attempt("dead-1", "dead", "1", 120, "good")
        };

        var threeLift = CompetitionPerformanceCalculator.Calculate(attempts, "KG");
        var benchOnly = CompetitionPerformanceCalculator.Calculate(
            attempts,
            "KG",
            new CompetitionLiftConfigurationDto(false, true, false));

        Assert.Equal(300, threeLift.CompleteThreeLiftTotalKg);
        Assert.Equal(80, benchOnly.ConfirmedSuccessfulLiftSumKg);
        Assert.Null(benchOnly.CompleteThreeLiftTotalKg);
        Assert.Equal(80, benchOnly.EligibleCompleteTotalKg);
        Assert.Equal(80, benchOnly.BenchOnlyResultKg);
    }

    [Fact]
    public void ConvertsPoundsToKilogramsAndRejectsUnsupportedUnits()
    {
        var attempts = new[]
        {
            Attempt("bench-1", "bench", "1", 220.462262, "good")
        };

        var pounds = CompetitionPerformanceCalculator.Calculate(
            attempts,
            "LB",
            new CompetitionLiftConfigurationDto(false, true, false));
        var unsupported = CompetitionPerformanceCalculator.Calculate(
            attempts,
            "unknown",
            new CompetitionLiftConfigurationDto(false, true, false));

        Assert.Equal(100d, pounds.BestBenchKg!.Value, 5);
        Assert.Equal(100d, pounds.EligibleCompleteTotalKg!.Value, 5);
        Assert.Null(unsupported.BestBenchKg);
        Assert.Null(unsupported.ConfirmedSuccessfulLiftSumKg);
    }

    [Fact]
    public void InvalidWeightsAndUnknownResultsDoNotBecomeConfirmedLifts()
    {
        var attempts = new[]
        {
            Attempt("bench-zero", "bench", "1", 0, "good"),
            Attempt("bench-negative", "bench", "2", -80, "good"),
            Attempt("bench-invalid", "bench", "3", "not-a-weight", "good"),
            Attempt("dead-unknown", "dead", "1", 150, "review"),
            Attempt("squat-unknown", "squat", "1", 120, "maybe")
        };

        var performance = CompetitionPerformanceCalculator.Calculate(attempts, "KG");

        Assert.Null(performance.BestBenchKg);
        Assert.Null(performance.ConfirmedSuccessfulLiftSumKg);
        Assert.Null(performance.CompleteThreeLiftTotalKg);
        Assert.Equal(270, performance.BaselineProjectedLiftSumKg);
    }

    [Fact]
    public void StrategyAppliesHypotheticalOutcomesWithoutMutatingOfficialAttempts()
    {
        var primary = Lifter(
            "lifter-primary",
            "Coach athlete",
            Attempt("bench-good", "bench", "1", 80, "good"),
            Attempt("bench-pending", "bench", "2", 90, null));
        var opponent = Lifter(
            "lifter-opponent",
            "Opponent",
            Attempt("opponent-bench-good", "bench", "1", 85, "good"),
            Attempt("opponent-bench-pending", "bench", "2", 95, null));
        var details = Details(primary, opponent);
        var request = new CompetitionStrategyRequest(
            "division-open",
            "weight-83",
            [
                new CompetitionStrategyLifterScenario(
                    primary.DocumentId,
                    [new CompetitionAttemptScenario("bench-pending", 100, "good")]),
                new CompetitionStrategyLifterScenario(
                    opponent.DocumentId,
                    [new CompetitionAttemptScenario("opponent-bench-pending", 95, "bad")])
            ]);

        var result = CompetitionStrategyCalculator.Calculate(details, request);

        Assert.Equal("90", primary.Attempts[1].Weight?.GetRawText());
        Assert.Null(primary.Attempts[1].Result);
        Assert.Equal(80, result.Standings.Single(row => row.LifterId == primary.DocumentId)
            .CurrentPerformance.ConfirmedSuccessfulLiftSumKg);
        Assert.Equal(100, result.Standings.Single(row => row.LifterId == primary.DocumentId)
            .ScenarioPerformance.EligibleCompleteTotalKg);
        Assert.Equal(85, result.Standings.Single(row => row.LifterId == opponent.DocumentId)
            .ScenarioPerformance.EligibleCompleteTotalKg);
        Assert.Equal(1, result.Standings.Single(row => row.LifterId == primary.DocumentId).ProvisionalPlace);
        Assert.Equal(2, result.Standings.Single(row => row.LifterId == opponent.DocumentId).ProvisionalPlace);
    }

    [Fact]
    public void StrategyRejectsAthletesWithOnlyTheSameNumericalWeightLimit()
    {
        var primary = Lifter("lifter-1", "Athlete", Attempt("bench-1", "bench", "1", 80, "good"));
        var unrelated = Lifter(
            "lifter-2",
            "Other division",
            [Attempt("other-bench-1", "bench", "1", 90, "good")],
            divisionId: "division-other");
        var details = Details(primary, unrelated);
        var request = new CompetitionStrategyRequest(
            "division-open",
            "weight-83",
            [
                new CompetitionStrategyLifterScenario(primary.DocumentId, []),
                new CompetitionStrategyLifterScenario(unrelated.DocumentId, [])
            ]);

        Assert.Throws<ArgumentException>(() => CompetitionStrategyCalculator.Calculate(details, request));
    }

    [Fact]
    public void StrategyRejectsScenariosForResolvedAttempts()
    {
        var lifter = Lifter("lifter-1", "Athlete", Attempt("bench-1", "bench", "1", 80, "good"));
        var request = new CompetitionStrategyRequest(
            "division-open",
            "weight-83",
            [
                new CompetitionStrategyLifterScenario(
                    lifter.DocumentId,
                    [new CompetitionAttemptScenario("bench-1", 90, "bad")])
            ]);

        Assert.Throws<ArgumentException>(() =>
            CompetitionStrategyCalculator.Calculate(
                Details(lifter),
                request));
    }

    private static CompetitionDetailsDto Details(params CompetitionLifterDto[] lifters) =>
        new(
            "competition-1",
            "Test meet",
            null,
            null,
            "KG",
            DateTimeOffset.UtcNow,
            [],
            lifters);

    private static CompetitionLifterDto Lifter(
        string id,
        string name,
        params CompetitionAttemptDto[] attempts) =>
        Lifter(id, name, attempts, "division-open");

    private static CompetitionLifterDto Lifter(
        string id,
        string name,
        CompetitionAttemptDto[] attempts,
        string divisionId) =>
        new(
            id,
            name,
            [
                new CompetitionDivisionDto(
                    divisionId,
                    "Open",
                    "weight-83",
                    "83",
                    83,
                    "MALE",
                    "RAW",
                    "TOTAL",
                    new CompetitionLiftConfigurationDto(false, true, false))
            ],
            "A",
            Value("90"),
            Value("10"),
            Value("80"),
            "platform-1",
            CompetitionPerformanceCalculator.Calculate(attempts, "KG"),
            attempts);

    private static CompetitionAttemptDto Attempt(
        string id,
        string lift,
        string number,
        object? weight,
        string? result) =>
        new(
            id,
            lift,
            Value(JsonSerializer.Serialize(number)),
            weight is null ? null : Value(JsonSerializer.Serialize(weight)),
            result is null ? null : Value(JsonSerializer.Serialize(result)));

    private static JsonElement Value(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
