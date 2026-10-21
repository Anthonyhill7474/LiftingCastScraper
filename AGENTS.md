# LiftingCast Tracker — Agent Instructions

## Project

A lightweight full-stack application for discovering and tracking powerlifting competitions from LiftingCast.

The initial goal is a simple, functioning foundation. Additional functionality will be implemented incrementally.

**Prioritize simplicity, maintainability and working code. Do not overengineer.**

## Technology Stack

- Backend: .NET 10, C#, ASP.NET Core Minimal APIs
- Frontend: React, TypeScript, Vite
- Database: PostgreSQL
- ORM: Entity Framework Core with Npgsql
- Database client: DBeaver
- Tests: xUnit
- Development environment: macOS and VS Code

## Repository Structure

Existing solution: `LiftingCastTracker.sln` at the repository root.

Backend projects:

- `backend/src/LiftingCastTracker.Controllers`
- `backend/src/LiftingCastTracker.Services`
- `backend/src/LiftingCastTracker.Repositories`
- `backend/tests/LiftingCastTracker.Tests`

Frontend: `frontend/`

Do not recreate existing projects or solution files.

## Backend Architecture

### Controllers

ASP.NET Core application entry point.

Responsibilities:
- HTTP endpoints
- Request handling
- Dependency injection configuration
- Application configuration
- Future background service hosting

### Services

Contains application logic independent of persistence and HTTP infrastructure.

Responsibilities:
- Domain models
- Business logic
- Repository interfaces
- External data source interfaces when needed
- Data validation and date parsing

### Repositories

Contains infrastructure and data-access implementations.

Responsibilities:
- EF Core DbContext
- PostgreSQL repositories
- Database migrations
- Future HTTP clients for LiftingCast

### Dependency Rules

- Controllers references Services and Repositories.
- Repositories references Services.
- Services must not reference Controllers or Repositories.
- Tests reference only the projects they need.

Keep these dependencies unidirectional.

Do not introduce additional architectural layers unless genuinely necessary.

## Engineering Principles

- Implement only what the current task requires.
- Prefer straightforward C# over elaborate patterns.
- Avoid unnecessary interfaces, classes, abstractions and dependencies.
- Do not introduce CQRS, MediatR, AutoMapper, repository-of-repository patterns, microservices or message queues.
- Do not introduce Docker, Kubernetes, Redis or cloud infrastructure.
- Use dependency injection where appropriate.
- Use async/await for I/O operations.
- Use nullable reference types.
- Follow standard C# naming conventions.
- Use `IHttpClientFactory` for future external HTTP integrations.
- Avoid speculative features and premature optimization.
- Write focused tests for meaningful business logic.
- Do not generate unnecessary placeholder code.
- Prefer a few readable files over many trivial files.

Do not refactor unrelated code.

## Database Conventions

- Use PostgreSQL exclusively.
- Use EF Core with the Npgsql provider.
- Use EF Core migrations for database schema changes.
- Use snake_case table and column names.
- Store timestamps in UTC using PostgreSQL `timestamptz` where appropriate.
- Preserve original LiftingCast competition IDs.
- Never commit connection strings containing credentials.
- Use .NET User Secrets for local database credentials.
- Do not introduce SQLite or Docker.
- Do not recreate or delete the existing PostgreSQL database.

Local database:
- Database name: `liftingcast`
- Application role: `liftingcast_app`
- Connection string configuration key: `ConnectionStrings:DefaultConnection`

DBeaver is used for inspecting the database, not as part of the application runtime.

## External LiftingCast Data

LiftingCast is an external service we do not control.

- Only use publicly accessible, read-only endpoints within agreed usage limits.
- Treat undocumented endpoints as potentially unstable.
- Do not implement polling until explicitly requested.
- Never enable automated external requests by default.
- Future synchronization must support cancellation, timeouts and error handling.
- Do not assume CouchDB `_changes` is available until verified.

## Development Workflow

Before changing code:

1. Read this file.
2. Read `.github/copilot-instructions.md`.
3. Read `docs/architecture.md` when relevant.
4. Inspect existing code and project configuration.
5. Prefer modifying existing code over generating duplicate structures.

After changing code:

1. Run relevant builds and tests.
2. Fix errors introduced by the changes.
3. Update documentation when necessary.
4. Summarize completed work and any unresolved issues.

Use the `dotnet` CLI for creating and modifying .NET solutions, projects, references and migrations.

Do not manually create `.sln` or `.csproj` files when equivalent CLI commands are available.

## Definition of Done

A task is complete when:

- The requested behaviour works.
- The solution builds successfully.
- Relevant tests pass.
- No unnecessary architecture or packages were introduced.
- No secrets were committed.
- The implementation is understandable without extensive explanation.

**When in doubt, choose the simpler implementation.**