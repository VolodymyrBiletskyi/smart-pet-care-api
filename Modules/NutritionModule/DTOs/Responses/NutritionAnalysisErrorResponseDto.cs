namespace smart_pet_care_api.Modules.NutritionModule.DTOs.Responses
{
    /// <summary>
    /// Public shape for a failed analysis. Internal classifier messages are
    /// never forwarded to clients.
    /// </summary>
    public class NutritionAnalysisErrorResponseDto
    {
        public required string Code { get; set; }
        public required string Message { get; set; }
        public required bool Retryable { get; set; }
        public int? RetryAfterSeconds { get; set; }
    }
}
