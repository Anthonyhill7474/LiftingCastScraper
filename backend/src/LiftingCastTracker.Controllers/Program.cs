using LiftingCastTracker.Repositories.Data;
using LiftingCastTracker.Repositories.ExternalData;
using LiftingCastTracker.Repositories.Implementations;
using LiftingCastTracker.Services.Import;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Parsing;
using LiftingCastTracker.Services.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<ICompetitionRepository, CompetitionRepository>();
builder.Services.AddScoped<CompetitionImportService>();
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
            ILiftingCastCompetitionClient couchDbClient,
            CancellationToken cancellationToken) =>
        {
            if (!CompetitionIdValidator.IsValid(id))
            {
                return Results.BadRequest(new { error = "Invalid competition ID." });
            }

            try
            {
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
                        response.Body,
                        DateTimeOffset.UtcNow);
                    return Results.Ok(diagnostic);
                }
                catch (JsonException)
                {
                    return Results.Ok(new CouchDbChangesDiagnostic(
                        id,
                        response.StatusCode,
                        false,
                        null,
                        null,
                        DateTimeOffset.UtcNow));
                }
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

app.Run();
