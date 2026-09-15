\set ON_ERROR_STOP on

-- Remove only data created by scripts/seed-wellness-test.sql. Fields written
-- into the pet row by -v fill_pet_profile=1 are not reverted — they are the
-- pet's own profile once set, and a revert cannot tell them from user edits.
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
-- Recalculation responses are stored separately in PetWellnessAssessments and
-- cannot be recognized by the fixture marker. To remove the exact assessment
-- returned by POST /api/pets/{petId}/wellness/evaluation, additionally pass:
--   -v assessment_id=<assessmentId-from-the-response>

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

-- Runs go before the reminders they hang off.
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

COMMIT;

SELECT 'Wellness E2E fixture removed for pet ' || :'pet_id' AS result;
