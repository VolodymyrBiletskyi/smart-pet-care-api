\set ON_ERROR_STOP on

-- Usage with a locally installed psql:
--   psql "<postgres-connection-uri>" -v pet_id=00000000-0000-0000-0000-000000000000 -f scripts/seed-wellness-test.sql
--
-- Usage with the repository's Docker Compose database (PowerShell):
--   Get-Content -Raw scripts/seed-wellness-test.sql |
--     docker compose exec -T db psql -U postgres -d smartPetCareDb -v pet_id=00000000-0000-0000-0000-000000000000
--
-- The script never updates user-created records. Before inserting, it replaces
-- only this script's previously tagged fixture for the same pet.
--
-- The pet row itself is left alone unless you ask for it:
--   -v fill_pet_profile=1
-- fills species, breed, birth date, sex, weight and behavioural notes, and only
-- where the pet has none. Those feed the classifier's baseline expectations
-- (age and weight decide what counts as enough activity), so a pet created with
-- name only scores against defaults until they are set.

\if :{?pet_id}
\else
\echo 'Missing pet_id. Pass it with: -v pet_id=<uuid>'
\quit
\endif

BEGIN;

\if :{?fill_pet_profile}
UPDATE "Pets"
SET "Species" = CASE WHEN "Species" IN ('Unknown', '') THEN 'Dog' ELSE "Species" END,
    "Breed" = COALESCE("Breed", 'Labrador Retriever'),
    "BirthDate" = COALESCE("BirthDate", (now() AT TIME ZONE 'UTC')::date - 1460),
    "Sex" = CASE WHEN "Sex" = 0 THEN 1 ELSE "Sex" END,
    "WeightKg" = COALESCE("WeightKg", 24.0),
    "BehavioralNotes" = COALESCE("BehavioralNotes",
        ARRAY['Wellness test: calm at home, eager on walks']),
    "UpdatedAt" = now()
WHERE "Id" = :'pet_id'::uuid;
\endif

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

-- Refresh a previous run without touching untagged records. Runs go before the
-- reminders they hang off.
DELETE FROM "ReminderRun"
WHERE "ReminderId" IN (
    SELECT "Id" FROM "Reminders"
    WHERE "PetId" = :'pet_id'::uuid
      AND "Description" = '[wellness-e2e-seed:v1]');

DELETE FROM "Reminders"
WHERE "PetId" = :'pet_id'::uuid
  AND "Description" = '[wellness-e2e-seed:v1]';

DELETE FROM "PetMedications"
WHERE "PetId" = :'pet_id'::uuid
  AND "Instructions" = '[wellness-e2e-seed:v1]';

DELETE FROM "PetConditions"
WHERE "PetId" = :'pet_id'::uuid
  AND "Description" = '[wellness-e2e-seed:v1]';

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

-- One active condition, so the response exercises the condition cap instead of
-- the unconstrained path. Type=0 is ConditionType.Chronic.
INSERT INTO "PetConditions"
    ("Id", "PetId", "IsActive", "Type", "Name", "Allergen", "Description", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:condition')::uuid,
    context.pet_id,
    true,
    0,
    'Wellness test hip dysplasia',
    NULL,
    '[wellness-e2e-seed:v1]',
    now()
FROM wellness_seed_context AS context
ON CONFLICT ("Id") DO NOTHING;

-- An open-ended daily medication. MedicationFrequency=1 is Frequency.Daily.
INSERT INTO "PetMedications"
    ("Id", "PetId", "Name", "Dosage", "Instructions", "MedicationFrequency", "Interval",
     "StartDate", "EndDate", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:medication')::uuid,
    context.pet_id,
    'Wellness test joint supplement',
    '1 tablet',
    '[wellness-e2e-seed:v1]',
    1,
    1,
    ((context.seed_date - 60) + time '09:00') AT TIME ZONE 'UTC',
    NULL,
    now()
FROM wellness_seed_context AS context
ON CONFLICT ("Id") DO NOTHING;

-- Adherence is read from reminder runs, not from the medication row, so the
-- medication needs a rule pointing back at it: SourceType=1 is
-- SourceType.Medication and SourceId is the PetMedications row.
-- Type=2 is ReminderType.Medication, RepeatType=3 is Daily, Status=0 is Active.
INSERT INTO "Reminders"
    ("Id", "PetId", "Title", "Description", "Type", "Status", "RepeatType", "IntervalN",
     "RecalcStrategy", "Days", "Date", "TimeOfDay", "UtcOffsetMinutes", "StartAt",
     "NextTriggerAt", "EndAt", "ScheduleAnchorAt", "OverdueSince", "LastCompletedAt",
     "IsSystemGenerated", "SourceType", "SourceId", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:reminder:medication')::uuid,
    context.pet_id,
    'Wellness test joint supplement',
    '[wellness-e2e-seed:v1]',
    2,
    0,
    3,
    1,
    0,
    '{}'::integer[],
    NULL,
    interval '9 hours',
    0,
    ((context.seed_date - 60) + time '09:00') AT TIME ZONE 'UTC',
    ((context.seed_date + 1) + time '09:00') AT TIME ZONE 'UTC',
    NULL,
    ((context.seed_date - 60) + time '09:00') AT TIME ZONE 'UTC',
    NULL,
    (context.seed_date + time '09:00') AT TIME ZONE 'UTC',
    true,
    1,
    md5(context.pet_id::text || ':wellness-e2e-v1:medication')::uuid,
    now()
FROM wellness_seed_context AS context
ON CONFLICT ("Id") DO NOTHING;

-- One dose per day across the evaluation window, four of them missed, so
-- CompletedDoses/ScheduledDoses is a real ratio rather than a perfect score.
-- Status 5/4 are ReminderRunStatus.Completed/Missed.
INSERT INTO "ReminderRun"
    ("Id", "ReminderId", "ScheduledFor", "SentAt", "CompletedAt", "PerformedAt", "Type",
     "Note", "Status", "Channel", "DeliveryMeta", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:medrun:' || day_offset)::uuid,
    md5(context.pet_id::text || ':wellness-e2e-v1:reminder:medication')::uuid,
    ((context.seed_date - day_offset) + time '09:00') AT TIME ZONE 'UTC',
    ((context.seed_date - day_offset) + time '09:00') AT TIME ZONE 'UTC',
    CASE WHEN day_offset % 7 = 3 THEN NULL
         ELSE ((context.seed_date - day_offset) + time '09:20') AT TIME ZONE 'UTC' END,
    CASE WHEN day_offset % 7 = 3 THEN NULL
         ELSE ((context.seed_date - day_offset) + time '09:20') AT TIME ZONE 'UTC' END,
    2,
    '[wellness-e2e-seed:v1]',
    CASE WHEN day_offset % 7 = 3 THEN 4 ELSE 5 END,
    'push',
    '{}'::jsonb,
    now()
FROM wellness_seed_context AS context
CROSS JOIN generate_series(0, 29) AS days(day_offset)
ON CONFLICT ("Id") DO NOTHING;

-- Routine care is read off Reminder.LastCompletedAt, one entry per type. The
-- spread is deliberate: PawCare has never been done (null branch), NailTrimming
-- is overdue, and the Grooming rule is older than the completed grooming event
-- above, which is the case where the event has to win.
-- Types 6/9/10/11/12/13/14 are Grooming/Bathing/Brushing/EarCleaning/
-- NailTrimming/PawCare/TeethCleaning. RepeatType=0 is Weekly, Days={6} is Saturday,
-- RecalcStrategy=2 is FromCompletionAlignedToWeekday.
INSERT INTO "Reminders"
    ("Id", "PetId", "Title", "Description", "Type", "Status", "RepeatType", "IntervalN",
     "RecalcStrategy", "Days", "Date", "TimeOfDay", "UtcOffsetMinutes", "StartAt",
     "NextTriggerAt", "EndAt", "ScheduleAnchorAt", "OverdueSince", "LastCompletedAt",
     "IsSystemGenerated", "SourceType", "SourceId", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:reminder:' || care_key)::uuid,
    context.pet_id,
    title,
    '[wellness-e2e-seed:v1]',
    care_type,
    0,
    0,
    1,
    2,
    '{6}'::integer[],
    NULL,
    interval '10 hours',
    0,
    ((context.seed_date - 90) + time '10:00') AT TIME ZONE 'UTC',
    ((context.seed_date + 1) + time '10:00') AT TIME ZONE 'UTC',
    NULL,
    COALESCE(
        ((context.seed_date - days_ago) + time '10:00') AT TIME ZONE 'UTC',
        ((context.seed_date - 90) + time '10:00') AT TIME ZONE 'UTC'),
    NULL,
    ((context.seed_date - days_ago) + time '10:00') AT TIME ZONE 'UTC',
    false,
    0,
    NULL,
    now()
FROM wellness_seed_context AS context
CROSS JOIN (VALUES
    ('bathing', 9, 'Wellness test bathing', 12),
    ('brushing', 10, 'Wellness test brushing', 1),
    ('ear-cleaning', 11, 'Wellness test ear cleaning', 20),
    ('nail-trimming', 12, 'Wellness test nail trimming', 35),
    ('teeth-cleaning', 14, 'Wellness test teeth cleaning', 3),
    ('paw-care', 13, 'Wellness test paw care', NULL),
    ('grooming-rule', 6, 'Wellness test grooming rule', 40)
) AS care(care_key, care_type, title, days_ago)
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
WHERE "PetId" = :'pet_id'::uuid AND "Description" = '[wellness-e2e-seed:v1]'
UNION ALL
SELECT 'PetConditions', count(*)
FROM "PetConditions"
WHERE "PetId" = :'pet_id'::uuid AND "Description" = '[wellness-e2e-seed:v1]'
UNION ALL
SELECT 'PetMedications', count(*)
FROM "PetMedications"
WHERE "PetId" = :'pet_id'::uuid AND "Instructions" = '[wellness-e2e-seed:v1]'
UNION ALL
SELECT 'Reminders', count(*)
FROM "Reminders"
WHERE "PetId" = :'pet_id'::uuid AND "Description" = '[wellness-e2e-seed:v1]'
UNION ALL
SELECT 'ReminderRun', count(*)
FROM "ReminderRun"
WHERE "Note" = '[wellness-e2e-seed:v1]'
  AND "ReminderId" IN (
      SELECT "Id" FROM "Reminders"
      WHERE "PetId" = :'pet_id'::uuid AND "Description" = '[wellness-e2e-seed:v1]');
