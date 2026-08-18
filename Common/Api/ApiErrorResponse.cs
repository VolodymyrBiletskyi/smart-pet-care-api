namespace smart_pet_care_api.Common.Api;

public class ApiErrorResponse
{
    public string Message { get; set; } = null!;
    public IDictionary<string, string[]>? Errors { get; set; }

    public static ApiErrorResponse FromMessage(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message)
            ? "An unexpected error occurred."
            : message.Trim();

        if (normalized[^1] is not ('.' or '!' or '?'))
            normalized += ".";

        return new ApiErrorResponse { Message = normalized };
    }
}
