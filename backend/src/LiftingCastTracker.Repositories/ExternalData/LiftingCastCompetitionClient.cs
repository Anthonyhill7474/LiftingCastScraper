using System.Net.Http;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Parsing;
using LiftingCastTracker.Services.Repositories;

namespace LiftingCastTracker.Repositories.ExternalData;

public sealed class LiftingCastCompetitionClient : ILiftingCastCompetitionClient
{
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
            $"{competitionId}_readonly/_all_docs?conflicts=true&update_seq=true&include_docs=true&limit=3",
            cancellationToken);
    }

    public Task<CouchDbHttpResponse> CheckChangesAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        ValidateCompetitionId(competitionId);
        return GetAsync(
            $"{competitionId}_readonly/_changes?feed=normal&since=now&limit=1",
            cancellationToken);
    }

    private async Task<CouchDbHttpResponse> GetAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

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
