# WorkOps.Api

[![CI](https://github.com/OWNER/REPO/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/REPO/actions/workflows/ci.yml)

Multi-tenant project management API with RBAC (Admin/Manager/Member), JWT auth, and CRUD for clients, projects, and tasks.

## Setup

1. Start SQL Server: `docker compose -f infra/docker-compose.yml up -d`
2. Configure secrets:
   ```bash
   cd WorkOps.Api/WorkOps.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=WorkOpsDb;User Id=sa;Password=YOUR_SA_PASSWORD;TrustServerCertificate=True;"
   dotnet user-secrets set "Jwt:Key" "your-secret-at-least-32-characters-long"
   ```
3. Migrations: `dotnet ef database update`
4. Run: `dotnet run`

API: `https://localhost:7239` | Swagger: `/swagger`

## Endpoints

### Auth
- `POST /api/auth/register` - `{ email, password, confirmPassword }`
- `POST /api/auth/login` - Returns `{ accessToken }`
- `GET /api/auth/me` - Current user

### Organizations
- `POST /api/orgs` - Create org (you become Admin)
- `GET /api/orgs` - List your orgs
- `GET /api/orgs/{orgId}/members` - List members (OrgManager+)
- `POST /api/orgs/{orgId}/members` - Add member (OrgAdmin)
- `DELETE /api/orgs/{orgId}/members/{userId}` - Remove member (OrgAdmin)

### Clients (`/api/orgs/{orgId}/clients`)
- `GET` - List (Member+) | `GET /{id}` - Get (Member+)
- `POST` - Create `{ name, email?, phone? }` (Manager+)
- `PUT /{id}` - Update (Manager+) | `DELETE /{id}` - Delete (Manager+)

**Pagination:** `?page=1&pageSize=20&q=search`

### Projects (`/api/orgs/{orgId}/projects`)
- `GET` - List (Member+) | `GET /{id}` - Get with `rowVersion` (Member+)
- `POST` - Create `{ name, clientId? }` (Manager+)
- `PUT /{id}` - Update with `rowVersion` (Manager+) | `DELETE /{id}` - Delete (Manager+)

**Filters:** `?status=1&clientId={guid}&q=search` | **Concurrency:** Requires `rowVersion` → 409 on mismatch

### Tasks (`/api/orgs/{orgId}/projects/{projectId}/tasks`)
- `GET` - List (Member+) | `GET /{id}` - Get (Member+)
- `POST` - Create `{ title, description?, priority, assigneeUserId?, dueDateUtc? }` (Manager+)
- `PUT /{id}` - Update (Manager+) | `DELETE /{id}` - Delete (Manager+)

**Filters:** `?status=1&assigneeId={userId}`

## Tests

```bash
dotnet test WorkOps.Api/WorkOps.Api.sln -c Release
```

## Tech

ASP.NET Core 8 · EF Core 8 · SQL Server · Identity + JWT · Swagger
