using System.Globalization;

namespace LiftingCastTracker.Services.Parsing;

public static class CompetitionDateParser
{
    private static readonly IReadOnlyDictionary<string, string> DateFormats =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DD/MM/YYYY"] = "dd/MM/yyyy",
            ["MM/DD/YYYY"] = "MM/dd/yyyy",
            ["YYYY/MM/DD"] = "yyyy/MM/dd",
            ["DD-MM-YYYY"] = "dd-MM-yyyy",
            ["MM-DD-YYYY"] = "MM-dd-yyyy",
            ["YYYY-MM-DD"] = "yyyy-MM-dd",
            ["DD.MM.YYYY"] = "dd.MM.yyyy",
            ["MM.DD.YYYY"] = "MM.dd.yyyy",
            ["YYYY.MM.DD"] = "yyyy.MM.dd"
        };

    public static DateOnly? Parse(string? date, string? dateFormat)
    {
        if (string.IsNullOrWhiteSpace(date)
            || string.IsNullOrWhiteSpace(dateFormat)
            || !DateFormats.TryGetValue(dateFormat.Trim(), out var format)
            || !DateOnly.TryParseExact(
                date.Trim(),
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            return null;
        }

        return parsedDate;
    }
}
