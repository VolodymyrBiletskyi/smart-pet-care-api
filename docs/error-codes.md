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
  "errors": { "weightKg": ["The field WeightKg is invalid."] },
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
| `errors` | no | Per-field detail for request-body validation, keyed by field name. |
| `messageId` | no | Chat only: which message the failure belongs to. |

One rule for the client: **an unknown `code` must not break anything.** The
catalogue grows without waiting for a client release, so fall back to `message`
for anything not in your translation table.

## Coverage

Every module, and every path into an error: a controller returning a result, a
service throwing, the bearer scheme rejecting a token, and anything unhandled.
The one place an alias is deliberately coarse is the nutrition **analysis**
path, where all validation shares `nutrition_analysis_invalid` — see that
section for why.

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

| Code | HTTP | When |
|---|---|---|
| `nutrition_goal_not_found` | 404 | No goal set for this pet. |
| `nutrition_utc_offset_invalid` | 400 | `utcOffsetMinutes` outside the allowed range. |
| `nutrition_calorie_target_negative` | 400 | Calorie target below zero. |
| `nutrition_portion_target_negative` | 400 | Portion target below zero. |
| `nutrition_meals_per_day_negative` | 400 | Meals per day below zero. |
| `nutrition_portion_unit_invalid` | 400 | Unit is not one of the known values. |
| `nutrition_portion_unit_required` | 400 | Portion target given without a unit. |
| `nutrition_analysis_invalid` | 400 | The AI analysis cannot run — in practice, no usable weight on the pet. |

`nutrition_analysis_invalid` is deliberately the only alias on the analysis path:
its failures are about the pet's stored profile, not the request, so the client
cannot fix them by changing the call.

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

Add the constant to `Common/Api/ErrorCodes.cs`, throw the matching
`AppException` (`NotFoundException`, `ValidationException`, `ConflictException`,
`UnprocessableException`) from the service, and add a row here. Do not derive a
code from an exception message: rewording the message would silently change the
code a client sees.
