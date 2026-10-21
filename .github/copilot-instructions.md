# GitHub Copilot Instructions

Follow `AGENTS.md` as the primary repository guidance.

Read `docs/architecture.md` when making architectural or feature decisions.

## Existing Project

This is an existing .NET 10 and React repository. Do not recreate its projects or solution.

Backend projects:
- `LiftingCastTracker.Controllers` — ASP.NET Core entry point and HTTP endpoints
- `LiftingCastTracker.Services` — models, interfaces and business logic
- `LiftingCastTracker.Repositories` — PostgreSQL persistence and external HTTP access

The frontend uses React, TypeScript and Vite.

The database is PostgreSQL with EF Core and Npgsql. Never substitute SQLite.

## Coding Behaviour

- Keep implementations small and direct.
- Do not overengineer.
- Do not introduce patterns or packages without a clear need.
- Reuse existing code and conventions.
- Avoid unnecessary interfaces, services and wrappers.
- Do not generate unrelated features or speculative infrastructure.
- Prefer minimal changes that satisfy the request.
- Ask for clarification only when necessary to avoid an important incorrect assumption.

## Database

- Use the existing PostgreSQL database.
- Use `ConnectionStrings:DefaultConnection`.
- Credentials are stored through .NET User Secrets.
- Use EF Core migrations for schema changes.
- Never hardcode or commit credentials.
- Do not perform destructive database operations without approval.

## LiftingCast Integration

- Do not enable live polling unless explicitly requested.
- Do not assume undocumented CouchDB capabilities are supported.
- Keep all external data access read-only.

## Verification

After making changes:
- Run relevant .NET builds and tests.
- Run frontend lint and build checks when applicable.
- Fix errors introduced by your changes.
- Report the files changed, verification results and unresolved issues.

**Prefer the simplest maintainable solution.**