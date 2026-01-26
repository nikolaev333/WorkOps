# WorkOps.Api

[![CI](https://github.com/OWNER/REPO/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/REPO/actions/workflows/ci.yml)

REST API with ASP.NET Core 8, EF Core, and SQL Server.

## Setup

**Prerequisites:** .NET 8 SDK, Docker (for local SQL Server)

### 1. Start SQL Server

```bash
docker compose -f infra/docker-compose.yml up -d
```

### 2. Configure secrets

Don’t store DB password or JWT key in `appsettings.json`. Use **User Secrets** or **env vars**.

**User Secrets:**

```bash
cd WorkOps.Api/WorkOps.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=WorkOpsDb;User Id=sa;Password=YOUR_SA_PASSWORD;TrustServerCertificate=True;"
dotnet user-secrets set "Jwt:Key" "your-secret-at-least-32-characters-long"
```

Use `MSSQL_SA_PASSWORD` from `infra/docker-compose.yml` as `YOUR_SA_PASSWORD`.

**Or env vars (e.g. PowerShell):**

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=WorkOpsDb;User Id=sa;Password=YOUR_SA_PASSWORD;TrustServerCertificate=True;"
$env:Jwt__Key = "your-secret-at-least-32-characters-long"
```

JWT issuer, audience, and expiry have defaults in `appsettings.json`; override if needed.

### 3. Migrations

```bash
cd WorkOps.Api/WorkOps.Api
dotnet ef database update
```

If you get "Jwt:Key not found" or "Connection string not found", set the env vars above first, or run:

```bash
$env:Jwt__Key = "dummy-at-least-32-chars-for-ef-tools"
dotnet ef database update --connection "Server=localhost,1433;Database=WorkOpsDb;User Id=sa;Password=YOUR_SA_PASSWORD;TrustServerCertificate=True;"
```

### 4. Run

```bash
cd WorkOps.Api/WorkOps.Api
dotnet run
```

API: `https://localhost:7239` (or see console).

## Endpoints

- **Health:** `GET /health/live`, `GET /health/ready` (JSON: `status`, `checks`)
- **Swagger:** `GET /swagger` (dev only)

### Auth

| Method | Path | Description |
|--------|------|-------------|
| POST | /api/auth/register | `{ email, password, confirmPassword }` → `{ userId, email }` |
| POST | /api/auth/login | `{ email, password }` → `{ accessToken, expiresAtUtc }` |
| GET | /api/auth/me | `Authorization: Bearer <token>` → `{ userId, email }` |

Password: 8+ chars, digit, upper, lower, one special char; `confirmPassword` must match.

**Example:**

```bash
curl -s -X POST https://localhost:7239/api/auth/register -k -H "Content-Type: application/json" -d "{\"email\":\"you@example.com\",\"password\":\"YourPass123!\",\"confirmPassword\":\"YourPass123!\"}"
curl -s -X POST https://localhost:7239/api/auth/login -k -H "Content-Type: application/json" -d "{\"email\":\"you@example.com\",\"password\":\"YourPass123!\"}"
curl -s https://localhost:7239/api/auth/me -k -H "Authorization: Bearer TOKEN"
```

In Swagger: **Authorize** → enter `Bearer <token>` or the token → **Authorize**.

### Organizations (multi-tenant workspaces)

An **organization** is a workspace. Only members can access org-scoped endpoints. If you are not a member, the API returns **404** (org existence is not disclosed). All org endpoints require `Authorization: Bearer <token>`.

**Roles:** Admin (full control, add/remove members, delete) · Manager (list members, manage content) · Member (read, limited actions). Insufficient role → **403**.

| Method | Path | Policy | Description |
|--------|------|--------|-------------|
| POST | /api/orgs | – | Create org; you become **Admin** |
| GET | /api/orgs | – | List orgs you belong to |
| GET | /api/orgs/{orgId}/members | OrgManager | List members |
| POST | /api/orgs/{orgId}/members | OrgAdmin | Add member `{ "email", "role" }` |
| DELETE | /api/orgs/{orgId}/members/{userId} | OrgAdmin | Remove member (cannot remove last Admin) |

**Create org:** `POST /api/orgs` with `{ "name": "My Workspace" }` (required, 1–200 chars). Returns `{ id, name, createdAtUtc }`.

**Add member:** `POST /api/orgs/{orgId}/members` with `{ "email": "user@example.com", "role": 2 }` (role: 1=Admin, 2=Manager, 3=Member). Fails with 400 if user not found, 409 if already a member.

**Convention:** Org-scoped routes use `/api/orgs/{orgId}/...`. Non-members get **404** for org-scoped resources.

### Projects

| Method | Path | Body |
|--------|------|------|
| GET | /api/projects | – |
| GET | /api/projects/{id} | – |
| POST | /api/projects | `{ "name": "..." }` |
| PUT | /api/projects/{id} | `{ "name": "..." }` |
| DELETE | /api/projects/{id} | – |

## Tests

No SQL Server or Docker needed (in-memory DB).

```bash
dotnet test WorkOps.Api/WorkOps.Api.sln -c Release
```

## Tech

ASP.NET Core 8 · EF Core 8 · SQL Server · Identity + JWT · Swagger

## CI

[`ci.yml`](.github/workflows/ci.yml): restore, build, test on push/PR. Edit the badge URL with your `OWNER/REPO`.
