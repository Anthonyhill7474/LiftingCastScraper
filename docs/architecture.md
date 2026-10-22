# LiftingCast Tracker — Architecture

## Overview

LiftingCast Tracker is a lightweight companion application for LiftingCast.

Its eventual purpose is to discover competitions, synchronize competition data, and provide an interface for viewing and analysing powerlifting meets.

The application is being developed incrementally.

The current application supports manually importing competition directory JSON, displaying stored competition metadata, and manually synchronizing sanitized current documents for an individual competition. Synchronization is Development-triggered only; this is not real-time or automatic tracking.

## Technology

### Backend
- .NET 10
- ASP.NET Core Minimal APIs
- Entity Framework Core
- Npgsql
- PostgreSQL

### Frontend
- React
- TypeScript
- Vite

### Development
- macOS
- VS Code
- GitHub Copilot
- DBeaver
- xUnit

## Repository Structure

```text
liftingcast-tracker/
├── .github/
│   └── copilot-instructions.md
├── backend/
│   ├── src/
│   │   ├── LiftingCastTracker.Controllers/
│   │   ├── LiftingCastTracker.Services/
│   │   └── LiftingCastTracker.Repositories/
│   └── tests/
│       └── LiftingCastTracker.Tests/
├── frontend/
├── docs/
│   └── architecture.md
├── AGENTS.md
├── LiftingCastTracker.sln
└── README.md
```

Do not introduce additional C# projects for the initial implementation.

## Backend Responsibilities

### Controllers

The ASP.NET Core application and composition root.

Responsibilities:
- Minimal API endpoints
- Dependency injection
- Configuration
- Future background processing

Initial endpoints:
- `GET /api/health`
- `GET /api/competitions`
- `GET /api/competitions/{id}/documents`
- Development-only `POST /api/development/competitions/import`
- Development-only `POST /api/development/competitions/{id}/sync`

### Services

The application logic layer.

Responsibilities:
- Competition model
- Business logic
- Competition directory JSON parsing and import decisions
- Manual synchronization orchestration and response sanitization
- Repository interfaces
- Future external data source interfaces
- Date parsing where needed

This project must not depend on Controllers or Repositories.

### Repositories

The infrastructure and persistence layer.

Responsibilities:
- PostgreSQL EF Core DbContext
- Entity configurations where needed
- EF Core migrations
- Repository implementations
- Read-only CouchDB HTTP client

Repositories depends on Services.

## Database

Database: PostgreSQL

Local database name: `liftingcast`

Application user: `liftingcast_app`

Database client: DBeaver

ORM: Entity Framework Core

Provider: `Npgsql.EntityFrameworkCore.PostgreSQL`

Configuration key:
`ConnectionStrings:DefaultConnection`

Local credentials are stored through .NET User Secrets.

Use EF Core migrations to manage schema changes.

Use snake_case naming in PostgreSQL.

### Initial Data Model

The `competitions` table stores directory metadata from LiftingCast. Synchronization adds only two small tables:

- `competition_documents` stores the selected current fields as `jsonb`, keyed by competition ID and CouchDB document ID; revision and synchronization time are separate columns.
- `competition_sync_state` stores the last successfully committed opaque CouchDB sequence and synchronization time, keyed by competition ID.

The document table is not normalized into lifter, attempt, division or platform tables. Each document batch and its checkpoint are saved atomically. Design documents and unneeded personal/payment fields are excluded before persistence.

Preserve the original LiftingCast competition ID and CouchDB document IDs.

Some optional fields may be missing or empty.

Do not normalize athletes, attempts, flights, platforms or historical changes until the relevant data structures and product requirements are verified.

## Frontend

Use React, TypeScript and Vite.

Initial requirements:
- Basic responsive layout
- Application title
- Fetch competitions from the local backend
- Show loading, error and empty states
- Display available competition metadata

Use `VITE_API_BASE_URL` for backend configuration.

Avoid complex UI libraries and state-management solutions for the initial scaffold.

## LiftingCast Data Source

LiftingCast exposes competition metadata through its website.

Known directory fields include:
- `_id`
- `name`
- `date`
- `dateFormat`
- `createDate`
- `showOnHomePage`

Competition dates may be formatted as:
- `DD/MM/YYYY`
- `MM/DD/YYYY`
- `YYYY/MM/DD`

Dates must be interpreted using `dateFormat`, not assumed to follow one regional format.

The competition creation timestamp does not establish the meet's time zone.

A competition may exist even when `showOnHomePage` is false or absent.

## Competition Lifecycle

The application is intended to support upcoming, live and completed competitions.

The completed competition `m80in4tnzkgx` is used as a reference dataset for understanding CouchDB document structures.

It is not a hardcoded target competition.

### Upcoming competitions

May have registered competitors, configured divisions and placeholder attempts. Many result fields may be absent or incomplete.

### Live competitions

Competition documents can change as attempts are submitted, judged or edited.

Future synchronization should retrieve changed documents efficiently and maintain current competition state.

### Completed competitions

Useful for understanding populated result structures and historical changes.

They do not establish the exact behaviour of live platform state, active clocks or attempt scheduling.

### Architecture Requirements

- Support multiple competition IDs.
- Never hardcode sample competition IDs in production logic.
- Account for missing and incomplete attempt data.
- Keep live synchronization independent of the frontend.
- Do not assume every competition follows exactly the same document structure.
- Use confirmed observations when designing permanent storage and business logic.

See `docs/couchdb-investigation.md` for findings from the completed competition.

### Competition Databases

A discovered competition database follows an observed CouchDB URL structure:

```text
https://couchdb.liftingcast.com/{meetId}_readonly/
```

The manually triggered initial snapshot endpoint is:

```text
_all_docs?conflicts=true&update_seq=true&include_docs=true
```

CouchDB `_changes` returned HTTP 200 with `since=now` for the observed competition, with no rows. Manual synchronization uses bounded `feed=normal` batches with `include_docs=true`, preserves the complete `last_seq` value as an opaque string, and commits each batch with its checkpoint. A run processes at most 20 batches of 25 changes; it reports `hasMore` when another manual call is needed. Responses are size-bounded, documents are sanitized, and design documents are not stored.

Do not implement automatic directory polling or meet synchronization yet.

## Competition Discovery Milestone

Competition directory JSON is imported manually from a configured local fixture file. The import endpoint is registered only in the Development environment and is not scheduled. Services parses records and decides whether each valid competition is inserted, updated, or unchanged; Repositories persists those changes using the existing `competitions` table.

The original competition date and date format remain stored as source metadata. A parsed date is derived for API responses and frontend sorting, not persisted. Missing or unparseable dates do not prevent importing a record with a valid ID. The directory import does not call any LiftingCast endpoint.

## Competition Data Inspection Milestone

The Repositories project has a read-only CouchDB HTTP client configured by `LiftingCast:CouchDbBaseUrl`. Development-only endpoints support bounded document-structure sampling, an explicitly selected sanitized document preview, the `since=now` changes check, and a manually supplied opaque checkpoint. These inspection endpoints do not persist their responses.

## Manual Competition Synchronization

The Development-only `POST /api/development/competitions/{id}/sync` endpoint verifies the competition exists in the local directory. If no sync state exists, it fetches a full `_all_docs` snapshot and saves sanitized documents with the snapshot sequence. Later calls process bounded `_changes` batches, applying additions, updates and deletions idempotently. Every successful batch and its `last_seq` checkpoint commit in one PostgreSQL transaction. A per-competition in-process lock prevents overlapping calls in one application instance.

`GET /api/competitions/{id}/documents` reads only the local PostgreSQL state and returns a bounded summary of document IDs, revisions, and sanitized top-level field names/types. It does not contact CouchDB. No raw responses or complete history arrays are stored.

## Future Synchronization

Potential future work:

1. Verify checkpoint behavior and synchronization limits with additional manually captured competition data.
2. Automate competition directory discovery and metadata refresh.
3. Detect active competitions and poll at an explicitly approved configurable frequency.
4. Add live UI or derived results only after attempt and scoring semantics are verified.

Polling should avoid unnecessarily downloading complete meet data when incremental synchronization is available. No polling is currently configured.

## Initial Milestone — Scaffolding

Implement only:

- Working .NET solution and project references
- Minimal layered C# architecture
- PostgreSQL EF Core configuration
- Initial Competition model
- Initial EF Core migration
- A basic local competitions repository
- Health and competitions API endpoints
- Simple React frontend
- Basic meaningful tests
- Local development instructions

The competitions list may initially be empty.

## Out of Scope

Do not implement the following during scaffolding:

- Active LiftingCast polling
- Background synchronization worker
- Full CouchDB document parsing
- SignalR
- User accounts or authentication
- Native mobile applications
- Production cloud deployment
- Docker
- Distributed processing
- Advanced analytics
- Attempt projections or coaching logic

## Design Principle

Build only what is needed for the current milestone.

Keep the architecture understandable, maintainable and easy to extend.

**Do not add complexity for hypothetical future requirements.**