# Migration Check History

## 2026-07-04

### Context

We reviewed the Entity Framework Core migrations for the Smart Pet Care API after the latest pet profile changes stopped showing red/error states in the editor.

The relevant change was the conversion of these `Pets` columns from `text` to PostgreSQL `text[]`:

- `Allergies`
- `ChronicConditions`
- `BehavioralNotes`

The corresponding model and DTO fields were changed to `List<string>?`.

### Initial Findings

The project had uncommitted migration-related changes:

- `Migrations/20260703185840_ConvertPetProfileFieldsToArrays.cs`
- `Migrations/20260703185840_ConvertPetProfileFieldsToArrays.Designer.cs`
- `Migrations/AppDbContextModelSnapshot.cs`
- `Models/Pet.cs`
- Pet DTO, mapper, and service files

The EF model snapshot matched the current model. `dotnet ef migrations has-pending-model-changes` reported:

```text
No changes have been made to the model since the last migration.
```

This meant EF did not see any missing migration changes.

### Problems Encountered

#### 1. Initial build failed because NuGet restore could not connect

The first `dotnet build` attempt failed while restoring packages from NuGet:

```text
error NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json.
The SSL connection could not be established.
Authentication failed.
No credentials are available in the security package.
```

This was an environment/network/credential issue, not a migration code issue.

After allowing the build command to access NuGet, the project built successfully.

#### 2. EF generated unsafe PostgreSQL conversion SQL

The generated SQL for the latest migration initially looked like this:

```sql
ALTER TABLE "Pets" ALTER COLUMN "ChronicConditions" TYPE text[];
ALTER TABLE "Pets" ALTER COLUMN "BehavioralNotes" TYPE text[];
ALTER TABLE "Pets" ALTER COLUMN "Allergies" TYPE text[];
```

This was risky because PostgreSQL generally needs an explicit `USING` clause when converting existing `text` values into `text[]`.

On a database with existing rows, that migration could fail during application startup or during `dotnet ef database update`.

#### 3. Debug build became locked by a running dotnet process

Later, `dotnet build --no-restore` failed with:

```text
CSC : error CS2012: Cannot open 'obj\Debug\net10.0\smart-pet-care-api.dll' for writing
Access to the path is denied.
```

There were active `dotnet.exe` processes, so the Debug output was likely locked by a running API/build/EF process.

This was not caused by the migration itself.

#### 4. Parallel EF commands caused a BuildHost race

Running EF commands in parallel caused:

```text
The file 'bin\Release\net10.0\BuildHost-net472\tr\System.CommandLine.resources.dll' already exists.
```

This was a local EF tooling race from concurrent commands, not a database or migration logic problem.

### Fix Applied

The migration file `Migrations/20260703185840_ConvertPetProfileFieldsToArrays.cs` was updated to use explicit SQL with `USING CASE`.

For each converted column:

- `NULL` remains `NULL`
- empty or whitespace-only text becomes an empty `text[]`
- non-empty text becomes a one-element `text[]`

Example:

```sql
ALTER TABLE "Pets"
ALTER COLUMN "Allergies" TYPE text[]
USING CASE
    WHEN "Allergies" IS NULL THEN NULL
    WHEN btrim("Allergies") = '' THEN ARRAY[]::text[]
    ELSE ARRAY["Allergies"]
END;
```

The `Down` migration was also changed to explicitly convert arrays back to text with:

```sql
USING array_to_string("Allergies", E'\n');
```

### Verification Results

The Release build passed:

```text
Build succeeded.
```

The generated migration SQL now includes the required PostgreSQL `USING CASE` clauses.

The EF model and snapshot were confirmed to be aligned before the migration-file-only SQL fix. The SQL fix does not change the EF model shape.

### Remaining Warnings

The build still reported unrelated warnings:

```text
NU1903: Package 'Microsoft.OpenApi' 2.0.0 has a known high severity vulnerability
CS8604: Possible null reference argument for parameter 'code' in AuthController.cs
```

These warnings are not caused by the migration change.

### Current Risk Assessment

For an empty database, the risk of this migration damaging data is very low because there is no existing data to convert.

The remaining risks are operational:

- connecting to the wrong database
- missing database permissions
- PostgreSQL not being ready
- multiple API instances attempting automatic migrations at the same time
- application startup failing if `Database.Migrate()` runs against an unavailable or misconfigured database

### Recommended Preparation Before Running Migration

Before running `dotnet ef database update`, verify:

- the exact connection string and target database name
- PostgreSQL container/server is running and healthy
- the database user has permission to create and alter schema objects
- only one migration runner/API instance will apply migrations
- the generated SQL is reviewed before applying
- the database is empty if that is the assumption
- a backup/snapshot exists if the database is not empty

### Outcome

The migration issue found during review was fixed.

The latest migration is now safer for PostgreSQL because type conversion from `text` to `text[]` is explicit.

For an empty database, applying the migration should be fast and low risk, assuming the connection string and database permissions are correct.

## 2026-07-04 Controlled Migration Run

### Preparation

Only the Docker Compose `db` service was started. The API service was not started, so the migration was not applied through `Database.Migrate()` on API startup.

PostgreSQL health checks passed:

- the `db` container was `healthy`
- `pg_isready` reported that PostgreSQL was accepting connections

### Important Correction

The first table-existence check used `to_regclass('public.Pets')`, which did not account for EF's case-sensitive quoted table name `"Pets"`.

Because of that, the database initially looked empty, but later verification showed it already had:

- an existing EF schema
- `4` rows in `"Pets"`
- an existing `"__EFMigrationsHistory"` table
- an existing database-side migration entry for `20260627134048_AddPetPhotoPublicId`

That migration file is not present in the current local `Migrations/` source folder, so the database currently has schema/history drift compared with the checked-out source.

### Migration Application

An idempotent SQL script was generated and applied through `psql` inside the PostgreSQL container with `ON_ERROR_STOP=1`.

The SQL execution completed successfully.

The latest migration applied was:

```text
20260703185840_ConvertPetProfileFieldsToArrays
```

### Post-Migration Verification

The EF migration history table contained `8` rows:

```text
20260506154852_initialMigration
20260518153038_RefactorReminderSchedule
20260603203431_AddUserTermsFields
20260607113947_AddPetProfileFields
20260618132607_AddAvatarUrlAndCleanupUserFields
20260625103214_AddUserAvatarData
20260627134048_AddPetPhotoPublicId
20260703185840_ConvertPetProfileFieldsToArrays
```

The `"Pets"` table had `4` rows after migration.

The converted columns were verified as PostgreSQL arrays:

```text
Allergies: ARRAY / _text
BehavioralNotes: ARRAY / _text
ChronicConditions: ARRAY / _text
```

Data-shape verification showed:

- `Allergies` non-null rows: `0`
- `ChronicConditions` non-null rows: `0`
- `BehavioralNotes` non-null rows: `3`
- rows passing array-column sanity check: `4`

### Remaining Drift

The database has a nullable `"PhotoPublicId"` column in `"Pets"`:

```text
PhotoPublicId | nullable | text
```

The current local source model and migration files do not include this migration file.

This is not immediately breaking because the column is nullable and EF can ignore unmapped extra columns, but it should be reconciled before treating the migration history as production-clean.

### Tooling Issues During Verification

Some `dotnet ef` commands from the host failed because the host-side configuration had an empty connection string or because the local environment could not access NuGet metadata:

```text
Unable to load the service index for source https://api.nuget.org/v3/index.json
```

The actual database verification was therefore done directly with `psql` inside the PostgreSQL container.

## 2026-07-04 PhotoPublicId Reconciliation

### Reason

`PhotoPublicId` is required for Cloudinary lifecycle management. `PhotoUrl` is used for displaying the image, while `PhotoPublicId` is needed by the backend to update, replace, or delete the Cloudinary asset.

The database already had a nullable `"Pets"."PhotoPublicId"` column from an older migration history entry:

```text
20260627134048_AddPetPhotoPublicId
```

However, that migration file was not present in the current local `Migrations/` source folder, and the active source model did not include the property.

### Source Changes

`PhotoPublicId` was restored into the active pet flow:

- `Models/Pet.cs`
- `Modules/PetModule/DTOs/CreatePetDto.cs`
- `Modules/PetModule/DTOs/UpdatePetDto.cs`
- `Modules/PetModule/DTOs/PetResponseDto.cs`
- `Modules/PetModule/Mapper/PetMapper.cs`
- `Modules/PetModule/Domain/PetService.cs`
- `Migrations/AppDbContextModelSnapshot.cs`

### Migration Added

A new migration was added:

```text
20260704113000_EnsurePetPhotoPublicId
```

The migration uses guarded SQL:

- if `"PhotoPublicId"` does not exist, it adds the column
- if the column already exists, it does nothing

This avoids a `column already exists` failure on the current database while still guaranteeing the column on clean future databases.

### Application Result

The migration was applied to the Docker PostgreSQL database using controlled SQL through `psql`.

Verification showed:

```text
photo_public_id_column | PhotoPublicId:YES:text
ensure_migration_history | 1
latest_migrations | 20260704113000_EnsurePetPhotoPublicId
```

The Release build passed after the source changes.

### Outcome

The project source now explicitly contains `PhotoPublicId`, and the current database has a migration history entry for the new source-level reconciliation migration.

There is still an older database-only history entry `20260627134048_AddPetPhotoPublicId`, but the active source now has a forward migration that makes future databases converge on the required Cloudinary column.

### Follow-up Review Fix

After review, `UpdatePetDto.PhotoUrl` and `UpdatePetDto.PhotoPublicId` were changed to `PatchField<string?>`.

This makes PATCH semantics explicit:

- omitted photo fields leave existing values unchanged
- `"photoUrl": null` clears `PhotoUrl`
- `"photoPublicId": null` clears `PhotoPublicId`
- non-null values replace the stored values

This prevents stale Cloudinary public ids after a photo is removed or cleared.
