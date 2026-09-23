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

Activity module tests are in `Tests/ActivityModule.Tests/`. They cover the
CRUD service (ownership, validation, UTC normalisation, persistence), the
repository (date-range and source filters, tracking behaviour), the
controller status mapping, the swappable activity source, the effort maths
(intensity weights, defaulting), the sleep log with its daily cap, and the
patch paths (partial application, clearing, whole-row revalidation, intensity
re-derivation, self-exclusion from the sleep day total).

### Activity logs

`ActivityLog` is one walk, play session or other activity record, with optional
steps, duration, intensity, place and free text. Wellness aggregates these
session logs directly; it does not read the legacy `ActivityDaily` model.

### Activity type, intensity and effort

A session also carries an optional `Type`, `Intensity` and `DurationMinutes`.
Only the last two are arithmetic: `ActivityEffort` turns them into
`ActiveMinutes` (duration × 0.4 / 0.7 / 1.0), which is the figure a wellness
score sums over a day. It is derived on read, never stored, so the weights can
be retuned without a backfill.

`ActivityType` is a label — for history and for chat context — and is
deliberately absent from that sum. Two consequences worth keeping:

- A walk done at a run is not a different type, it is a harder one, so
  obedience class and competition prep share `Training`, and the "we just went
  out and came back" walk shares `Walk` with the one that was all running.
- `Other` plus a `Note` costs nothing, because an activity the enum never
  anticipated still scores exactly like one it did. Rejecting unknown
  activities would buy no accuracy at all.

`Intensity` is intensity rather than difficulty on purpose: how hard a session
was *for this pet* depends on age, weight and condition, which is what the
score computes — asking the user for it would feed the answer back into
itself.

When a duration is present and no intensity was sent, one is derived from the
type (`Walk` → Low, `Run`/`Swimming` → High, everything else → Moderate).
Making the field required would buy a number the user picked to get past the
form; a duration with no intensity would silently weigh nothing.

### Editing logs

Both logs take a `PATCH` on the item route, using the repo's `PatchField<T>`
convention: absent leaves the field alone, `null` clears it, an empty body is a
400. `Source` is not patchable — it records which provider produced the row, not
what the row says, so a hand-edit cannot turn a typed note into a collar
reading.

A patch is validated as a whole row rather than field by field, through the same
`ValidateReading` the create path uses: the rules that matter are about the row
that results, and "a log has to record something" cannot be checked against a
single cleared field. The intensity derivation runs again on the result, so
adding a duration — or clearing an intensity that still has one — fills the
intensity back in rather than leaving the session unweighted.

The sleep day cap is re-checked on every edit with the edited row excluded from
the day's sum (`GetLoggedHoursAsync`'s `excludeSleepLogId`). Without that, a row
would be weighed against the version of itself still in the table and no
correction upwards would ever pass.

### Sleep logs

Sleep is `SleepLog` — `POST /api/pets/{petId}/sleep-logs` — and not an
`ActivityType`. It shares none of `ActivityLog`'s shape (no steps, no place, no
intensity) and the score reads it as a daily total rather than as a session, so
folding it in would make every reader of `ActivityLogs` filter sleep rows out.

A row is a local day plus hours; the day is stored as UTC midnight with the
caller's date taken at face value, since converting would move evening entries
onto the wrong date for half the world. Several rows may share a date — naps,
and eventually a collar feed — so the ceiling is on the day's **sum**, not on
the row. That sum check is what catches the same night entered twice, which is
the mistake a unique index would have caught, without banning nap-by-nap
logging. Wellness sums these rows per day and includes their average in the
classifier request.

Where the numbers come from is behind `IActivitySourceProvider`, picked per
request by `IActivitySourceResolver` from the `source` field. Only
`ManualActivitySourceProvider` is registered, so any other source is a 400
until its integration lands. A collar integration is one more provider
registration in `ActivityModuleExtensions`; `ActivityLogService` does not
change. Validation runs on the `ActivityReading` the provider returned, not on
the request body, so a device feed answers to the same rules as a typed-in
note.

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

A completion that arrives before the occurrence ever fired has to create the
run itself. It is filed on the pending trigger only when the recalculated
trigger moves *past* that instant — "I did Saturday's bath on Thursday". When
the recalculation lands back on the pending instant, the completion was an
extra event rather than the coming occurrence done early, so the run is filed
at the time it happened and the pending trigger survives. Daily rules
confirmed the evening before hit this, and `Calendar` rules always do, since
they leave a future trigger untouched by definition. Filing on the pending
slot there would swallow a notification the user still wants and collide with
the scheduler's own row on the unique `(ReminderId, ScheduledFor)` index.

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
- `docs/wellness-contract-v1.md`

The classifier exposes four routes: `predict`, `chat`, `wellness` and
`feeding-summary`. The backend wires `chat`, `wellness` and `feeding-summary`.

### Wellness test fixture

A wellness score needs thirty days of several unrelated tables at once, so
`scripts/seed-wellness-test.sql` fills them for one existing pet:

```bash
docker compose exec -T db psql -U postgres -d smartPetCareDb \
  -v pet_id=<uuid> -v fill_pet_profile=1 < scripts/seed-wellness-test.sql
```

PowerShell has no input redirection for native commands, so there it is a pipe:

```powershell
Get-Content -Raw scripts/seed-wellness-test.sql |
  docker compose exec -T db psql -U postgres -d smartPetCareDb -v pet_id=<uuid> -v fill_pet_profile=1
```

It covers every dimension the aggregator reads — activity and sleep across the
whole window, two meals a day, weight history, preventive-care events, an active
condition, a medication with its reminder runs (26 of 30 doses taken, so
adherence is a ratio rather than a perfect score) and routine-care reminders
with a deliberate spread, including one never done and a grooming rule older
than the grooming event that has to override it.

Every row carries the marker `[wellness-e2e-seed:v1]` in a free-text column and a
deterministic ID derived from the pet, which is what makes a re-run replace the
previous fixture and leave user-created rows alone.
`scripts/cleanup-wellness-test.sql` removes it again, and takes an optional
`-v assessment_id=<uuid>` to drop the assessment a recalculation stored.

`-v fill_pet_profile=1` is the one thing that writes to the pet itself, and only
where a field is empty: species, breed, birth date, sex, weight and behavioural
notes. Age and weight decide what counts as enough activity for this pet, so a
name-only pet scores against defaults until they are set. The cleanup script
does not revert them — once set they are the pet's own profile.

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

### Backups and rollback

Because migrations are applied automatically at startup, a deploy changes the
code and the schema in one step and neither reverts on its own. The procedure —
what is dumped when, and which of the three rollback cases applies (bad code /
bad migration / bad data) — is in `docs/backup-and-rollback.md`.

```bash
bash scripts/backup-db.sh [label]          # dump, verify, prune, upload to S3
bash scripts/restore-db.sh <dump> [--yes]  # drop, recreate, restore; leaves api stopped
bash scripts/rollback.sh --last            # back to the previously deployed commit
```

Dumps and per-release manifests live in `backups/` on the server (gitignored,
mounted into the db container as `/backups`). The deploy workflow takes one
before every release and records the commit it replaced in
`backups/.last-deployed-sha`.

The image is built in CI, not on the server: the `build` job pushes
`ghcr.io/volodymyrbiletskyi/smart-pet-care-api` tagged with the commit sha, and
the deploy pulls it. So the host never needs the ~3GB sdk image, a failed build
cannot take production down with it, and `rollback.sh` goes back to the tag for
a commit rather than rebuilding it. `docker compose up --build` locally is
unchanged — `api` carries both `image:` and `build:`. The ten most recent
versions are kept in the registry and older ones are deleted after each push,
since nothing there expires on its own.

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
