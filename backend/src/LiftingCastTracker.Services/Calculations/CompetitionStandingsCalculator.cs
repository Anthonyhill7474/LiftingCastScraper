using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Calculations;

public static class CompetitionStandingsCalculator
{
    public static CompetitionStandingsDto Calculate(CompetitionDetailsDto details)
    {
        var weightClassGroups = new Dictionary<WeightClassKey, List<WeightClassCandidate>>();
        var overallGroups = new Dictionary<OverallKey, List<OverallCandidate>>();
        var unresolvedMembershipCount = 0;
        var unsupportedOverallMembershipCount = 0;

        foreach (var lifter in details.Lifters)
        {
            foreach (var division in lifter.Divisions)
            {
                if (string.IsNullOrWhiteSpace(division.DivisionId)
                    || string.IsNullOrWhiteSpace(division.WeightClassId))
                {
                    unresolvedMembershipCount++;
                    continue;
                }

                var genderKey = NormalizeGender(division.Gender) ?? NormalizeUnknown(division.Gender);
                var equipmentKey = NormalizeEquipment(division.RawOrEquipped)
                    ?? NormalizeUnknown(division.RawOrEquipped);
                var eventType = GetEventType(division.Lifts);
                var scoreByKey = NormalizeUnknown(division.ScoreBy);
                var weightClassKey = new WeightClassKey(
                    division.DivisionId,
                    division.WeightClassId,
                    genderKey,
                    equipmentKey,
                    eventType,
                    scoreByKey);
                var performance = CompetitionPerformanceCalculator.Calculate(
                    lifter.Attempts,
                    details.Units,
                    division.Lifts);
                var hasPendingAttempts = CompetitionPerformanceCalculator.HasPendingSelectedAttempt(
                    lifter.Attempts,
                    details.Units,
                    division.Lifts);
                var confirmedResultKg = GetRankableResult(performance, eventType);
                var projectedResultKg = hasPendingAttempts
                    ? GetProjectedResult(performance, eventType)
                    : null;
                var rankingLimitation = GetWeightClassRankingLimitation(
                    division,
                    genderKey,
                    equipmentKey,
                    eventType);
                var bodyWeightKg = CompetitionPerformanceCalculator.TryGetKilograms(
                    lifter.BodyWeight,
                    details.Units,
                    out var parsedBodyWeightKg)
                    ? parsedBodyWeightKg
                    : (double?)null;

                if (!weightClassGroups.TryGetValue(weightClassKey, out var weightClassCandidates))
                {
                    weightClassCandidates = [];
                    weightClassGroups.Add(weightClassKey, weightClassCandidates);
                }

                weightClassCandidates.Add(new WeightClassCandidate(
                    lifter,
                    division,
                    performance,
                    bodyWeightKg,
                    confirmedResultKg,
                    projectedResultKg,
                    hasPendingAttempts,
                    rankingLimitation));

                if (IsOverallCategorySupported(genderKey, equipmentKey, eventType))
                {
                    var overallKey = new OverallKey(genderKey, equipmentKey, eventType);
                    if (!overallGroups.TryGetValue(overallKey, out var overallCandidates))
                    {
                        overallCandidates = [];
                        overallGroups.Add(overallKey, overallCandidates);
                    }

                    overallCandidates.Add(new OverallCandidate(
                        lifter,
                        division,
                        bodyWeightKg,
                        hasPendingAttempts,
                        division.PointsScores ?? CreateUnavailableScores()));
                }
                else
                {
                    unsupportedOverallMembershipCount++;
                }
            }
        }

        var weightClasses = weightClassGroups
            .Select(group => CreateWeightClassLeaderboard(group.Key, group.Value))
            .OrderBy(item => item.Gender, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DivisionName ?? item.DivisionId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.WeightClassName ?? item.WeightClassId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DivisionId, StringComparer.Ordinal)
            .ThenBy(item => item.WeightClassId, StringComparer.Ordinal)
            .ToArray();
        var overallCategories = overallGroups
            .Select(group => CreateOverallLeaderboard(group.Key, group.Value))
            .OrderBy(item => item.Gender, StringComparer.Ordinal)
            .ThenBy(item => item.EquipmentCategory, StringComparer.Ordinal)
            .ThenBy(item => item.EventType, StringComparer.Ordinal)
            .ToArray();

        return new CompetitionStandingsDto(
            details.CompetitionId,
            details.Name,
            details.Date,
            details.DateFormat,
            details.LastSyncedAtUtc,
            unresolvedMembershipCount,
            unsupportedOverallMembershipCount,
            weightClasses,
            overallCategories);
    }

    private static CompetitionWeightClassLeaderboardDto CreateWeightClassLeaderboard(
        WeightClassKey key,
        IReadOnlyCollection<WeightClassCandidate> candidates)
    {
        var uniqueCandidates = candidates
            .GroupBy(candidate => candidate.Lifter.DocumentId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(candidate => candidate.HasPendingAttempts)
                .ThenBy(candidate => candidate.Lifter.Name, StringComparer.OrdinalIgnoreCase)
                .First())
            .ToArray();
        var isRankable = uniqueCandidates.First().RankingLimitation is null;
        var confirmedRanks = isRankable
            ? Rank(
                uniqueCandidates
                    .Where(candidate => candidate.ConfirmedResultKg.HasValue)
                    .Select(candidate => new RankCandidate(
                        candidate.Lifter.DocumentId,
                        candidate.ConfirmedResultKg!.Value)))
            : new Dictionary<string, RankResult>(StringComparer.Ordinal);
        var projectedRanks = isRankable
            ? Rank(
                uniqueCandidates
                    .Where(candidate => candidate.HasPendingAttempts
                        && candidate.ProjectedResultKg.HasValue)
                    .Select(candidate => new RankCandidate(
                        candidate.Lifter.DocumentId,
                        candidate.ProjectedResultKg!.Value)))
            : new Dictionary<string, RankResult>(StringComparer.Ordinal);

        var standings = uniqueCandidates
            .Select(candidate =>
            {
                var confirmedRank = confirmedRanks.GetValueOrDefault(candidate.Lifter.DocumentId);
                var projectedRank = projectedRanks.GetValueOrDefault(candidate.Lifter.DocumentId);
                return new CompetitionWeightClassStandingDto(
                    candidate.Lifter.DocumentId,
                    candidate.Lifter.Name,
                    candidate.BodyWeightKg,
                    candidate.Performance.BestSquatKg,
                    candidate.Performance.BestBenchKg,
                    candidate.Performance.BestDeadliftKg,
                    candidate.Performance.ConfirmedSuccessfulLiftSumKg,
                    candidate.Performance.CompleteThreeLiftTotalKg,
                    candidate.ConfirmedResultKg,
                    candidate.ProjectedResultKg,
                    candidate.HasPendingAttempts,
                    confirmedRank?.Rank,
                    confirmedRank?.Status ?? GetUnavailableStatus(
                        candidate,
                        isRankable,
                        isProjected: false),
                    projectedRank?.Rank,
                    projectedRank?.Status ?? GetUnavailableStatus(
                        candidate,
                        isRankable,
                        isProjected: true));
            })
            .OrderBy(standing => standing.ConfirmedRank ?? int.MaxValue)
            .ThenBy(standing => standing.ProjectedRank ?? int.MaxValue)
            .ThenBy(standing => standing.AthleteName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(standing => standing.AthleteId, StringComparer.Ordinal)
            .ToArray();

        var first = uniqueCandidates.First().Division;
        var eventType = key.EventType;
        var rankingLimitation = uniqueCandidates.First().RankingLimitation;
        var rankingBasis = eventType switch
        {
            "full-power" => "complete-three-lift-total",
            "bench-only" => "best-successful-bench",
            _ => "unavailable"
        };

        return new CompetitionWeightClassLeaderboardDto(
            key.DivisionId,
            first.Name,
            key.WeightClassId,
            first.WeightClassName,
            key.Gender,
            key.Equipment,
            eventType,
            first.ScoreBy,
            rankingBasis,
            rankingLimitation,
            standings);
    }

    private static CompetitionOverallLeaderboardDto CreateOverallLeaderboard(
        OverallKey key,
        IReadOnlyCollection<OverallCandidate> candidates)
    {
        var uniqueAthletes = candidates
            .GroupBy(candidate => candidate.Lifter.DocumentId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(candidate => candidate.HasPendingAttempts)
                .ThenByDescending(candidate => candidate.PointsScores.Values.Any(score =>
                    score.ConfirmedScore.HasValue || score.ProjectedScore.HasValue))
                .ThenBy(candidate => candidate.Division.DivisionId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Division.WeightClassId, StringComparer.Ordinal)
                .First())
            .ToArray();

        var rankResults = new Dictionary<string, Dictionary<string, CompetitionOverallRankDto>>(
            StringComparer.Ordinal);
        foreach (var systemId in CompetitionPointsCalculator.SystemIds)
        {
            var confirmedRanks = Rank(uniqueAthletes
                .Where(candidate => candidate.PointsScores.TryGetValue(systemId, out var score)
                    && score.ConfirmedScore.HasValue)
                .Select(candidate => new RankCandidate(
                    candidate.Lifter.DocumentId,
                    candidate.PointsScores[systemId].ConfirmedScore!.Value)));
            var projectedRanks = Rank(uniqueAthletes
                .Where(candidate => candidate.HasPendingAttempts
                    && candidate.PointsScores.TryGetValue(systemId, out var score)
                    && score.ProjectedScore.HasValue)
                .Select(candidate => new RankCandidate(
                    candidate.Lifter.DocumentId,
                    candidate.PointsScores[systemId].ProjectedScore!.Value)));

            foreach (var candidate in uniqueAthletes)
            {
                var confirmedRank = confirmedRanks.GetValueOrDefault(candidate.Lifter.DocumentId);
                var projectedRank = projectedRanks.GetValueOrDefault(candidate.Lifter.DocumentId);
                if (!rankResults.TryGetValue(candidate.Lifter.DocumentId, out var systems))
                {
                    systems = new Dictionary<string, CompetitionOverallRankDto>(StringComparer.Ordinal);
                    rankResults.Add(candidate.Lifter.DocumentId, systems);
                }

                systems[systemId] = new CompetitionOverallRankDto(
                    confirmedRank?.Rank,
                    confirmedRank?.Status ?? "unavailable",
                    projectedRank?.Rank,
                    projectedRank?.Status
                        ?? (candidate.HasPendingAttempts ? "unavailable" : "no-projection"));
            }
        }

        var standings = uniqueAthletes
            .Select(candidate => new CompetitionOverallStandingDto(
                candidate.Lifter.DocumentId,
                candidate.Lifter.Name,
                candidate.BodyWeightKg,
                candidate.Division.DivisionId!,
                candidate.Division.Name,
                candidate.Division.WeightClassId!,
                candidate.Division.WeightClassName,
                candidate.Division.ScoreBy,
                candidate.HasPendingAttempts,
                candidate.PointsScores,
                rankResults[candidate.Lifter.DocumentId]))
            .OrderBy(standing => standing.AthleteName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(standing => standing.AthleteId, StringComparer.Ordinal)
            .ToArray();

        return new CompetitionOverallLeaderboardDto(
            key.Gender,
            key.EquipmentCategory,
            key.EventType,
            standings);
    }

    private static Dictionary<string, RankResult> Rank(IEnumerable<RankCandidate> candidates)
    {
        var values = candidates.ToArray();
        var result = new Dictionary<string, RankResult>(StringComparer.Ordinal);
        foreach (var group in values.GroupBy(candidate => candidate.Value))
        {
            if (group.Count() > 1)
            {
                foreach (var candidate in group)
                {
                    result[candidate.Id] = new RankResult(null, "tied");
                }

                continue;
            }

            var candidateValue = group.Key;
            var rank = values.Count(candidate => candidate.Value > candidateValue) + 1;
            result[group.Single().Id] = new RankResult(rank, "ranked");
        }

        return result;
    }

    private static string GetUnavailableStatus(
        WeightClassCandidate candidate,
        bool isRankable,
        bool isProjected)
    {
        if (!isRankable)
        {
            return "unrankable";
        }

        if (!isProjected)
        {
            return candidate.Performance.ConfirmedSuccessfulLiftSumKg.HasValue
                ? "incomplete"
                : "unavailable";
        }

        if (!candidate.HasPendingAttempts)
        {
            return "no-projection";
        }

        return candidate.Performance.BaselineProjectedLiftSumKg.HasValue
            ? "incomplete"
            : "unavailable";
    }

    private static double? GetRankableResult(
        CompetitionLifterPerformanceDto performance,
        string eventType) =>
        eventType switch
        {
            "full-power" => performance.CompleteThreeLiftTotalKg,
            "bench-only" => performance.BenchOnlyResultKg,
            _ => null
        };

    private static double? GetProjectedResult(
        CompetitionLifterPerformanceDto performance,
        string eventType) =>
        eventType switch
        {
            "full-power" => performance.BaselineProjectedThreeLiftTotalKg,
            "bench-only" => performance.BaselineProjectedEligibleTotalKg,
            _ => null
        };

    private static string? GetWeightClassRankingLimitation(
        CompetitionDivisionDto division,
        string gender,
        string equipment,
        string eventType)
    {
        if (!division.WeightClassResolved)
        {
            return "The declared weight-class configuration reference is unresolved.";
        }

        if (gender is not ("male" or "female"))
        {
            return "Gender is missing or unsupported.";
        }

        if (equipment is not ("classic" or "equipped"))
        {
            return "Equipment category is missing or unsupported.";
        }

        if (eventType is not ("full-power" or "bench-only"))
        {
            return "Ranking rules for this event category are not verified.";
        }

        if (!string.Equals(division.ScoreBy?.Trim(), "TOTAL", StringComparison.OrdinalIgnoreCase))
        {
            return "The configured scoreBy is not TOTAL; the award scoring basis is not verified.";
        }

        return null;
    }

    private static bool IsOverallCategorySupported(
        string gender,
        string equipment,
        string eventType) =>
        gender is "male" or "female"
        && equipment is "classic" or "equipped"
        && eventType is "full-power" or "bench-only";

    private static IReadOnlyDictionary<string, CompetitionPointsScoreDto> CreateUnavailableScores() =>
        CompetitionPointsCalculator.SystemIds.ToDictionary(
            systemId => systemId,
            _ => new CompetitionPointsScoreDto(null, null),
            StringComparer.Ordinal);

    private static string GetEventType(CompetitionLiftConfigurationDto lifts)
    {
        if (lifts.Squat is null || lifts.Bench is null || lifts.Dead is null)
        {
            return "unknown";
        }

        if (lifts.Squat == true && lifts.Bench == true && lifts.Dead == true)
        {
            return "full-power";
        }

        if (lifts.Squat == false && lifts.Bench == true && lifts.Dead == false)
        {
            return "bench-only";
        }

        var enabled = new List<string>(3);
        if (lifts.Squat == true) enabled.Add("squat");
        if (lifts.Bench == true) enabled.Add("bench");
        if (lifts.Dead == true) enabled.Add("dead");
        return enabled.Count == 0 ? "unknown" : string.Join("-", enabled);
    }

    private static string? NormalizeGender(string? gender) =>
        gender?.Trim().ToLowerInvariant() switch
        {
            "male" => "male",
            "female" => "female",
            _ => null
        };

    private static string? NormalizeEquipment(string? equipment) =>
        equipment?.Trim().ToLowerInvariant() switch
        {
            "raw" or "classic" => "classic",
            "equipped" => "equipped",
            _ => null
        };

    private static string NormalizeUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();

    private sealed record WeightClassKey(
        string DivisionId,
        string WeightClassId,
        string Gender,
        string Equipment,
        string EventType,
        string ScoreBy);

    private sealed record OverallKey(string Gender, string EquipmentCategory, string EventType);

    private sealed record WeightClassCandidate(
        CompetitionLifterDto Lifter,
        CompetitionDivisionDto Division,
        CompetitionLifterPerformanceDto Performance,
        double? BodyWeightKg,
        double? ConfirmedResultKg,
        double? ProjectedResultKg,
        bool HasPendingAttempts,
        string? RankingLimitation);

    private sealed record OverallCandidate(
        CompetitionLifterDto Lifter,
        CompetitionDivisionDto Division,
        double? BodyWeightKg,
        bool HasPendingAttempts,
        IReadOnlyDictionary<string, CompetitionPointsScoreDto> PointsScores);

    private sealed record RankCandidate(string Id, double Value);
    private sealed record RankResult(int? Rank, string Status);
}
