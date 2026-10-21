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

This command will:
1. Read the migration from `Migrations/20261021141813_InitialCreate.cs`
2. Connect to PostgreSQL using the configured connection string
3. Create the `competitions` table and schema

### Run the Backend

```bash
cd backend/src/LiftingCastTracker.Controllers
dotnet run
```

The API will be available at `https://localhost:7194` (or `https://localhost:5001` if using HTTP).

Test the API:
- Health check: `GET https://localhost:7194/api/health`
- Competitions list: `GET https://localhost:7194/api/competitions`

## Frontend Setup

### Install Dependencies

```bash
cd frontend
npm install
```

### Configuration

The frontend expects the backend API URL via the `VITE_API_BASE_URL` environment variable.

Copy `.env.example` to `.env.local` and update if needed:

```bash
cd frontend
cp .env.example .env.local
```

Default: `https://localhost:7194`

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
