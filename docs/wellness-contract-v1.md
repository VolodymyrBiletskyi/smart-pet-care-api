# Wellness classifier contract 2.0.0

This document records the deployed Python service contract from `openapi.json`.
The C# wire types live in
`Infrastructure/Classifier/Contracts/ClassifierWellnessContracts.cs`.

## Transport

- `POST wellness`, JSON request and response;
- `X-API-Key` authentication;
- camel-case property names;
- null request properties are omitted by the C# client;
- common classifier timeout, circuit-breaker and error handling is shared with
  `chat` and `feeding-summary`.

## Request ownership

The C# API verifies pet ownership and aggregates a 30-calendar-day inclusive
UTC window. The deployed Python schema permits other window sizes, but this
integration always sends 30 dates. Preventive-care flags use completed pet
events from the previous 12 months, matching the Python schema description.

`currentSymptoms` is optional caller-supplied free text (maximum 4000
characters). The C# API does not infer it from health records. Missing activity,
feeding and preventive-care datasets are sent as omitted properties; empty
condition, medication and weight collections are sent as arrays.

The request contains:

- `pet` (required): species, breed, age, sex, current weight and behavioral notes;
- optional aggregated `activity` and `feeding`;
- active conditions and medications;
- weight history;
- optional preventive-care flags and current symptoms;
- grooming-style `routineCare` entries with their latest completion dates;
- previous successful numeric score;
- inclusive `evaluationWindow.startDate/endDate`.

Routine care is derived from grooming events and the backend's grooming-style
reminders. Missing or stale routine-care entries may produce tracking
recommendations, but routine care is not an additional scored breakdown
dimension.

## Response

`wellnessScore`, `band`, `bandLabel`, `trend`, `conditionCap` and
`classifierCondition` are nullable. An `INSUFFICIENT_DATA` response must not
contain a score or band. Scored `COMPLETE` and `PARTIAL` responses must contain
both.

Required fields are `scoreStatus`, `dataCoverage`, `calculationVersion`,
`evaluatedAt`, all six breakdown dimensions, `narrative`, `recommendations` and
`disclaimer`. `dataCoverage` is in the range 0 through 1 and
`calculationVersion` is a semantic version such as `1.0.0`.

Breakdown dimensions are activity, sleep, diet, symptoms, preventive care and
baseline. Each carries `score`, `maxScore`, `availability`, `included`, at least
one reason code, and optional scalar evidence.

The complete enum values remain encoded on the C# contract types and are
covered by serialization tests. Python may suggest any backend reminder type,
including weighing, deworming and the grooming-specific Bathing, Brushing,
EarCleaning, NailTrimming, PawCare and TeethCleaning values. Tracking
recommendations may use the additional `RoutineCare` dimension.

## Persistence and public API

A successful response is stored in `PetWellnessAssessments`: indexed metadata
supports current/history queries and the complete classifier response is kept
as JSONB. Failed or invalid responses are not persisted.

- `POST /api/pets/{petId}/wellness/recalculate`
- `GET /api/pets/{petId}/wellness/current`
- `GET /api/pets/{petId}/wellness/history?page=1&pageSize=20`

Recalculation accepts `{ "currentSymptoms": "..." }` or an empty/null body.
Calls for the same pet are serialized within one application process. Python
429, invalid responses and availability failures map to public 429, 502 and 503
responses respectively.
