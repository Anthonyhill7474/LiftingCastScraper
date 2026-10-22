# LiftingCast Tracker

A lightweight full-stack application for discovering and tracking powerlifting competitions from LiftingCast.

## Project Structure

```text
liftingcast-tracker/
├── .github/
│   └── copilot-instructions.md      — GitHub Copilot task guidelines
├── backend/
│   ├── src/
│   │   ├── LiftingCastTracker.Controllers/     — ASP.NET Core web API
│   │   ├── LiftingCastTracker.Services/        — Models and business logic
│   │   └── LiftingCastTracker.Repositories/    — EF Core and database access
│   └── tests/
│       └── LiftingCastTracker.Tests/           — xUnit tests
├── frontend/
│   ├── src/
│   │   ├── App.tsx                 — Main React component
│   │   ├── main.tsx                — Entry point
│   │   └── index.css               — Styling
│   ├── index.html
│   ├── vite.config.ts
│   ├── tsconfig.json
│   └── package.json
├── docs/
│   └── architecture.md              — Architecture decisions
├── AGENTS.md                        — Development guidelines
├── LiftingCastTracker.sln           — .NET solution
└── README.md                        — This file
```

## Technology Stack

- **Backend**: .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core
- **Frontend**: React 18, TypeScript, Vite
- **Database**: PostgreSQL with Npgsql
- **Testing**: xUnit

## Prerequisites

- .NET 10 SDK
- Node.js 18+ and npm
- PostgreSQL 12+
- VS Code (recommended)

## Database Configuration

The application uses PostgreSQL. You must configure the database and connection string before running.

### Local PostgreSQL Setup

Ensure PostgreSQL is installed and running locally with:

- **Database**: `liftingcast`
- **Application user**: `liftingcast_app`

If not configured, run these commands as the PostgreSQL admin:

```sql
CREATE DATABASE liftingcast;
CREATE ROLE liftingcast_app WITH LOGIN PASSWORD 'your_secure_password';
GRANT ALL PRIVILEGES ON DATABASE liftingcast TO liftingcast_app;
```

### Connection String Configuration

The backend uses .NET User Secrets to store the connection string securely.

**Set the connection string:**

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=liftingcast;Username=liftingcast_app;Password=your_password"
```

Replace `your_password` with the actual password you set for the `liftingcast_app` user.

## Backend Setup

### Restore and Build

```bash
cd backend
dotnet restore
dotnet build
```

### Apply Database Migration

Before running the application, apply the initial migration to create the database schema:

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet ef database update --project ../LiftingCastTracker.Repositories --startup-project .
```

This applies all reviewed migrations, including the competition synchronization tables described below.

### Run the Backend

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet run --launch-profile https
```

The API will be available at `https://localhost:7251` or `http://localhost:5262`.

Test the API:
- Health check: `GET https://localhost:7251/api/health`
- Competitions list: `GET https://localhost:7251/api/competitions`

## Frontend Setup

### Install Dependencies

```bash
cd frontend
npm install
```

### Configuration

The frontend defaults to the local backend at `https://localhost:7251`. To use a different API URL, create `frontend/.env.local` with:

```env
VITE_API_BASE_URL=https://localhost:7251
```

Restart the Vite dev server after changing this value.

### Run Development Server

```bash
cd frontend
npm run dev
```

The application will be available at `http://localhost:5173`.

### Build for Production

```bash
cd frontend
npm run build
```

Output is in `dist/`.

### Lint Code

```bash
cd frontend
npm run lint
```

## Running Tests

### Backend Tests

```bash
cd backend
dotnet test
```

### Frontend Linting

```bash
cd frontend
npm run lint
```

## API Endpoints

### Health Check
- **Endpoint**: `GET /api/health`
- **Response**: `{ "status": "healthy" }`

### Get All Competitions
- **Endpoint**: `GET /api/competitions`
- **Response**: Array of competition objects
- **Example**:
```json
[
  {
    "id": "comp123",
    "name": "Regional Powerlifting Meet 2024",
    "date": "15/11/2024",
    "dateFormat": "DD/MM/YYYY",
    "createDate": "2024-10-22T10:30:00Z",
    "showOnHomePage": true
  }
]
```

### Get Competition Details

Open a competition from the directory to view its locally synchronized lifters and attempts. The details endpoint reads PostgreSQL only; it never contacts LiftingCast.

```bash
curl -k https://localhost:7251/api/competitions/COMPETITION_ID/details
```

The response includes competition metadata, the last synchronization time, platform runtime summaries, and lifters joined to attempts by `lifterId`. Lifter details include available divisions, flight/session/lot, bodyweight, and attempt slots. Missing source fields remain null or absent; totals, placings, and scores are not calculated. Dates of birth, payment/contact data, and document change histories are not returned.

The local completed-meet fixture uses string attempt numbers `"1"`–`"3"` and lift names `squat`, `bench`, and `dead`; the frontend maps these into the three lift rows and selects attempts numerically. Its observed result labels `good` and `bad` are shown with success/failure colors. An explicit `pending` result is highlighted separately; a missing result is shown as “No result” rather than inferred to mean a successful or failed lift. No upcoming `_all_docs` fixture is available locally, so upcoming-state behavior is covered with incomplete mocked records rather than a second full meet dataset.

An unknown competition returns HTTP 404. A directory competition that has not been synchronized returns HTTP 409 with a not-synchronized message. In Development, manually synchronize it first:

```bash
curl -k -X POST https://localhost:7251/api/development/competitions/COMPETITION_ID/sync
```

Then reload the details page. No frontend polling is performed. Bodyweight is included in the synchronization allowlist; documents stored before that field was allowed include it after a full refresh or a later upstream document update.

### Platform Dashboard

The competition details page includes a read-only platform dashboard backed only by synchronized PostgreSQL documents. Its endpoint is:

```bash
curl -k https://localhost:7251/api/competitions/COMPETITION_ID/platforms
```

The response includes platform IDs/names and available `clockState`, `clockTimerLength`, and `currentAttemptId` source values. A current attempt is included only when its ID matches a stored attempt; its lifter is included only when `lifterId` resolves to a stored lifter. The dashboard displays the timer length as a raw value and does not interpret clock units, calculate a countdown, or predict what comes next. Missing runtime fields and unresolved references remain unknown or unavailable. The endpoint does not return stored raw documents, attempt results, or sensitive lifter fields.

The completed-meet fixture confirms these field names and relationships in stored data. Clock-state meanings, timer units, and when platforms update during an active meet still require observation during a live competition; no universal runtime behavior is assumed.

## Refreshing Competition Data

On a Development backend, the competition-details page shows **Refresh changes** and **Full refresh** controls only after `GET /api/development/status` explicitly reports Development mode. The synchronization endpoint remains Development-only regardless of frontend visibility.

- **Refresh changes** uses the saved opaque checkpoint and requests bounded CouchDB `_changes` batches.
- **Full refresh** fetches a fresh `_all_docs` snapshot and atomically replaces this competition's cached documents and checkpoint. It removes locally cached documents absent from the snapshot, but does not change the competition directory entry. The UI asks for confirmation because the local cache is replaced.

Trigger either action directly when needed:

```bash
curl -k -X POST https://localhost:7251/api/development/competitions/COMPETITION_ID/sync
curl -k -X POST 'https://localhost:7251/api/development/competitions/COMPETITION_ID/sync?full=true'
```

The response reports inserted, updated, unchanged and deleted document counts, batch count, checkpoint and whether more changes remain. A changes refresh processes at most 500 changes per call; if `hasMore` is true, call it again. A full snapshot is limited to 10 MiB and fails without replacing local data if the response is too large, malformed, unsuccessful, or the database transaction fails. Each update batch and its checkpoint commit atomically.

Verify successful state in DBeaver with the read-only synchronization queries below, and compare the `last_sequence`/`last_synced_at` row before and after a refresh. No new migration is required for full refresh.

## Importing Competition Directory JSON

No LiftingCast directory URL is assumed. The backend includes a local example at `backend/src/LiftingCastTracker.Controllers/competition-directory.json`, and the import reads that file by default.

To import a directory JSON file you already have, set its absolute path with User Secrets from the Controllers project:

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet user-secrets set "CompetitionImport:FixturePath" "/absolute/path/to/competition-directory.json"
```

Start the backend with the Development environment (the standard `dotnet run` launch profile does this), then manually trigger the import:

```bash
curl -k -X POST https://localhost:7251/api/development/competitions/import
```

The response reports `inserted`, `updated`, `unchanged`, and `invalid` record counts. The endpoint is only registered in Development. Invalid records include entries with a missing or blank `_id`, duplicate IDs, or incorrectly typed metadata fields. Missing optional values are accepted; empty names and dates are allowed, and unparseable dates remain stored as source text.

To verify the imported data in DBeaver, connect to the `liftingcast` database and run:

```sql
SELECT id, name, date, date_format, create_date, show_on_home_page
FROM competitions
ORDER BY create_date DESC NULLS LAST;
```

The competition date is parsed according to its `date_format` for display and sorting; the source date text remains unchanged in PostgreSQL.

## Inspecting Individual Competition Data

The backend reads individual competition data from CouchDB using its read-only database URL. The default base URL is `https://couchdb.liftingcast.com/`; override it in User Secrets if needed:

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet user-secrets set "LiftingCast:CouchDbBaseUrl" "https://couchdb.liftingcast.com/"
```

The following Development-only endpoints make requests only when called:

```bash
curl -k https://localhost:7251/api/development/competitions/mnk13ga524h1/snapshot
curl -k https://localhost:7251/api/development/competitions/mnk13ga524h1/changes/check
```

The snapshot response contains the competition ID, CouchDB `total_rows`, `update_seq`, the number of ordinary documents inspected, and up to five recurring top-level field structures. Each structure includes an occurrence count from the inspected sample, up to three document IDs, and up to ten field names/types. CouchDB design documents are excluded. The request is limited to 25 rows and responses to 1 MiB; document values are not returned.

To inspect a specific document, choose one of its IDs from the snapshot and explicitly request its sanitized structure preview. Use `--data-urlencode` so IDs containing reserved URL characters are encoded correctly:

```bash
curl -k --get \
  --data-urlencode 'documentId=REPLACE_WITH_DOCUMENT_ID' \
  https://localhost:7251/api/development/competitions/mnk13ga524h1/snapshot
```

The preview reports at most ten properties per object, five array elements, and four levels of JSON nodes. It includes names and JSON types only, never scalar values, and marks truncated structures.

To check incremental changes, copy the complete `update_seq` value from the snapshot response. The earlier changes diagnostic checks `since=now`; the checkpoint endpoint uses a POST body so the opaque sequence is not placed in the route:

```bash
curl -k -X POST \
  -H 'Content-Type: application/json' \
  -d '{"since":"PASTE_THE_COMPLETE_UPDATE_SEQUENCE_HERE","limit":10}' \
  https://localhost:7251/api/development/competitions/mnk13ga524h1/changes/checkpoint
```

The checkpoint is sent unchanged as the `_changes` `since` parameter, URL-encoded by the client. A successful response includes the original checkpoint, upstream status, changed document IDs, revisions, deletion flags, and the returned `last_seq`; document bodies are not included. The limit must be between 1 and 25, and checkpoint strings are limited to 2,048 characters. CouchDB errors are returned as gateway errors; the `/changes/check` probe instead reports the upstream status and feed-shape result in its response.

No raw inspection responses are logged. All diagnostic requests are explicit and Development-only; none runs at startup or on a schedule. For `mnk13ga524h1`, the observed directory has 327 documents, the snapshot update sequence began with `377-`, top-level fields included `_id`, `_rev`, `liftName`, `attemptNumber`, `lifterId`, `changes`, and `createDate`, and `_changes?since=now` returned HTTP 200 with no rows.

## Manually Synchronizing Competition Documents

After the competition has been imported into the local `competitions` table and the synchronization migration has been applied, run a manual synchronization from a Development backend:

```bash
curl -k -X POST https://localhost:7251/api/development/competitions/COMPETITION_ID/sync
```

The first request fetches `_all_docs?update_seq=true&include_docs=true`; later requests use `_changes?feed=normal&since=...&include_docs=true` with the exact saved checkpoint. Each changes batch and its returned checkpoint commit together. A run processes at most 20 batches of 25 changes (up to 500 changes); if `hasMore` is true, call the endpoint again to continue from the saved checkpoint. Empty batches still record the returned sequence. Concurrent sync calls for one competition return HTTP 409. This endpoint is registered only in Development and never runs automatically.

Before persistence, only selected tracking and competition-configuration fields are retained. Design documents, CouchDB `changes` history, payment identifiers, dates of birth, contact details and other unneeded profile fields are excluded. The source response is bounded to 10 MiB and is not logged. The stored document data is JSONB; revisions and opaque checkpoints are stored separately.

Inspect the local synchronized-document page without contacting CouchDB:

```bash
curl -k 'https://localhost:7251/api/competitions/COMPETITION_ID/documents?offset=0&limit=25'
```

The response includes document IDs, revisions and sanitized top-level field names/types only, with at most 50 documents per page. It does not return document values or raw JSON. Use `offset` to view subsequent pages.

To verify the local state and checkpoint in DBeaver without exposing document values, query:

```sql
SELECT competition_id, document_id, revision, last_synced_at
FROM competition_documents
WHERE competition_id = 'YOUR_IMPORTED_COMPETITION_ID'
ORDER BY document_id
LIMIT 50;

SELECT competition_id, last_sequence, last_synced_at
FROM competition_sync_state
WHERE competition_id = 'YOUR_IMPORTED_COMPETITION_ID';
```

The synchronization migration has been generated but is not applied automatically. Review `backend/src/LiftingCastTracker.Repositories/Migrations/*_AddCompetitionSynchronization.cs` and, if desired, generate its SQL script:

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet ef migrations script --project ../LiftingCastTracker.Repositories --startup-project .
```

Apply the migration only after reviewing it:

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet ef database update --project ../LiftingCastTracker.Repositories --startup-project .
```

This stores the current allowlisted document state, not a full document archive. A manual capped run may require another call; the automatic worker described below uses backoff and the same incremental limits. The full snapshot is limited to 10 MiB and a changes refresh to 500 results per call. Checkpoint handling is tested with mocked responses, but CouchDB checkpoint behavior has not been re-verified against a newly captured live sequence as part of build or test runs.

## Controlled Background Synchronization

The API includes one incremental `BackgroundService`, but it is **disabled by default**. It never discovers competitions automatically. Each competition must first be explicitly opted in locally, and the worker only considers parsed meet dates from one UTC calendar day before through one day after today. This date window is an approximation; it does not confirm that a meet is active, and unknown meet time zones/start/end times can affect eligibility.

Apply the new migration after reviewing it. It adds `competitions.automatic_sync_enabled` with a default of `false`:

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet ef migrations script --project ../LiftingCastTracker.Repositories --startup-project .
dotnet ef database update --project ../LiftingCastTracker.Repositories --startup-project .
```

Opt in only a competition you intend to synchronize, using DBeaver:

```sql
UPDATE competitions
SET automatic_sync_enabled = true
WHERE id = 'YOUR_COMPETITION_ID';
```

Disable that competition by setting the flag to `false`. A running sync may finish its current bounded request/batch; the worker will skip it on the next scan. The flag is preserved when competition directory metadata is imported again.

Enable the worker locally through User Secrets (interval and limits are optional; defaults shown):

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet user-secrets set "AutomaticSync:Enabled" "true"
dotnet user-secrets set "AutomaticSync:IntervalSeconds" "30"
dotnet user-secrets set "AutomaticSync:MaxConcurrentCompetitions" "2"
dotnet user-secrets set "AutomaticSync:MaxActiveCompetitionsPerRun" "5"
```

The interval cannot be configured below 30 seconds. Concurrency is limited to 1–5 competitions at a time, and each scan selects at most 1–50 eligible competitions, rotating through larger opted-in sets. A competition without a saved checkpoint receives the existing initial snapshot; later runs use incremental `_changes` batches, capped at 500 changes per competition. Failures use exponential backoff from the configured interval up to 15 minutes; HTTP 429 responses honor `Retry-After`. An oversized response pauses automatic attempts for that competition until the worker observes it opted out for a scan and then opted back in, or the API process is restarted.

Check the Development-only worker status without exposing document contents:

```bash
curl -k https://localhost:7251/api/development/automatic-sync/status
```

It reports whether the worker is enabled, each opted-in competition's approximate date eligibility, last successful sync, safe failure status, and next eligible run time. To disable all automatic synchronization immediately, set `AutomaticSync:Enabled` to `false` in User Secrets and restart the API; shutdown cancels in-flight requests:

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet user-secrets set "AutomaticSync:Enabled" "false"
```

Restart the API after changing worker configuration. The default setting is false.

To test the scheduler without contacting LiftingCast, run `dotnet test`; all synchronization tests use fake repositories and CouchDB responses. You can also enable the worker locally while leaving every `automatic_sync_enabled` value false: the status endpoint will report an enabled worker with no opted-in competitions, so no upstream request is made. Do not opt in a competition without verifying network behavior and receiving upstream permission before any production use.

The worker and its failure/backoff state are process-local. Do not run multiple API instances with the same competitions opted in; no distributed coordination is implemented.

## Development Workflow

1. **Backend Changes**: Modify code in `backend/src/` and rebuild with `dotnet build`
2. **Frontend Changes**: Edit files in `frontend/src/` and the dev server auto-reloads
3. **Database Changes**: Use EF Core migrations
   ```bash
   cd backend/src/LiftingCastTracker.Controllers
   dotnet ef migrations add YourMigrationName --project ../LiftingCastTracker.Repositories --startup-project .
   dotnet ef database update --project ../LiftingCastTracker.Repositories --startup-project .
   ```

## Creating a New Migration

If you modify the Competition model or add new entities:

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet ef migrations add YourDescriptiveMigrationName --project ../LiftingCastTracker.Repositories --startup-project .
```

Review the generated migration in `backend/src/LiftingCastTracker.Repositories/Migrations/`, then apply it:

```bash
dotnet ef database update --project ../LiftingCastTracker.Repositories --startup-project .
```

## Troubleshooting

### PostgreSQL Connection Issues

- Verify PostgreSQL is running: `psql -U liftingcast_app -d liftingcast -c "SELECT 1;"`
- Check the connection string in User Secrets: `dotnet user-secrets list`
- Ensure the `liftingcast_app` user has correct permissions

### Frontend Build Issues

- Clear node_modules and reinstall: `rm -rf node_modules && npm install`
- Clear Vite cache: `rm -rf dist && npm run build`

### API Connection Issues from Frontend

- Ensure backend is running on the correct port (default: 7194)
- Check CORS configuration in `Program.cs` matches your frontend URL
- Browser console will show CORS errors if misconfigured

## Next Steps

- Implement LiftingCast competition discovery
- Add search and filtering functionality
- Implement real-time competition synchronization
- Add user authentication if needed
- Deploy to production infrastructure

## See Also

- `AGENTS.md` — Development guidelines
- `docs/architecture.md` — Architectural decisions
- `.github/copilot-instructions.md` — GitHub Copilot task guidelines
