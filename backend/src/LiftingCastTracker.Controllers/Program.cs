using LiftingCastTracker.Repositories.Data;
using LiftingCastTracker.Repositories.ExternalData;
using LiftingCastTracker.Repositories.Implementations;
using LiftingCastTracker.Controllers.BackgroundServices;
using LiftingCastTracker.Services.Import;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Parsing;
using LiftingCastTracker.Services.Repositories;
using LiftingCastTracker.Services.Sync;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<ICompetitionRepository, CompetitionRepository>();
builder.Services.AddScoped<ICompetitionSyncRepository, CompetitionSyncRepository>();
builder.Services.AddScoped<CompetitionImportService>();
builder.Services.AddScoped<CompetitionSyncService>();
builder.Services.AddOptions<AutomaticSyncOptions>()
    .Bind(builder.Configuration.GetSection(AutomaticSyncOptions.SectionName))
    .Validate(options => options.IntervalSeconds >= 30, "AutomaticSync:IntervalSeconds must be at least 30.")
    .Validate(
        options => options.MaxConcurrentCompetitions is >= 1 and <= AutomaticSyncOptions.MaximumConcurrentCompetitions,
        $"AutomaticSync:MaxConcurrentCompetitions must be between 1 and {AutomaticSyncOptions.MaximumConcurrentCompetitions}.")
    .Validate(
        options => options.MaxActiveCompetitionsPerRun is >= 1 and <= AutomaticSyncOptions.MaximumActiveCompetitionsPerRun,
        $"AutomaticSync:MaxActiveCompetitionsPerRun must be between 1 and {AutomaticSyncOptions.MaximumActiveCompetitionsPerRun}.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CompetitionSyncBackgroundService>();
builder.Services.AddHostedService(
    services => services.GetRequiredService<CompetitionSyncBackgroundService>());
var couchDbBaseUrl = builder.Configuration["LiftingCast:CouchDbBaseUrl"]
    ?? "https://couchdb.liftingcast.com/";
if (!Uri.TryCreate(couchDbBaseUrl, UriKind.Absolute, out var couchDbBaseUri)
    || couchDbBaseUri.Scheme is not ("http" or "https"))
{
    throw new InvalidOperationException(
        "Configuration 'LiftingCast:CouchDbBaseUrl' must be an absolute HTTP or HTTPS URL.");
}

builder.Services.AddHttpClient<ILiftingCastCompetitionClient, LiftingCastCompetitionClient>(client =>
{
    client.BaseAddress = new Uri(couchDbBaseUri.AbsoluteUri.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("LocalVite", policy =>
        {
            policy
                .WithOrigins("http://localhost:5173", "http://localhost:5174")
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("LocalVite");

    app.MapGet("/api/development/status", () => Results.Ok(new
    {
        environment = "Development",
        isDevelopment = true
    }))
        .WithName("GetDevelopmentStatus");

    app.MapGet(
        "/api/development/automatic-sync/status",
        async (
            CompetitionSyncBackgroundService syncWorker,
            CancellationToken cancellationToken) =>
                Results.Ok(await syncWorker.GetStatusAsync(cancellationToken)))
        .WithName("GetAutomaticSyncStatus");

    app.MapPost(
        "/api/development/competitions/import",
        async (
            IConfiguration configuration,
            IWebHostEnvironment environment,
            CompetitionImportService importService,
            CancellationToken cancellationToken) =>
        {
            var fixturePath = configuration["CompetitionImport:FixturePath"] ?? "competition-directory.json";
            var fullFixturePath = Path.GetFullPath(fixturePath, environment.ContentRootPath);
            if (!File.Exists(fullFixturePath))
            {
                return Results.NotFound(new { error = $"Competition directory JSON not found: {fullFixturePath}" });
            }

            var json = await File.ReadAllTextAsync(fullFixturePath, cancellationToken);
            try
            {
                var summary = await importService.ImportAsync(json, cancellationToken);
                return Results.Ok(summary);
            }
            catch (JsonException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
        .WithName("ImportCompetitions");

    app.MapGet(
        "/api/development/competitions/{id}/snapshot",
        async (
            string id,
            string? documentId,
            ILiftingCastCompetitionClient couchDbClient,
            CancellationToken cancellationToken) =>
        {
            if (!CompetitionIdValidator.IsValid(id))
            {
                return Results.BadRequest(new { error = "Invalid competition ID." });
            }

            if (documentId is not null && !CouchDbDocumentIdValidator.IsValid(documentId))
            {
                return Results.BadRequest(new { error = "Invalid CouchDB document ID." });
            }

            try
            {
                if (documentId is not null)
                {
                    var documentResponse = await couchDbClient.GetDocumentAsync(
                        id,
                        documentId,
                        cancellationToken);
                    if (documentResponse.StatusCode is < 200 or >= 300)
                    {
                        return Results.Problem(
                            detail: $"CouchDB returned HTTP {documentResponse.StatusCode}.",
                            statusCode: StatusCodes.Status502BadGateway);
                    }

                    var preview = CouchDbResponseParser.ParseDocumentPreview(
                        id,
                        documentId,
                        documentResponse.Body,
                        DateTimeOffset.UtcNow);
                    return Results.Ok(preview);
                }

                var response = await couchDbClient.GetSnapshotAsync(id, cancellationToken);
                if (response.StatusCode is < 200 or >= 300)
                {
                    return Results.Problem(
                        detail: $"CouchDB returned HTTP {response.StatusCode}.",
                        statusCode: StatusCodes.Status502BadGateway);
                }

                var summary = CouchDbResponseParser.ParseSnapshot(
                    id,
                    response.Body,
                    DateTimeOffset.UtcNow);
                return Results.Ok(summary);
            }
            catch (JsonException)
            {
                return Results.Problem(
                    detail: "CouchDB returned a malformed snapshot response.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (InvalidDataException)
            {
                return Results.Problem(
                    detail: "CouchDB response exceeded the inspection size limit.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.Problem(
                    detail: "The CouchDB snapshot request timed out.",
                    statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (HttpRequestException)
            {
                return Results.Problem(
                    detail: "The CouchDB snapshot request failed.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithName("GetCompetitionSnapshot");

    app.MapGet(
        "/api/development/competitions/{id}/changes/check",
        async (
            string id,
            ILiftingCastCompetitionClient couchDbClient,
            CancellationToken cancellationToken) =>
        {
            if (!CompetitionIdValidator.IsValid(id))
            {
                return Results.BadRequest(new { error = "Invalid competition ID." });
            }

            try
            {
                var response = await couchDbClient.CheckChangesAsync(id, cancellationToken);
                try
                {
                    var diagnostic = CouchDbResponseParser.ParseChanges(
                        id,
                        response.StatusCode,
                        "now",
                        response.Body,
                        DateTimeOffset.UtcNow);
                    return Results.Ok(diagnostic);
                }
                catch (JsonException)
                {
                    return Results.Ok(new CouchDbChangesDiagnostic(
                        id,
                        "now",
                        response.StatusCode,
                        false,
                        null,
                        [],
                        null,
                        DateTimeOffset.UtcNow));
                }
            }
            catch (InvalidDataException)
            {
                return Results.Problem(
                    detail: "CouchDB response exceeded the inspection size limit.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.Problem(
                    detail: "The CouchDB changes diagnostic timed out.",
                    statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (HttpRequestException)
            {
                return Results.Problem(
                    detail: "The CouchDB changes diagnostic request failed.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithName("CheckCompetitionChanges");

    app.MapPost(
        "/api/development/competitions/{id}/changes/checkpoint",
        async (
            string id,
            CouchDbChangesRequest request,
            ILiftingCastCompetitionClient couchDbClient,
            CancellationToken cancellationToken) =>
        {
            if (!CompetitionIdValidator.IsValid(id))
            {
                return Results.BadRequest(new { error = "Invalid competition ID." });
            }

            if (!CouchDbChangesRequestValidator.IsValid(request.Since, request.Limit))
            {
                return Results.BadRequest(new
                {
                    error = $"A non-empty checkpoint of at most {CouchDbChangesRequestValidator.MaximumCheckpointLength} characters and a limit from 1 to {CouchDbChangesRequestValidator.MaximumLimit} are required."
                });
            }

            try
            {
                var response = await couchDbClient.GetChangesSinceAsync(
                    id,
                    request.Since!,
                    request.Limit,
                    cancellationToken);
                if (response.StatusCode is < 200 or >= 300)
                {
                    return Results.Problem(
                        detail: $"CouchDB returned HTTP {response.StatusCode}.",
                        statusCode: StatusCodes.Status502BadGateway);
                }

                var diagnostic = CouchDbResponseParser.ParseChanges(
                    id,
                    response.StatusCode,
                    request.Since!,
                    response.Body,
                    DateTimeOffset.UtcNow);
                return Results.Ok(diagnostic);
            }
            catch (JsonException)
            {
                return Results.Problem(
                    detail: "CouchDB returned a malformed changes response.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (InvalidDataException)
            {
                return Results.Problem(
                    detail: "CouchDB response exceeded the inspection size limit.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.Problem(
                    detail: "The CouchDB changes request timed out.",
                    statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (HttpRequestException)
            {
                return Results.Problem(
                    detail: "The CouchDB changes request failed.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithName("GetCompetitionChangesSinceCheckpoint");

    app.MapPost(
        "/api/development/competitions/{id}/sync",
        async (
            string id,
            bool? full,
            CompetitionSyncService syncService,
            CancellationToken cancellationToken) =>
        {
            if (!CompetitionIdValidator.IsValid(id))
            {
                return Results.BadRequest(new { error = "Invalid competition ID." });
            }

            try
            {
                var result = await syncService.SyncAsync(id, cancellationToken, full == true);
                return Results.Ok(result);
            }
            catch (CompetitionNotFoundException)
            {
                return Results.NotFound(new { error = "Competition not found in the local directory." });
            }
            catch (CompetitionSyncAlreadyRunningException)
            {
                return Results.Conflict(new { error = "A synchronization for this competition is already running." });
            }
            catch (CouchDbSyncException exception)
            {
                return Results.Problem(
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (JsonException)
            {
                return Results.Problem(
                    detail: "CouchDB returned an incomplete or malformed synchronization response.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (InvalidDataException)
            {
                return Results.Problem(
                    detail: "CouchDB response exceeded the synchronization size limit.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.Problem(
                    detail: "The CouchDB synchronization request timed out.",
                    statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (HttpRequestException)
            {
                return Results.Problem(
                    detail: "The CouchDB synchronization request failed.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithName("SynchronizeCompetition");
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/api/health", () => new { status = "healthy" })
    .WithName("GetHealth")
    .WithOpenApi();

app.MapGet("/api/competitions", async (ICompetitionRepository repository) =>
{
    var competitions = await repository.GetAllAsync();
    return Results.Ok(competitions);
})
    .WithName("GetCompetitions")
    .WithOpenApi();

app.MapGet(
    "/api/competitions/{id}/documents",
    async (
        string id,
        int? offset,
        int? limit,
        ICompetitionRepository competitionRepository,
        ICompetitionSyncRepository syncRepository,
        CancellationToken cancellationToken) =>
    {
        var pageOffset = offset ?? 0;
        var pageLimit = limit ?? 25;
        if (!CompetitionIdValidator.IsValid(id))
        {
            return Results.BadRequest(new { error = "Invalid competition ID." });
        }

        if (pageOffset < 0 || pageLimit is < 1 or > 50)
        {
            return Results.BadRequest(new { error = "Offset must be non-negative and limit must be between 1 and 50." });
        }

        if (await competitionRepository.GetByIdAsync(id) is null)
        {
            return Results.NotFound(new { error = "Competition not found in the local directory." });
        }

        var (totalDocuments, documents) = await syncRepository.GetDocumentsAsync(
            id,
            pageOffset,
            pageLimit,
            cancellationToken);
        var summaries = documents
            .Select(document => new SynchronizedDocumentSummary(
                document.DocumentId,
                document.Revision,
                CouchDbSyncResponseParser.GetFieldSummary(document.DocumentDataJson),
                document.LastSyncedAt))
            .ToArray();

        return Results.Ok(new SynchronizedDocumentsPage(
            id,
            totalDocuments,
            pageOffset,
            pageLimit,
            summaries));
    })
    .WithName("GetSynchronizedCompetitionDocuments");

app.MapGet(
    "/api/competitions/{id}/details",
    async (
        string id,
        ICompetitionRepository competitionRepository,
        ICompetitionSyncRepository syncRepository,
        CancellationToken cancellationToken) =>
    {
        if (!CompetitionIdValidator.IsValid(id))
        {
            return Results.BadRequest(new { error = "Invalid competition ID." });
        }

        var competition = await competitionRepository.GetByIdAsync(id);
        if (competition is null)
        {
            return Results.NotFound(new { error = "Competition not found in the local directory." });
        }

        var (syncState, documents) = await syncRepository.GetDetailsDataAsync(id, cancellationToken);
        if (syncState is null)
        {
            return Results.Conflict(new
            {
                error = "Competition has not been synchronized yet.",
                competitionId = id
            });
        }

        try
        {
            return Results.Ok(CompetitionDetailsMapper.Create(
                competition,
                documents,
                syncState.LastSyncedAt));
        }
        catch (JsonException)
        {
            return Results.Problem(
                detail: "Stored competition data is malformed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    })
    .WithName("GetCompetitionDetails");

app.MapGet(
    "/api/competitions/{id}/platforms",
    async (
        string id,
        ICompetitionRepository competitionRepository,
        ICompetitionSyncRepository syncRepository,
        CancellationToken cancellationToken) =>
    {
        if (!CompetitionIdValidator.IsValid(id))
        {
            return Results.BadRequest(new { error = "Invalid competition ID." });
        }

        if (await competitionRepository.GetByIdAsync(id) is null)
        {
            return Results.NotFound(new { error = "Competition not found in the local directory." });
        }

        var (syncState, documents) = await syncRepository.GetDetailsDataAsync(id, cancellationToken);
        if (syncState is null)
        {
            return Results.Conflict(new
            {
                error = "Competition has not been synchronized yet.",
                competitionId = id
            });
        }

        try
        {
            return Results.Ok(CompetitionDetailsMapper.CreatePlatformDashboard(
                id,
                documents,
                syncState.LastSyncedAt));
        }
        catch (JsonException)
        {
            return Results.Problem(
                detail: "Stored competition data is malformed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    })
    .WithName("GetCompetitionPlatforms");

app.Run();
