# LiftingCast Tracker — Architecture

## Overview

LiftingCast Tracker is a lightweight companion application for LiftingCast.

Its eventual purpose is to discover competitions, synchronize competition data, and provide an interface for viewing and analysing powerlifting meets.

The application is being developed incrementally.

The current application supports manually importing competition directory JSON, displaying stored competition metadata, and manually synchronizing sanitized current documents for an individual competition. A controlled incremental background worker is also available, but is disabled by default and requires explicit local opt-in per competition. This is not real-time tracking.

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
- Hosting the opt-in background synchronization worker

Initial endpoints:
- `GET /api/health`
- `GET /api/competitions`
- `GET /api/competitions/{id}/documents`
- `GET /api/competitions/{id}/details`
- Development-only `POST /api/development/competitions/import`
- Development-only `POST /api/development/competitions/{id}/sync`
- Development-only `GET /api/development/automatic-sync/status`

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

The `competitions` table stores directory metadata from LiftingCast and a default-off `automatic_sync_enabled` local opt-in. Synchronization adds two small tables:

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
- Open locally synchronized competition details with lifters grouped by flight/session and attempts 1–3

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

Competition directory polling is not implemented. The controlled automatic meet synchronization worker is described below and remains disabled by default.

## Competition Discovery Milestone

Competition directory JSON is imported manually from a configured local fixture file. The import endpoint is registered only in the Development environment and is not scheduled. Services parses records and decides whether each valid competition is inserted, updated, or unchanged; Repositories persists those changes using the existing `competitions` table.

The original competition date and date format remain stored as source metadata. A parsed date is derived for API responses and frontend sorting, not persisted. Missing or unparseable dates do not prevent importing a record with a valid ID. The directory import does not call any LiftingCast endpoint.

## Competition Data Inspection Milestone

The Repositories project has a read-only CouchDB HTTP client configured by `LiftingCast:CouchDbBaseUrl`. Development-only endpoints support bounded document-structure sampling, an explicitly selected sanitized document preview, the `since=now` changes check, and a manually supplied opaque checkpoint. These inspection endpoints do not persist their responses.

## Manual Competition Synchronization

The Development-only `POST /api/development/competitions/{id}/sync` endpoint verifies the competition exists in the local directory. If no sync state exists, it fetches a full `_all_docs` snapshot and saves sanitized documents with the snapshot sequence. Later calls process bounded `_changes` batches, applying additions, updates and deletions idempotently. Every successful batch and its `last_seq` checkpoint commit in one PostgreSQL transaction. A per-competition in-process lock prevents overlapping calls in one application instance.

`GET /api/competitions/{id}/documents` reads only the local PostgreSQL state and returns a bounded summary of document IDs, revisions, and sanitized top-level field names/types. It does not contact CouchDB. No raw responses or complete history arrays are stored.

`GET /api/competitions/{id}/details` also reads only PostgreSQL. It maps observed profile, attempt, division-configuration and platform-runtime document shapes into explicit DTOs; attempts are joined to lifters by `lifterId`. It returns only requested display fields and never exposes raw documents, dates of birth, payment/contact data, or full change histories. Missing values remain absent/null; no source official totals, official placings, or coefficient scores are exposed. The React page fetches details when a competition is opened and does not poll.

The details DTO also carries the minimal competition/division configuration used by Services calculations: source units, gender, equipment category, enabled lifts, score category, and weight-class ID/name/limit. `CompetitionPerformanceCalculator` derives positive finite best lifts, confirmed partial sums, complete three-lift totals, configured-category totals, and baseline projections from the stored attempt DTOs. Only `good` outcomes count as confirmed; `bad` is excluded, while selected unresolved weights are used only in a labelled hypothetical baseline. Known kilograms and pound units normalize to kilograms. Unknown units or incomplete eligible lifts produce null calculations, not fabricated zeroes.

The React coaching UI stores only followed lifter document IDs per competition and the selected points-system ID in browser local storage. Scenario edits remain in component memory. The stateless `POST /api/competitions/{id}/strategy` endpoint accepts selected division/weight-class IDs and scenario changes for existing unresolved attempts, validates their eligibility, then returns current and scenario calculations plus an unofficial projected-total ordering. It never contacts CouchDB or writes competition data. Comparisons require the same division document, weight-class ID, gender, equipment category, and lift configuration; no official tie-break or award rules are inferred.

The requested original JavaScript scoring reference and coefficient tables are not present in the repository. The UI can persist a selected system (default IPF GL), but does not calculate IPF GL, DOTS, Wilks, or legacy IPF points until the reference is supplied. Best Overall ranking is unavailable for the same reason; cross-weight-class totals are not treated as comparable points.

The completed fixture does not verify an upcoming lifting queue. Profile platform/flight/session/lot data and attempt slots do not establish ordering, and the completed platform runtime has no current attempt. The UI therefore shows order unavailable and does not estimate attempts ahead or wait time. Live order and timing require observation during an active competition.

The Development-only `GET /api/development/status` reports the backend environment. The frontend displays manual refresh controls only when this endpoint explicitly confirms Development; backend synchronization routes remain registered only in Development. `POST /api/development/competitions/{id}/sync` uses the existing checkpoint by default, while `?full=true` requests a fresh bounded snapshot. Full refresh replaces only that competition's cached documents and checkpoint in one transaction and leaves the competition directory entry untouched.

## Controlled Background Synchronization

The Controllers project hosts one `BackgroundService` using the existing `CompetitionSyncService`. `AutomaticSync:Enabled` defaults to `false`; the default interval is 30 seconds. A competition must have `automatic_sync_enabled = true` in PostgreSQL before it can be selected. Directory imports update competition metadata without resetting this local opt-in.

The worker uses each competition's parsed date as an approximate eligibility filter: from one UTC calendar day before through one day after today. The date and timezone do not prove that a meet is live or establish its start/end time. Competitions with missing or invalid dates are not selected. Each scan is capped by `MaxActiveCompetitionsPerRun` and rotates through larger sets; at most `MaxConcurrentCompetitions` competition synchronizations run at once, with a hard maximum of five.

Each competition uses the existing incremental checkpoint and batch limits. Failures receive exponential backoff up to 15 minutes; HTTP 429 responses honor `Retry-After`. An oversized CouchDB response pauses retries for that competition until it is opted out for an observed scan and re-enabled, or the process restarts. Runtime failure/backoff state is not persisted. Existing per-competition synchronization locks prevent overlap with manual refreshes in the same application process.

`GET /api/development/automatic-sync/status` reports worker configuration state and opted-in competition eligibility, last successful synchronization, safe failure status, and next eligible time. There is no distributed lock: run only one API instance with opted-in competitions. Automatic synchronization must remain disabled until upstream permission and acceptable usage limits are confirmed before production use.

## Future Synchronization

Potential future work:

1. Verify checkpoint behavior and synchronization limits with additional manually captured competition data.
2. Automate competition directory discovery and metadata refresh.
3. Validate the date eligibility approximation and polling limits against an actively running competition with upstream permission.
4. Add live UI or derived results only after attempt and scoring semantics are verified.

Polling should avoid unnecessarily downloading complete meet data when incremental synchronization is available. Automatic synchronization remains disabled by default and requires local opt-in.

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

## Out of Scope for Initial Scaffolding

Do not implement the following during scaffolding:

- Active LiftingCast polling
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