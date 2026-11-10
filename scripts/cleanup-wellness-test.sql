\set ON_ERROR_STOP on

-- Remove only data created by scripts/seed-wellness-test.sql. It is found by
-- the same deterministic IDs the seed derives from the pet, so a row whose text
-- the user has since edited still goes, and a row the user wrote themselves
-- never does. Fields written into the pet row by -v fill_pet_profile=1 are not
-- reverted — they are the pet's own profile once set, and a revert cannot tell
-- them from user edits.
--
--   psql "<postgres-connection-uri>" -v pet_id=00000000-0000-0000-0000-000000000000 -f scripts/cleanup-wellness-test.sql
--
-- bash + Docker Compose (e.g. on the server):
--   docker compose exec -T db psql -U postgres -d smartPetCareDb \
--     -v pet_id=00000000-0000-0000-0000-000000000000 < scripts/cleanup-wellness-test.sql
--
-- PowerShell + Docker Compose:
--   Get-Content -Raw scripts/cleanup-wellness-test.sql |
--     docker compose exec -T db psql -U postgres -d smartPetCareDb -v pet_id=00000000-0000-0000-0000-000000000000
--
-- Wellness responses are stored separately in PetWellnessAssessments and carry
-- no marker of their own, so they are left alone unless you name one:
--   -v assessment_id=<uuid>
-- The response body of GET /api/pets/{petId}/wellness/evaluation does not carry
-- its ID, so take it from the table:
--   SELECT "Id", "WellnessScore", "CreatedAt" FROM "PetWellnessAssessments"
--   WHERE "PetId" = '<pet_id>' ORDER BY "CreatedAt" DESC;
-- Worth doing between two runs of the seed: an assessment younger than three
-- days is returned as it stands and the new fixture is never read.

\if :{?pet_id}
\else
\echo 'Missing pet_id. Pass it with: -v pet_id=<uuid>'
\quit
\endif

BEGIN;

-- An assessment is deleted only by its exact ID and only when it belongs to
-- the selected pet. If assessment_id is omitted, wellness history is preserved.
\if :{?assessment_id}
DELETE FROM "PetWellnessAssessments"
WHERE "Id" = :'assessment_id'::uuid
  AND "PetId" = :'pet_id'::uuid;
\endif

-- The ID list is the one in scripts/seed-wellness-test.sql; the two are only
-- useful together, so a key added there has to be added here as well.
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

-- Runs go by reminder rather than by their own ID, so a run the scheduler added
-- to a fixture reminder goes with it.
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

COMMIT;

SELECT 'Wellness E2E fixture removed for pet ' || :'pet_id' AS result;
