\set ON_ERROR_STOP on

-- Usage with a locally installed psql:
--   psql "<postgres-connection-uri>" -v pet_id=00000000-0000-0000-0000-000000000000 -f scripts/seed-wellness-test.sql
--
-- Usage with the repository's Docker Compose database (PowerShell):
--   Get-Content -Raw scripts/seed-wellness-test.sql |
--     docker compose exec -T db psql -U postgres -d smartPetCareDb -v pet_id=00000000-0000-0000-0000-000000000000
--
-- The script never updates the pet or user-created records. Before inserting,
-- it replaces only this script's previously tagged fixture for the same pet.

\if :{?pet_id}
\else
\echo 'Missing pet_id. Pass it with: -v pet_id=<uuid>'
\quit
\endif

BEGIN;

CREATE TEMP TABLE wellness_seed_context ON COMMIT DROP AS
SELECT
    "Id" AS pet_id,
    COALESCE("WeightKg", 10.0) AS weight_kg,
    (now() AT TIME ZONE 'UTC')::date AS seed_date
FROM "Pets"
WHERE "Id" = :'pet_id'::uuid;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM wellness_seed_context) THEN
        RAISE EXCEPTION 'The selected pet does not exist';
    END IF;
END
$$;

-- Refresh a previous run without touching untagged records.
DELETE FROM "PetEvents"
WHERE "PetId" = :'pet_id'::uuid
  AND "Description" = '[wellness-e2e-seed:v1]';

DELETE FROM "PetWeightLogs"
WHERE "PetId" = :'pet_id'::uuid
  AND "Notes" = '[wellness-e2e-seed:v1]';

DELETE FROM "FeedingLogs"
WHERE "PetId" = :'pet_id'::uuid
  AND "Description" = '[wellness-e2e-seed:v1]';

DELETE FROM "SleepLogs"
WHERE "PetId" = :'pet_id'::uuid
  AND "Note" = '[wellness-e2e-seed:v1]';

DELETE FROM "ActivityLogs"
WHERE "PetId" = :'pet_id'::uuid
  AND "Note" = '[wellness-e2e-seed:v1]';

-- Activity: one high-intensity session for each day in the complete 30-day
-- wellness evaluation window. Source=0 is ActivitySource.Manual. ActiveMinutes
-- is derived by the backend from DurationMinutes and Intensity rather than stored.
INSERT INTO "ActivityLogs"
    ("Id", "PetId", "RecordedAt", "Steps", "Type", "Intensity", "DurationMinutes", "Location", "Note", "Source", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:activity:' || day_offset)::uuid,
    context.pet_id,
    ((context.seed_date - day_offset) + time '12:00') AT TIME ZONE 'UTC',
    8000 + (day_offset % 5) * 400,
    4,
    2,
    55 + (day_offset % 4) * 5,
    NULL,
    '[wellness-e2e-seed:v1]',
    0,
    now()
FROM wellness_seed_context AS context
CROSS JOIN generate_series(0, 29) AS days(day_offset)
ON CONFLICT ("Id") DO NOTHING;

-- Sleep: one manual daily total for the same window.
INSERT INTO "SleepLogs"
    ("Id", "PetId", "SleepDate", "Hours", "Note", "Source", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:sleep:' || day_offset)::uuid,
    context.pet_id,
    (context.seed_date - day_offset)::timestamp AT TIME ZONE 'UTC',
    9 + (day_offset % 3) * 0.5,
    '[wellness-e2e-seed:v1]',
    0,
    now()
FROM wellness_seed_context AS context
CROSS JOIN generate_series(0, 29) AS days(day_offset)
ON CONFLICT ("Id") DO NOTHING;

-- Feeding: two meals per day throughout the same 30-day window. FoodType 0/1
-- are DryFood/WetFood; PortionUnit=0 is Gram.
INSERT INTO "FeedingLogs"
    ("Id", "PetId", "FedAt", "FoodType", "FoodName", "PortionAmount", "PortionUnit", "ApproxCalories", "Description", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:feeding:' || day_offset || ':' || meal)::uuid,
    context.pet_id,
    ((context.seed_date - day_offset)
        + CASE meal WHEN 0 THEN time '08:00' ELSE time '18:00' END) AT TIME ZONE 'UTC',
    meal,
    CASE meal WHEN 0 THEN 'Wellness test dry food' ELSE 'Wellness test wet food' END,
    CASE meal WHEN 0 THEN 120 ELSE 180 END,
    0,
    300,
    '[wellness-e2e-seed:v1]',
    now()
FROM wellness_seed_context AS context
CROSS JOIN generate_series(0, 29) AS days(day_offset)
CROSS JOIN generate_series(0, 1) AS meals(meal)
ON CONFLICT ("Id") DO NOTHING;

-- A small stable weight history. Existing pet data is not modified.
INSERT INTO "PetWeightLogs"
    ("Id", "PetId", "WeightKg", "MeasuredAt", "Notes", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:weight:' || day_offset)::uuid,
    context.pet_id,
    LEAST(230, GREATEST(0.1,
        context.weight_kg + CASE day_offset WHEN 21 THEN -0.2 WHEN 14 THEN -0.1 ELSE 0 END)),
    ((context.seed_date - day_offset) + time '12:00') AT TIME ZONE 'UTC',
    '[wellness-e2e-seed:v1]',
    now()
FROM wellness_seed_context AS context
CROSS JOIN (VALUES (0), (7), (14), (21)) AS days(day_offset)
ON CONFLICT DO NOTHING;

-- Completed preventive-care records inside the 12-month lookup window.
-- Types 0/1/2 are VetVisit/Grooming/Vaccination; Status=1 is Completed.
INSERT INTO "PetEvents"
    ("Id", "PetId", "Type", "Title", "Description", "ScheduledAt", "Status", "Priority", "IsSystemGenerated", "SourceType", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:event:' || event_key)::uuid,
    context.pet_id,
    event_type,
    title,
    '[wellness-e2e-seed:v1]',
    ((context.seed_date - day_offset) + time '10:00') AT TIME ZONE 'UTC',
    1,
    1,
    false,
    0,
    now()
FROM wellness_seed_context AS context
CROSS JOIN (VALUES
    ('vet-visit', 0, 'Wellness test annual checkup', 20),
    ('grooming', 1, 'Wellness test grooming', 10),
    ('vaccination', 2, 'Wellness test vaccination', 150)
) AS events(event_key, event_type, title, day_offset)
ON CONFLICT ("Id") DO NOTHING;

COMMIT;

SELECT 'ActivityLogs' AS table_name, count(*) AS fixture_rows
FROM "ActivityLogs"
WHERE "PetId" = :'pet_id'::uuid AND "Note" = '[wellness-e2e-seed:v1]'
UNION ALL
SELECT 'SleepLogs', count(*)
FROM "SleepLogs"
WHERE "PetId" = :'pet_id'::uuid AND "Note" = '[wellness-e2e-seed:v1]'
UNION ALL
SELECT 'FeedingLogs', count(*)
FROM "FeedingLogs"
WHERE "PetId" = :'pet_id'::uuid AND "Description" = '[wellness-e2e-seed:v1]'
UNION ALL
SELECT 'PetWeightLogs', count(*)
FROM "PetWeightLogs"
WHERE "PetId" = :'pet_id'::uuid AND "Notes" = '[wellness-e2e-seed:v1]'
UNION ALL
SELECT 'PetEvents', count(*)
FROM "PetEvents"
WHERE "PetId" = :'pet_id'::uuid AND "Description" = '[wellness-e2e-seed:v1]';
