\set ON_ERROR_STOP on

-- Usage with a locally installed psql:
--   psql "<postgres-connection-uri>" -v pet_id=00000000-0000-0000-0000-000000000000 -f scripts/seed-wellness-test.sql
--
-- Usage with the repository's Docker Compose database (bash, e.g. on the server):
--   docker compose exec -T db psql -U postgres -d smartPetCareDb \
--     -v pet_id=00000000-0000-0000-0000-000000000000 < scripts/seed-wellness-test.sql
--
-- The same through PowerShell, which has no input redirection for native commands:
--   Get-Content -Raw scripts/seed-wellness-test.sql |
--     docker compose exec -T db psql -U postgres -d smartPetCareDb -v pet_id=00000000-0000-0000-0000-000000000000
--
-- The script never updates user-created records. Before inserting, it replaces
-- only the rows a previous run of itself wrote for the same pet, which it finds
-- by their deterministic IDs — see the comment above wellness_seed_ids.
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
        ARRAY['Calm at home, eager on walks',
              'Friendly with other dogs, wary of strangers at first']),
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

-- The fixture is identified by ID and by nothing else. Every row it writes has a
-- deterministic ID derived from the pet and a key, which is what makes a re-run
-- replace exactly the rows the previous run wrote and leave user-created rows
-- alone. Identifying it by a marker in a free-text column — as this script used
-- to — meant the marker was shown wherever the app shows a note: it was the body
-- of the push a seeded reminder sends, and the condition and medication names
-- reach the classifier and come back inside the narrative. The text below is
-- therefore free to read like a real pet's history.
--
-- The keys are repeated in the inserts, so the two can drift. The counts printed
-- at the end are taken through this list rather than off the inserts, which is
-- what turns a drift into a zero in the report instead of a silent duplicate.
CREATE TEMP TABLE wellness_seed_ids ON COMMIT DROP AS
WITH pet AS (SELECT (:'pet_id'::uuid)::text AS key)
SELECT 'activity' AS kind, md5(pet.key || ':wellness-e2e-v1:activity:' || n)::uuid AS id
FROM pet CROSS JOIN generate_series(0, 29) AS n
UNION ALL
SELECT 'sleep', md5(pet.key || ':wellness-e2e-v1:sleep:' || n)::uuid
FROM pet CROSS JOIN generate_series(0, 29) AS n
UNION ALL
SELECT 'feeding', md5(pet.key || ':wellness-e2e-v1:feeding:' || d || ':' || m)::uuid
FROM pet CROSS JOIN generate_series(0, 29) AS d CROSS JOIN generate_series(0, 1) AS m
UNION ALL
SELECT 'weight', md5(pet.key || ':wellness-e2e-v1:weight:' || n)::uuid
FROM pet CROSS JOIN (VALUES (0), (7), (14), (21)) AS days(n)
UNION ALL
SELECT 'event', md5(pet.key || ':wellness-e2e-v1:event:' || k)::uuid
FROM pet CROSS JOIN (VALUES ('vet-visit'), ('grooming'), ('vaccination')) AS events(k)
UNION ALL
SELECT 'condition', md5(pet.key || ':wellness-e2e-v1:condition')::uuid FROM pet
UNION ALL
SELECT 'medication', md5(pet.key || ':wellness-e2e-v1:medication')::uuid FROM pet
UNION ALL
SELECT 'reminder', md5(pet.key || ':wellness-e2e-v1:reminder:' || k)::uuid
FROM pet CROSS JOIN (VALUES
    ('medication'), ('bathing'), ('brushing'), ('ear-cleaning'),
    ('nail-trimming'), ('teeth-cleaning'), ('paw-care'), ('grooming-rule')) AS rules(k)
UNION ALL
SELECT 'medrun', md5(pet.key || ':wellness-e2e-v1:medrun:' || n)::uuid
FROM pet CROSS JOIN generate_series(0, 29) AS n;

-- Refresh a previous run. Runs go by reminder rather than by their own ID, so a
-- run the scheduler added to a fixture reminder goes with it; everything else
-- goes by the ID the previous run wrote. The "PetId" predicates are redundant —
-- the IDs are derived from the pet — and are kept as the belt against a key
-- that is one edit away from naming somebody else's row.
DELETE FROM "ReminderRun"
WHERE "ReminderId" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'reminder');

DELETE FROM "Reminders"
WHERE "PetId" = :'pet_id'::uuid
  AND "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'reminder');

DELETE FROM "PetMedications"
WHERE "PetId" = :'pet_id'::uuid
  AND "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'medication');

DELETE FROM "PetConditions"
WHERE "PetId" = :'pet_id'::uuid
  AND "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'condition');

DELETE FROM "PetEvents"
WHERE "PetId" = :'pet_id'::uuid
  AND "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'event');

DELETE FROM "PetWeightLogs"
WHERE "PetId" = :'pet_id'::uuid
  AND "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'weight');

DELETE FROM "FeedingLogs"
WHERE "PetId" = :'pet_id'::uuid
  AND "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'feeding');

DELETE FROM "SleepLogs"
WHERE "PetId" = :'pet_id'::uuid
  AND "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'sleep');

DELETE FROM "ActivityLogs"
WHERE "PetId" = :'pet_id'::uuid
  AND "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'activity');

-- Activity: one session for each day in the complete 30-day wellness evaluation
-- window, on a five-day rotation so the history reads like a real week rather
-- than thirty identical rows. Source=0 is ActivitySource.Manual. ActiveMinutes
-- is derived by the backend from DurationMinutes and Intensity rather than
-- stored, and the rotation is weighted to average out at roughly an hour a day:
-- the two High days are short, the Moderate ones long. Swimming carries no
-- steps, which is both true of a collar and the one row that exercises the
-- aggregator's null-steps branch.
-- Types 0/1/2/3/4 are Walk/Play/Training/Swimming/Run; intensity 1/2 is
-- Moderate/High.
INSERT INTO "ActivityLogs"
    ("Id", "PetId", "RecordedAt", "Steps", "Type", "Intensity", "DurationMinutes", "Location", "Note", "Source", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:activity:' || day_offset)::uuid,
    context.pet_id,
    ((context.seed_date - day_offset) + plan.at) AT TIME ZONE 'UTC',
    plan.steps + (day_offset % 4) * 250,
    plan.type,
    plan.intensity,
    plan.duration + (day_offset % 3) * 5,
    plan.place,
    plan.note,
    0,
    now()
FROM wellness_seed_context AS context
CROSS JOIN generate_series(0, 29) AS days(day_offset)
JOIN (VALUES
    (0, time '08:10', 0, 1, 85, 8300, 'Riverside park',
     'Long morning walk, steady pace the whole way'),
    (1, time '18:30', 4, 2, 60, 8600, 'River path',
     'Evening run, kept up for the whole loop'),
    (2, time '17:00', 1, 1, 85, 7900, 'Dog park',
     'Fetch, then chasing about with the collie from next door'),
    (3, time '11:00', 2, 1, 85, 7400, 'Back garden',
     'Training: recall and heel work, finished on a good one'),
    (4, time '12:30', 3, 2, 60, NULL::integer, 'Lake',
     'Swimming. The collar reports no steps in the water')
) AS plan(slot, at, type, intensity, duration, steps, place, note)
  ON plan.slot = day_offset % 5
ON CONFLICT ("Id") DO NOTHING;

-- Sleep: one manual daily total for the same window, with the note matching the
-- hours it is attached to.
INSERT INTO "SleepLogs"
    ("Id", "PetId", "SleepDate", "Hours", "Note", "Source", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:sleep:' || day_offset)::uuid,
    context.pet_id,
    (context.seed_date - day_offset)::timestamp AT TIME ZONE 'UTC',
    9 + (day_offset % 3) * 0.5,
    CASE day_offset % 3
        WHEN 0 THEN 'Settled early, slept through the night'
        WHEN 1 THEN 'Slept through, plus a nap after the afternoon walk'
        ELSE 'Up once in the night, went straight back down'
    END,
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
    CASE meal
        WHEN 0 THEN 'Dry kibble, adult large breed'
        ELSE 'Wet food, turkey and rice'
    END,
    CASE meal WHEN 0 THEN 120 ELSE 180 END,
    0,
    300,
    CASE meal
        WHEN 0 THEN 'Breakfast, with the joint supplement'
        ELSE 'Dinner, after the evening walk'
    END,
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
    CASE day_offset
        WHEN 21 THEN 'Weighed at the clinic, just before the annual checkup'
        ELSE 'Weekly weigh-in at home, same scale each time'
    END,
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
    description,
    ((context.seed_date - day_offset) + time '10:00') AT TIME ZONE 'UTC',
    1,
    1,
    false,
    0,
    now()
FROM wellness_seed_context AS context
CROSS JOIN (VALUES
    ('vet-visit', 0, 'Annual checkup',
     'Routine examination. Weight and joints checked, everything in order.', 20),
    ('grooming', 1, 'Full groom',
     'Bath, trim, nails clipped and ears cleaned.', 10),
    ('vaccination', 2, 'Annual booster',
     'DHPP and rabies booster. No reaction afterwards.', 150)
) AS events(event_key, event_type, title, description, day_offset)
ON CONFLICT ("Id") DO NOTHING;

-- One active condition, so the response exercises the condition cap instead of
-- the unconstrained path. Type=0 is ConditionType.Chronic. The name reaches the
-- classifier and can come back quoted in the narrative, so it reads as a real
-- diagnosis rather than as fixture text.
INSERT INTO "PetConditions"
    ("Id", "PetId", "IsActive", "Type", "Name", "Allergen", "Description", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:condition')::uuid,
    context.pet_id,
    true,
    0,
    'Mild hip dysplasia',
    NULL,
    'Seen on an X-ray at the annual checkup. Managed with a daily joint '
        || 'supplement and steady, low-impact exercise.',
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
    'Joint supplement',
    '1 tablet',
    'One tablet with breakfast, every morning.',
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
-- Description is what a push sends as its body, so it is written to be read on
-- a lock screen.
INSERT INTO "Reminders"
    ("Id", "PetId", "Title", "Description", "Type", "Status", "RepeatType", "IntervalN",
     "RecalcStrategy", "Days", "Date", "TimeOfDay", "UtcOffsetMinutes", "StartAt",
     "NextTriggerAt", "EndAt", "ScheduleAnchorAt", "OverdueSince", "LastCompletedAt",
     "IsSystemGenerated", "SourceType", "SourceId", "CreatedAt")
SELECT
    md5(context.pet_id::text || ':wellness-e2e-v1:reminder:medication')::uuid,
    context.pet_id,
    'Joint supplement',
    'One tablet with breakfast.',
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
-- Status 5/4 are ReminderRunStatus.Completed/Missed. A missed dose carries no
-- note, since nobody was there to write one.
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
    CASE WHEN day_offset % 7 = 3 THEN NULL ELSE 'Given with breakfast' END,
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
    description,
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
    ('bathing', 9, 'Bath', 'Bath day. Rinse well and dry the ears.', 12),
    ('brushing', 10, 'Brushing', 'Brush the coat through, check behind the ears for mats.', 1),
    ('ear-cleaning', 11, 'Ear cleaning', 'Wipe both ears with the cleaner from the vet.', 20),
    ('nail-trimming', 12, 'Nail trimming', 'Trim the nails, a little at a time.', 35),
    ('teeth-cleaning', 14, 'Teeth cleaning', 'Brush the teeth with the enzymatic paste.', 3),
    ('paw-care', 13, 'Paw check', 'Check the pads and between the toes, especially after the lake.', NULL),
    ('grooming-rule', 6, 'Grooming', 'Book the full groom, or do it at home.', 40)
) AS care(care_key, care_type, title, description, days_ago)
ON CONFLICT ("Id") DO NOTHING;

-- Counted through the ID list rather than off the inserts above, so a key that
-- drifts between the two shows up here as a zero.
SELECT 'ActivityLogs' AS table_name, count(*) AS fixture_rows
FROM "ActivityLogs"
WHERE "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'activity')
UNION ALL
SELECT 'SleepLogs', count(*)
FROM "SleepLogs"
WHERE "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'sleep')
UNION ALL
SELECT 'FeedingLogs', count(*)
FROM "FeedingLogs"
WHERE "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'feeding')
UNION ALL
SELECT 'PetWeightLogs', count(*)
FROM "PetWeightLogs"
WHERE "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'weight')
UNION ALL
SELECT 'PetEvents', count(*)
FROM "PetEvents"
WHERE "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'event')
UNION ALL
SELECT 'PetConditions', count(*)
FROM "PetConditions"
WHERE "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'condition')
UNION ALL
SELECT 'PetMedications', count(*)
FROM "PetMedications"
WHERE "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'medication')
UNION ALL
SELECT 'Reminders', count(*)
FROM "Reminders"
WHERE "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'reminder')
UNION ALL
SELECT 'ReminderRun', count(*)
FROM "ReminderRun"
WHERE "Id" IN (SELECT id FROM wellness_seed_ids WHERE kind = 'medrun');

COMMIT;
