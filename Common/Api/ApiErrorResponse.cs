using System.Text.Json.Serialization;

namespace smart_pet_care_api.Common.Api;

public class ApiErrorResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }
    public string Message { get; set; } = null!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetryAfterSeconds { get; set; }

    public static ApiErrorResponse FromMessage(
        string message,
        string? code = null,
        int? retryAfterSeconds = null)
    {
        var normalized = string.IsNullOrWhiteSpace(message)
            ? "An unexpected error occurred."
            : message.Trim();

        if (normalized[^1] is not ('.' or '!' or '?'))
            normalized += ".";

        return new ApiErrorResponse
        {
            Code = code,
            Message = normalized,
            RetryAfterSeconds = retryAfterSeconds
        };
    }
}
