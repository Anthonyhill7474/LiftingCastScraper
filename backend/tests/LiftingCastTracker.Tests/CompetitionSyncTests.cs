using System.Net;
using System.Text;
using System.Text.Json;
using LiftingCastTracker.Repositories.ExternalData;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Repositories;
using LiftingCastTracker.Services.Sync;

namespace LiftingCastTracker.Tests;

public class CompetitionSyncTests
{
    [Fact]
    public async Task ImportsInitialSnapshotAndExcludesDesignAndPrivateFields()
    {
        var client = new FakeCouchDbClient
        {
            SnapshotResponse = new CouchDbHttpResponse(
                200,
                """
                {
                  "total_rows": 3,
                  "update_seq": "377-g1AAA",
                  "rows": [
                    {
                      "id": "attempt:1",
                      "doc": {
                        "_id": "attempt:1",
                        "_rev": "2-attempt",
                        "lifterId": "lifter:1",
                        "liftName": "Squat",
                        "attemptNumber": 1,
                        "weight": 100,
                        "result": "good",
                        "decisions": {
                          "head": { "decision": "good", "cards": { "blue": true } }
                        },
                        "changes": [{ "attribute": "name", "value": "private-history-value" }],
                        "contactEmail": "private@example.test"
                      }
                    },
                    {
                      "id": "_design/views",
                      "doc": { "_id": "_design/views", "_rev": "1-design" }
                    },
                    {
                      "id": "lifter:1",
                      "doc": {
                        "_id": "lifter:1",
                        "_rev": "1-lifter",
                        "name": "Lifter Name",
                        "bodyWeight": 82.5,
                        "birthDate": "private-birth-date",
                        "stripePaymentIntent": "private-payment-id",
                        "divisions": [
                          { "divisionId": "division:1", "declaredAwardsWeightClassId": "class:1" }
                        ]
                      }
                    }
                  ]
                }
                """)
        };
        var syncRepository = new InMemoryCompetitionSyncRepository();
        var service = CreateService(client, syncRepository);

        var result = await service.SyncAsync("meet-1");

        Assert.True(result.InitialSync);
        Assert.Equal(2, result.Inserted);
        Assert.Equal("377-g1AAA", result.LastSequence);
        Assert.Equal("377-g1AAA", syncRepository.State?.LastSequence);
        Assert.Equal(2, syncRepository.Documents.Count);
        Assert.DoesNotContain(syncRepository.Documents.Keys, id => id.StartsWith("_design/", StringComparison.Ordinal));

        var storedData = string.Join('\n', syncRepository.Documents.Values.Select(item => item.DocumentDataJson));
        Assert.DoesNotContain("birthDate", storedData);
        Assert.DoesNotContain("stripePaymentIntent", storedData);
        Assert.DoesNotContain("contactEmail", storedData);
        Assert.DoesNotContain("private-history-value", storedData);
        Assert.Contains("decisions", storedData);
        Assert.Contains("divisionId", storedData);
        Assert.Contains("bodyWeight", storedData);
    }

    [Fact]
    public async Task AppliesDocumentUpdatesAndDeletes()
    {
        var syncRepository = new InMemoryCompetitionSyncRepository();
        await SeedAsync(syncRepository, "meet-1", "opaque:1",
            Document("attempt:1", "1-first", """{"liftName":"Squat","weight":100}"""),
            Document("attempt:2", "1-second", """{"liftName":"Bench","weight":50}"""));
        var client = new FakeCouchDbClient();
        client.ChangesResponses.Enqueue(new CouchDbHttpResponse(
            200,
            """
            {
              "results": [
                {
                  "id": "attempt:1",
                  "changes": [{ "rev": "2-updated" }],
                  "doc": {
                    "_id": "attempt:1",
                    "_rev": "2-updated",
                    "liftName": "Squat",
                    "weight": 105
                  }
                },
                {
                  "id": "attempt:2",
                  "changes": [{ "rev": "2-deleted" }],
                  "deleted": true
                }
              ],
              "last_seq": "opaque:2",
              "pending": 0
            }
            """));
        var service = CreateService(client, syncRepository);

        var result = await service.SyncAsync("meet-1");

        Assert.False(result.InitialSync);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Deleted);
        Assert.Equal(0, result.Inserted);
        Assert.Equal("opaque:2", syncRepository.State?.LastSequence);
        Assert.Equal("2-updated", syncRepository.Documents["attempt:1"].Revision);
        Assert.DoesNotContain("attempt:2", syncRepository.Documents.Keys);
    }

    [Fact]
    public async Task RepeatedSyncIsIdempotentAndEmptyBatchAdvancesOpaqueCheckpoint()
    {
        var syncRepository = new InMemoryCompetitionSyncRepository();
        var client = new FakeCouchDbClient();
        client.ChangesResponses.Enqueue(new CouchDbHttpResponse(200, """
            {
              "results": [
                {
                  "id": "attempt:1",
                  "changes": [{ "rev": "1-same" }],
                  "doc": { "_id": "attempt:1", "_rev": "1-same", "weight": 100 }
                }
              ],
              "last_seq": "opaque:2",
              "pending": 0
            }
            """));
        client.ChangesResponses.Enqueue(new CouchDbHttpResponse(
            200,
            """{"results":[],"last_seq":"opaque:2","pending":0}"""));
        await SeedAsync(
            syncRepository,
            "meet-1",
            "opaque:1",
            Document("attempt:1", "1-same", """{"weight":100}"""));
        var service = CreateService(client, syncRepository);

        var first = await service.SyncAsync("meet-1");
        var second = await service.SyncAsync("meet-1");

        Assert.Equal(1, first.Unchanged);
        Assert.Equal(0, second.Inserted + second.Updated + second.Deleted);
        Assert.Single(syncRepository.Documents);
        Assert.Equal("opaque:2", syncRepository.State?.LastSequence);
        Assert.Equal(new[] { "opaque:1", "opaque:2" }, client.RequestedCheckpoints);
    }

    [Fact]
    public async Task ProcessesMultipleBoundedChangeBatchesWithoutSkipping()
    {
        var syncRepository = new InMemoryCompetitionSyncRepository();
        await SeedAsync(syncRepository, "meet-1", "opaque:0");
        var firstPage = Enumerable.Range(1, CompetitionSyncService.BatchSize)
            .Select(index => $$"""
                {
                  "id": "attempt:{{index}}",
                  "changes": [{ "rev": "1-{{index}}" }],
                  "doc": { "_id": "attempt:{{index}}", "_rev": "1-{{index}}", "weight": {{index}} }
                }
                """)
            .ToArray();
        var client = new FakeCouchDbClient();
        client.ChangesResponses.Enqueue(new CouchDbHttpResponse(
            200,
            CreateChangesResponse("opaque:1", 1, firstPage)));
        client.ChangesResponses.Enqueue(new CouchDbHttpResponse(
            200,
            CreateChangesResponse(
                "opaque:2",
                0,
                """
                {
                  "id": "attempt:26",
                  "changes": [{ "rev": "1-26" }],
                  "doc": { "_id": "attempt:26", "_rev": "1-26", "weight": 26 }
                }
                """)));
        var service = CreateService(client, syncRepository);

        var result = await service.SyncAsync("meet-1");

        Assert.Equal(2, result.Batches);
        Assert.Equal(26, result.Inserted);
        Assert.False(result.HasMore);
        Assert.Equal("opaque:2", result.LastSequence);
        Assert.Equal(new[] { "opaque:0", "opaque:1" }, client.RequestedCheckpoints);
        Assert.All(client.RequestedLimits, limit => Assert.Equal(CompetitionSyncService.BatchSize, limit));
        Assert.Equal(26, syncRepository.Documents.Count);
    }

    [Fact]
    public async Task StopsAfterMaximumBatchesAndLeavesCheckpointForNextManualRun()
    {
        var syncRepository = new InMemoryCompetitionSyncRepository();
        await SeedAsync(syncRepository, "meet-1", "opaque:0");
        var client = new FakeCouchDbClient();
        for (var batch = 1; batch <= CompetitionSyncService.MaximumBatchesPerRun; batch++)
        {
            client.ChangesResponses.Enqueue(new CouchDbHttpResponse(
                200,
                CreateChangesResponse(
                    $"opaque:{batch}",
                    1,
                    $$"""
                    {
                      "id": "attempt:{{batch}}",
                      "changes": [{ "rev": "1-{{batch}}" }],
                      "doc": { "_id": "attempt:{{batch}}", "_rev": "1-{{batch}}", "weight": {{batch}} }
                    }
                    """)));
        }

        var result = await CreateService(client, syncRepository).SyncAsync("meet-1");

        Assert.Equal(CompetitionSyncService.MaximumBatchesPerRun, result.Batches);
        Assert.True(result.HasMore);
        Assert.Equal($"opaque:{CompetitionSyncService.MaximumBatchesPerRun}", result.LastSequence);
        Assert.Equal(result.LastSequence, syncRepository.State?.LastSequence);
    }

    [Fact]
    public async Task DoesNotAdvanceCheckpointWhenProcessingOrPersistenceFails()
    {
        var syncRepository = new InMemoryCompetitionSyncRepository();
        await SeedAsync(syncRepository, "meet-1", "opaque:before",
            Document("attempt:1", "1-first", """{"weight":100}"""));
        var client = new FakeCouchDbClient();
        client.ChangesResponses.Enqueue(new CouchDbHttpResponse(
            200,
            """
            {
              "results": [
                {
                  "id": "attempt:1",
                  "changes": [{ "rev": "2-next" }],
                  "doc": { "_id": "attempt:1", "_rev": "2-next", "weight": 105 }
                }
              ],
              "last_seq": "opaque:after",
              "pending": 0
            }
            """));
        syncRepository.FailNextCommit = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(client, syncRepository).SyncAsync("meet-1"));

        Assert.Equal("opaque:before", syncRepository.State?.LastSequence);
        Assert.Equal("1-first", syncRepository.Documents["attempt:1"].Revision);
    }

    [Fact]
    public async Task RejectsMalformedOrUnsuccessfulResponsesBeforePersistence()
    {
        var syncRepository = new InMemoryCompetitionSyncRepository();
        var client = new FakeCouchDbClient
        {
            SnapshotResponse = new CouchDbHttpResponse(200, """{"total_rows":0,"rows":[]}""")
        };

        await Assert.ThrowsAsync<JsonException>(
            () => CreateService(client, syncRepository).SyncAsync("meet-1"));
        Assert.Null(syncRepository.State);
        Assert.Empty(syncRepository.Documents);

        client.SnapshotResponse = new CouchDbHttpResponse(503, """{"error":"unavailable"}""");
        await Assert.ThrowsAsync<CouchDbSyncException>(
            () => CreateService(client, syncRepository).SyncAsync("meet-1"));
        Assert.Null(syncRepository.State);
    }

    [Fact]
    public async Task RejectsSyncForCompetitionNotInLocalDirectory()
    {
        var service = CreateService(new FakeCouchDbClient(), new InMemoryCompetitionSyncRepository());

        await Assert.ThrowsAsync<CompetitionNotFoundException>(() => service.SyncAsync("unknown"));
    }

    [Fact]
    public async Task PreventsOverlappingSyncsForTheSameCompetition()
    {
        var client = new FakeCouchDbClient();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.SnapshotResponseProvider = async cancellationToken =>
        {
            started.SetResult();
            await continueRequest.Task.WaitAsync(cancellationToken);
            return new CouchDbHttpResponse(
                200,
                """{"total_rows":0,"update_seq":"1","rows":[]}""");
        };
        var service = CreateService(client, new InMemoryCompetitionSyncRepository(), "lock-test");

        var running = service.SyncAsync("lock-test");
        await started.Task;
        await Assert.ThrowsAsync<CompetitionSyncAlreadyRunningException>(
            () => service.SyncAsync("lock-test"));
        continueRequest.SetResult();
        await running;
    }

    [Fact]
    public async Task SyncClientUsesFullSnapshotAndPreservesEncodedCheckpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"total_rows":0,"update_seq":"1","rows":[]}""", Encoding.UTF8, "application/json")
        });
        var client = new LiftingCastCompetitionClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        });

        await client.GetSyncSnapshotAsync("meet-1");
        Assert.Equal(
            "https://example.test/meet-1_readonly/_all_docs?update_seq=true&include_docs=true",
            handler.RequestUri?.AbsoluteUri);

        await client.GetSyncChangesAsync("meet-1", "377-g1AAA+opaque/1", 25);
        Assert.Equal(
            "feed=normal&since=377-g1AAA%2Bopaque%2F1&include_docs=true&limit=25",
            handler.RequestUri?.GetComponents(UriComponents.Query, UriFormat.UriEscaped));
    }

    private static CompetitionSyncService CreateService(
        FakeCouchDbClient client,
        InMemoryCompetitionSyncRepository syncRepository,
        string competitionId = "meet-1") =>
        new(
            new InMemoryCompetitionRepository(competitionId),
            syncRepository,
            client);

    private static async Task SeedAsync(
        InMemoryCompetitionSyncRepository repository,
        string competitionId,
        string sequence,
        params CouchDbSyncDocument[] documents) =>
        await repository.SaveSnapshotAsync(
            competitionId,
            documents,
            sequence,
            DateTimeOffset.UtcNow);

    private static CouchDbSyncDocument Document(string id, string revision, string dataJson) =>
        new(id, revision, dataJson);

    private static string CreateChangesResponse(string lastSequence, long pending, params string[] results)
    {
        using var parsedResults = JsonDocument.Parse($"[{string.Join(',', results)}]");
        return JsonSerializer.Serialize(new
        {
            results = parsedResults.RootElement,
            last_seq = lastSequence,
            pending
        });
    }

    private sealed class InMemoryCompetitionRepository(string competitionId) : ICompetitionRepository
    {
        public Task<List<Competition>> GetAllAsync() =>
            Task.FromResult(new List<Competition> { new() { Id = competitionId, Name = "Test" } });

        public Task<Competition?> GetByIdAsync(string id) =>
            Task.FromResult(id == competitionId
                ? new Competition { Id = id, Name = "Test" }
                : null);

        public Task<List<Competition>> GetByIdsAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ids.Contains(competitionId)
                ? new List<Competition> { new() { Id = competitionId, Name = "Test" } }
                : []);

        public Task SaveImportedAsync(
            IReadOnlyCollection<Competition> inserted,
            IReadOnlyCollection<Competition> updated,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class InMemoryCompetitionSyncRepository : ICompetitionSyncRepository
    {
        public Dictionary<string, CompetitionDocument> Documents { get; } = new(StringComparer.Ordinal);
        public CompetitionSyncState? State { get; private set; }
        public bool FailNextCommit { get; set; }

        public Task<CompetitionSyncState?> GetStateAsync(
            string competitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(State?.CompetitionId == competitionId ? State : null);

        public Task<CompetitionSyncCounts> SaveSnapshotAsync(
            string competitionId,
            IReadOnlyCollection<CouchDbSyncDocument> documents,
            string lastSequence,
            DateTimeOffset syncedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var incoming = documents.ToDictionary(document => document.Id, StringComparer.Ordinal);
            var nextDocuments = new Dictionary<string, CompetitionDocument>(StringComparer.Ordinal);
            var inserted = 0;
            var updated = 0;
            var unchanged = 0;
            var deleted = 0;

            foreach (var document in incoming.Values)
            {
                if (Documents.TryGetValue(document.Id, out var current))
                {
                    if (current.Revision == document.Revision)
                    {
                        unchanged++;
                        current.LastSyncedAt = syncedAtUtc;
                        nextDocuments.Add(document.Id, current);
                    }
                    else
                    {
                        updated++;
                        nextDocuments.Add(document.Id, CreateEntity(competitionId, document, syncedAtUtc));
                    }
                }
                else
                {
                    inserted++;
                    nextDocuments.Add(document.Id, CreateEntity(competitionId, document, syncedAtUtc));
                }
            }

            deleted = Documents.Keys.Count(id => !incoming.ContainsKey(id));
            Commit(competitionId, nextDocuments, lastSequence, syncedAtUtc);
            return Task.FromResult(new CompetitionSyncCounts(inserted, updated, unchanged, deleted));
        }

        public Task<CompetitionSyncCounts> ApplyChangesAsync(
            string competitionId,
            IReadOnlyCollection<CouchDbSyncChange> changes,
            string lastSequence,
            DateTimeOffset syncedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var nextDocuments = new Dictionary<string, CompetitionDocument>(Documents, StringComparer.Ordinal);
            var inserted = 0;
            var updated = 0;
            var unchanged = 0;
            var deleted = 0;

            foreach (var change in changes.GroupBy(item => item.Id, StringComparer.Ordinal).Select(group => group.Last()))
            {
                if (change.Deleted)
                {
                    if (nextDocuments.Remove(change.Id))
                    {
                        deleted++;
                    }

                    continue;
                }

                var incoming = change.Document!;
                if (nextDocuments.TryGetValue(change.Id, out var current))
                {
                    if (current.Revision == incoming.Revision)
                    {
                        unchanged++;
                        current.LastSyncedAt = syncedAtUtc;
                    }
                    else
                    {
                        updated++;
                        nextDocuments[change.Id] = CreateEntity(competitionId, incoming, syncedAtUtc);
                    }
                }
                else
                {
                    inserted++;
                    nextDocuments.Add(change.Id, CreateEntity(competitionId, incoming, syncedAtUtc));
                }
            }

            Commit(competitionId, nextDocuments, lastSequence, syncedAtUtc);
            return Task.FromResult(new CompetitionSyncCounts(inserted, updated, unchanged, deleted));
        }

        public Task<(int TotalDocuments, IReadOnlyList<CompetitionDocument> Documents)> GetDocumentsAsync(
            string competitionId,
            int offset,
            int limit,
            CancellationToken cancellationToken = default)
        {
            var documents = Documents.Values
                .Where(document => document.CompetitionId == competitionId)
                .OrderBy(document => document.DocumentId, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult((documents.Length, (IReadOnlyList<CompetitionDocument>)documents.Skip(offset).Take(limit).ToArray()));
        }

        public Task<(CompetitionSyncState? State, IReadOnlyList<CompetitionDocument> Documents)> GetDetailsDataAsync(
            string competitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((
                State?.CompetitionId == competitionId ? State : null,
                (IReadOnlyList<CompetitionDocument>)Documents.Values
                    .Where(document => document.CompetitionId == competitionId)
                    .ToArray()));

        private void Commit(
            string competitionId,
            Dictionary<string, CompetitionDocument> nextDocuments,
            string lastSequence,
            DateTimeOffset syncedAtUtc)
        {
            if (FailNextCommit)
            {
                FailNextCommit = false;
                throw new InvalidOperationException("Simulated atomic persistence failure.");
            }

            Documents.Clear();
            foreach (var (id, document) in nextDocuments)
            {
                Documents.Add(id, document);
            }

            State = new CompetitionSyncState
            {
                CompetitionId = competitionId,
                LastSequence = lastSequence,
                LastSyncedAt = syncedAtUtc
            };
        }

        private static CompetitionDocument CreateEntity(
            string competitionId,
            CouchDbSyncDocument document,
            DateTimeOffset syncedAtUtc) =>
            new()
            {
                CompetitionId = competitionId,
                DocumentId = document.Id,
                Revision = document.Revision,
                DocumentDataJson = document.DataJson,
                LastSyncedAt = syncedAtUtc
            };
    }

    private sealed class FakeCouchDbClient : ILiftingCastCompetitionClient
    {
        public CouchDbHttpResponse SnapshotResponse { get; set; } =
            new(200, """{"total_rows":0,"update_seq":"1","rows":[]}""");
        public Func<CancellationToken, Task<CouchDbHttpResponse>>? SnapshotResponseProvider { get; set; }
        public Queue<CouchDbHttpResponse> ChangesResponses { get; } = new();
        public List<string> RequestedCheckpoints { get; } = [];
        public List<int> RequestedLimits { get; } = [];

        public Task<CouchDbHttpResponse> GetSnapshotAsync(
            string competitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SnapshotResponse);

        public Task<CouchDbHttpResponse> GetSyncSnapshotAsync(
            string competitionId,
            CancellationToken cancellationToken = default) =>
            SnapshotResponseProvider?.Invoke(cancellationToken) ?? Task.FromResult(SnapshotResponse);

        public Task<CouchDbHttpResponse> GetDocumentAsync(
            string competitionId,
            string documentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CouchDbHttpResponse(404, "{}"));

        public Task<CouchDbHttpResponse> CheckChangesAsync(
            string competitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CouchDbHttpResponse(200, """{"results":[],"last_seq":"1"}"""));

        public Task<CouchDbHttpResponse> GetChangesSinceAsync(
            string competitionId,
            string checkpoint,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CouchDbHttpResponse(200, """{"results":[],"last_seq":"1"}"""));

        public Task<CouchDbHttpResponse> GetSyncChangesAsync(
            string competitionId,
            string checkpoint,
            int limit,
            CancellationToken cancellationToken = default)
        {
            RequestedCheckpoints.Add(checkpoint);
            RequestedLimits.Add(limit);
            if (ChangesResponses.Count == 0)
            {
                throw new InvalidOperationException("No changes response was configured for this test.");
            }

            return Task.FromResult(ChangesResponses.Dequeue());
        }
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
