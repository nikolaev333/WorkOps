# WorkOps.Api

[![CI](https://github.com/OWNER/REPO/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/REPO/actions/workflows/ci.yml)

REST API built with ASP.NET Core 8.0, Entity Framework Core, and SQL Server.

## Setup

### Prerequisites
- .NET 8 SDK
- Docker Desktop (for local SQL Server)

### Start SQL Server (Docker)

```bash
docker compose -f infra/docker-compose.yml up -d
```

### Configure connection string

Do **not** put the DB password in `appsettings.json` or `appsettings.Development.json`. Use one of:

**User Secrets (recommended for local dev):**

```bash
cd WorkOps.Api/WorkOps.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=WorkOpsDb;User Id=sa;Password=YOUR_SA_PASSWORD;TrustServerCertificate=True;"
```

Use the `MSSQL_SA_PASSWORD` from `infra/docker-compose.yml` as `YOUR_SA_PASSWORD`.

**Environment variable:**

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=WorkOpsDb;User Id=sa;Password=YOUR_SA_PASSWORD;TrustServerCertificate=True;"
```

### Run migrations

```bash
cd WorkOps.Api/WorkOps.Api
dotnet ef database update
```

### Run

```bash
cd WorkOps.Api/WorkOps.Api
dotnet run
```

API runs on `https://localhost:7239` (or check console output).

## Health endpoints

- `GET /health/live` – liveness (always healthy)
- `GET /health/ready` – readiness (includes DB check); returns JSON with `status`, `totalDuration`, `checks`

## Endpoints

- `GET /swagger` – Swagger UI (dev only)

### Projects API

- `GET /api/projects` – List (sorted by CreatedAtUtc desc)
- `GET /api/projects/{id}` – Get by id
- `POST /api/projects` – Create (body: `{"name":"..."}`)
- `PUT /api/projects/{id}` – Update (body: `{"name":"..."}`)
- `DELETE /api/projects/{id}` – Delete

Example (base URL `https://localhost:7239` or from `dotnet run`):

```bash
# list
curl -s https://localhost:7239/api/projects -k

# create (use the returned id for update/delete)
curl -s -X POST https://localhost:7239/api/projects -k -H "Content-Type: application/json" -d "{\"name\":\"My Project\"}"

# update (replace {id} with real Guid)
curl -s -X PUT https://localhost:7239/api/projects/{id} -k -H "Content-Type: application/json" -d "{\"name\":\"Updated Name\"}"

# delete
curl -s -X DELETE https://localhost:7239/api/projects/{id} -k
```

## Running tests

Tests do **not** require SQL Server or Docker. They use an in-memory DB.

```bash
dotnet test WorkOps.Api/WorkOps.Api.sln --configuration Release
```

## Tech Stack

- ASP.NET Core 8.0
- Entity Framework Core 8.0
- SQL Server 2019 (Docker)
- Swagger/OpenAPI

## CI

The [`.github/workflows/ci.yml`](.github/workflows/ci.yml) workflow runs on push and pull_request: restore, build, and test. Replace `OWNER/REPO` in the badge above with your GitHub repo.
