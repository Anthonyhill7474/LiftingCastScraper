using System.Text.Json;
using LiftingCastTracker.Services.Calculations;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Tests;

public sealed class CompetitionPointsCalculatorTests
{
    [Theory]
    [InlineData("male", "classic", false, 80d, 500d, 70.518653604429)]
    [InlineData("male", "equipped", false, 80d, 500d, 59.014391380657)]
    [InlineData("male", "classic", true, 80d, 150d, 76.787329873028)]
    [InlineData("male", "equipped", true, 80d, 150d, 54.850786240815)]
    [InlineData("female", "classic", false, 60d, 300d, 67.812043460803)]
    [InlineData("female", "equipped", false, 60d, 300d, 55.721271669703)]
    [InlineData("female", "classic", true, 60d, 100d, 85.907797771682)]
    [InlineData("female", "equipped", true, 60d, 100d, 62.291168103670)]
    public void IpfGlUsesTheProvidedGenderEquipmentAndEventCoefficients(
        string gender,
        string equipment,
        bool benchOnly,
        double bodyWeight,
        double total,
        double expected)
    {
        var actual = Calculate(
            "ipf-gl",
            bodyWeight,
            total,
            gender,
            equipment,
            benchOnly ? BenchOnly : FullPowerlifting);

        Assert.Equal(expected, actual!.Value, 9);
    }

    [Theory]
    [InlineData("dots", "male", 80d, 500d, 344.7732270438612)]
    [InlineData("dots", "female", 60d, 300d, 332.56368739944025)]
    [InlineData("wilks-1", "male", 80d, 500d, 341.3492950841584)]
    [InlineData("wilks-1", "female", 60d, 300d, 334.46606258791513)]
    [InlineData("wilks-2", "male", 80d, 500d, 409.56924830589327)]
    [InlineData("wilks-2", "female", 60d, 300d, 395.70982184512036)]
    public void ThreeLiftSystemsUseTheProvidedGenderCoefficients(
        string system,
        string gender,
        double bodyWeight,
        double total,
        double expected)
    {
        var actual = Calculate(system, bodyWeight, total, gender, "RAW", FullPowerlifting);

        Assert.Equal(expected, actual!.Value, 9);
    }

    [Theory]
    [InlineData("male", "classic", false, 80d, 500d, 495.843459823095)]
    [InlineData("male", "equipped", false, 80d, 500d, 442.125199344487)]
    [InlineData("male", "classic", true, 80d, 150d, 626.410098984138)]
    [InlineData("male", "equipped", true, 80d, 150d, 510.777520369850)]
    [InlineData("female", "classic", false, 60d, 300d, 528.701046121818)]
    [InlineData("female", "equipped", false, 60d, 300d, 443.802456278542)]
    [InlineData("female", "classic", true, 60d, 100d, 804.717278896792)]
    [InlineData("female", "equipped", true, 60d, 100d, 584.213706299047)]
    public void LegacyIpfUsesTheProvidedGenderEquipmentAndEventCoefficients(
        string gender,
        string equipment,
        bool benchOnly,
        double bodyWeight,
        double total,
        double expected)
    {
        var actual = Calculate(
            "legacy-ipf",
            bodyWeight,
            total,
            gender,
            equipment,
            benchOnly ? BenchOnly : FullPowerlifting);

        Assert.Equal(expected, actual!.Value, 9);
    }

    [Theory]
    [InlineData("dots")]
    [InlineData("wilks-1")]
    [InlineData("wilks-2")]
    public void ThreeLiftOnlySystemsDoNotScoreBenchOnlyEvents(string system)
    {
        Assert.Null(Calculate(system, 80, 150, "MALE", "RAW", BenchOnly));
    }

    [Fact]
    public void ConvertsPoundsToKilogramsBeforeScoring()
    {
        var bodyWeightSource = JsonValue(220.462262);
        var totalSource = JsonValue(1102.31131);
        Assert.True(CompetitionPerformanceCalculator.TryGetKilograms(
            bodyWeightSource,
            "LB",
            out var bodyWeightKg));
        Assert.True(CompetitionPerformanceCalculator.TryGetKilograms(
            totalSource,
            "LB",
            out var totalKg));

        var score = Calculate("ipf-gl", bodyWeightKg, totalKg, "male", "raw", FullPowerlifting);

        Assert.Equal(99.99999991614095, bodyWeightKg, 10);
        Assert.Equal(499.99999958070475, totalKg, 10);
        Assert.Equal(63.164622548735935, score!.Value, 9);
    }

    [Fact]
    public void MissingInvalidAndIncompleteInputsAreUnavailable()
    {
        Assert.Null(Calculate("ipf-gl", null, 500, "male", "raw", FullPowerlifting));
        Assert.Null(Calculate("ipf-gl", 80, null, "male", "raw", FullPowerlifting));
        Assert.Null(Calculate("ipf-gl", 0, 500, "male", "raw", FullPowerlifting));
        Assert.Null(Calculate("ipf-gl", double.NaN, 500, "male", "raw", FullPowerlifting));
        Assert.Null(Calculate("ipf-gl", double.PositiveInfinity, 500, "male", "raw", FullPowerlifting));
        Assert.Null(Calculate("ipf-gl", 80, -1, "male", "raw", FullPowerlifting));
        Assert.Null(Calculate("ipf-gl", 80, double.NaN, "male", "raw", FullPowerlifting));
        Assert.Null(Calculate("ipf-gl", 80, 500, null, "raw", FullPowerlifting));
        Assert.Null(Calculate("ipf-gl", 80, 500, "male", null, FullPowerlifting));
        Assert.Null(Calculate("ipf-gl", 80, 500, "male", "raw", null));
        Assert.Null(Calculate(
            "ipf-gl",
            80,
            500,
            "male",
            "raw",
            new CompetitionLiftConfigurationDto(true, true, false)));

        var partialPerformance = CompetitionPerformanceCalculator.Calculate(
            [
                Attempt("squat", "squat", 200, "good"),
                Attempt("bench", "bench", 100, "good")
            ],
            "KG",
            FullPowerlifting);
        Assert.Null(Calculate(
            "dots",
            80,
            partialPerformance.EligibleCompleteTotalKg,
            "male",
            "raw",
            FullPowerlifting));
    }

    [Fact]
    public void RejectsInvalidDenominatorsNonfiniteResultsAndUnknownSystems()
    {
        Assert.Null(Calculate("dots", 0.01, 500, "male", "raw", FullPowerlifting));
        Assert.Null(Calculate("dots", double.MaxValue, 500, "male", "raw", FullPowerlifting));
        Assert.Null(Calculate("dots", 15, double.MaxValue, "male", "raw", FullPowerlifting));
        Assert.Null(Calculate("dots", 80, double.PositiveInfinity, "male", "raw", FullPowerlifting));
        Assert.Throws<ArgumentException>(() =>
            Calculate("not-a-system", 80, 500, "male", "raw", FullPowerlifting));
    }

    private static readonly CompetitionLiftConfigurationDto FullPowerlifting = new(true, true, true);
    private static readonly CompetitionLiftConfigurationDto BenchOnly = new(false, true, false);

    private static double? Calculate(
        string system,
        double? bodyWeight,
        double? total,
        string? gender,
        string? equipment,
        CompetitionLiftConfigurationDto? lifts) =>
        CompetitionPointsCalculator.Calculate(
            system,
            bodyWeight,
            total,
            gender,
            equipment,
            lifts);

    private static CompetitionAttemptDto Attempt(
        string id,
        string lift,
        double weight,
        string result) =>
        new(id, lift, null, JsonValue(weight), JsonValue(result));

    private static JsonElement JsonValue<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }
}
