using System.Net.Http;
using System.Text;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Parsing;
using LiftingCastTracker.Services.Repositories;

namespace LiftingCastTracker.Repositories.ExternalData;

public sealed class LiftingCastCompetitionClient : ILiftingCastCompetitionClient
{
    private const int MaximumInspectionResponseBytes = 1_048_576;
    private const int MaximumSyncResponseBytes = 10 * 1_048_576;
    private readonly HttpClient _httpClient;

    public LiftingCastCompetitionClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<CouchDbHttpResponse> GetSnapshotAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        ValidateCompetitionId(competitionId);
        return GetAsync(
            $"{competitionId}_readonly/_all_docs?conflicts=true&update_seq=true&include_docs=true&limit=25",
            MaximumInspectionResponseBytes,
            cancellationToken);
    }

    public Task<CouchDbHttpResponse> GetSyncSnapshotAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        ValidateCompetitionId(competitionId);
        return GetAsync(
            $"{competitionId}_readonly/_all_docs?update_seq=true&include_docs=true",
            MaximumSyncResponseBytes,
            cancellationToken);
    }

    public Task<CouchDbHttpResponse> GetDocumentAsync(
        string competitionId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ValidateCompetitionId(competitionId);
        if (!CouchDbDocumentIdValidator.IsValid(documentId))
        {
            throw new ArgumentException("The CouchDB document ID is invalid.", nameof(documentId));
        }

        return GetAsync(
            $"{competitionId}_readonly/{Uri.EscapeDataString(documentId)}",
            MaximumInspectionResponseBytes,
            cancellationToken);
    }

    public Task<CouchDbHttpResponse> CheckChangesAsync(
        string competitionId,
        CancellationToken cancellationToken = default) =>
        GetChangesSinceAsync(competitionId, "now", 1, cancellationToken);

    public Task<CouchDbHttpResponse> GetChangesSinceAsync(
        string competitionId,
        string checkpoint,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateCompetitionId(competitionId);
        if (!CouchDbChangesRequestValidator.IsValid(checkpoint, limit))
        {
            throw new ArgumentException("The checkpoint or requested limit is invalid.", nameof(checkpoint));
        }

        return GetAsync(
            $"{competitionId}_readonly/_changes?feed=normal&since={Uri.EscapeDataString(checkpoint)}&limit={limit}",
            MaximumInspectionResponseBytes,
            cancellationToken);
    }

    public Task<CouchDbHttpResponse> GetSyncChangesAsync(
        string competitionId,
        string checkpoint,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateCompetitionId(competitionId);
        if (!CouchDbChangesRequestValidator.IsValid(checkpoint, limit))
        {
            throw new ArgumentException("The checkpoint or requested limit is invalid.", nameof(checkpoint));
        }

        return GetAsync(
            $"{competitionId}_readonly/_changes?feed=normal&since={Uri.EscapeDataString(checkpoint)}&include_docs=true&limit={limit}",
            MaximumSyncResponseBytes,
            cancellationToken);
    }

    private async Task<CouchDbHttpResponse> GetAsync(
        string relativeUrl,
        int maximumResponseBytes,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.Content.Headers.ContentLength > maximumResponseBytes)
        {
            throw new InvalidDataException("The CouchDB response exceeded the inspection size limit.");
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var contentBuffer = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var bytesRead = await contentStream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            if (contentBuffer.Length + bytesRead > maximumResponseBytes)
            {
                throw new InvalidDataException("The CouchDB response exceeded the inspection size limit.");
            }

            await contentBuffer.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        var body = Encoding.UTF8.GetString(contentBuffer.ToArray());

        return new CouchDbHttpResponse((int)response.StatusCode, body);
    }

    private static void ValidateCompetitionId(string competitionId)
    {
        if (!CompetitionIdValidator.IsValid(competitionId))
        {
            throw new ArgumentException("The competition ID contains unsupported characters.", nameof(competitionId));
        }
    }
}
