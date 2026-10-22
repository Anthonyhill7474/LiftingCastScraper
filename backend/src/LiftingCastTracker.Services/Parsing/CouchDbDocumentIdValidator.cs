namespace LiftingCastTracker.Services.Parsing;

public static class CouchDbDocumentIdValidator
{
    public static bool IsValid(string? documentId) =>
        !string.IsNullOrEmpty(documentId)
        && documentId.Length <= 512
        && documentId is not ("." or "..")
        && !documentId.Any(char.IsControl);
}
