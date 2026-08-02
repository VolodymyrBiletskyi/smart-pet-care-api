# Feeding-Summary Contract v1

Status: implemented on both sides. The classifier exposes `feeding-summary`
today; this document records the shape the C# backend relies on.

This document is the source of truth for the feeding-summary call between the
Smart Pet Care C# backend and the classifier service. The C# types in
`Infrastructure/Classifier/Contracts/ClassifierFeedingSummaryContracts.cs`
implement this contract. Transport, authentication, failure semantics and the
circuit breaker are shared with `chat` — see
`docs/chat-classifier-contract-v1.md`.

It replaces the never-implemented `nutrition-analysis` contract. That route does
not exist on the classifier; `feeding-summary` is what it actually offers.

## Responsibilities

### C# backend owns

- user authentication and pet ownership checks;
- resolving the caller's local day into a UTC window and reading that day's
  feeding logs;
- assembling pet context (species, breed, weight, age) and the product list;
- persisting the analysis and enforcing retention;
- mapping classifier failures to the public API.

### C# backend also owns

- replacing the derived target with the pet's nutrition goal when one is set,
  and re-deriving the deviation and status from it.

### Classifier owns

- deriving the pet's daily calorie target from its body data, used whenever no
  goal target is set;
- comparing the supplied products against that target and returning a status
  and a deviation;
- the user-facing safety disclaimer.

### Classifier does not own

- reading or writing feeding logs — it grades only the supplied products;
- database writes or any durable state.

The classifier must be stateless and side-effect-free.

## Transport

- Method: `POST`
- Relative path: `feeding-summary`
- Content type: `application/json`
- Authentication header: `X-API-Key: <configured key>`
- JSON property naming: camel case
- Null request properties are omitted

The final URL is resolved relative to `Classifier:BaseUrl`.

## Request

The route grades a batch. The API analyses one pet at a time, so `pets` always
carries exactly one entry and the matching result is picked out of the response
by `petId`.

```json
{
  "pets": [
    {
      "petId": "9197ec8f-17a6-476c-a747-f3ecf9df1134",
      "species": "dog",
      "breed": "Labrador",
      "weightKg": 12.4,
      "ageMonths": 12,
      "products": [
        {
          "name": "Chicken kibble",
          "calories": 300
        },
        {
          "name": "Salmon treat",
          "calories": 180
        }
      ]
    }
  ]
}
```

| Field | Requirement | Meaning |
|---|---|---|
| `pets` | required array, 1–1000 entries | Pets to grade. The API sends one. |
| `pets[].petId` | required string, 1–100 chars | Correlation id. The backend sends the pet's UUID in `D` format and matches the result on it. |
| `pets[].species` | optional enum, defaults to `dog` | `dog`, `cat`, `rabbit`, `hamster`, `guinea_pig`, `bird`, `fish`, `turtle`, or `other`. |
| `pets[].breed` | optional string | Free text as entered by the user. |
| `pets[].weightKg` | **required** number, `> 0` and `<= 500` | Current recorded weight. The calorie target is derived from it. |
| `pets[].ageMonths` | optional integer, 0–600 | Approximate age. Below 12 months the classifier applies a growth-energy multiplier instead of the adult maintenance factor. |
| `pets[].products` | optional array, at most 100 entries | What the pet ate on the analysed day. |
| `products[].name` | required string, 1–200 chars | Food name. |
| `products[].calories` | required number, 0–20000 | Calories for that food. |

### How the backend fills the request

Every field has a caller-supplied override and a stored-data fallback. The
public endpoint takes an **optional** JSON body; each property in it wins over
the pet's stored data, and anything omitted falls back to the behaviour below.
A caller that sends no body at all gets exactly that fallback behaviour.

```json
{
  "species": "dog",
  "breed": "Labrador",
  "weightKg": 12.4,
  "ageMonths": 12,
  "products": [{ "name": "Chicken kibble", "calories": 480 }]
}
```

`petId` is not part of the body — it comes from the route.

| Body field | Fallback when omitted |
|---|---|
| `species` | `Pet.Species`, mapped to the classifier's enum |
| `breed` | `Pet.Breed` |
| `weightKg` | `Pet.WeightKg` |
| `ageMonths` | Derived from `Pet.BirthDate` |
| `products` | Built from the analysed day's feeding logs |

The body is validated against the same limits the classifier enforces, so an
out-of-range value is a `400` naming the field rather than an opaque `502`.
`species` is a string enum and accepts the `AnimalSpecies` names used elsewhere
in the public API; it is matched case-insensitively, so `"dog"` and `"Dog"` both
work.

- **The day.** `date` and `utcOffsetMinutes` on the public endpoint resolve to a
  half-open UTC window, the same one the daily summary reads. The route itself
  has no notion of a date — only the products that fall inside that window are
  sent. The window is still resolved when `products` is supplied, because the
  stored analysis is filed under a local date either way.
- **Products.** When supplied in the body they are passed through in the order
  given, un-merged — the caller chose that list. Otherwise: one per distinct
  `FoodName` on the day's feeding logs, with `ApproxCalories` summed; logs
  without calories count as zero and logs without a name fall back to their
  `FoodType`. Merging by name keeps the list inside the 100-entry cap without
  losing calories. Past 100 distinct foods the smallest entries are merged into
  a single `Other foods` product rather than dropped.
  **`"products": []` is not the same as omitting it** — an empty array grades
  the day as having eaten nothing, whereas omitting it reads the logs.
- **Weight.** Taken from the body when supplied, otherwise from the pet. If
  neither yields a value inside `0 < w <= 500` the request is rejected with
  `400 Bad Request` before the call is made — the classifier cannot derive a
  target without it. A pet with no recorded weight can therefore still be
  analysed by sending `weightKg`.
- **Age.** Taken from the body when supplied, otherwise derived from the pet's
  birth date at 30 days per month and clamped to 600 months.
- **`mealCount`.** The stored snapshot counts the supplied products when the
  body carries them, and the day's feeding logs otherwise.

The pet's **nutrition goal is not sent** — the route accepts no target input and
always derives one from body data. The backend therefore applies a user-set goal
to the *response* instead; see "Nutrition goal overrides the target" below.

## Successful response

Captured from the live route for the request above (a 12.4 kg, 12-month-old
Beagle fed 480 kcal):

```json
{
  "results": [
    {
      "petId": "9197ec8f-17a6-476c-a747-f3ecf9df1134",
      "status": "UNDER_TARGET",
      "targetCalories": 740.1,
      "actualCalories": 480.0,
      "deviationPct": -35.1
    }
  ],
  "disclaimer": "This feeding summary is an estimate based on logged food and standard calorie formulas. It is not a substitute for a vet-prescribed diet plan."
}
```

**The three numbers are fractional, not integers.** `targetCalories` came back
as `740.1` and `deviationPct` as `-35.1`, so they are stored as `numeric(10,2)`;
reading them as integers truncates. `actualCalories` echoes the supplied product
total exactly.

Two other observed behaviours, both consistent with this contract:

- A pet with no products scores `EXTREME_UNDER_TARGET` at `-100.0`, so an
  unlogged day grades rather than erroring.
- `deviationPct` is unbounded above — 9000 kcal against an 832.4 kcal target
  returned `981.2`.

| Field | Requirement | Meaning |
|---|---|---|
| `results` | required, non-empty array | One entry per requested pet. |
| `results[].petId` | required string | Echoes the request's `petId`. |
| `results[].status` | required enum | `EXTREME_UNDER_TARGET`, `UNDER_TARGET`, `ON_TARGET`, `OVER_TARGET`, or `EXTREME_OVER_TARGET`. |
| `results[].targetCalories` | required number | Daily calorie need derived from the pet's body data. |
| `results[].actualCalories` | required number | Calories in the supplied products. |
| `results[].deviationPct` | required number | Signed percentage away from `targetCalories`. |
| `disclaimer` | required string | User-facing safety disclaimer, shared by every result. |

The C# backend rejects a response that omits `results` or `disclaimer`, whose
`results` is empty, or that contains an entry without a `petId` or with an
unknown `status`. It also rejects a response whose `results` holds no entry for
the pet that was asked about. A rejected response is reported to clients as
`502 Bad Gateway`.

Two details cause most integration failures:

- **`status` is case-sensitive.** `"UNDER_TARGET"` is accepted, `"under_target"`
  is not.
- **`results` must be an array**, even for a single pet.

Every rejection is logged with the offending field or JSON path, for example
`results is empty` or
`The JSON value could not be converted to ... Path: $.results`. The reason names
fields and paths only — never their values — and is never sent to clients.

## Nutrition goal overrides the target

`feeding-summary` has no field for a caller-supplied calorie target — it always
derives one from body weight. So when the pet has a `NutritionGoal` whose
`DailyCalorieTarget` is **above zero**, the backend re-grades the classifier's
answer against that goal after the response arrives:

```
targetCalories = goal.DailyCalorieTarget
deviationPct   = round((actualCalories - target) / target * 100, 2)
status         = band(deviationPct)
```

All three move together. Keeping the classifier's `deviationPct` next to a
different `targetCalories` would store a self-contradictory row, so the
deviation and the status are recomputed whenever the target is replaced.
`actualCalories` and `disclaimer` always come from the classifier untouched.

The bands:

| Deviation | Status |
|---|---|
| `<= -50%` | `EXTREME_UNDER_TARGET` |
| `-50%` to `-10%` (exclusive) | `UNDER_TARGET` |
| `-10%` to `+10%` (inclusive) | `ON_TARGET` |
| `+10%` to `+50%` (exclusive) | `OVER_TARGET` |
| `>= +50%` | `EXTREME_OVER_TARGET` |

These reproduce every classifier result observed on the live route: `-35.1%`
came back `UNDER_TARGET`, `-100%` came back `EXTREME_UNDER_TARGET`, and `+981.2%`
sits in `EXTREME_OVER_TARGET`. They are the backend's own bands, though — the
classifier does not publish its thresholds, so a pet **with** a goal and a pet
**without** one can be graded on slightly different boundaries.

The classifier's own target stands when there is no goal, when the goal sets no
`DailyCalorieTarget`, or when that target is `0` — zero has no meaningful
deviation and would divide by zero. `GET /api/pets/{petId}/nutrition-summary`
still reports the goal comparison directly and is unaffected.

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

A `422` carries FastAPI's `HTTPValidationError` body (`detail[].loc/msg/type`).
The backend does not forward it; the request is shaped to the documented limits
so a `422` means the contract drifted, not that the user did something wrong.

For `429` and `503` the backend prefers the HTTP `Retry-After` header and falls
back to `retryAfterSeconds` from the classifier JSON error. The public error
object contains `code`, a safe user-facing `message`, `retryable`, and
`retryAfterSeconds`; internal classifier messages are not forwarded.

Nothing is persisted when the call fails — there is no partial analysis and no
retry record. The client simply repeats the request.

`feeding-summary` shares the in-process circuit breaker with `chat`, so a
classifier outage observed on either route opens the circuit for both.

## Persistence and retention

The C# backend stores each successful analysis together with the `mealCount` it
was based on, because feeding logs can change afterwards.

**Only the two most recent analyses per pet are kept.** Storing a third deletes
the oldest. `GET /api/pets/{petId}/nutrition-summary/analysis` returns them as
`latest` and `previous`, so a client can show the current status against the one
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
unknown `status` values.
