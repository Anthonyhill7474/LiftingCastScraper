# LiftingCast Tracker — Architecture

## Overview

LiftingCast Tracker is a lightweight companion application for LiftingCast.

Its eventual purpose is to discover competitions, synchronize competition data, and provide an interface for viewing and analysing powerlifting meets.

The application is being developed incrementally.

The current milestone includes manually importing competition directory JSON and displaying stored competition metadata. It is not real-time competition tracking.

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
- Development-only `POST /api/development/competitions/import`

### Services

The application logic layer.

Responsibilities:
- Competition model
- Business logic
- Competition directory JSON parsing and import decisions
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
- Future CouchDB HTTP client

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

Start with a single Competition entity representing metadata from LiftingCast.

Required source fields:
- `_id`
- `name`
- `date`
- `dateFormat`
- `createDate`
- `showOnHomePage`

Preserve the original LiftingCast competition ID.

Some optional fields may be missing or empty.

Do not create tables for athletes, attempts, flights, platforms or historical synchronization until the relevant data structures have been inspected.

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

### Competition Databases

A discovered competition database follows an observed CouchDB URL structure:

```text
https://couchdb.liftingcast.com/{meetId}_readonly/
```

An observed snapshot endpoint is:

```text
_all_docs?conflicts=true&update_seq=true&include_docs=true
```

CouchDB supports a `_changes` mechanism generally, but access and behaviour on LiftingCast must be verified.

Do not implement automatic directory polling or meet data synchronization.

## Competition Discovery Milestone

Competition directory JSON is imported manually from a configured local fixture file. The import endpoint is registered only in the Development environment and is not scheduled. Services parses records and decides whether each valid competition is inserted, updated, or unchanged; Repositories persists those changes using the existing `competitions` table.

The original competition date and date format remain stored as source metadata. A parsed date is derived for API responses and frontend sorting, not persisted. Missing or unparseable dates do not prevent importing a record with a valid ID. No unverified LiftingCast endpoint is called.

## Future Synchronization

Potential future work:

1. Automate competition directory discovery and metadata refresh.
2. Read individual competition documents.
3. Investigate incremental CouchDB changes.
4. Store synchronization checkpoints.
5. Detect active competitions.
6. Poll active competitions at a configurable frequency.
7. Expose synchronized information through our own API.

An initial target is up to 20 active meets at five-second intervals, subject to owner approval and performance testing.

Polling should avoid unnecessarily downloading complete meet data when incremental synchronization is available.

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