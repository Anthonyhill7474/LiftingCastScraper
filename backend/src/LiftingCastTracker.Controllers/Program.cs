using LiftingCastTracker.Repositories.Data;
using LiftingCastTracker.Repositories.Implementations;
using LiftingCastTracker.Services.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<ICompetitionRepository, CompetitionRepository>();

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
}

app.UseHttpsRedirection();

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
