# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore dependencies
dotnet restore

# Run chat module tests
dotnet test Modules/ChatModule.Tests/ChatModule.Tests.csproj --configuration Debug

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

Chat module tests are in
`Modules/ChatModule.Tests/ChatModule.Tests.csproj`. They cover the
classifier client and configuration, session replacement, eight-message
history replay and pagination, ownership checks, response mapping, and
relational persistence.

Nutrition module tests are in `Tests/NutritionModule.Tests/`. They cover the
daily summary (day-window math, goal comparison) and the AI feeding analysis
(ownership, weight validation, classifier request building, caller-supplied
overrides, response validation, persistence and the two-analyses-per-pet
retention rule).

Reminder module tests are in `Tests/ReminderModule.Tests/`. They cover the
schedule calculator (interval arithmetic, week parity against the anchor,
month clamping, forward-only weekday alignment, local-vs-UTC date handling),
the recalculation service (completion closing an occurrence, early and
backdated completion, idempotency, end-of-series) and the completion service
(health record filing, type mapping, ownership).

### Reminder recalculation

`Reminder.RecalcStrategy` decides what a completion does to the schedule:

- `Calendar` — the completion is recorded and the calendar is left alone
  (brushing, weighing, activity).
- `FromCompletion` — next trigger is the performed date plus the interval,
  exactly. Forced on the server for Vaccination, ParasiteTreatment,
  Deworming and VetVisit, since the interval is a safety property there.
- `FromCompletionAlignedToWeekday` — same, then moved *forward* to the nearest
  selected weekday so habits like "bathing on Saturdays" survive.

All trigger dates come from `ReminderScheduleCalculator`, counted from
`Reminder.ScheduleAnchorAt` rather than from "now".

Recalculation has one implementation (`IReminderRecalculationService`) and
several callers, all keyed on the performed date so registering the same
completion twice changes nothing. Every type can be completed through
`POST /api/reminders/{id}/complete`; what the completion additionally files
depends on the type:

| Reminder type | `/complete` also files |
|---|---|
| Vaccination, ParasiteTreatment, Deworming, VetVisit | a HealthRecord |
| Grooming, Activity, Medication, anything else | nothing — the closed run is the log |
| Weighing, Feeding | nothing — the measurement is optional, see below |

Weighing and Feeding have a richer path as well: `POST
/api/pets/{petId}/weight-history` and `POST /api/pets/{petId}/feeding-logs`,
both with `reminderId`, close the occurrence *and* store the measurement.
That path is optional on purpose. Requiring it would mean a user answering a
push cannot tick the reminder off in one tap, and the portion they invent to
get past the form is worse than no portion at all — it reaches the classifier
and costs tokens to produce nonsense. A completion with no log stays out of
the nutrition summary and the feeding analysis, both of which read
`FeedingLogs`. A HealthRecord created by hand with a `reminderId` closes an
occurrence the same way, for treatments given with no rule behind them.

Because a completion can arrive twice from two different endpoints — Done on
the push, then the feeding log saved a minute later — the duplicate guard
matches on the **local day** of the performed date rather than the exact
instant. No rule the calculator can express fires twice in one local day, so
same day means same occurrence. Matching exactly would let the second call
through, and it would not merely duplicate history: it materialises the next
occurrence and closes that too, skipping a slot.

Classifier integration documentation:

- `docs/chat-classifier-contract-v1.md`
- `docs/feeding-summary-contract-v1.md`

The classifier exposes four routes: `predict`, `chat`, `wellness` and
`feeding-summary`. Only `chat` and `feeding-summary` are wired up.

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
