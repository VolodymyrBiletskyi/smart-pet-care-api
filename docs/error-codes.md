# Error contract v1

Every failed request answers with this shape, including the ones the framework
produces itself — an expired token, a rejected body, an unhandled bug. `code` is
the contract: it is the only field a client may branch on. `message` is an
English fallback for a `code` the client does not know yet and must never be
parsed.

```json
{
  "code": "weight_log_weight_too_large",
  "message": "WeightKg cannot be greater than 230.",
  "params": { "max": 230 },
  "traceId": "0HN7A2K9QJ3B4:00000012",
  "retryable": false,
  "retryAfterSeconds": 30,
  "errors": { "weightKg": ["weight_log_measurement_time_required"] },
  "messageId": "0f1c…"
}
```

| Field | Always present | Meaning |
|---|---|---|
| `code` | yes | The alias. Branch and localize on this. |
| `message` | yes | English fallback. Show only when `code` is unknown to the client. |
| `params` | no | Values embedded in the message — limits, ceilings, ranges. Present only where the message carries a number, so the client never hardcodes a server rule. |
| `traceId` | yes | The request id, also written to the server log. Put it in bug reports. |
| `retryable` | no | Whether repeating the identical request could succeed. |
| `retryAfterSeconds` | no | Mirrors the `Retry-After` header on 429 and 503. |
| `errors` | no | Per-field aliases for request-body validation, keyed by field name. |
| `messageId` | no | Chat only: which message the failure belongs to. |

One rule for the client: **an unknown `code` must not break anything.** The
catalogue grows without waiting for a client release, so fall back to `message`
for anything not in your translation table.

## Coverage

Every module, and every path into an error: a controller returning a result, a
service throwing, the bearer scheme rejecting a token, and anything unhandled.
Field-level entries inside `errors` are aliases too, so a form can localize
every message it shows.

## What changed, and what will break

Before this release the error body had three different shapes depending on which
controller answered, and several statuses were wrong. Both were fixed at once,
so **some endpoints now answer with a different HTTP status than they used to**.
Nothing in the catalogue is new-only: if your client branches on status, read
this table before shipping.

| Situation | Was | Now |
|---|---|---|
| Token present but carrying no usable user id | `500` on every route except one | `401` `authentication_token_invalid` |
| 401 and 403 from the bearer scheme | correct status, **empty body** | same status with a full body and a `code` |
| "Pet not found" on reminder list, create and update | `400` | `404` `pet_not_found` |
| Google sign-in failure | any internal fault became `401` | a real rejection is `401` `auth_google_failed`; a bug is `500` |
| An exception nobody designed for | disguised as `400` or `404` with a plausible message | `500` `internal_error` |

Two consequences worth planning for:

- **The 5xx rate will rise after this deploy.** Those are not new breakages —
  they are bugs that used to be reported as business errors. If you alert on the
  5xx share, expect it to move.
- **A client that refreshes on every 401 will now loop.** Two of the three 401
  codes must not trigger a refresh. See below.

## What to do per status

`code` is what you localize; the status is what decides the shape of the
reaction. This is the whole decision table:

| Status | Meaning | Client should |
|---|---|---|
| `400` | The request is wrong | Show the message next to the field or form. Do not retry unchanged. |
| `401` | Not authenticated | Depends on the code — see the next section. Never blindly refresh. |
| `403` | Authenticated, not allowed | Show the message. A refresh will not help. |
| `404` | No such object, or not the caller's | Treat "not yours" and "does not exist" as the same thing; the server does not distinguish them on purpose, so an id cannot be probed. |
| `409` | Conflict with something that exists | Usually means the action already happened. Re-read the resource before offering a retry. |
| `410` | It existed and expired | Only email confirmation codes. Offer to request a new one. |
| `422` | Well-formed, but the state does not allow it | Today only `wellness_insufficient_data`. Nothing was stored; tell the user what to add. |
| `429` | Rate limited | Back off for `retryAfterSeconds`, then retry. |
| `500` | A bug | Show a generic apology, log the `traceId`. Do not retry — it will fail identically. |
| `502` / `503` | A dependency failed | `retryable` says whether a retry can help. |

## Authentication: the three 401s

These are not synonyms, and telling them apart is the one place where getting it
wrong produces an infinite loop rather than a wrong message:

| Code | What happened | Client should |
|---|---|---|
| `auth_authentication_required` | No token, expired, or failed validation | **Refresh**, then replay the request once |
| `auth_refresh_token_invalid` | The refresh token is unknown, expired or spent | **Do not refresh.** Clear the session, go to login |
| `auth_account_no_longer_exists` | The token is valid, the account is gone | **Do not refresh.** Clear the session, go to login |
| `authentication_token_invalid` | The token parses but carries no usable user id | **Do not refresh.** Clear the session, go to login |
| `auth_invalid_credentials` | Wrong email or password at login | Show the message. Not a session problem |
| `auth_google_failed` | Google rejected the token or the exchange failed | Show the message, offer the flow again |

Rule of thumb: refresh on exactly one code. Everything else on 401 means the
session is finished.

## Retrying

Two fields carry this, and both are absent unless they apply:

- `retryable` — `true` means the identical request could succeed later. It
  appears on dependency failures (`502`, `503`, `429`), never on `400`-class
  errors, where the fix is to change the request.
- `retryAfterSeconds` — mirrors the `Retry-After` header. Present on `429`, and
  on `503` when the dependency said how long.

For assistant-backed features (chat, wellness, feeding analysis) treat **any**
unrecognised code on `429` or `503` as transient and honour `retryAfterSeconds`;
those codes come from the classifier at runtime and the list below cannot be
exhaustive.

## Email confirmation aliases

Every module now answers with `ApiErrorResponse` and a `code`. One group keeps an
older screaming-case convention, because the mobile client already branches on
the exact strings and renaming them means shipping a client release first:

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
| `request_validation_failed` | 400 | Request body failed model binding. Per-field aliases in `errors` — see below. |
| `authentication_token_invalid` | 401 | Token present but its user id is missing or unparseable. |
| `pet_not_found` | 404 | The pet does not exist, or does not belong to the caller. |
| `reminder_not_found` | 404 | A `reminderId` in the body matches no reminder for this pet. |

### Field-level aliases

These appear as the **values** inside `errors`, not as the top-level `code`,
which is always `request_validation_failed` for this class of failure:

```json
{
  "code": "request_validation_failed",
  "message": "Request validation failed.",
  "traceId": "0HNOR41IID7C8:00000004",
  "errors": {
    "email": ["auth_email_invalid"],
    "password": ["auth_password_too_short", "auth_password_too_weak"]
  }
}
```

A field can carry more than one alias when several rules failed at once.

| Code | Field it guards |
|---|---|
| `auth_email_required` | Email on register, login, confirm, resend. |
| `auth_email_invalid` | Email is not an address, or has no domain part. |
| `auth_password_required` | Password on register and login. |
| `auth_password_too_short` | Password under the minimum length. |
| `auth_password_too_weak` | Password lacks a letter, a digit or a symbol. |
| `auth_password_confirm_required` | Confirmation field empty. |
| `auth_passwords_do_not_match` | The two password fields differ. |
| `auth_terms_not_accepted` | Terms checkbox not ticked. |
| `auth_confirmation_code_required` | Confirmation code empty. |
| `auth_confirmation_code_malformed` | Code is not six digits. |
| `chat_pet_id_required` | Pet id on session create. |
| `chat_client_message_id_required` | Client message id on post. |
| `chat_message_text_required` | Message text empty. |
| `chat_message_text_too_long` | Message text over the ceiling. |
| `pet_species_required` | Species on pet create. |
| `feeding_time_required` | `fedAt` on feeding create. |
| `weight_log_measurement_time_required` | `measuredAt` on weight create. |
| `reminder_utc_offset_required` | `utcOffsetMinutes` on reminder create. |
| `nutrition_breed_too_long` | Breed override too long. |
| `nutrition_weight_out_of_range` | `weightKg` override outside the analysable range. |
| `nutrition_age_out_of_range` | `ageMonths` override outside the range. |
| `nutrition_products_too_many` | More products than one call may analyse. |
| `nutrition_product_name_required` | A product without a name. |
| `nutrition_product_name_too_long` | Product name too long. |
| `nutrition_product_calories_out_of_range` | Product calories outside the range. |

### Pet

| Code | HTTP | `params` | When |
|---|---|---|---|
| `pet_name_required` | 400 | — | Create without a name. |
| `pet_name_empty` | 400 | — | Patch set the name to blank. |
| `pet_species_required` | 400 | — | Create without a species. |
| `pet_species_invalid` | 400 | — | Species is not one of the known values. |
| `pet_sex_invalid` | 400 | — | Sex is not one of the known values. |
| `pet_update_empty` | 400 | — | Patch body had no fields at all. |
| `pet_birth_date_in_future` | 400 | — | Birth date is later than today. |
| `pet_weight_not_positive` | 400 | — | Weight is zero or negative. |
| `pet_weight_too_large` | 400 | `max` | Weight above the ceiling. |
| `pet_photo_required` | 400 | — | Photo upload with no file. |
| `pet_photo_url_empty` | 400 | — | Patch cleared the photo URL to blank. |
| `pet_photo_public_id_empty` | 400 | — | Patch cleared the photo public id to blank. |
| `pet_photo_type_invalid` | 400 | `allowed` | File is not JPEG, PNG or WEBP. `allowed` is the array of accepted MIME types. |
| `pet_photo_too_large` | 400 | `maxMegabytes` | File over the ceiling. |
| `pet_photo_upload_failed` | 502 | — | Cloudinary rejected the upload. `retryable: true`. |

### Weight history

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

| Code | HTTP | `params` | When |
|---|---|---|---|
| `feeding_log_not_found` | 404 | — | No such log for this pet. |
| `feeding_update_empty` | 400 | — | Patch body had no fields. |
| `feeding_time_required` | 400 | — | `fedAt` missing. |
| `feeding_time_too_far_in_future` | 400 | `maxMinutes` | `fedAt` beyond the future tolerance. |
| `feeding_portion_amount_negative` | 400 | — | Portion amount below zero. |
| `feeding_portion_unit_required` | 400 | — | Portion amount given without a unit. |
| `feeding_portion_unit_invalid` | 400 | — | Unit is not one of the known values. |
| `feeding_calories_negative` | 400 | — | Calories below zero. |
| `feeding_food_type_invalid` | 400 | — | Food type is not one of the known values. |

### Chat

| Code | HTTP | `params` | When |
|---|---|---|---|
| `chat_session_not_found` | 404 | — | No such session for this user. |
| `chat_message_not_found` | 404 | — | No such message in this session. |
| `chat_pet_id_required` | 400 | — | Session created without a pet id. |
| `chat_page_limit_invalid` | 400 | `min`, `max` | `limit` outside the allowed range. |
| `chat_cursor_invalid` | 400 | — | Pagination cursor is malformed. |
| `chat_message_text_required` | 400 | — | Empty message text. |
| `chat_message_text_too_long` | 400 | `maxLength` | Message text over the ceiling. |
| `chat_client_message_id_required` | 400 | — | Post without a client message id. |
| `chat_client_message_id_conflict` | 409 | — | Same client message id reused with different text. |
| `chat_message_not_retryable` | 409 | — | Retry on a message that is not a failed retryable one. |
| `chat_message_processing_or_retry_required` | 409 | — | The message is in flight, or needs an explicit retry. |
| `chat_stored_response_invalid` | 409 | — | The stored assistant response cannot be read back. |

Chat failures carry `messageId` whenever the failure belongs to a specific
message, including the classifier `429` and `503` below. Use it to mark the
right bubble as failed rather than the last one sent — the two differ as soon as
the user sends a second message while the first is still in flight.

### Wellness

| Code | HTTP | `params` | When |
|---|---|---|---|
| `wellness_insufficient_data` | 422 | — | Not enough logged data to score. Nothing is stored; add data and retry. |
| `wellness_history_query_invalid` | 400 | `min`, `max` | `page` or `pageSize` outside the allowed range. |
| `wellness_service_rate_limited` | 429 | — | Classifier rate limit. `retryable: true`, honour `retryAfterSeconds`. |
| `wellness_service_invalid_response` | 502 | — | Classifier returned something off-contract. `retryable: false`. |
| `wellness_service_unavailable` | 503 | — | Classifier unreachable. `retryable: true`. |

Wellness wraps the classifier's failures in its own three `wellness_service_*`
codes rather than passing the classifier's through, so a wellness screen only
ever has to know these. Chat is the opposite — see the Classifier section.

### Activity

| Code | HTTP | `params` | When |
|---|---|---|---|
| `activity_log_not_found` | 404 | — | No such log for this pet. |
| `activity_update_empty` | 400 | — | Patch body had no fields. |
| `activity_source_invalid` | 400 | — | `source` is not one of the known values. |
| `activity_source_not_supported` | 400 | — | A known source with no provider registered yet. |
| `activity_date_range_invalid` | 400 | — | `from` is later than `to`. |
| `activity_recorded_at_in_future` | 400 | — | `recordedAt` is in the future. |
| `activity_steps_negative` | 400 | — | Steps below zero. |
| `activity_steps_too_large` | 400 | `max` | Steps above the ceiling. |
| `activity_type_invalid` | 400 | — | Type is not one of the known values. |
| `activity_intensity_invalid` | 400 | — | Intensity is not one of the known values. |
| `activity_duration_not_positive` | 400 | — | Duration is zero or negative. |
| `activity_duration_too_long` | 400 | `max` | Duration above the ceiling. |
| `activity_location_too_long` | 400 | `maxLength` | Place text too long. |
| `activity_note_too_long` | 400 | `maxLength` | Note too long. |
| `activity_nothing_recorded` | 400 | — | A log has to record something: type, duration, steps, place or note. |

### Sleep

| Code | HTTP | `params` | When |
|---|---|---|---|
| `sleep_log_not_found` | 404 | — | No such log for this pet. |
| `sleep_update_empty` | 400 | — | Patch body had no fields. |
| `sleep_date_range_invalid` | 400 | — | `from` is later than `to`. |
| `sleep_date_in_future` | 400 | — | `sleepDate` is in the future. |
| `sleep_hours_not_positive` | 400 | — | Hours zero or negative. |
| `sleep_hours_too_large` | 400 | `max` | A single row above the ceiling. |
| `sleep_note_too_long` | 400 | `maxLength` | Note too long. |
| `sleep_daily_hours_exceeded` | 400 | `maxHoursPerDay`, `alreadyLogged` | The day's **sum** would exceed the cap. A 400 rather than a 409: the day is a value the request got wrong, not a resource to conflict with. |

### Health

| Code | HTTP | `params` | When |
|---|---|---|---|
| `health_record_not_found` | 404 | — | No such record for this pet. |
| `health_date_range_invalid` | 400 | — | `from` is later than `to`. |
| `health_type_invalid` | 400 | — | Type is not one of the known values. |
| `health_title_required` | 400 | — | Title missing. |
| `health_title_too_long` | 400 | `maxLength` | Title too long. |
| `health_performed_at_in_future` | 400 | — | `performedAt` is in the future. |
| `health_next_due_before_performed` | 400 | — | `nextDueAt` is earlier than `performedAt`. |
| `health_symptom_invalid` | 400 | — | Symptom is not one of the known values. |
| `health_description_too_long` | 400 | `maxLength` | Description too long. |
| `health_dosage_too_long` | 400 | `maxLength` | Dosage too long. |
| `health_provider_too_long` | 400 | `maxLength` | Provider too long. |

### Journal

| Code | HTTP | `params` | When |
|---|---|---|---|
| `journal_entry_not_found` | 404 | — | No such entry for this pet. |
| `journal_date_range_invalid` | 400 | — | `from` is later than `to`. |
| `journal_type_invalid` | 400 | — | Type is not one of the known values. |
| `journal_severity_invalid` | 400 | — | Severity is not one of the known values. |
| `journal_symptom_invalid` | 400 | — | Symptom is not one of the known values. |
| `journal_title_required` | 400 | — | Title missing. |
| `journal_title_too_long` | 400 | `maxLength` | Title too long. |
| `journal_observed_at_in_future` | 400 | — | `observedAt` is in the future. |
| `journal_notes_too_long` | 400 | `maxLength` | Notes too long. |

### Notes

| Code | HTTP | `params` | When |
|---|---|---|---|
| `note_not_found` | 404 | — | No such note for this pet. |
| `note_update_empty` | 400 | — | A `PATCH` body with no fields set. |
| `note_title_required` | 400 | — | Title missing, blank, or cleared by a patch. |
| `note_title_too_long` | 400 | `maxLength` | Title too long. |
| `note_content_required` | 400 | — | Content missing, blank, or cleared by a patch. |
| `note_content_too_long` | 400 | `maxLength` | Content too long. |

### Reminder

| Code | HTTP | `params` | When |
|---|---|---|---|
| `reminder_not_found` | 404 | — | No such reminder for this user. |
| `reminder_run_not_found` | 404 | — | No such occurrence. |
| `reminder_run_already_acknowledged` | 409 | — | The occurrence was already acknowledged. |
| `reminder_end_at_not_in_future` | 400 | — | `endAt` is not in the future. |
| `reminder_date_not_in_future` | 400 | — | A one-off reminder's date is not in the future. |
| `reminder_date_range_invalid` | 400 | `maxDays` | `from` later than `to`, or a window beyond the horizon. |
| `reminder_schedule_invalid` | 400 | `min`, `max` | The repeat rule does not hold together — interval out of range, weekly without days, daily with a date, and so on. |
| `reminder_performed_at_in_future` | 400 | — | A completion dated in the future. |
| `reminder_note_too_long` | 400 | `maxLength` | Completion note too long. |

### Nutrition

| Code | HTTP | `params` | When |
|---|---|---|---|
| `nutrition_goal_not_found` | 404 | — | No goal set for this pet. |
| `nutrition_utc_offset_invalid` | 400 | — | `utcOffsetMinutes` outside the allowed range. |
| `nutrition_calorie_target_negative` | 400 | — | Calorie target below zero. |
| `nutrition_portion_target_negative` | 400 | — | Portion target below zero. |
| `nutrition_meals_per_day_negative` | 400 | — | Meals per day below zero. |
| `nutrition_portion_unit_invalid` | 400 | — | Unit is not one of the known values. |
| `nutrition_portion_unit_required` | 400 | — | Portion target given without a unit. |
| `nutrition_weight_required` | 400 | — | No weight on the pet and none supplied, so the analysis cannot run. |
| `nutrition_weight_out_of_range` | 400 | `max` | A weight is present but outside what can be analysed. |

The two weight aliases are separate because the user's fix differs: one means
record a weight, the other means correct the one that is there.

### Auth

| Code | HTTP | When |
|---|---|---|
| `auth_authentication_required` | 401 | No token, an expired one, or one that fails validation. Refresh, then retry. |
| `auth_account_no_longer_exists` | 401 | The token is valid but its account is gone. Refreshing will not help — clear the session and send the user to login. |
| `auth_refresh_token_invalid` | 401 | The refresh token is unknown, expired or already used. Same: start a new session. |
| `auth_forbidden` | 403 | Authenticated, but not allowed to do this. |
| `auth_email_already_taken` | 409 | Registering an address that already has an account. |
| `auth_account_not_found` | 404 | No account for the address. |
| `auth_invalid_credentials` | 401 | Wrong email or password. |
| `auth_google_failed` | 401 | Google rejected the token or the exchange failed. |
| `auth_oauth_code_required` | 400 | OAuth callback without a code. |

### User

| Code | HTTP | `params` | When |
|---|---|---|---|
| `user_not_found` | 404 | — | No such user. |
| `user_avatar_not_found` | 404 | — | The user exists but has no avatar stored. |
| `user_delete_forbidden` | 403 | — | Deleting an account that is not the caller's. |
| `user_photo_required` | 400 | — | Upload with no file. |
| `user_photo_type_invalid` | 400 | — | File is not JPEG, PNG or WebP. |
| `user_photo_too_large` | 400 | `maxMegabytes` | File over the ceiling. |

### Notification

| Code | HTTP | When |
|---|---|---|
| `device_token_required` | 400 | Device registration without a token. |

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

*(Backend note — the client does not need this section.)*

Add the constant to `Common/Api/ErrorCodes.cs`, throw the matching
`AppException` subclass from the service, and add a row to the catalogue above.
`Tests/CommonApi.Tests/ErrorCodeCatalogueTests.cs` fails the build if a constant
is undocumented or duplicated, so the two cannot drift apart.

The subclass picks the status; nothing in the controller does:

| Throw | Status |
|---|---|
| `ValidationException(code, message, params?)` | 400 |
| `UnauthorizedException(code, message)` | 401 |
| `ForbiddenException(code, message)` | 403 |
| `NotFoundException(code, message)` | 404 |
| `ConflictException(code, message)` | 409 |
| `GoneException(code, message)` | 410 |
| `UnprocessableException(code, message)` | 422 |
| `TooManyRequestsException(code, message)` | 429 |
| `UpstreamException(code, status, message, retryable, retryAfterSeconds?)` | as given |

Two rules behind this:

- **Do not derive a code from an exception message.** Rewording a message would
  silently change the code a client sees.
- **Do not catch a domain failure in a controller.** `GlobalExceptionHandler`
  turns any `AppException` into the response above, and anything that is *not*
  one becomes `internal_error` with a `500` — which is the point. A bare
  `catch (InvalidOperationException)` per controller is how bugs used to get
  reported as business errors and stay invisible.

Whenever a message carries a number, pass that number in `params` too. The
client is not supposed to know the server's limits, and a hardcoded "5 MB" in
the UI is a bug waiting for the ceiling to move.

## Appendix: every code, for the translation table

All 156 aliases in one block, ready to seed an i18n file. Field-level aliases
are included — they appear inside `errors` and need text just as much. Values
are intentionally blank: the English text lives in `message`, and this file is
for your own translations.

```json
{
  "internal_error": "",
  "request_validation_failed": "",
  "authentication_token_invalid": "",
  "pet_not_found": "",
  "reminder_not_found": "",

  "auth_email_required": "",
  "auth_email_invalid": "",
  "auth_password_required": "",
  "auth_password_too_short": "",
  "auth_password_too_weak": "",
  "auth_password_confirm_required": "",
  "auth_terms_not_accepted": "",
  "auth_passwords_do_not_match": "",
  "auth_confirmation_code_required": "",
  "auth_confirmation_code_malformed": "",
  "reminder_utc_offset_required": "",
  "nutrition_breed_too_long": "",
  "nutrition_age_out_of_range": "",
  "nutrition_products_too_many": "",
  "nutrition_product_name_required": "",
  "nutrition_product_name_too_long": "",
  "nutrition_product_calories_out_of_range": "",

  "pet_name_required": "",
  "pet_name_empty": "",
  "pet_species_required": "",
  "pet_species_invalid": "",
  "pet_sex_invalid": "",
  "pet_update_empty": "",
  "pet_birth_date_in_future": "",
  "pet_weight_not_positive": "",
  "pet_weight_too_large": "",
  "pet_photo_required": "",
  "pet_photo_url_empty": "",
  "pet_photo_public_id_empty": "",
  "pet_photo_type_invalid": "",
  "pet_photo_too_large": "",
  "pet_photo_upload_failed": "",

  "weight_log_not_found": "",
  "weight_log_update_empty": "",
  "weight_log_weight_not_positive": "",
  "weight_log_weight_too_large": "",
  "weight_log_measurement_time_required": "",
  "weight_log_measurement_time_too_far_in_future": "",
  "weight_log_measurement_time_conflict": "",
  "weight_log_date_range_invalid": "",
  "weight_log_notes_empty": "",

  "feeding_log_not_found": "",
  "feeding_update_empty": "",
  "feeding_time_required": "",
  "feeding_time_too_far_in_future": "",
  "feeding_portion_amount_negative": "",
  "feeding_portion_unit_required": "",
  "feeding_portion_unit_invalid": "",
  "feeding_calories_negative": "",
  "feeding_food_type_invalid": "",

  "activity_log_not_found": "",
  "activity_update_empty": "",
  "activity_source_invalid": "",
  "activity_date_range_invalid": "",
  "activity_recorded_at_in_future": "",
  "activity_steps_negative": "",
  "activity_type_invalid": "",
  "activity_intensity_invalid": "",
  "activity_duration_not_positive": "",
  "activity_duration_too_long": "",
  "activity_steps_too_large": "",
  "activity_location_too_long": "",
  "activity_note_too_long": "",
  "activity_source_not_supported": "",
  "activity_nothing_recorded": "",

  "sleep_log_not_found": "",
  "sleep_update_empty": "",
  "sleep_date_range_invalid": "",
  "sleep_date_in_future": "",
  "sleep_hours_not_positive": "",
  "sleep_hours_too_large": "",
  "sleep_note_too_long": "",
  "sleep_daily_hours_exceeded": "",

  "health_record_not_found": "",
  "health_date_range_invalid": "",
  "health_type_invalid": "",
  "health_title_required": "",
  "health_title_too_long": "",
  "health_performed_at_in_future": "",
  "health_next_due_before_performed": "",
  "health_symptom_invalid": "",
  "health_description_too_long": "",
  "health_dosage_too_long": "",
  "health_provider_too_long": "",

  "journal_entry_not_found": "",
  "journal_date_range_invalid": "",
  "journal_type_invalid": "",
  "journal_severity_invalid": "",
  "journal_symptom_invalid": "",
  "journal_title_required": "",
  "journal_title_too_long": "",
  "journal_observed_at_in_future": "",
  "journal_notes_too_long": "",

  "note_not_found": "",
  "note_update_empty": "",
  "note_title_required": "",
  "note_title_too_long": "",
  "note_content_required": "",
  "note_content_too_long": "",

  "reminder_run_not_found": "",
  "reminder_run_already_acknowledged": "",
  "reminder_end_at_not_in_future": "",
  "reminder_date_not_in_future": "",
  "reminder_date_range_invalid": "",
  "reminder_schedule_invalid": "",
  "reminder_performed_at_in_future": "",
  "reminder_note_too_long": "",

  "auth_email_already_taken": "",
  "auth_account_not_found": "",
  "auth_invalid_credentials": "",
  "auth_google_failed": "",
  "auth_refresh_token_invalid": "",
  "auth_oauth_code_required": "",
  "auth_authentication_required": "",
  "auth_account_no_longer_exists": "",
  "auth_forbidden": "",

  "user_not_found": "",
  "user_photo_required": "",
  "user_photo_type_invalid": "",
  "user_photo_too_large": "",
  "user_avatar_not_found": "",
  "user_delete_forbidden": "",

  "device_token_required": "",

  "chat_session_not_found": "",
  "chat_message_not_found": "",
  "chat_pet_id_required": "",
  "chat_page_limit_invalid": "",
  "chat_cursor_invalid": "",
  "chat_message_text_required": "",
  "chat_message_text_too_long": "",
  "chat_client_message_id_required": "",
  "chat_client_message_id_conflict": "",
  "chat_message_not_retryable": "",
  "chat_message_processing_or_retry_required": "",
  "chat_stored_response_invalid": "",

  "nutrition_weight_required": "",
  "nutrition_weight_out_of_range": "",
  "nutrition_goal_not_found": "",
  "nutrition_utc_offset_invalid": "",
  "nutrition_calorie_target_negative": "",
  "nutrition_portion_target_negative": "",
  "nutrition_meals_per_day_negative": "",
  "nutrition_portion_unit_invalid": "",
  "nutrition_portion_unit_required": "",

  "wellness_insufficient_data": "",
  "wellness_history_query_invalid": "",
  "wellness_service_rate_limited": "",
  "wellness_service_invalid_response": "",
  "wellness_service_unavailable": "",

  "CONFIRMATION_CODE_INVALID": "",
  "CONFIRMATION_CODE_EXPIRED": "",
  "EMAIL_ALREADY_CONFIRMED": "",
  "CONFIRMATION_TOO_MANY_ATTEMPTS": "",
  "CONFIRMATION_RESEND_TOO_SOON": "",
  "EMAIL_NOT_CONFIRMED": "",

  "classifier_invalid_response": "",
  "service_unavailable": ""
}
```

This list is a snapshot, not a closed set. The classifier adds its own codes on
`429` and `503` at runtime, and the catalogue grows between client releases —
so keep the fallback to `message` for anything missing here.
