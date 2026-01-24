# WorkOps.Api

REST API built with ASP.NET Core 8.0, Entity Framework Core, and SQL Server.

## Setup

### Prerequisites
- .NET 8 SDK
- Docker Desktop

### Database

Start SQL Server:
```bash
docker compose -f infra/docker-compose.yml up -d
```

Run migrations:
```bash
cd WorkOps.Api/WorkOps.Api
dotnet ef database update
```

### Configuration

Set the connection string. Get the SQL Server password from `infra/docker-compose.yml` (MSSQL_SA_PASSWORD).

**PowerShell:**
```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=WorkOpsDb;User Id=sa;Password=<password-from-docker-compose>;TrustServerCertificate=True;"
```

**Visual Studio:** Replace `CHANGE_ME` in `Properties/launchSettings.json` with the password from docker-compose.yml, or use User Secrets.

### Run

```bash
dotnet run
```

API runs on `https://localhost:7239` (or check console output).

## Endpoints

- `GET /health` - Health check
- `GET /api/projects` - List projects
- `GET /swagger` - Swagger UI (dev only)

## Tech Stack

- ASP.NET Core 8.0
- Entity Framework Core 8.0
- SQL Server 2019 (Docker)
- Swagger/OpenAPI
