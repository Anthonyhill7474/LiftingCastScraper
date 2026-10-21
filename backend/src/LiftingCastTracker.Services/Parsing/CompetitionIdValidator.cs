namespace LiftingCastTracker.Services.Parsing;

public static class CompetitionIdValidator
{
    public static bool IsValid(string? competitionId) =>
        !string.IsNullOrEmpty(competitionId)
        && competitionId.Length <= 128
        && competitionId.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
