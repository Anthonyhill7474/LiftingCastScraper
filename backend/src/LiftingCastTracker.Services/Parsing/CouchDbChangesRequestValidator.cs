namespace LiftingCastTracker.Services.Parsing;

public static class CouchDbChangesRequestValidator
{
    public const int DefaultLimit = 10;
    public const int MaximumLimit = 25;
    public const int MaximumCheckpointLength = 2048;

    public static bool IsValid(string? checkpoint, int limit) =>
        !string.IsNullOrEmpty(checkpoint)
        && checkpoint.Length <= MaximumCheckpointLength
        && limit is >= 1 and <= MaximumLimit;
}
