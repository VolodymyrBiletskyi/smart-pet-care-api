using System.Text.Json.Serialization;

namespace smart_pet_care_api.Common.Api;

/// <summary>
/// The single shape every failed request answers with. <see cref="Code"/> is the
/// contract; <see cref="Message"/> is an English fallback for codes a client does
/// not know yet and must not be parsed.
/// </summary>
public class ApiErrorResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }

    public string Message { get; set; } = null!;

    /// <summary>
    /// Values embedded in the message that the client needs to build its own
    /// text — limits, ceilings, allowed ranges. Present only where the message
    /// actually carries a number, so the frontend never hardcodes a server rule.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, object?>? Params { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; set; }

    /// <summary>Whether repeating the identical request could succeed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Retryable { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetryAfterSeconds { get; set; }

    /// <summary>
    /// Chat only: the message the failure belongs to, so the client can mark the
    /// right bubble as failed. Carried here rather than in a chat-specific shape
    /// because clients otherwise need a second error parser for one field.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? MessageId { get; set; }

    public static ApiErrorResponse FromMessage(
        string message,
        string? code = null,
        int? retryAfterSeconds = null)
    {
        return new ApiErrorResponse
        {
            Code = code,
            Message = Normalize(message),
            RetryAfterSeconds = retryAfterSeconds
        };
    }

    public static string Normalize(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message)
            ? "An unexpected error occurred."
            : message.Trim();

        return normalized[^1] is '.' or '!' or '?' ? normalized : normalized + ".";
    }
}
