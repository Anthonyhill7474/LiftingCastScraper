using System.Net;
using System.Text;
using LiftingCastTracker.Repositories.ExternalData;
using LiftingCastTracker.Services.Parsing;

namespace LiftingCastTracker.Tests;

public class CouchDbInspectionTests
{
    [Theory]
    [InlineData("mnk13ga524h1", true)]
    [InlineData("meet_123-abc", true)]
    [InlineData("", false)]
    [InlineData("../other", false)]
    [InlineData("id?include_docs=true", false)]
    [InlineData("ümlaut", false)]
    public void ValidatesCompetitionIds(string? id, bool expected)
    {
        Assert.Equal(expected, CompetitionIdValidator.IsValid(id));
    }

    [Fact]
    public void RejectsCompetitionIdsLongerThanTheLimit()
    {
        Assert.False(CompetitionIdValidator.IsValid(new string('a', 129)));
    }

    [Fact]
    public void ParsesBoundedSnapshotSummaryWithoutReturningDocumentValues()
    {
        const string json = """
            {
              "total_rows": 400,
              "update_seq": "17-g1AAA",
              "rows": [
                {
                  "id": "athlete:1",
                  "doc": {
                    "name": "Private document value",
                    "active": true,
                    "weight": 100,
                    "metadata": { "division": "open" },
                    "attempts": [1, 2]
                  }
                }
              ]
            }
            """;

        var retrievedAt = DateTimeOffset.Parse("2026-10-22T01:00:00Z");
        var summary = CouchDbResponseParser.ParseSnapshot("meet-1", json, retrievedAt);
        var sample = Assert.Single(summary.SampleDocuments);

        Assert.Equal("meet-1", summary.CompetitionId);
        Assert.Equal(400, summary.TotalDocuments);
        Assert.Equal("17-g1AAA", summary.UpdateSequence?.GetString());
        Assert.Equal("athlete:1", sample.Id);
        Assert.Equal(
            new[] { "name:string", "active:boolean", "weight:number", "metadata:object", "attempts:array" },
            sample.Properties.Select(property => $"{property.Name}:{property.Type}"));
        Assert.Equal(retrievedAt, summary.RetrievedAtUtc);
        Assert.DoesNotContain(
            "Private document value",
            System.Text.Json.JsonSerializer.Serialize(summary));
    }

    [Fact]
    public void RejectsMalformedSnapshotResponse()
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => CouchDbResponseParser.ParseSnapshot(
                "meet-1",
                """{"total_rows":2}""",
                DateTimeOffset.UtcNow));
        Assert.Throws<System.Text.Json.JsonException>(
            () => CouchDbResponseParser.ParseSnapshot(
                "meet-1",
                """{"rows":[]}""",
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ParsesChangesFeedShapeAndReportsHttpStatus()
    {
        const string feed = """{"results":[],"last_seq":"17-g1AAA","pending":0}""";
        var checkedAt = DateTimeOffset.Parse("2026-10-22T01:00:00Z");

        var success = CouchDbResponseParser.ParseChanges("meet-1", 200, feed, checkedAt);
        var denied = CouchDbResponseParser.ParseChanges(
            "meet-1",
            401,
            """{"error":"unauthorized","reason":"Authentication required"}""",
            checkedAt);

        Assert.True(success.IsValidChangesFeed);
        Assert.Equal(200, success.HttpStatusCode);
        Assert.Equal(0, success.ResultCount);
        Assert.Equal("17-g1AAA", success.LastSequence?.GetString());
        Assert.False(denied.IsValidChangesFeed);
        Assert.Equal(401, denied.HttpStatusCode);
        Assert.Null(denied.ResultCount);
    }

    [Fact]
    public async Task ClientUsesConfiguredBaseUrlAndBoundedChangesRequest()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"error":"not_found"}""", Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var client = new LiftingCastCompetitionClient(httpClient);

        var response = await client.CheckChangesAsync("mnk13ga524h1");

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("""{"error":"not_found"}""", response.Body);
        Assert.Equal(
            "https://example.test/mnk13ga524h1_readonly/_changes?feed=normal&since=now&limit=1",
            handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task ClientUsesBoundedSnapshotRequestAndRejectsUnsafeId()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"total_rows":0,"rows":[]}""")
        });
        var client = new LiftingCastCompetitionClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        });

        await client.GetSnapshotAsync("mnk13ga524h1");

        Assert.Equal(
            "https://example.test/mnk13ga524h1_readonly/_all_docs?conflicts=true&update_seq=true&include_docs=true&limit=3",
            handler.RequestUri?.AbsoluteUri);
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetSnapshotAsync("../other"));
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }
}
