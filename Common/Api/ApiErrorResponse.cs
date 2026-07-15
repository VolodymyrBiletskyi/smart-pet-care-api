namespace smart_pet_care_api.Common.Api;

public class ApiErrorResponse
{
    public string Message { get; set; } = null!;
    public IDictionary<string, string[]>? Errors { get; set; }
}
