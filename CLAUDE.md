# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore dependencies
dotnet restore

# Run the application (dev)
dotnet run

# Apply database migrations
dotnet ef database update

# Build for production
dotnet publish -c Release -o /app/publish

# Run with Docker
docker-compose up --build
```

API docs (Scalar UI): `http://localhost:8080/scalar/v1`

No test project exists in this repo.

## Architecture

This is a **.NET 10 Web API** for a pet care management system using a **feature-based modular structure**.

### Request Flow

```
HTTP Request → Controller → Service → Repository → AppDbContext (EF Core) → PostgreSQL
```

- **Controllers** (`Modules/*/Api/`) — thin, no business logic, delegate to services
- **Services** (`Modules/*/Domain/`) — business logic, depend on repository interfaces
- **Repositories** (`Modules/*/Repository/`) — data access only; read queries use `AsNoTracking()`
- **DTOs** (`Modules/*/DTOs/`) — separate request/response objects; never expose domain models directly
- **Mappers** (`Modules/*/Mapper/`) — convert between entities and DTOs

### Module Layout

Each feature module (e.g. `UserModule`, `AuthModule`) is self-contained:
```
Modules/<Feature>/
├── Api/           # Controller
├── Domain/        # IService + Service
├── Repository/    # IRepository + Repository
├── DTOs/          # Request + Response DTOs
└── Mapper/        # Entity ↔ DTO mapping
```

New modules should follow this layout and register their services in a dedicated `*ModuleExtensions.cs` file, then call it from `Program.cs`.

### Database

- **PostgreSQL 16** via EF Core (Npgsql provider), code-first
- `AppDbContext` is in `Data/AppDbContext.cs`
- Migrations are in `Migrations/`; the app runs `db.Database.Migrate()` at startup automatically
- Connection string comes from `appsettings.json` or the `DB_PASSWORD` env var in Docker

### Authentication

Dual auth strategy — JWT cookies + Google OAuth:

- **JWT:** Access tokens (15 min) and refresh tokens (7 days) stored as HTTP-only, Secure, SameSite=Strict cookies. Refresh tokens are persisted as SHA256 hashes in the `RefreshTokens` table.
- **Google OAuth:** Web (authorization code flow) and mobile (ID token validation) flows both supported via `Google.Apis.Auth`.
- `AuthMiddleware` validates that the JWT user still exists in the database; results are cached for 3 minutes to reduce DB hits.
- Protected endpoints use `[Authorize]`. Retrieve the current user ID via `ClaimsPrincipalExtensions.GetUserId()`.

JWT and OAuth settings are loaded from `appsettings.json` (`JwtOptions`, `GoogleOAuthOptions`) and can be overridden with env vars (see `.env`).

### Key Dependencies

| Package | Purpose |
|---|---|
| `BCrypt.Net-Next` | Password hashing |
| `Google.Apis.Auth` | Google OAuth token validation |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT bearer scheme |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core PostgreSQL provider |
| `Scalar.AspNetCore` | OpenAPI / interactive API docs |
