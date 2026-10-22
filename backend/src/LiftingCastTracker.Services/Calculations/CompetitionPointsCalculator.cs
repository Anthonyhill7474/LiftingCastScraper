using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Services.Calculations;

public static class CompetitionPointsCalculator
{
    public const string DefaultSystem = "ipf-gl";

    public static IReadOnlyList<string> SystemIds { get; } =
        Array.AsReadOnly(["ipf-gl", "dots", "wilks-1", "wilks-2", "legacy-ipf"]);

    private static readonly IReadOnlyDictionary<(string Gender, string Equipment, bool BenchOnly), Coefficient3>
        IpfGlCoefficients = new Dictionary<(string, string, bool), Coefficient3>
        {
            [("male", "equipped", false)] = new(1236.25115, 1449.21864, 0.01644),
            [("male", "classic", false)] = new(1199.72839, 1025.18162, 0.00921),
            [("male", "equipped", true)] = new(381.22073, 733.79378, 0.02398),
            [("male", "classic", true)] = new(320.98041, 281.40258, 0.01008),
            [("female", "equipped", false)] = new(758.63878, 949.31382, 0.02435),
            [("female", "classic", false)] = new(610.32796, 1045.59282, 0.03048),
            [("female", "equipped", true)] = new(221.82209, 357.00377, 0.02937),
            [("female", "classic", true)] = new(142.40398, 442.52671, 0.04724)
        };

    private static readonly IReadOnlyDictionary<string, Coefficient5> DotsCoefficients =
        new Dictionary<string, Coefficient5>(StringComparer.Ordinal)
        {
            ["male"] = new(-0.000001093, 0.0007391293, -0.1918759221, 24.0900756, -307.75076),
            ["female"] = new(-0.0000010706, 0.0005158568, -0.1126655495, 13.6175032, -57.96288)
        };

    private static readonly IReadOnlyDictionary<(string Gender, bool Wilks2), Coefficient6>
        WilksCoefficients = new Dictionary<(string, bool), Coefficient6>
        {
            [("male", false)] = new(
                -0.00000001291, 0.00000701863, -0.00113732, -0.002388645, 16.2606339, -216.0475144),
            [("female", false)] = new(
                -0.00000009054, 0.00004731582, -0.00930733913, 0.82112226871, -27.23842536447, 594.31747775582),
            [("male", true)] = new(
                -0.0000000120804336482315, 0.00000707665973070743, -0.00139583381094385,
                0.073694103462609, 8.47206137941125, 47.4617885411949),
            [("female", true)] = new(
                -0.000000023334613884954, 0.00000938773881462799, -0.0010504000506583,
                -0.0330725063103405, 13.7121941940668, -125.425539779509)
        };

    private static readonly IReadOnlyDictionary<(string Gender, string Equipment, bool BenchOnly), Coefficient4>
        LegacyIpfCoefficients = new Dictionary<(string, string, bool), Coefficient4>
        {
            [("male", "classic", false)] = new(310.6700, 857.7850, 53.2160, 147.0835),
            [("male", "classic", true)] = new(86.4745, 259.1550, 17.5785, 53.1220),
            [("male", "equipped", false)] = new(387.2650, 1121.2800, 80.6324, 222.4896),
            [("male", "equipped", true)] = new(133.9400, 441.4650, 35.3938, 113.0057),
            [("female", "classic", false)] = new(125.1435, 228.0300, 34.5246, 86.8301),
            [("female", "classic", true)] = new(25.0485, 43.8480, 6.7172, 13.9520),
            [("female", "equipped", false)] = new(176.5800, 373.3150, 48.4534, 110.0103),
            [("female", "equipped", true)] = new(49.1060, 124.2090, 23.1990, 67.4926)
        };

    public static bool IsKnownSystem(string? systemId) =>
        SystemIds.Contains(systemId?.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static double? Calculate(
        string systemId,
        double? bodyWeightKg,
        double? totalKg,
        string? gender,
        string? rawOrEquipped,
        CompetitionLiftConfigurationDto? lifts)
    {
        var normalizedSystem = systemId?.Trim().ToLowerInvariant();
        if (!IsKnownSystem(normalizedSystem))
        {
            throw new ArgumentException("Unsupported points system.", nameof(systemId));
        }

        if (bodyWeightKg is not { } bodyWeight
            || totalKg is not { } total
            || !double.IsFinite(bodyWeight)
            || !double.IsFinite(total)
            || bodyWeight <= 0
            || total <= 0
            || !TryGetGender(gender, out var normalizedGender)
            || !TryGetEquipment(rawOrEquipped, out var equipment)
            || !TryGetEvent(lifts, out var benchOnly))
        {
            return null;
        }

        double? score = normalizedSystem switch
        {
            "ipf-gl" => CalculateIpfGl(bodyWeight, total, normalizedGender, equipment, benchOnly),
            "dots" when !benchOnly => CalculateDots(bodyWeight, total, normalizedGender),
            "wilks-1" when !benchOnly => CalculateWilks(bodyWeight, total, normalizedGender, wilks2: false),
            "wilks-2" when !benchOnly => CalculateWilks(bodyWeight, total, normalizedGender, wilks2: true),
            "legacy-ipf" => CalculateLegacyIpf(bodyWeight, total, normalizedGender, equipment, benchOnly),
            _ => null
        };

        return score is { } value && double.IsFinite(value) ? value : null;
    }

    private static double? CalculateIpfGl(
        double bodyWeight,
        double total,
        string gender,
        string equipment,
        bool benchOnly)
    {
        if (!IpfGlCoefficients.TryGetValue((gender, equipment, benchOnly), out var coefficients))
        {
            return null;
        }

        var denominator = coefficients.A
            - coefficients.B * Math.Exp(-coefficients.C * bodyWeight);
        return TryScaleByCoefficient(100d, denominator, total, out var score) ? score : null;
    }

    private static double? CalculateDots(double bodyWeight, double total, string gender)
    {
        var coefficients = DotsCoefficients[gender];
        var denominator = coefficients.A * Math.Pow(bodyWeight, 4)
            + coefficients.B * Math.Pow(bodyWeight, 3)
            + coefficients.C * Math.Pow(bodyWeight, 2)
            + coefficients.D * bodyWeight
            + coefficients.E;
        return TryScaleByCoefficient(500d, denominator, total, out var score) ? score : null;
    }

    private static double? CalculateWilks(double bodyWeight, double total, string gender, bool wilks2)
    {
        var coefficients = WilksCoefficients[(gender, wilks2)];
        var denominator = coefficients.F
            + coefficients.E * bodyWeight
            + coefficients.D * Math.Pow(bodyWeight, 2)
            + coefficients.C * Math.Pow(bodyWeight, 3)
            + coefficients.B * Math.Pow(bodyWeight, 4)
            + coefficients.A * Math.Pow(bodyWeight, 5);
        var multiplier = wilks2 ? 600d : 500d;
        return TryScaleByCoefficient(multiplier, denominator, total, out var score) ? score : null;
    }

    private static double? CalculateLegacyIpf(
        double bodyWeight,
        double total,
        string gender,
        string equipment,
        bool benchOnly)
    {
        if (!LegacyIpfCoefficients.TryGetValue((gender, equipment, benchOnly), out var coefficients))
        {
            return null;
        }

        var logBodyWeight = Math.Log(bodyWeight);
        var denominator = coefficients.C * logBodyWeight - coefficients.D;
        if (!double.IsFinite(logBodyWeight)
            || !double.IsFinite(denominator)
            || denominator <= 0)
        {
            return null;
        }

        var numerator = total - (coefficients.A * logBodyWeight - coefficients.B);
        var coefficient = numerator / denominator;
        var score = 500d + 100d * coefficient;
        return double.IsFinite(numerator) && double.IsFinite(coefficient) && double.IsFinite(score)
            ? score
            : null;
    }

    private static bool TryScaleByCoefficient(
        double multiplier,
        double denominator,
        double total,
        out double result)
    {
        result = 0;
        if (!double.IsFinite(multiplier)
            || !double.IsFinite(denominator)
            || !double.IsFinite(total)
            || denominator <= 0)
        {
            return false;
        }

        var coefficient = multiplier / denominator;
        result = total * coefficient;
        return double.IsFinite(coefficient) && double.IsFinite(result);
    }

    private static bool TryGetGender(string? gender, out string normalizedGender)
    {
        normalizedGender = gender?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalizedGender is "male" or "female";
    }

    private static bool TryGetEquipment(string? rawOrEquipped, out string equipment)
    {
        var normalized = rawOrEquipped?.Trim().ToLowerInvariant();
        equipment = normalized switch
        {
            "raw" or "classic" => "classic",
            "equipped" => "equipped",
            _ => string.Empty
        };
        return equipment.Length > 0;
    }

    private static bool TryGetEvent(
        CompetitionLiftConfigurationDto? lifts,
        out bool benchOnly)
    {
        benchOnly = false;
        if (lifts?.Squat == true && lifts.Bench == true && lifts.Dead == true)
        {
            return true;
        }

        if (lifts?.Squat == false && lifts.Bench == true && lifts.Dead == false)
        {
            benchOnly = true;
            return true;
        }

        return false;
    }

    private sealed record Coefficient3(double A, double B, double C);
    private sealed record Coefficient4(double A, double B, double C, double D);
    private sealed record Coefficient5(double A, double B, double C, double D, double E);
    private sealed record Coefficient6(double A, double B, double C, double D, double E, double F);
}
