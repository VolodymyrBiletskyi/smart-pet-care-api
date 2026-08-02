# Nutrition-Analysis Contract v1

Status: proposed. The C# side is implemented; the classifier service must
implement the `nutrition-analysis` route before the endpoint returns anything
other than `503`.

This document is the source of truth for the nutrition-analysis call between
the Smart Pet Care C# backend and the classifier service. The C# types in
`Infrastructure/Classifier/Contracts/ClassifierNutritionContracts.cs` implement
this contract. Transport, authentication, failure semantics and the circuit
breaker are shared with `chat` — see `docs/chat-classifier-contract-v1.md`.

## Responsibilities

### C# backend owns

- user authentication and pet ownership checks;
- computing the daily nutrition summary (meal count, calories, portion totals,
  scheduled feedings) and the goal-vs-actual comparison;
- assembling pet context (species, breed, weight, age);
- persisting the analysis and enforcing retention;
- mapping classifier failures to the public API.

### Classifier owns

- grading the supplied day and scoring it from 0 to 100;
- writing the user-facing summary sentence and the advice list;
- the user-facing safety disclaimer.

### Classifier does not own

- computing the nutrition figures — it must grade only the supplied numbers;
- database writes or any durable state.

The classifier must be stateless and side-effect-free. `requestId` is a
correlation identifier only.

## Transport

- Method: `POST`
- Relative path: `nutrition-analysis`
- Content type: `application/json`
- Authentication header: `X-API-Key: <configured key>`
- JSON property naming: camel case
- Null request properties are omitted

The final URL is resolved relative to `Classifier:BaseUrl`.

## Request

```json
{
  "requestId": "9197ec8f-17a6-476c-a747-f3ecf9df1134",
  "petType": "dog",
  "breed": "Beagle",
  "weightKg": 12.4,
  "ageMonths": 12,
  "date": "2026-07-15",
  "mealCount": 2,
  "totalCalories": 480,
  "portionTotals": [
    {
      "unit": "gram",
      "totalAmount": 210
    }
  ],
  "scheduledFeedings": 3,
  "goal": {
    "dailyCalorieTarget": 600,
    "dailyPortionTarget": 250,
    "portionUnit": "gram",
    "mealsPerDay": 3
  },
  "comparison": {
    "caloriesRemaining": 120,
    "calorieTargetMet": false,
    "mealsRemaining": 1,
    "mealsTargetMet": false,
    "portionRemaining": 40,
    "portionTargetMet": false
  }
}
```

| Field | Requirement | Meaning |
|---|---|---|
| `requestId` | optional UUID string | Correlation identifier assigned by the C# backend. |
| `petType` | optional enum | `dog`, `cat`, `rabbit`, `hamster`, `guinea_pig`, `bird`, `fish`, `turtle`, or `other`. |
| `breed` | optional string | Free text as entered by the user. |
| `weightKg` | optional number | Current recorded weight. |
| `ageMonths` | optional integer | Approximate age, derived from the pet's birth date. |
| `date` | required string | The analysed local day, `yyyy-MM-dd`. |
| `mealCount` | required integer | Feeding logs recorded on that local day. |
| `totalCalories` | required integer | Sum of logged approximate calories; logs without calories count as zero. |
| `portionTotals` | required array | Logged portion totals, one entry per unit. Empty when no portions were logged. |
| `portionTotals[].unit` | required enum | `gram`, `milliliter`, `cup`, or `piece`. |
| `portionTotals[].totalAmount` | required number | Total logged in that unit. |
| `scheduledFeedings` | required integer | Active feeding reminders for the pet. |
| `goal` | optional object | The pet's nutrition goal. Omitted when none is set. Every field inside is itself optional. |
| `comparison` | optional object | Goal-vs-actual figures. Omitted when no goal is set; individual fields are null when the goal does not set that target. |

Portion totals are reported per unit and are never mixed. `comparison`
compares only against the goal's own `portionUnit`.

## Successful response

```json
{
  "grade": "B",
  "score": 78,
  "summary": "Buddy came in slightly under his calorie target today.",
  "advice": [
    "Add a small evening meal to close the 120 kcal gap.",
    "Keep portions consistent across the day."
  ],
  "disclaimer": "This guidance does not replace a veterinary examination."
}
```

| Field | Requirement | Meaning |
|---|---|---|
| `grade` | required enum | `A`, `B`, `C`, `D`, or `F`. |
| `score` | required integer | Overall quality of the day, 0 to 100 inclusive. |
| `summary` | required string | One or two user-facing sentences describing the day. |
| `advice` | required string array | Actionable suggestions; return an empty array when none apply. |
| `disclaimer` | required string | User-facing safety disclaimer. |

The C# backend rejects a response whose `grade` is not one of the listed
values, whose `score` falls outside 0–100, or that omits any required field.
A rejected response is reported to clients as `502 Bad Gateway`.

Three details cause most integration failures:

- **`grade` is case-sensitive.** `"B"` is accepted, `"b"` is not.
- **`score` must be a JSON integer.** `78` is accepted, `78.0` is not — a
  Python `float` serialises with a decimal point and is rejected. Cast to
  `int` before returning.
- **`advice` must be an array**, even for a single suggestion, and `[]` when
  there is nothing to add.

Every rejection is logged with the offending field or JSON path, for example
`score is outside 0-100` or
`The JSON value could not be converted to ... Path: $.score`. The reason names
fields and paths only — never their values — and is never sent to clients.

`grade` and `score` should agree: broadly A ≥ 90, B ≥ 75, C ≥ 60, D ≥ 40, F
below 40. The C# backend does not enforce this.

## Failure semantics

Identical to `chat`. The classifier outcome maps to the public
`POST /api/pets/{petId}/nutrition-summary/analysis` status as follows:

| Classifier outcome | Public analysis API |
|---|---|
| Valid 2xx response | `200 OK` |
| `429 Too Many Requests` | `429 Too Many Requests` with retry metadata |
| `422 Unprocessable Entity` | `502 Bad Gateway` |
| Other 4xx response | `502 Bad Gateway` |
| 5xx response | `503 Service Unavailable` with retry metadata when supplied |
| Network, read failure, or timeout | `503 Service Unavailable` |
| Malformed or contract-invalid 2xx body | `502 Bad Gateway` |

For `429` and `503` the backend prefers the HTTP `Retry-After` header and falls
back to `retryAfterSeconds` from the classifier JSON error. The public error
object contains `code`, a safe user-facing `message`, `retryable`, and
`retryAfterSeconds`; internal classifier messages are not forwarded.

Nothing is persisted when the call fails — there is no partial analysis and no
retry record. The client simply repeats the request.

`nutrition-analysis` shares the in-process circuit breaker with `chat`, so a
classifier outage observed on either route opens the circuit for both.

## Persistence and retention

The C# backend stores each successful analysis together with a snapshot of the
`mealCount` and `totalCalories` it was based on, because feeding logs can change
afterwards.

**Only the two most recent analyses per pet are kept.** Storing a third deletes
the oldest. `GET /api/pets/{petId}/nutrition-summary/analysis` returns them as
`latest` and `previous`, so a client can show the current grade against the one
before it. Analyses cascade-delete with their pet.

Retention is per pet, not per day: two analyses of the same day replace an
older analysis of a different day.

## Compatibility policy

This is contract version 1. Removing fields, renaming fields, changing enum
wire values, making optional fields required, or altering field meaning
requires a new contract version and coordinated deployment.

Backward-compatible within v1: adding optional response fields the C# backend
can ignore, and relaxing classifier-side validation without changing field
meaning. New enum values are not backward-compatible — the C# backend rejects
unknown `grade` values.
