using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Calculations;

public static class CompetitionStrategyCalculator
{
    private const int MaximumComparedLifters = 20;
    private const int MaximumScenarioAttemptsPerLifter = 9;
    private const string WeightClassMode = "weight-class";
    private const string BestOverallMode = "best-overall";

    public static CompetitionStrategyResult Calculate(
        CompetitionDetailsDto details,
        CompetitionStrategyRequest request)
    {
        var mode = (request.Mode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode is not (WeightClassMode or BestOverallMode))
        {
            throw new ArgumentException("Select a supported strategy comparison mode.");
        }

        if (!CompetitionPointsCalculator.IsKnownSystem(request.PointsSystem))
        {
            throw new ArgumentException("Select a supported points system.");
        }

        if (request.Lifters is null
            || string.IsNullOrWhiteSpace(request.DivisionId)
            || string.IsNullOrWhiteSpace(request.WeightClassId)
            || request.Lifters.Count is < 1 or > MaximumComparedLifters)
        {
            throw new ArgumentException("Select a division, weight class, and between 1 and 20 lifters.");
        }

        var lifterIds = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<StrategyEntry>(request.Lifters.Count);
        CompetitionDivisionDto? selectedCategory = null;
        for (var index = 0; index < request.Lifters.Count; index++)
        {
            var scenario = request.Lifters[index];
            if (string.IsNullOrWhiteSpace(scenario.LifterId)
                || !lifterIds.Add(scenario.LifterId))
            {
                throw new ArgumentException("Each compared lifter must be selected once.");
            }

            var lifter = details.Lifters.FirstOrDefault(
                item => item.DocumentId == scenario.LifterId)
                ?? throw new ArgumentException("A selected lifter is not in this competition.");

            var divisionId = scenario.DivisionId ?? request.DivisionId;
            var weightClassId = scenario.WeightClassId ?? request.WeightClassId;
            var division = lifter.Divisions.FirstOrDefault(item =>
                item.DivisionId == divisionId
                && item.WeightClassId == weightClassId)
                ?? throw new ArgumentException(
                    "A selected lifter does not belong to the submitted division and weight class.");

            if (index == 0
                && (division.DivisionId != request.DivisionId
                    || division.WeightClassId != request.WeightClassId))
            {
                throw new ArgumentException(
                    "The first selected lifter must use the selected primary division and weight class.");
            }

            if (!HasCompleteCategory(division))
            {
                throw new ArgumentException(
                    "The selected division does not have enough verified category data for comparison.");
            }

            if (selectedCategory is not null
                && !HasSameCategory(selectedCategory, division))
            {
                throw new ArgumentException(
                    "Selected lifters do not share the same gender, equipment, and lift category.");
            }

            if (mode == WeightClassMode
                && (division.DivisionId != request.DivisionId
                    || division.WeightClassId != request.WeightClassId))
            {
                throw new ArgumentException(
                    "Weight-class comparisons require the exact selected division and weight-class IDs.");
            }

            selectedCategory ??= division;
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
            var currentPerformance = CompetitionPerformanceCalculator.Calculate(
                lifter.Attempts,
                details.Units,
                division.Lifts);
            var scenarioPerformance = CompetitionPerformanceCalculator.Calculate(
                scenarioAdjustedAttempts,
                details.Units,
                division.Lifts);
            var hasProjection = scenarioAttempts.Count > 0
                || CompetitionPerformanceCalculator.HasPendingSelectedAttempt(
                    lifter.Attempts,
                    details.Units,
                    division.Lifts);
            var currentConfirmedPoints = CompetitionPointsCalculator.Calculate(
                request.PointsSystem,
                GetBodyWeightKg(lifter, details.Units),
                currentPerformance.EligibleCompleteTotalKg,
                division.Gender,
                division.RawOrEquipped,
                division.Lifts);
            var projectedPoints = hasProjection
                ? CompetitionPointsCalculator.Calculate(
                    request.PointsSystem,
                    GetBodyWeightKg(lifter, details.Units),
                    scenarioPerformance.BaselineProjectedEligibleTotalKg,
                    division.Gender,
                    division.RawOrEquipped,
                    division.Lifts)
                : null;

            entries.Add(new StrategyEntry(
                lifter,
                division,
                currentPerformance,
                scenarioPerformance,
                currentConfirmedPoints,
                projectedPoints));
        }

        var rankedEntries = entries
            .OrderByDescending(entry => GetRankingValue(entry, mode))
            .ThenBy(entry => entry.Lifter.DocumentId, StringComparer.Ordinal)
            .ToArray();
        var standings = new List<CompetitionStrategyStandingDto>(rankedEntries.Length);
        double? previousValue = null;
        int? previousPlace = null;

        for (var index = 0; index < rankedEntries.Length; index++)
        {
            var entry = rankedEntries[index];
            var value = GetRankingValue(entry, mode);
            var hasTie = value is not null
                && previousValue is not null
                && Math.Abs(value.Value - previousValue.Value) < 0.00000001;
            var place = value is null
                ? null
                : hasTie
                    ? previousPlace
                    : index + 1;
            if (hasTie && standings.Count > 0)
            {
                standings[^1] = standings[^1] with { IsTied = true };
            }

            standings.Add(new CompetitionStrategyStandingDto(
                entry.Lifter.DocumentId,
                entry.Lifter.Name,
                entry.Lifter.BodyWeight,
                entry.CurrentPerformance,
                entry.ScenarioPerformance,
                place,
                hasTie,
                entry.Division.WeightClassId,
                entry.Division.WeightClassName,
                entry.CurrentConfirmedPoints,
                entry.ProjectedPoints));
            previousValue = value;
            previousPlace = place;
        }

        return new CompetitionStrategyResult(
            request.DivisionId,
            mode == WeightClassMode ? request.WeightClassId : null,
            mode,
            request.PointsSystem.Trim().ToLowerInvariant(),
            standings);
    }

    private static double? GetRankingValue(StrategyEntry entry, string mode) =>
        mode == BestOverallMode
            ? entry.ProjectedPoints ?? entry.CurrentConfirmedPoints
            : entry.ScenarioPerformance.BaselineProjectedEligibleTotalKg;

    private static double? GetBodyWeightKg(CompetitionLifterDto lifter, string? units) =>
        CompetitionPerformanceCalculator.TryGetKilograms(lifter.BodyWeight, units, out var kilograms)
            ? kilograms
            : null;

    private static bool HasCompleteCategory(CompetitionDivisionDto division) =>
        NormalizeGender(division.Gender) is "male" or "female"
        && NormalizeEquipment(division.RawOrEquipped) is "classic" or "equipped"
        && division.Lifts.Squat is not null
        && division.Lifts.Bench is not null
        && division.Lifts.Dead is not null
        && (division.Lifts.Squat.Value || division.Lifts.Bench.Value || division.Lifts.Dead.Value);

    private static bool HasSameCategory(
        CompetitionDivisionDto first,
        CompetitionDivisionDto second) =>
        NormalizeGender(first.Gender) == NormalizeGender(second.Gender)
        && NormalizeEquipment(first.RawOrEquipped) == NormalizeEquipment(second.RawOrEquipped)
        && first.Lifts == second.Lifts;

    private static string? NormalizeGender(string? gender) =>
        gender?.Trim().ToLowerInvariant();

    private static string? NormalizeEquipment(string? rawOrEquipped) =>
        rawOrEquipped?.Trim().ToLowerInvariant() switch
        {
            "raw" or "classic" => "classic",
            "equipped" => "equipped",
            _ => null
        };

    private sealed record StrategyEntry(
        CompetitionLifterDto Lifter,
        CompetitionDivisionDto Division,
        CompetitionLifterPerformanceDto CurrentPerformance,
        CompetitionLifterPerformanceDto ScenarioPerformance,
        double? CurrentConfirmedPoints,
        double? ProjectedPoints);
}
