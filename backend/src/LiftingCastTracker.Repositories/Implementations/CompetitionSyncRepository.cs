using LiftingCastTracker.Repositories.Data;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LiftingCastTracker.Repositories.Implementations;

public sealed class CompetitionSyncRepository(ApplicationDbContext context)
    : ICompetitionSyncRepository
{
    public Task<CompetitionSyncState?> GetStateAsync(
        string competitionId,
        CancellationToken cancellationToken = default) =>
        context.CompetitionSyncStates
            .AsNoTracking()
            .FirstOrDefaultAsync(state => state.CompetitionId == competitionId, cancellationToken);

    public async Task<CompetitionSyncCounts> SaveSnapshotAsync(
        string competitionId,
        IReadOnlyCollection<CouchDbSyncDocument> documents,
        string lastSequence,
        DateTimeOffset syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var currentDocuments = await context.CompetitionDocuments
            .Where(document => document.CompetitionId == competitionId)
            .ToListAsync(cancellationToken);
        var currentById = currentDocuments.ToDictionary(document => document.DocumentId, StringComparer.Ordinal);
        var incomingById = documents.ToDictionary(document => document.Id, StringComparer.Ordinal);
        var inserted = 0;
        var updated = 0;
        var unchanged = 0;
        var deleted = 0;

        foreach (var incoming in incomingById.Values)
        {
            if (!currentById.TryGetValue(incoming.Id, out var existing))
            {
                context.CompetitionDocuments.Add(CreateEntity(competitionId, incoming, syncedAtUtc));
                inserted++;
                continue;
            }

            if (HasSameRevision(existing, incoming))
            {
                existing.LastSyncedAt = syncedAtUtc;
                unchanged++;
            }
            else
            {
                UpdateEntity(existing, incoming, syncedAtUtc);
                updated++;
            }
        }

        foreach (var existing in currentDocuments.Where(document => !incomingById.ContainsKey(document.DocumentId)))
        {
            context.CompetitionDocuments.Remove(existing);
            deleted++;
        }

        await SaveCheckpointAsync(competitionId, lastSequence, syncedAtUtc, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CompetitionSyncCounts(inserted, updated, unchanged, deleted);
    }

    public async Task<CompetitionSyncCounts> ApplyChangesAsync(
        string competitionId,
        IReadOnlyCollection<CouchDbSyncChange> changes,
        string lastSequence,
        DateTimeOffset syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var finalChanges = changes
            .GroupBy(change => change.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();
        var changedIds = finalChanges.Select(change => change.Id).ToArray();
        var currentDocuments = changedIds.Length == 0
            ? []
            : await context.CompetitionDocuments
                .Where(document => document.CompetitionId == competitionId
                    && changedIds.Contains(document.DocumentId))
                .ToListAsync(cancellationToken);
        var currentById = currentDocuments.ToDictionary(document => document.DocumentId, StringComparer.Ordinal);
        var inserted = 0;
        var updated = 0;
        var unchanged = 0;
        var deleted = 0;

        foreach (var change in finalChanges)
        {
            if (change.Deleted)
            {
                if (currentById.TryGetValue(change.Id, out var removedDocument))
                {
                    context.CompetitionDocuments.Remove(removedDocument);
                    deleted++;
                }

                continue;
            }

            var incoming = change.Document
                ?? throw new InvalidOperationException("A non-deleted CouchDB change must contain a document.");
            if (!currentById.TryGetValue(change.Id, out var existing))
            {
                context.CompetitionDocuments.Add(CreateEntity(competitionId, incoming, syncedAtUtc));
                inserted++;
            }
            else if (HasSameRevision(existing, incoming))
            {
                existing.LastSyncedAt = syncedAtUtc;
                unchanged++;
            }
            else
            {
                UpdateEntity(existing, incoming, syncedAtUtc);
                updated++;
            }
        }

        await SaveCheckpointAsync(competitionId, lastSequence, syncedAtUtc, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CompetitionSyncCounts(inserted, updated, unchanged, deleted);
    }

    public async Task<(int TotalDocuments, IReadOnlyList<CompetitionDocument> Documents)> GetDocumentsAsync(
        string competitionId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = context.CompetitionDocuments
            .AsNoTracking()
            .Where(document => document.CompetitionId == competitionId);
        var totalDocuments = await query.CountAsync(cancellationToken);
        var documents = await query
            .OrderBy(document => document.DocumentId)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (totalDocuments, documents);
    }

    public async Task<(CompetitionSyncState? State, IReadOnlyList<CompetitionDocument> Documents)> GetDetailsDataAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        var state = await context.CompetitionSyncStates
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.CompetitionId == competitionId, cancellationToken);
        var documents = await context.CompetitionDocuments
            .AsNoTracking()
            .Where(document => document.CompetitionId == competitionId)
            .OrderBy(document => document.DocumentId)
            .ToListAsync(cancellationToken);

        return (state, documents);
    }

    private async Task SaveCheckpointAsync(
        string competitionId,
        string lastSequence,
        DateTimeOffset syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var state = await context.CompetitionSyncStates
            .FirstOrDefaultAsync(item => item.CompetitionId == competitionId, cancellationToken);
        if (state is null)
        {
            context.CompetitionSyncStates.Add(new CompetitionSyncState
            {
                CompetitionId = competitionId,
                LastSequence = lastSequence,
                LastSyncedAt = syncedAtUtc
            });
        }
        else
        {
            state.LastSequence = lastSequence;
            state.LastSyncedAt = syncedAtUtc;
        }
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

    private static bool HasSameRevision(CompetitionDocument existing, CouchDbSyncDocument incoming) =>
        existing.Revision == incoming.Revision;

    private static void UpdateEntity(
        CompetitionDocument existing,
        CouchDbSyncDocument incoming,
        DateTimeOffset syncedAtUtc)
    {
        existing.Revision = incoming.Revision;
        existing.DocumentDataJson = incoming.DataJson;
        existing.LastSyncedAt = syncedAtUtc;
    }
}
