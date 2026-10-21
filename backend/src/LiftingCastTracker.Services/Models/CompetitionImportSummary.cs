namespace LiftingCastTracker.Services.Models;

public sealed record CompetitionImportSummary(
    int Inserted,
    int Updated,
    int Unchanged,
    int Invalid);
