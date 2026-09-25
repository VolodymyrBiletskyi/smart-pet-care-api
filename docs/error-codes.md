# Error contract v1

This is the shape a failed request answers with — everywhere except the
endpoints listed under *Legacy shape*, which are not migrated yet. `code` is the
contract: it is the only field a client may branch on. `message` is an English
fallback for a `code` the client does not know yet and must never be parsed.

```json
{
  "code": "weight_log_weight_too_large",
  "message": "WeightKg cannot be greater than 230.",
  "params": { "max": 230 },
  "traceId": "0HN7A2K9QJ3B4:00000012",
  "retryable": false,
  "retryAfterSeconds": 30,
  "errors": { "weightKg": ["The field WeightKg is invalid."] },
  "messageId": "0f1c…"
}
```

| Field | Always present | Meaning |
|---|---|---|
| `code` | no — see *Coverage* | The alias. Branch and localize on this. |
| `message` | yes | English fallback. Show only when `code` is unknown to the client. |
| `params` | no | Values embedded in the message — limits, ceilings, ranges. Present only where the message carries a number, so the client never hardcodes a server rule. |
| `traceId` | yes | The request id, also written to the server log. Put it in bug reports. |
| `retryable` | no | Whether repeating the identical request could succeed. |
| `retryAfterSeconds` | no | Mirrors the `Retry-After` header on 429 and 503. |
| `errors` | no | Per-field detail for request-body validation, keyed by field name. |
| `messageId` | no | Chat only: which message the failure belongs to. |

Two rules for the client:

1. **An unknown `code` must not break anything.** The catalogue grows; fall back
   to `message` for anything not in your translation table.
2. **A missing `code` must not break anything either.** Some modules still answer
   without one — see below.

## Coverage

Aliases are being rolled out module by module.

| Module | `code` on errors | Shape |
|---|---|---|
| Pet, Feeding, Chat, Weight history, Wellness | yes | `ApiErrorResponse` |
| Nutrition | partial — one alias per class of failure, not per rule | `ApiErrorResponse` |
| Activity, Sleep, Health, Journal | no | `ApiErrorResponse` without `code` |
| Auth, Reminder, User, Profile, Notification | see *Legacy shape* | anonymous object |

## Legacy shape

Auth, Reminder, User, Profile and Notification have not been migrated and answer
with a bare object instead of `ApiErrorResponse`:

```json
{ "message": "Reminder not found" }
```

No `traceId`, no `retryable`, and usually no `code`. The one exception is the
email confirmation flow, which does carry a `code` — in an older screaming-case
convention that predates these aliases:

| Code | HTTP | When |
|---|---|---|
| `CONFIRMATION_CODE_INVALID` | 400 | The submitted code does not match. |
| `CONFIRMATION_CODE_EXPIRED` | 410 | The code is past its lifetime; request a new one. |
| `EMAIL_ALREADY_CONFIRMED` | 409 | Confirming an address that is already confirmed. |
| `CONFIRMATION_TOO_MANY_ATTEMPTS` | 429 | Too many wrong codes; request a new one. |
| `CONFIRMATION_RESEND_TOO_SOON` | 429 | A code was sent recently. |
| `EMAIL_NOT_CONFIRMED` | 403 | Login before the address was confirmed. |

These strings stay as they are until the client can ship a release that accepts
the lower-case forms; do not assume the casing of an alias from this table
matches the rest of the catalogue.

## Unexpected failures

Anything the server did not deliberately name answers `500` with
`code: internal_error` and a generic message. The detail is in the log under the
same `traceId`. A 500 is always a bug — report it rather than handling it.

## Catalogue

### Common

| Code | HTTP | When |
|---|---|---|
| `internal_error` | 500 | Unhandled failure. Always a bug. |
| `request_validation_failed` | 400 | Request body failed model binding. Detail in `errors`. |
| `authentication_token_invalid` | 401 | Token present but its user id is missing or unparseable. |
| `pet_not_found` | 404 | The pet does not exist, or does not belong to the caller. |
| `reminder_not_found` | 404 | A `reminderId` in the body matches no reminder for this pet. |

### Pet

| Code | HTTP | When |
|---|---|---|
| `pet_name_required` | 400 | Create without a name. |
| `pet_name_empty` | 400 | Patch set the name to blank. |
| `pet_species_required` | 400 | Create without a species. |
| `pet_species_invalid` | 400 | Species is not one of the known values. |
| `pet_sex_invalid` | 400 | Sex is not one of the known values. |
| `pet_update_empty` | 400 | Patch body had no fields at all. |
| `pet_birth_date_in_future` | 400 | Birth date is later than today. |
| `pet_weight_not_positive` | 400 | Weight is zero or negative. |
| `pet_weight_too_large` | 400 | Weight above the ceiling. |
| `pet_photo_required` | 400 | Photo upload with no file. |
| `pet_photo_url_empty` | 400 | Patch cleared the photo URL to blank. |
| `pet_photo_public_id_empty` | 400 | Patch cleared the photo public id to blank. |
| `pet_photo_type_invalid` | 400 | File is not JPEG, PNG or WEBP. |
| `pet_photo_too_large` | 400 | File is over 5 MB. |
| `pet_validation_failed` | 400 | Validation failure with no more specific alias. |
| `pet_photo_upload_failed` | 502 | Cloudinary rejected the upload. |

### Weight history

Fully migrated; this is the shape the other modules are moving to.

| Code | HTTP | `params` | When |
|---|---|---|---|
| `weight_log_not_found` | 404 | — | No such log for this pet. |
| `weight_log_update_empty` | 400 | — | Patch body had no fields. |
| `weight_log_weight_not_positive` | 400 | — | Weight is zero or negative. |
| `weight_log_weight_too_large` | 400 | `max` | Weight above the ceiling. |
| `weight_log_measurement_time_required` | 400 | — | `measuredAt` missing or default. |
| `weight_log_measurement_time_too_far_in_future` | 400 | `maxMinutes` | `measuredAt` beyond the future tolerance. |
| `weight_log_measurement_time_conflict` | 409 | — | This pet already has a log at that instant. |
| `weight_log_date_range_invalid` | 400 | — | `from` is later than `to`. |
| `weight_log_notes_empty` | 400 | — | Notes present but whitespace only. |

### Feeding

| Code | HTTP | When |
|---|---|---|
| `feeding_log_not_found` | 404 | No such log for this pet. |
| `feeding_update_empty` | 400 | Patch body had no fields. |
| `feeding_time_required` | 400 | `fedAt` missing. |
| `feeding_time_too_far_in_future` | 400 | `fedAt` beyond the future tolerance. |
| `feeding_portion_amount_negative` | 400 | Portion amount below zero. |
| `feeding_portion_unit_required` | 400 | Portion amount given without a unit. |
| `feeding_portion_unit_invalid` | 400 | Unit is not one of the known values. |
| `feeding_calories_negative` | 400 | Calories below zero. |
| `feeding_food_type_invalid` | 400 | Food type is not one of the known values. |
| `feeding_validation_failed` | 400 | Validation failure with no more specific alias. |

### Chat

| Code | HTTP | When |
|---|---|---|
| `chat_session_not_found` | 404 | No such session for this user. |
| `chat_message_not_found` | 404 | No such message in this session. |
| `chat_pet_id_required` | 400 | Session created without a pet id. |
| `chat_page_limit_invalid` | 400 | `limit` outside the allowed range. |
| `chat_cursor_invalid` | 400 | Pagination cursor is malformed. |
| `chat_message_text_required` | 400 | Empty message text. |
| `chat_message_text_too_long` | 400 | Message text over the ceiling. |
| `chat_client_message_id_required` | 400 | Post without a client message id. |
| `chat_client_message_id_conflict` | 409 | Same client message id reused with different text. |
| `chat_message_not_retryable` | 409 | Retry on a message that is not a failed retryable one. |
| `chat_message_processing_or_retry_required` | 409 | The message is in flight, or needs an explicit retry. |
| `chat_stored_response_invalid` | 409 | The stored assistant response cannot be read back. |
| `chat_request_invalid` | 400 | Request failure with no more specific alias. |
| `chat_state_conflict` | 409 | State conflict with no more specific alias. |

### Wellness

| Code | HTTP | When |
|---|---|---|
| `wellness_insufficient_data` | 422 | Not enough logged data to score. Nothing is stored; add data and retry. |
| `wellness_history_query_invalid` | 400 | `page` or `pageSize` outside the allowed range. |
| `wellness_service_rate_limited` | 429 | Classifier rate limit. |
| `wellness_service_invalid_response` | 502 | Classifier returned something off-contract. |
| `wellness_service_unavailable` | 503 | Classifier unreachable. |

### Nutrition

| Code | HTTP | When |
|---|---|---|
| `nutrition_analysis_invalid` | 400 | Any validation failure in the analysis request, including a missing usable weight. |

Coarse on purpose for now: the module still raises framework exceptions, so its
rules do not yet have one alias each.

### Classifier

Chat, wellness and nutrition all talk to the Python classifier and surface its
failures the same way.

| Code | HTTP | When |
|---|---|---|
| `classifier_invalid_response` | 502 | Response broke the contract. `retryable: false`. |
| `service_unavailable` | 503 | Unreachable or timed out. `retryable: true`. |

On 429 and 503 the classifier may supply its own code — `rate_limit_exceeded`,
`service_overloaded`, `request_timeout` and others — which is passed through
unchanged. Treat any unrecognised code on these two statuses as a transient
failure and use `retryAfterSeconds`.

## Adding a code

Add the constant to `Common/Api/ErrorCodes.cs`, throw the matching
`AppException` (`NotFoundException`, `ValidationException`, `ConflictException`,
`UnprocessableException`) from the service, and add a row here. Do not derive a
code from an exception message: rewording the message would silently change the
code a client sees.
