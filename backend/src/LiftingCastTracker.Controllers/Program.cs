using LiftingCastTracker.Repositories.Data;
using LiftingCastTracker.Repositories.Implementations;
using LiftingCastTracker.Services.Import;
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
