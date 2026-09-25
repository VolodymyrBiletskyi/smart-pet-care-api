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

    /// <summary>
    /// Coarse for now: the nutrition module still raises plain framework
    /// exceptions, so its validation failures share one alias until it moves to
    /// <see cref="AppException"/> like the weight history module has.
    /// </summary>
    public static class Nutrition
    {
        public const string AnalysisInvalid = "nutrition_analysis_invalid";
    }

    public static class Wellness
    {
        public const string InsufficientData = "wellness_insufficient_data";
        public const string HistoryQueryInvalid = "wellness_history_query_invalid";
        public const string ServiceRateLimited = "wellness_service_rate_limited";
        public const string ServiceInvalidResponse = "wellness_service_invalid_response";
        public const string ServiceUnavailable = "wellness_service_unavailable";
    }

    public static class Classifier
    {
        public const string InvalidResponse = "classifier_invalid_response";
        public const string Unavailable = "service_unavailable";
    }
}
