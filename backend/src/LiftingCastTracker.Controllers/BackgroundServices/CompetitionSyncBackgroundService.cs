using System.Collections.Concurrent;
using System.Text.Json;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Repositories;
using LiftingCastTracker.Services.Sync;
using Microsoft.Extensions.Options;

namespace LiftingCastTracker.Controllers.BackgroundServices;

public sealed class AutomaticSyncOptions
{
    public const string SectionName = "AutomaticSync";
    public const int MaximumConcurrentCompetitions = 5;
    public const int MaximumActiveCompetitionsPerRun = 50;

    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 30;
    public int MaxConcurrentCompetitions { get; set; } = 2;
    public int MaxActiveCompetitionsPerRun { get; set; } = 5;
}

public sealed record AutomaticSyncWorkerStatus(
    bool Enabled,
    int IntervalSeconds,
    int MaxConcurrentCompetitions,
    int MaxActiveCompetitionsPerRun,
    IReadOnlyList<AutomaticSyncCompetitionStatus> Competitions);

public sealed record AutomaticSyncCompetitionStatus(
    string CompetitionId,
    string CompetitionName,
    DateOnly? CompetitionDate,
    bool DateEligible,
    DateTimeOffset? LastSuccessfulSyncAtUtc,
    string? LastFailureStatus,
    DateTimeOffset? LastFailureAtUtc,
    int ConsecutiveFailures,
    DateTimeOffset? NextEligibleSyncAtUtc,
    bool Paused);

public sealed class CompetitionSyncBackgroundService : BackgroundService
{
    private static readonly TimeSpan MaximumBackoff = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AutomaticSyncOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CompetitionSyncBackgroundService> _logger;
    private readonly ConcurrentDictionary<string, CompetitionRuntimeState> _runtimeStates =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private long _nextRunAtUnixMilliseconds = -1;
    private int _selectionCursor;

    public CompetitionSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AutomaticSyncOptions> options,
        TimeProvider timeProvider,
        ILogger<CompetitionSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AutomaticSyncWorkerStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var candidates = await GetCandidatesAsync(cancellationToken);
        return CreateStatus(candidates, _timeProvider.GetUtcNow());
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !await _runLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var candidates = await GetCandidatesAsync(cancellationToken);
            RemoveOptedOutState(candidates);
            var now = _timeProvider.GetUtcNow();
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var eligibleCandidates = candidates
                .Where(candidate => IsDateEligible(candidate.CompetitionDate, today))
                .OrderBy(candidate => candidate.CompetitionId, StringComparer.Ordinal)
                .Where(candidate => IsDue(candidate.CompetitionId, now))
                .ToArray();
            var selected = SelectCandidates(eligibleCandidates, _options.MaxActiveCompetitionsPerRun);

            await Parallel.ForEachAsync(
                selected,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = _options.MaxConcurrentCompetitions,
                    CancellationToken = cancellationToken
                },
                async (candidate, token) => await SynchronizeCandidateAsync(candidate, token));
        }
        finally
        {
            Interlocked.Exchange(
                ref _nextRunAtUnixMilliseconds,
                (_timeProvider.GetUtcNow() + TimeSpan.FromSeconds(_options.IntervalSeconds))
                    .ToUnixTimeMilliseconds());
            _runLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_options.Enabled)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        "Automatic synchronization scan failed ({FailureType}).",
                        GetFailureStatus(exception));
                    Interlocked.Exchange(
                        ref _nextRunAtUnixMilliseconds,
                        (_timeProvider.GetUtcNow() + TimeSpan.FromSeconds(_options.IntervalSeconds))
                            .ToUnixTimeMilliseconds());
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<IReadOnlyList<AutomaticSyncCandidate>> GetCandidatesAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICompetitionSyncRepository>();
        return await repository.GetAutomaticSyncCandidatesAsync(cancellationToken);
    }

    private async Task SynchronizeCandidateAsync(
        AutomaticSyncCandidate candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var syncService = scope.ServiceProvider.GetRequiredService<CompetitionSyncService>();
            await syncService.SyncAsync(candidate.CompetitionId, cancellationToken);
            _runtimeStates.TryRemove(candidate.CompetitionId, out _);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CompetitionSyncAlreadyRunningException)
        {
            _runtimeStates[candidate.CompetitionId] = new CompetitionRuntimeState(
                0,
                _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(_options.IntervalSeconds),
                false,
                "Synchronization already in progress",
                _timeProvider.GetUtcNow());
        }
        catch (Exception exception)
        {
            RecordFailure(candidate.CompetitionId, exception);
        }
    }

    private void RecordFailure(string competitionId, Exception exception)
    {
        var failedAt = _timeProvider.GetUtcNow();
        var paused = exception is InvalidDataException;
        var failureStatus = GetFailureStatus(exception);

        _runtimeStates.AddOrUpdate(
            competitionId,
            _ => CreateFailureState(1),
            (_, previous) => CreateFailureState(Math.Min(previous.ConsecutiveFailures + 1, 20)));

        _logger.LogWarning(
            "Automatic synchronization failed for competition {CompetitionId} ({FailureStatus}).",
            competitionId,
            failureStatus);

        CompetitionRuntimeState CreateFailureState(int failureCount)
        {
            if (paused)
            {
                return new CompetitionRuntimeState(
                    failureCount,
                    null,
                    true,
                    failureStatus,
                    failedAt);
            }

            var exponent = Math.Min(failureCount - 1, 10);
            var backoffSeconds = Math.Min(
                _options.IntervalSeconds * Math.Pow(2, exponent),
                MaximumBackoff.TotalSeconds);
            var retryAt = failedAt + TimeSpan.FromSeconds(backoffSeconds);
            if (exception is CouchDbSyncException { StatusCode: 429, RetryAfterUtc: { } retryAfter }
                && retryAfter > retryAt)
            {
                retryAt = retryAfter;
            }

            return new CompetitionRuntimeState(
                failureCount,
                retryAt,
                false,
                failureStatus,
                failedAt);
        }
    }

    private AutomaticSyncWorkerStatus CreateStatus(
        IReadOnlyList<AutomaticSyncCandidate> candidates,
        DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var nextRunAt = Interlocked.Read(ref _nextRunAtUnixMilliseconds);
        var scheduledRun = nextRunAt < 0 ? now : DateTimeOffset.FromUnixTimeMilliseconds(nextRunAt);
        var statuses = candidates
            .OrderBy(candidate => candidate.CompetitionId, StringComparer.Ordinal)
            .Select(candidate =>
            {
                var dateEligible = IsDateEligible(candidate.CompetitionDate, today);
                _runtimeStates.TryGetValue(candidate.CompetitionId, out var runtime);
                DateTimeOffset? nextEligible = null;
                if (!_options.Enabled)
                {
                    nextEligible = null;
                }
                else if (!dateEligible)
                {
                    nextEligible = GetNextDateEligibleAt(candidate.CompetitionDate, today);
                }
                else if (runtime?.Paused != true)
                {
                    nextEligible = runtime?.RetryAtUtc is { } retryAt && retryAt > scheduledRun
                        ? retryAt
                        : scheduledRun;
                }

                return new AutomaticSyncCompetitionStatus(
                    candidate.CompetitionId,
                    candidate.CompetitionName,
                    candidate.CompetitionDate,
                    dateEligible,
                    candidate.LastSyncedAtUtc,
                    runtime?.LastFailureStatus,
                    runtime?.LastFailureAtUtc,
                    runtime?.ConsecutiveFailures ?? 0,
                    nextEligible,
                    runtime?.Paused ?? false);
            })
            .ToArray();

        return new AutomaticSyncWorkerStatus(
            _options.Enabled,
            _options.IntervalSeconds,
            _options.MaxConcurrentCompetitions,
            _options.MaxActiveCompetitionsPerRun,
            statuses);
    }

    private AutomaticSyncCandidate[] SelectCandidates(
        AutomaticSyncCandidate[] candidates,
        int maximumCount)
    {
        if (candidates.Length <= maximumCount)
        {
            _selectionCursor = 0;
            return candidates;
        }

        var start = _selectionCursor % candidates.Length;
        var selected = Enumerable.Range(0, maximumCount)
            .Select(offset => candidates[(start + offset) % candidates.Length])
            .ToArray();
        _selectionCursor = (start + selected.Length) % candidates.Length;
        return selected;
    }

    private bool IsDue(string competitionId, DateTimeOffset now) =>
        !_runtimeStates.TryGetValue(competitionId, out var state)
        || (!state.Paused && (state.RetryAtUtc is null || state.RetryAtUtc <= now));

    private void RemoveOptedOutState(IReadOnlyList<AutomaticSyncCandidate> candidates)
    {
        var optedInIds = candidates
            .Select(candidate => candidate.CompetitionId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var competitionId in _runtimeStates.Keys)
        {
            if (!optedInIds.Contains(competitionId))
            {
                _runtimeStates.TryRemove(competitionId, out _);
            }
        }
    }

    private static bool IsDateEligible(DateOnly? competitionDate, DateOnly today)
    {
        if (competitionDate is null)
        {
            return false;
        }

        return competitionDate.Value >= today.AddDays(-1)
            && competitionDate.Value <= today.AddDays(1);
    }

    private static DateTimeOffset? GetNextDateEligibleAt(DateOnly? competitionDate, DateOnly today)
    {
        if (competitionDate is null || competitionDate.Value < today.AddDays(-1))
        {
            return null;
        }

        if (competitionDate.Value > today.AddDays(1))
        {
            return competitionDate.Value.AddDays(-1)
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        }

        return null;
    }

    private static string GetFailureStatus(Exception exception) =>
        exception switch
        {
            CouchDbSyncException syncException => $"CouchDB HTTP {syncException.StatusCode}",
            InvalidDataException => "CouchDB response exceeded the synchronization size limit",
            JsonException => "CouchDB returned an invalid synchronization response",
            HttpRequestException => "CouchDB request failed",
            _ => "Synchronization failed"
        };

    private sealed record CompetitionRuntimeState(
        int ConsecutiveFailures,
        DateTimeOffset? RetryAtUtc,
        bool Paused,
        string LastFailureStatus,
        DateTimeOffset LastFailureAtUtc);
}
