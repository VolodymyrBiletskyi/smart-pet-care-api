# Activity & Sleep Fields v1

Status: implemented. This document is the field-level reference for everything
added on `feature/activity-intensity-and-sleep-logs` — the three new
`ActivityLog` fields, the derived `activeMinutes`, and the new `SleepLog`
entity and endpoints.

It is written to be handed to a client developer or a code-generating model
without further context, so each field states not only its type and rules but
what it means and how it is expected to be filled in.

Implementing types:

- `Models/ActivityLog.cs`, `Models/SleepLog.cs`
- `Modules/ActivityModule/DTOs/` (requests and responses)
- `Modules/ActivityModule/Domain/ActivityEffort.cs` (the effort maths)
- `Modules/ActivityModule/Domain/ActivityLogService.cs`,
  `Modules/ActivityModule/Domain/SleepLogService.cs` (validation)

## The idea in one paragraph

A pet's wellness needs three things logging can supply: how long the pet moved,
how hard it moved, and how long it slept. `ActivityLog` covers the first two per
session; `SleepLog` covers the third per day. The *kind* of activity is recorded
too, but purely as a label — it never enters any calculation. That separation is
the single most important thing to preserve in a UI: the picker for "what did
you do" is descriptive, the picker for "how hard was it" is the one that
actually changes numbers.

## Conventions

- All enums serialise as **strings** in both directions (`"Walk"`, not `0`).
  Casing is exactly as listed below.
- All timestamps are ISO-8601. A `DateTime` sent with no offset is read as UTC.
- Errors use `ApiErrorResponse`: `{ "message": "...", "errors": null }`. The
  message always ends with a full stop.
- `404` means the pet does not exist or does not belong to the caller — the API
  does not distinguish the two. `400` means the payload was rejected.
- All endpoints require authentication; the pet must belong to the caller.

---

# Part 1 — Activity logs

`GET|POST /api/pets/{petId}/activity-logs`
`GET|DELETE /api/pets/{petId}/activity-logs/{activityLogId}`

Three new request fields (`type`, `intensity`, `durationMinutes`) and one new
response-only field (`activeMinutes`).

## Request shape

```jsonc
POST /api/pets/{petId}/activity-logs
{
  "recordedAt": "2026-02-09T07:00:00Z",  // required
  "type": "Run",                          // new, optional
  "intensity": "Moderate",                // new, optional
  "durationMinutes": 60,                  // new, optional
  "steps": 5000,                          // existing, optional
  "location": "Park",                     // existing, optional
  "note": "Met the neighbour's dog",      // existing, optional
  "source": "Manual"                      // existing, optional, defaults to Manual
}
```

At least one of `type`, `durationMinutes`, `steps`, `location` or `note` must be
present — a row with only a timestamp records nothing. Note that `intensity`
alone does **not** satisfy this: intensity without a duration is not a log.

## `type` — what the pet did

- **Type:** `ActivityType?` (string enum), nullable, no default.
- **Purpose:** a label for the history list and for chat context.
- **Not used in any calculation.** Two sessions of the same length and intensity
  score identically whether they are `Swimming` or `Other`.

| Value | Means | Typical UI copy |
|---|---|---|
| `Walk` | Ordinary walk — out, around, back | Прогулянка |
| `Play` | Play session, fetch, games with other animals | Гра |
| `Training` | Obedience, tricks, sport/competition prep | Дресирування / тренування |
| `Swimming` | Pool, river, hydrotherapy | Плавання |
| `Run` | Sustained running, jogging alongside a bike | Біг |
| `Other` | Anything the list does not name | Інше |

**Why the list is this short.** Physical difficulty is carried by `intensity`,
not by the type, so variants of the same activity do not need their own values.
A gentle obedience class and competition training are both `Training` at
different intensities; a sniff-every-lamppost walk and a walk that was mostly
running are both `Walk`. Splitting them into separate types would double the
picker and change nothing downstream.

**`Other` is the escape hatch and it is cheap.** Because the type is never
computed on, an activity nobody anticipated still scores exactly like a known
one. Clients should pair `Other` with a free-text `note` describing what it was
(canicross, agility, nose work, treadmill rehab, cat wand play). A separate
"custom label" field was deliberately not added — the existing `note` carries
it, and if a phrase recurs often enough it can be promoted to a real enum value
later.

**Null is legal.** Rows created before this field existed have `null`, and a
client that only reports step counts may keep sending `null`.

**Enum stability:** stored as an integer in Postgres. New values must be
**appended**; inserting in the middle silently rewrites the meaning of existing
rows.

## `intensity` — how hard the pet worked

- **Type:** `ActivityIntensity?` (string enum), nullable.
- **Purpose:** the multiplier behind `activeMinutes`. This is the field that
  actually moves the numbers.
- **Derived when omitted** — see the table below.

| Value | Means | Weight |
|---|---|---|
| `Low` | Mostly standing, sniffing, slow lead walking | 0.4 |
| `Moderate` | Steady movement, occasional bursts | 0.7 |
| `High` | Sustained effort — running, swimming, hard training | 1.0 |

**Why "intensity" and not "difficulty".** How hard a session was *for this
particular pet* depends on its age, weight and condition — which is exactly what
a wellness score is supposed to compute. Asking the user for it would feed the
answer back into itself. "How intense was it" is a question about the activity
and the user can answer it; "how hard was it for the dog" is a question about
the dog and the user would be guessing.

**Defaulting.** If `durationMinutes` is present and `intensity` is not, the
server fills it in from the type and stores the result — the response will show
a concrete value, never `null`:

| `type` | Derived `intensity` |
|---|---|
| `Walk` | `Low` |
| `Run`, `Swimming` | `High` |
| `Play`, `Training`, `Other`, `null` | `Moderate` |

An explicitly sent `intensity` always wins over the default. In the worked
example above, `Run` + `Moderate` stays `Moderate` — the `High` default did not
apply because the client stated a value.

If `durationMinutes` is **absent**, nothing is derived and `intensity` stays
`null`. There is nothing to weight, so guessing would add a value the user never
stated.

**UI recommendation:** show the intensity control pre-selected at the type's
default rather than empty. The user confirms or adjusts one tap instead of
answering a second mandatory question, and the stored value is honest either
way.

## `durationMinutes` — how long it lasted

- **Type:** `int?`, nullable.
- **Range:** `1`–`1440` inclusive. Also enforced in the database
  (`CK_ActivityLogs_DurationMinutesInRange`).
- **Purpose:** elapsed wall-clock minutes of the session.

**Why the 1440 ceiling.** One log is one session. A span longer than a day is
several sessions and belongs in several rows — a single intensity applied to a
24-hour stretch is a fiction. This is a deliberate modelling constraint, not a
storage limit.

**A type plus a duration is a complete log.** No steps, location or note are
required alongside it. This is the expected common case: most walks are logged
from a phone with no step counter anywhere near the animal.

## `activeMinutes` — response only, derived

- **Type:** `int?`, **response only**. Never sent by a client, never stored in
  the database.
- **Formula:**

```
activeMinutes = round(durationMinutes × weight(intensity))

weight(Low)      = 0.4
weight(Moderate) = 0.7
weight(High)     = 1.0
```

Rounded to the nearest integer, halves away from zero. `60 × 0.7 = 42`.

- **`null`** whenever `durationMinutes` or `intensity` is missing — including on
  rows written before this branch. A missing value is not the same as zero: zero
  would drag a day's totals down, `null` correctly means "not measured".

**Reading it:** the unit is literal. "Of these 60 elapsed minutes, 42 counted as
active." The weight is capped at 1.0 precisely so that reading always holds —
active minutes can never exceed elapsed minutes.

**It is derived on read**, so retuning the weights changes the whole history at
once with no migration and no backfill. Clients must therefore treat it as
computed output: do not cache it as if it were user data, and do not attempt to
reproduce the arithmetic client-side, because the weights may change server-side
without an API version bump.

**What it is calibrated against: nothing, yet.** The weights are a working
heuristic modelled on human active-minute schemes, not a measured veterinary
constant. The property the system actually relies on is ordering — a harder
minute is never worth less than an easier one — which is covered by tests. Treat
the absolute number as meaningful only when compared against the same pet's own
history, not against any universal target. There is no per-species norm in the
system, because a husky's and a pug's differ by multiples.

## Activity validation errors (all `400`)

| Message | Cause |
|---|---|
| `Type is invalid.` | `type` is not one of the listed values |
| `Intensity is invalid.` | `intensity` is not one of the listed values |
| `DurationMinutes must be greater than zero.` | `durationMinutes` ≤ 0 |
| `DurationMinutes must be 1440 or less.` | `durationMinutes` > 1440 |
| `At least one of Type, DurationMinutes, Steps, Location or Note is required.` | Empty log |
| `RecordedAt cannot be in the future.` | More than 10 minutes ahead of server time |
| `Steps cannot be negative.` / `Steps must be 1000000 or less.` | `steps` out of range |
| `Location must be 200 characters or less.` | `location` too long |
| `Note must be 2000 characters or less.` | `note` too long |
| `Source is invalid.` | `source` is not a known value |
| `Activity source Device is not supported yet.` | Known source with no provider registered |

## Worked example

```jsonc
// Request
{
  "recordedAt": "2026-02-09T07:00:00Z",
  "steps": 5000,
  "type": "Run",
  "intensity": "Moderate",
  "durationMinutes": 60,
  "location": "Park"
}

// 201 Created
{
  "id": "7808645f-4d07-4921-8e92-9799dc8b8a40",
  "petId": "ea3c1adc-6e6d-4cd4-af3c-487ee9433821",
  "recordedAt": "2026-02-09T07:00:00Z",
  "steps": 5000,
  "type": "Run",
  "intensity": "Moderate",   // kept as sent; the Run default (High) did not apply
  "durationMinutes": 60,
  "activeMinutes": 42,       // 60 × 0.7, derived
  "location": "Park",
  "note": null,
  "source": "Manual",
  "createdAt": "2026-09-02T07:31:47.5809896Z"
}
```

---

# Part 2 — Sleep logs

`GET|POST /api/pets/{petId}/sleep-logs`
`GET|DELETE /api/pets/{petId}/sleep-logs/{sleepLogId}`

A new entity, so every field below is new.

## Why sleep is not an activity type

`SleepLog` is separate from `ActivityLog` rather than being `ActivityType.Sleep`
for two concrete reasons:

1. **It shares none of the shape.** Sleep has no steps, no location and no
   intensity. Every one of those fields would be permanently null, and
   `ActivityLog`'s "at least one field must be present" rule does not apply to
   it at all.
2. **It is read as a daily total, not as a session.** Everything that consumes
   `ActivityLogs` would have to remember to filter sleep rows out, and the one
   that forgot would silently corrupt a score.

## Request shape

```jsonc
POST /api/pets/{petId}/sleep-logs
{
  "sleepDate": "2026-02-09",   // required
  "hours": 12.5,               // required
  "note": "Restless night"     // optional
}
```

## `sleepDate` — the day the sleep belongs to

- **Type:** `DateTime`, required.
- **Stored as:** the date part only, pinned to UTC midnight. Any time-of-day
  component in the request is discarded.
- **Must not be in the future.** Today is accepted.

**Timezone handling — important for clients.** The date is taken at face value
as the owner's local day; it is **not** converted from the client's timezone.
Converting would push evening entries onto the neighbouring date for roughly
half the world. Send the date the user actually picked. Sending
`2026-02-09T23:45:00+02:00` stores `2026-02-09`, not the 10th.

## `hours` — how long the pet slept

- **Type:** `decimal`, required.
- **Range:** greater than `0`, at most `24`. Also enforced in the database
  (`CK_SleepLogs_HoursInRange`).
- **Precision:** `numeric(4,2)` — two decimal places, so quarter-hours work
  (`11.25`).

**Several rows may share one `sleepDate`.** Naps are logged individually, and a
future collar integration will report sleep sessions rather than a daily figure.
The API does not merge them; a day is the sum of its rows.

**The 24-hour ceiling is on the day's sum, not on the row.** A `POST` is
rejected when the existing rows for that date plus the new one would exceed 24
hours. This is what catches the same night being entered twice — the mistake a
unique index would have caught — without forbidding nap-by-nap logging.

**Client note:** because there is no update endpoint, correcting a day means
`DELETE` the wrong row and `POST` the right one. A UI that lets the user edit a
day should do exactly that.

## `note` — free text

- **Type:** `string?`, optional, max 2000 characters.
- Whitespace-only input is stored as `null`; surrounding whitespace is trimmed.

## `source` — response only

- **Type:** `ActivitySource` (string enum). Always `"Manual"` today.
- **Not accepted in requests.** The field exists so a device integration can be
  distinguished later; a client cannot claim to be one.
- A device feed currently writes `ActivityDaily.SleepHours` — a different table,
  not this one.

## `id`, `petId`, `createdAt` — response only

Standard: server-assigned `Guid`s and a UTC creation timestamp. `petId` mirrors
the route parameter.

## Query parameters on `GET`

| Parameter | Type | Meaning |
|---|---|---|
| `from` | `DateTime?` | Inclusive lower bound, normalised to a whole day |
| `to` | `DateTime?` | Inclusive upper bound, normalised to a whole day |

Both bounds are truncated to dates the same way `sleepDate` is, so a time of day
in the filter does not exclude entries. Results come back newest date first,
then newest created first. `from` later than `to` is a `400`.

## Sleep validation errors (all `400`)

| Message | Cause |
|---|---|
| `SleepDate cannot be in the future.` | Date after today |
| `Hours must be greater than zero.` | `hours` ≤ 0 |
| `Hours must be 24 or less.` | `hours` > 24 |
| `Sleep for 2026-02-09 would total more than 24 hours (20 already logged).` | The day's sum would exceed 24 |
| `Note must be 2000 characters or less.` | `note` too long |
| `From cannot be later than To.` | Inverted range on `GET` |

Note the day-total error is a `400`, not a `409`: the day is not a resource the
caller can discover a conflict against, it is a value the request got wrong. The
message names the date and the hours already logged so a client can offer to
delete the existing entry.

## Worked example

```jsonc
// Request
{ "sleepDate": "2026-02-09", "hours": 12.5, "note": "  Restless night  " }

// 201 Created
{
  "id": "3f1c9b02-77a1-4f0e-9a55-2c9c1c8f7d10",
  "petId": "ea3c1adc-6e6d-4cd4-af3c-487ee9433821",
  "sleepDate": "2026-02-09T00:00:00Z",  // time dropped, pinned to UTC midnight
  "hours": 12.5,
  "note": "Restless night",             // trimmed
  "source": "Manual",
  "createdAt": "2026-09-02T07:31:47.5809896Z"
}
```

---

# Not covered by this branch

The wellness score itself does not exist. `Models/WealnessScore.cs` defines a
`WellnessScore` entity and nothing writes to it; the classifier's `wellness`
route is not wired up.

Two decisions are therefore still open, and a client should not assume either
way:

- **Which source wins for a day.** `ActivityDaily` already has stored
  `ActiveMinutes` and `SleepHours` columns intended for a wearable feed, and
  nothing writes to them yet. A day could eventually have both a device
  aggregate and manual logs; the precedence rule is not written.
- **What the score compares against.** There are no per-species or per-breed
  activity or sleep targets in the system. Any "goal" shown in a UI today would
  be invented by that UI.
