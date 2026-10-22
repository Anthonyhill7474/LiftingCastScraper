using System.Collections.Concurrent;
using System.Text.Json;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Parsing;
using LiftingCastTracker.Services.Repositories;

namespace LiftingCastTracker.Services.Sync;

public sealed class CompetitionSyncService
{
    public const int BatchSize = CouchDbChangesRequestValidator.MaximumLimit;
    public const int MaximumBatchesPerRun = 20;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SyncLocks = new(StringComparer.Ordinal);

    private readonly ICompetitionRepository _competitionRepository;
    private readonly ICompetitionSyncRepository _syncRepository;
    private readonly ILiftingCastCompetitionClient _couchDbClient;

    public CompetitionSyncService(
        ICompetitionRepository competitionRepository,
        ICompetitionSyncRepository syncRepository,
        ILiftingCastCompetitionClient couchDbClient)
    {
        _competitionRepository = competitionRepository;
        _syncRepository = syncRepository;
        _couchDbClient = couchDbClient;
    }

    public async Task<CompetitionSyncResult> SyncAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        if (!CompetitionIdValidator.IsValid(competitionId))
        {
            throw new ArgumentException("The competition ID is invalid.", nameof(competitionId));
        }

        var syncLock = SyncLocks.GetOrAdd(competitionId, static _ => new SemaphoreSlim(1, 1));
        if (!syncLock.Wait(0))
        {
            throw new CompetitionSyncAlreadyRunningException(competitionId);
        }

        try
        {
            if (await _competitionRepository.GetByIdAsync(competitionId) is null)
            {
                throw new CompetitionNotFoundException(competitionId);
            }

            var syncState = await _syncRepository.GetStateAsync(competitionId, cancellationToken);
            return syncState is null
                ? await SynchronizeSnapshotAsync(competitionId, cancellationToken)
                : await SynchronizeChangesAsync(competitionId, syncState.LastSequence, cancellationToken);
        }
        finally
        {
            syncLock.Release();
        }
    }

    private async Task<CompetitionSyncResult> SynchronizeSnapshotAsync(
        string competitionId,
        CancellationToken cancellationToken)
    {
        var response = await _couchDbClient.GetSyncSnapshotAsync(competitionId, cancellationToken);
        EnsureSuccessfulResponse(response);

        var snapshot = CouchDbSyncResponseParser.ParseSnapshot(response.Body);
        var syncedAtUtc = DateTimeOffset.UtcNow;
        var counts = await _syncRepository.SaveSnapshotAsync(
            competitionId,
            snapshot.Documents,
            snapshot.LastSequence,
            syncedAtUtc,
            cancellationToken);

        return new CompetitionSyncResult(
            competitionId,
            true,
            counts.Inserted,
            counts.Updated,
            counts.Unchanged,
            counts.Deleted,
            1,
            snapshot.LastSequence,
            false,
            syncedAtUtc);
    }

    private async Task<CompetitionSyncResult> SynchronizeChangesAsync(
        string competitionId,
        string checkpoint,
        CancellationToken cancellationToken)
    {
        var counts = new CompetitionSyncCounts(0, 0, 0, 0);
        var batchCount = 0;
        var hasMore = false;
        var syncedAtUtc = DateTimeOffset.UtcNow;

        for (var batchNumber = 0; batchNumber < MaximumBatchesPerRun; batchNumber++)
        {
            var response = await _couchDbClient.GetSyncChangesAsync(
                competitionId,
                checkpoint,
                BatchSize,
                cancellationToken);
            EnsureSuccessfulResponse(response);

            var batch = CouchDbSyncResponseParser.ParseChanges(response.Body, BatchSize);
            if (batch.LastSequence == checkpoint
                && (batch.ResultCount > 0 || batch.Pending is > 0))
            {
                throw new JsonException("The CouchDB changes response did not advance its checkpoint.");
            }

            syncedAtUtc = DateTimeOffset.UtcNow;
            var batchCounts = await _syncRepository.ApplyChangesAsync(
                competitionId,
                batch.Changes,
                batch.LastSequence,
                syncedAtUtc,
                cancellationToken);

            counts += batchCounts;
            batchCount++;
            checkpoint = batch.LastSequence;

            var shouldContinue = batch.Pending is > 0
                || (batch.Pending is null && batch.ResultCount == BatchSize);
            if (!shouldContinue)
            {
                hasMore = false;
                break;
            }

            hasMore = batchNumber == MaximumBatchesPerRun - 1;
        }

        return new CompetitionSyncResult(
            competitionId,
            false,
            counts.Inserted,
            counts.Updated,
            counts.Unchanged,
            counts.Deleted,
            batchCount,
            checkpoint,
            hasMore,
            syncedAtUtc);
    }

    private static void EnsureSuccessfulResponse(CouchDbHttpResponse response)
    {
        if (response.StatusCode is < 200 or >= 300)
        {
            throw new CouchDbSyncException(response.StatusCode);
        }
    }
}

public sealed class CompetitionNotFoundException(string competitionId)
    : Exception($"Competition '{competitionId}' was not found in the local directory.");

public sealed class CompetitionSyncAlreadyRunningException(string competitionId)
    : Exception($"A synchronization for competition '{competitionId}' is already running.");

public sealed class CouchDbSyncException(int statusCode)
    : Exception($"CouchDB returned HTTP {statusCode}.")
{
    public int StatusCode { get; } = statusCode;
}
