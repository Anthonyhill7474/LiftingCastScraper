using System.Globalization;
using System.Text.Json;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Calculations;

public static class CompetitionPerformanceCalculator
{
    private const double PoundsToKilograms = 0.45359237;

    public static CompetitionLifterPerformanceDto Calculate(
        IReadOnlyList<CompetitionAttemptDto> attempts,
        string? units,
        CompetitionLiftConfigurationDto? lifts = null)
    {
        var enabledLifts = GetEnabledLifts(lifts);
        var kilogramsPerUnit = GetKilogramsPerUnit(units);
        var bestByLift = new Dictionary<string, double?>(StringComparer.Ordinal)
        {
            ["squat"] = BestWeight(attempts, "squat", kilogramsPerUnit, isConfirmed: true),
            ["bench"] = BestWeight(attempts, "bench", kilogramsPerUnit, isConfirmed: true),
            ["dead"] = BestWeight(attempts, "dead", kilogramsPerUnit, isConfirmed: true)
        };
        var projectedByLift = new Dictionary<string, double?>(StringComparer.Ordinal)
        {
            ["squat"] = BestWeight(attempts, "squat", kilogramsPerUnit, isConfirmed: false),
            ["bench"] = BestWeight(attempts, "bench", kilogramsPerUnit, isConfirmed: false),
            ["dead"] = BestWeight(attempts, "dead", kilogramsPerUnit, isConfirmed: false)
        };

        var confirmedSum = SumAvailable(enabledLifts.Select(lift => bestByLift[lift]));
        var projectedSum = SumAvailable(enabledLifts.Select(lift => projectedByLift[lift]));
        var isThreeLiftCategory = enabledLifts.Count == 3
            && enabledLifts.Contains("squat", StringComparer.Ordinal)
            && enabledLifts.Contains("bench", StringComparer.Ordinal)
            && enabledLifts.Contains("dead", StringComparer.Ordinal);
        var completeThreeLiftTotal = isThreeLiftCategory
            ? CompleteTotal(["squat", "bench", "dead"], bestByLift)
            : null;
        var eligibleCompleteTotal = CompleteTotal(enabledLifts, bestByLift);
        var baselineProjectedThreeLiftTotal = isThreeLiftCategory
            ? CompleteTotal(["squat", "bench", "dead"], projectedByLift)
            : null;
        var baselineProjectedEligibleTotal = CompleteTotal(enabledLifts, projectedByLift);
        var benchOnlyResult = enabledLifts.Count == 1 && enabledLifts[0] == "bench"
            ? bestByLift["bench"]
            : null;

        return new CompetitionLifterPerformanceDto(
            bestByLift["squat"],
            bestByLift["bench"],
            bestByLift["dead"],
            confirmedSum,
            completeThreeLiftTotal,
            eligibleCompleteTotal,
            benchOnlyResult,
            projectedSum,
            baselineProjectedThreeLiftTotal,
            baselineProjectedEligibleTotal);
    }

    public static bool IsEditableScenarioAttempt(CompetitionAttemptDto attempt, string? units)
    {
        if (attempt.Result is { } result
            && result.ValueKind == JsonValueKind.String
            && IsFinalResult(result.GetString()))
        {
            return false;
        }

        return TryGetKilograms(attempt.Weight, GetKilogramsPerUnit(units), out _)
            && TryGetLiftKey(attempt.LiftName, out _);
    }

    public static bool TryGetKilograms(
        JsonElement? value,
        string? units,
        out double kilograms) =>
        TryGetKilograms(value, GetKilogramsPerUnit(units), out kilograms);

    public static bool HasPendingSelectedAttempt(
        IReadOnlyList<CompetitionAttemptDto> attempts,
        string? units,
        CompetitionLiftConfigurationDto? lifts)
    {
        var enabledLifts = GetEnabledLifts(lifts);
        var kilogramsPerUnit = GetKilogramsPerUnit(units);
        if (enabledLifts.Count == 0 || kilogramsPerUnit is null)
        {
            return false;
        }

        return attempts.Any(attempt =>
            TryGetLiftKey(attempt.LiftName, out var lift)
            && enabledLifts.Contains(lift, StringComparer.Ordinal)
            && !IsFinalResult(GetString(attempt.Result))
            && TryGetKilograms(attempt.Weight, kilogramsPerUnit, out _));
    }

    public static CompetitionAttemptDto ApplyScenario(
        CompetitionAttemptDto attempt,
        CompetitionAttemptScenario scenario)
    {
        using var weightDocument = JsonDocument.Parse(
            scenario.Weight.ToString("R", CultureInfo.InvariantCulture));
        using var resultDocument = JsonDocument.Parse(
            JsonSerializer.Serialize(scenario.Result));
        return attempt with
        {
            Weight = weightDocument.RootElement.Clone(),
            Result = resultDocument.RootElement.Clone()
        };
    }

    public static bool TryGetLiftKey(string? liftName, out string liftKey)
    {
        liftKey = liftName?.Trim().ToLowerInvariant() switch
        {
            "squat" => "squat",
            "bench" => "bench",
            "dead" or "deadlift" => "dead",
            _ => string.Empty
        };
        return liftKey.Length > 0;
    }

    private static IReadOnlyList<string> GetEnabledLifts(CompetitionLiftConfigurationDto? lifts)
    {
        if (lifts is null)
        {
            return ["squat", "bench", "dead"];
        }

        var enabled = new List<string>(3);
        if (lifts.Squat == true) enabled.Add("squat");
        if (lifts.Bench == true) enabled.Add("bench");
        if (lifts.Dead == true) enabled.Add("dead");
        return enabled;
    }

    private static double? BestWeight(
        IReadOnlyList<CompetitionAttemptDto> attempts,
        string liftKey,
        double? kilogramsPerUnit,
        bool isConfirmed)
    {
        if (kilogramsPerUnit is null)
        {
            return null;
        }

        var weights = new List<double>();
        foreach (var attempt in attempts)
        {
            if (!TryGetLiftKey(attempt.LiftName, out var attemptLift)
                || attemptLift != liftKey
                || !TryGetKilograms(attempt.Weight, kilogramsPerUnit, out var weight))
            {
                continue;
            }

            var final = IsFinalResult(GetString(attempt.Result));
            var successful = string.Equals(
                GetString(attempt.Result)?.Trim(),
                "good",
                StringComparison.OrdinalIgnoreCase);
            if (isConfirmed ? successful : successful || !final)
            {
                weights.Add(weight);
            }
        }

        return weights.Count == 0 ? null : weights.Max();
    }

    private static double? CompleteTotal(
        IReadOnlyList<string> enabledLifts,
        IReadOnlyDictionary<string, double?> bestByLift)
    {
        if (enabledLifts.Count == 0)
        {
            return null;
        }

        var values = enabledLifts.Select(lift => bestByLift[lift]).ToArray();
        if (!values.All(value => value.HasValue))
        {
            return null;
        }

        var total = values.Sum(value => value!.Value);
        return double.IsFinite(total) ? total : null;
    }

    private static double? SumAvailable(IEnumerable<double?> values)
    {
        var available = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        if (available.Length == 0)
        {
            return null;
        }

        var sum = available.Sum();
        return double.IsFinite(sum) ? sum : null;
    }

    private static bool IsFinalResult(string? result) =>
        string.Equals(result?.Trim(), "good", StringComparison.OrdinalIgnoreCase)
        || string.Equals(result?.Trim(), "bad", StringComparison.OrdinalIgnoreCase);

    private static string? GetString(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } element
            ? element.GetString()
            : null;

    private static double? GetKilogramsPerUnit(string? units) =>
        units?.Trim().ToUpperInvariant() switch
        {
            "KG" or "KGS" or "KILOGRAM" or "KILOGRAMS" => 1d,
            "LB" or "LBS" or "POUND" or "POUNDS" => PoundsToKilograms,
            _ => null
        };

    private static bool TryGetKilograms(
        JsonElement? value,
        double? kilogramsPerUnit,
        out double kilograms)
    {
        kilograms = 0;
        if (kilogramsPerUnit is null || value is not { } element)
        {
            return false;
        }

        double sourceWeight;
        if (element.ValueKind == JsonValueKind.Number
            && element.TryGetDouble(out sourceWeight))
        {
            // Numeric source weights are expected.
        }
        else if (element.ValueKind == JsonValueKind.String
                 && double.TryParse(
                     element.GetString(),
                     NumberStyles.Float,
                     CultureInfo.InvariantCulture,
                     out sourceWeight))
        {
            // Accept numeric strings without changing the stored source value.
        }
        else
        {
            return false;
        }

        kilograms = sourceWeight * kilogramsPerUnit.Value;
        return double.IsFinite(kilograms) && kilograms > 0;
    }
}
