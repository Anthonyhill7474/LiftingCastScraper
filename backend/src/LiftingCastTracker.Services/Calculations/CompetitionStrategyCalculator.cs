using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Calculations;

public static class CompetitionStrategyCalculator
{
    private const int MaximumComparedLifters = 20;
    private const int MaximumScenarioAttemptsPerLifter = 9;

    public static CompetitionStrategyResult Calculate(
        CompetitionDetailsDto details,
        CompetitionStrategyRequest request)
    {
        if (request.Lifters is null
            || string.IsNullOrWhiteSpace(request.DivisionId)
            || string.IsNullOrWhiteSpace(request.WeightClassId)
            || request.Lifters.Count is < 1 or > MaximumComparedLifters)
        {
            throw new ArgumentException("Select a division, weight class, and between 1 and 20 lifters.");
        }

        var lifterIds = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<StrategyEntry>(request.Lifters.Count);
        CompetitionDivisionDto? selectedDivision = null;

        foreach (var scenario in request.Lifters)
        {
            if (string.IsNullOrWhiteSpace(scenario.LifterId)
                || !lifterIds.Add(scenario.LifterId))
            {
                throw new ArgumentException("Each compared lifter must be selected once.");
            }

            var lifter = details.Lifters.FirstOrDefault(
                item => item.DocumentId == scenario.LifterId)
                ?? throw new ArgumentException("A selected lifter is not in this competition.");
            var division = lifter.Divisions.FirstOrDefault(item =>
                item.DivisionId == request.DivisionId
                && item.WeightClassId == request.WeightClassId)
                ?? throw new ArgumentException(
                    "Each selected lifter must belong to the selected division and weight class.");

            if (division.Gender is null
                || division.Lifts.Squat is null
                || division.Lifts.Bench is null
                || division.Lifts.Dead is null
                || !(division.Lifts.Squat.Value || division.Lifts.Bench.Value || division.Lifts.Dead.Value))
            {
                throw new ArgumentException(
                    "The selected division does not have enough verified configuration for comparison.");
            }

            if (selectedDivision is not null
                && (selectedDivision.Gender != division.Gender
                    || selectedDivision.RawOrEquipped != division.RawOrEquipped
                    || selectedDivision.Lifts != division.Lifts))
            {
                throw new ArgumentException(
                    "Selected lifters do not share the same gender and competition category.");
            }

            selectedDivision ??= division;
            var attempts = lifter.Attempts.ToDictionary(
                attempt => attempt.DocumentId,
                StringComparer.Ordinal);
            if (scenario.Attempts is null
                || scenario.Attempts.Count > MaximumScenarioAttemptsPerLifter)
            {
                throw new ArgumentException("Too many scenario attempts were submitted.");
            }

            var scenarioAttemptIds = new HashSet<string>(StringComparer.Ordinal);
            var scenarioAttempts = new Dictionary<string, CompetitionAttemptScenario>(
                StringComparer.Ordinal);
            foreach (var attemptScenario in scenario.Attempts)
            {
                if (string.IsNullOrWhiteSpace(attemptScenario.AttemptId)
                    || !scenarioAttemptIds.Add(attemptScenario.AttemptId)
                    || !double.IsFinite(attemptScenario.Weight)
                    || attemptScenario.Weight <= 0
                    || !(string.Equals(attemptScenario.Result, "good", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(attemptScenario.Result, "bad", StringComparison.OrdinalIgnoreCase))
                    || !attempts.TryGetValue(attemptScenario.AttemptId, out var originalAttempt)
                    || !CompetitionPerformanceCalculator.IsEditableScenarioAttempt(
                        originalAttempt,
                        details.Units))
                {
                    throw new ArgumentException(
                        "Scenarios may edit only unresolved selected attempts using a positive weight and good/bad result.");
                }

                scenarioAttempts.Add(attemptScenario.AttemptId, attemptScenario);
            }

            var scenarioAdjustedAttempts = lifter.Attempts
                .Select(attempt => scenarioAttempts.TryGetValue(attempt.DocumentId, out var updated)
                    ? CompetitionPerformanceCalculator.ApplyScenario(attempt, updated)
                    : attempt)
                .ToArray();
            entries.Add(new StrategyEntry(
                lifter,
                CompetitionPerformanceCalculator.Calculate(
                    lifter.Attempts,
                    details.Units,
                    division.Lifts),
                CompetitionPerformanceCalculator.Calculate(
                    scenarioAdjustedAttempts,
                    details.Units,
                    division.Lifts)));
        }

        var rankedEntries = entries
            .OrderByDescending(entry => entry.ScenarioPerformance.BaselineProjectedEligibleTotalKg)
            .ToArray();
        var standings = new List<CompetitionStrategyStandingDto>(rankedEntries.Length);
        double? previousTotal = null;
        int? previousPlace = null;

        for (var index = 0; index < rankedEntries.Length; index++)
        {
            var entry = rankedEntries[index];
            var total = entry.ScenarioPerformance.BaselineProjectedEligibleTotalKg;
            var hasTie = total is not null
                && previousTotal is not null
                && Math.Abs(total.Value - previousTotal.Value) < 0.0000001;
            var place = total is null
                ? null
                : hasTie
                    ? previousPlace
                    : index + 1;
            if (hasTie)
            {
                if (standings.Count > 0)
                {
                    standings[^1] = standings[^1] with { IsTied = true };
                }
            }

            standings.Add(new CompetitionStrategyStandingDto(
                entry.Lifter.DocumentId,
                entry.Lifter.Name,
                entry.Lifter.BodyWeight,
                entry.CurrentPerformance,
                entry.ScenarioPerformance,
                place,
                hasTie));
            previousTotal = total;
            previousPlace = place;
        }

        return new CompetitionStrategyResult(
            request.DivisionId,
            request.WeightClassId,
            standings);
    }

    private sealed record StrategyEntry(
        CompetitionLifterDto Lifter,
        CompetitionLifterPerformanceDto CurrentPerformance,
        CompetitionLifterPerformanceDto ScenarioPerformance);
}
