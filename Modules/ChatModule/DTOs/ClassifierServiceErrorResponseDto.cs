namespace smart_pet_care_api.Modules.ChatModule.DTOs;

public sealed record ClassifierServiceErrorResponseDto
{
    public Guid? MessageId { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required bool Retryable { get; init; }
    public int? RetryAfterSeconds { get; init; }
}
