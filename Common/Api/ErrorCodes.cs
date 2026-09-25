namespace smart_pet_care_api.Common.Api;

/// <summary>
/// Every alias the API can return. The frontend keys its translations off these
/// strings, so a value here is part of the public contract: rename one and the
/// client falls back to English until it ships a matching release.
/// </summary>
/// <remarks>
/// Codes the classifier itself returns (<see cref="Classifier"/>) are the one
/// exception — they arrive from the Python service at runtime and are passed
/// through, so the list below is not exhaustive for 429 and 503.
/// </remarks>
public static class ErrorCodes
{
    /// <summary>Anything that is not a deliberate, named failure.</summary>
    public const string Internal = "internal_error";

    public const string RequestValidationFailed = "request_validation_failed";
    public const string AuthenticationTokenInvalid = "authentication_token_invalid";

    public const string PetNotFound = "pet_not_found";
    public const string ReminderNotFound = "reminder_not_found";

    public static class Pet
    {
        public const string NameRequired = "pet_name_required";
        public const string NameEmpty = "pet_name_empty";
        public const string SpeciesRequired = "pet_species_required";
        public const string SpeciesInvalid = "pet_species_invalid";
        public const string SexInvalid = "pet_sex_invalid";
        public const string UpdateEmpty = "pet_update_empty";
        public const string BirthDateInFuture = "pet_birth_date_in_future";
        public const string WeightNotPositive = "pet_weight_not_positive";
        public const string WeightTooLarge = "pet_weight_too_large";
        public const string PhotoRequired = "pet_photo_required";
        public const string PhotoUrlEmpty = "pet_photo_url_empty";
        public const string PhotoPublicIdEmpty = "pet_photo_public_id_empty";
        public const string PhotoTypeInvalid = "pet_photo_type_invalid";
        public const string PhotoTooLarge = "pet_photo_too_large";
        public const string PhotoUploadFailed = "pet_photo_upload_failed";
        public const string ValidationFailed = "pet_validation_failed";
    }

    public static class WeightLog
    {
        public const string NotFound = "weight_log_not_found";
        public const string UpdateEmpty = "weight_log_update_empty";
        public const string WeightNotPositive = "weight_log_weight_not_positive";
        public const string WeightTooLarge = "weight_log_weight_too_large";
        public const string MeasurementTimeRequired = "weight_log_measurement_time_required";
        public const string MeasurementTimeTooFarInFuture = "weight_log_measurement_time_too_far_in_future";
        public const string MeasurementTimeConflict = "weight_log_measurement_time_conflict";
        public const string DateRangeInvalid = "weight_log_date_range_invalid";
        public const string NotesEmpty = "weight_log_notes_empty";
    }

    public static class Feeding
    {
        public const string LogNotFound = "feeding_log_not_found";
        public const string UpdateEmpty = "feeding_update_empty";
        public const string TimeRequired = "feeding_time_required";
        public const string TimeTooFarInFuture = "feeding_time_too_far_in_future";
        public const string PortionAmountNegative = "feeding_portion_amount_negative";
        public const string PortionUnitRequired = "feeding_portion_unit_required";
        public const string PortionUnitInvalid = "feeding_portion_unit_invalid";
        public const string CaloriesNegative = "feeding_calories_negative";
        public const string FoodTypeInvalid = "feeding_food_type_invalid";
        public const string ValidationFailed = "feeding_validation_failed";
    }

    public static class Activity
    {
        public const string LogNotFound = "activity_log_not_found";
        public const string UpdateEmpty = "activity_update_empty";
        public const string SourceInvalid = "activity_source_invalid";
        public const string DateRangeInvalid = "activity_date_range_invalid";
        public const string RecordedAtInFuture = "activity_recorded_at_in_future";
        public const string StepsNegative = "activity_steps_negative";
        public const string TypeInvalid = "activity_type_invalid";
        public const string IntensityInvalid = "activity_intensity_invalid";
        public const string DurationNotPositive = "activity_duration_not_positive";
        public const string DurationTooLong = "activity_duration_too_long";
        public const string StepsTooLarge = "activity_steps_too_large";
        public const string LocationTooLong = "activity_location_too_long";
        public const string NoteTooLong = "activity_note_too_long";
        public const string SourceNotSupported = "activity_source_not_supported";
        public const string NothingRecorded = "activity_nothing_recorded";
    }

    public static class Sleep
    {
        public const string LogNotFound = "sleep_log_not_found";
        public const string UpdateEmpty = "sleep_update_empty";
        public const string DateRangeInvalid = "sleep_date_range_invalid";
        public const string DateInFuture = "sleep_date_in_future";
        public const string HoursNotPositive = "sleep_hours_not_positive";
        public const string HoursTooLarge = "sleep_hours_too_large";
        public const string NoteTooLong = "sleep_note_too_long";
        public const string DailyHoursExceeded = "sleep_daily_hours_exceeded";
    }

    public static class Health
    {
        public const string RecordNotFound = "health_record_not_found";
        public const string DateRangeInvalid = "health_date_range_invalid";
        public const string TypeInvalid = "health_type_invalid";
        public const string TitleRequired = "health_title_required";
        public const string TitleTooLong = "health_title_too_long";
        public const string PerformedAtInFuture = "health_performed_at_in_future";
        public const string NextDueBeforePerformed = "health_next_due_before_performed";
        public const string SymptomInvalid = "health_symptom_invalid";
        public const string DescriptionTooLong = "health_description_too_long";
        public const string DosageTooLong = "health_dosage_too_long";
        public const string ProviderTooLong = "health_provider_too_long";
    }

    public static class Journal
    {
        public const string EntryNotFound = "journal_entry_not_found";
        public const string DateRangeInvalid = "journal_date_range_invalid";
        public const string TypeInvalid = "journal_type_invalid";
        public const string SeverityInvalid = "journal_severity_invalid";
        public const string SymptomInvalid = "journal_symptom_invalid";
        public const string TitleRequired = "journal_title_required";
        public const string TitleTooLong = "journal_title_too_long";
        public const string ObservedAtInFuture = "journal_observed_at_in_future";
        public const string NotesTooLong = "journal_notes_too_long";
    }

    public static class Reminder
    {
        public const string RunNotFound = "reminder_run_not_found";
        public const string RunAlreadyAcknowledged = "reminder_run_already_acknowledged";
        public const string EndAtNotInFuture = "reminder_end_at_not_in_future";
        public const string DateNotInFuture = "reminder_date_not_in_future";
        public const string DateRangeInvalid = "reminder_date_range_invalid";
        public const string ScheduleInvalid = "reminder_schedule_invalid";
        public const string PerformedAtInFuture = "reminder_performed_at_in_future";
        public const string NoteTooLong = "reminder_note_too_long";
    }

    public static class Auth
    {
        public const string EmailAlreadyTaken = "auth_email_already_taken";
        public const string AccountNotFound = "auth_account_not_found";
        public const string InvalidCredentials = "auth_invalid_credentials";
        public const string GoogleAuthFailed = "auth_google_failed";
        public const string RefreshTokenInvalid = "auth_refresh_token_invalid";
        public const string OAuthCodeRequired = "auth_oauth_code_required";
    }

    public static class User
    {
        public const string NotFound = "user_not_found";
        public const string PhotoRequired = "user_photo_required";
        public const string PhotoTypeInvalid = "user_photo_type_invalid";
        public const string PhotoTooLarge = "user_photo_too_large";
        public const string AvatarNotFound = "user_avatar_not_found";
        public const string DeleteForbidden = "user_delete_forbidden";
    }

    public static class Notification
    {
        public const string DeviceTokenRequired = "device_token_required";
    }

    public static class Chat
    {
        public const string SessionNotFound = "chat_session_not_found";
        public const string MessageNotFound = "chat_message_not_found";
        public const string PetIdRequired = "chat_pet_id_required";
        public const string PageLimitInvalid = "chat_page_limit_invalid";
        public const string CursorInvalid = "chat_cursor_invalid";
        public const string MessageTextRequired = "chat_message_text_required";
        public const string MessageTextTooLong = "chat_message_text_too_long";
        public const string ClientMessageIdRequired = "chat_client_message_id_required";
        public const string ClientMessageIdConflict = "chat_client_message_id_conflict";
        public const string MessageNotRetryable = "chat_message_not_retryable";
        public const string MessageProcessingOrRetryRequired = "chat_message_processing_or_retry_required";
        public const string StoredResponseInvalid = "chat_stored_response_invalid";
        public const string RequestInvalid = "chat_request_invalid";
        public const string StateConflict = "chat_state_conflict";
    }

    public static class Nutrition
    {
        /// <summary>
        /// The analysis path validates the pet's body data rather than the
        /// request, so its failures share one alias: the client cannot fix them
        /// by changing the call, only by filling in the pet's profile.
        /// </summary>
        public const string AnalysisInvalid = "nutrition_analysis_invalid";

        public const string GoalNotFound = "nutrition_goal_not_found";
        public const string UtcOffsetInvalid = "nutrition_utc_offset_invalid";
        public const string CalorieTargetNegative = "nutrition_calorie_target_negative";
        public const string PortionTargetNegative = "nutrition_portion_target_negative";
        public const string MealsPerDayNegative = "nutrition_meals_per_day_negative";
        public const string PortionUnitInvalid = "nutrition_portion_unit_invalid";
        public const string PortionUnitRequired = "nutrition_portion_unit_required";
    }

    public static class Wellness
    {
        public const string InsufficientData = "wellness_insufficient_data";
        public const string HistoryQueryInvalid = "wellness_history_query_invalid";
        public const string ServiceRateLimited = "wellness_service_rate_limited";
        public const string ServiceInvalidResponse = "wellness_service_invalid_response";
        public const string ServiceUnavailable = "wellness_service_unavailable";
    }

    /// <summary>
    /// Screaming case and no <c>ApiErrorResponse</c> around them: these predate
    /// the alias convention and the mobile client already branches on the exact
    /// strings, so they are recorded here as-is rather than renamed. Changing
    /// them means shipping a client release first.
    /// </summary>
    public static class EmailConfirmation
    {
        public const string CodeInvalid = "CONFIRMATION_CODE_INVALID";
        public const string CodeExpired = "CONFIRMATION_CODE_EXPIRED";
        public const string AlreadyConfirmed = "EMAIL_ALREADY_CONFIRMED";
        public const string TooManyAttempts = "CONFIRMATION_TOO_MANY_ATTEMPTS";
        public const string ResendTooSoon = "CONFIRMATION_RESEND_TOO_SOON";
        public const string NotConfirmed = "EMAIL_NOT_CONFIRMED";
    }

    public static class Classifier
    {
        public const string InvalidResponse = "classifier_invalid_response";
        public const string Unavailable = "service_unavailable";
    }
}

