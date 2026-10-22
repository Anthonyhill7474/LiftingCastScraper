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

    [Theory]
    [InlineData("athlete:1", true)]
    [InlineData("_design/view", true)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("bad\nid", false)]
    public void ValidatesDocumentIds(string? id, bool expected)
    {
        Assert.Equal(expected, CouchDbDocumentIdValidator.IsValid(id));
    }

    [Theory]
    [InlineData("377-g1AAA", 10, true)]
    [InlineData("", 10, false)]
    [InlineData("checkpoint", 0, false)]
    [InlineData("checkpoint", 26, false)]
    public void ValidatesCheckpointAndRequestedLimit(string? checkpoint, int limit, bool expected)
    {
        Assert.Equal(expected, CouchDbChangesRequestValidator.IsValid(checkpoint, limit));
    }

    [Fact]
    public void RejectsOverlongCheckpoint()
    {
        Assert.False(CouchDbChangesRequestValidator.IsValid(
            new string('x', CouchDbChangesRequestValidator.MaximumCheckpointLength + 1),
            10));
    }

    [Fact]
    public void GroupsRecurringDocumentStructuresAndExcludesDesignDocuments()
    {
        const string json = """
            {
              "total_rows": 4,
              "update_seq": "17-g1AAA",
              "rows": [
                {
                  "id": "_design/views",
                  "doc": { "views": { "all": {} } }
                },
                {
                  "id": "attempt:1",
                  "doc": { "liftName": "Squat", "attemptNumber": 1 }
                },
                {
                  "id": "attempt:2",
                  "doc": { "attemptNumber": 3, "liftName": "Deadlift" }
                },
                {
                  "id": "lifter:1",
                  "doc": { "lifterId": "private-value" }
                }
              ]
            }
            """;

        var retrievedAt = DateTimeOffset.Parse("2026-10-22T01:00:00Z");
        var summary = CouchDbResponseParser.ParseSnapshot("meet-1", json, retrievedAt);
        var repeated = summary.Structures.Single(structure => structure.Occurrences == 2);

        Assert.Equal("meet-1", summary.CompetitionId);
        Assert.Equal(4, summary.TotalDocuments);
        Assert.Equal("17-g1AAA", summary.UpdateSequence?.GetString());
        Assert.Equal(3, summary.InspectedDocuments);
        Assert.Equal(
            new[] { "attempt:1", "attempt:2" },
            repeated.SampleDocumentIds);
        Assert.Equal(
            new[] { "attemptNumber:number", "liftName:string" },
            repeated.Fields.Select(property => $"{property.Name}:{property.Type}"));
        Assert.Equal(retrievedAt, summary.RetrievedAtUtc);
        Assert.DoesNotContain(
            summary.Structures.SelectMany(structure => structure.SampleDocumentIds),
            id => id.StartsWith("_design/", StringComparison.Ordinal));
        Assert.DoesNotContain("private-value", System.Text.Json.JsonSerializer.Serialize(summary));
    }

    [Fact]
    public void ParsesNestedDocumentStructureWithoutReturningValuesAndBoundsDepth()
    {
        const string json = """
            {
              "_id": "selected:1",
              "_rev": "2-abc",
              "changes": [
                { "date": "private-date", "actor": { "name": "private-name" } },
                { "date": "private-date-2" }
              ],
              "attempts": [
                "private-attempt-1",
                "private-attempt-2",
                "private-attempt-3",
                "private-attempt-4",
                "private-attempt-5",
                "private-attempt-6"
              ]
            }
            """;

        var preview = CouchDbResponseParser.ParseDocumentPreview(
            "meet-1",
            "selected:1",
            json,
            DateTimeOffset.Parse("2026-10-22T01:00:00Z"));
        var serialized = System.Text.Json.JsonSerializer.Serialize(preview);

        Assert.Equal("selected:1", preview.DocumentId);
        Assert.Contains(preview.Fields, field => field.Name == "changes" && field.Type == "array");
        var actor = preview.Fields[2].Children[0].Children[1];
        Assert.Equal("actor", actor.Name);
        Assert.Contains(actor.Children, field => field.Name == "name" && field.Type == "string");
        var attempts = Assert.Single(preview.Fields, field => field.Name == "attempts");
        Assert.Equal(5, attempts.Children.Count);
        Assert.True(attempts.IsTruncated);
        Assert.DoesNotContain("private-name", serialized);
        Assert.DoesNotContain("private-date", serialized);
        Assert.DoesNotContain("private-attempt", serialized);
        Assert.Contains("\"actor\"", serialized);
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
        const string feed = """
            {
              "results": [
                {
                  "seq": "378-g1AAA",
                  "id": "attempt:1",
                  "changes": [{ "rev": "2-abc" }],
                  "deleted": true,
                  "doc": { "lifterName": "private-value" }
                },
                {
                  "seq": "379-g1AAB",
                  "id": "attempt:2",
                  "changes": [{ "rev": "1-def" }]
                }
              ],
              "last_seq": "379-g1AAB",
              "pending": 0
            }
            """;
        var checkedAt = DateTimeOffset.Parse("2026-10-22T01:00:00Z");

        var success = CouchDbResponseParser.ParseChanges("meet-1", 200, "377-g1AAA", feed, checkedAt);
        var denied = CouchDbResponseParser.ParseChanges(
            "meet-1",
            401,
            "377-g1AAA",
            """{"error":"unauthorized","reason":"Authentication required"}""",
            checkedAt);

        Assert.True(success.IsValidChangesFeed);
        Assert.Equal("377-g1AAA", success.SinceSequence);
        Assert.Equal(200, success.HttpStatusCode);
        Assert.Equal(2, success.ResultCount);
        Assert.Equal("379-g1AAB", success.LastSequence?.GetString());
        Assert.Equal("attempt:1", success.Changes[0].Id);
        Assert.Equal(new[] { "2-abc" }, success.Changes[0].Revisions);
        Assert.True(success.Changes[0].Deleted);
        Assert.False(success.Changes[1].Deleted);
        Assert.DoesNotContain("private-value", System.Text.Json.JsonSerializer.Serialize(success));
        Assert.False(denied.IsValidChangesFeed);
        Assert.Equal(401, denied.HttpStatusCode);
        Assert.Null(denied.ResultCount);
        Assert.Empty(denied.Changes);
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
            "https://example.test/mnk13ga524h1_readonly/_all_docs?conflicts=true&update_seq=true&include_docs=true&limit=25",
            handler.RequestUri?.AbsoluteUri);
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetSnapshotAsync("../other"));
    }

    [Fact]
    public async Task ClientPreservesAndEncodesOpaqueCheckpointAndLimit()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"results":[],"last_seq":"next-seq"}""")
        });
        var client = new LiftingCastCompetitionClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        });
        const string checkpoint = "377-g1AAA+opaque/with?symbols&x=1";

        await client.GetChangesSinceAsync("meet-1", checkpoint, 7);

        Assert.Equal(
            "feed=normal&since=377-g1AAA%2Bopaque%2Fwith%3Fsymbols%26x%3D1&limit=7",
            handler.RequestUri?.GetComponents(UriComponents.Query, UriFormat.UriEscaped));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetChangesSinceAsync("meet-1", "", 7));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetChangesSinceAsync("meet-1", checkpoint, 26));
    }

    [Fact]
    public async Task ClientEncodesExplicitDocumentIdsAndPreservesHttpFailures()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"error":"not_found"}""")
        });
        var client = new LiftingCastCompetitionClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        });

        var response = await client.GetDocumentAsync("meet-1", "attempt/1");

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("https://example.test/meet-1_readonly/attempt%2F1", handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task ClientRejectsCouchDbResponsesOverTheSizeLimit()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('x', 1_048_577))
        });
        var client = new LiftingCastCompetitionClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        });

        await Assert.ThrowsAsync<InvalidDataException>(
            () => client.GetSnapshotAsync("meet-1"));
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
