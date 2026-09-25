namespace smart_pet_care_api.Common.Api;

/// <summary>
/// A failure the API means to report. The alias is set where the failure is
/// raised rather than derived from the message downstream, so rewording a
/// message can never silently change the code a client sees.
/// </summary>
public abstract class AppException(
    string code,
    int statusCode,
    string message,
    IDictionary<string, object?>? parameters = null,
    bool? retryable = null) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public IDictionary<string, object?>? Parameters { get; } = parameters;
    public bool? Retryable { get; } = retryable;
}

public class NotFoundException(string code, string message)
    : AppException(code, StatusCodes.Status404NotFound, message);

public class ValidationException(
    string code,
    string message,
    IDictionary<string, object?>? parameters = null)
    : AppException(code, StatusCodes.Status400BadRequest, message, parameters);

public class ConflictException(string code, string message)
    : AppException(code, StatusCodes.Status409Conflict, message);

public class UnprocessableException(string code, string message)
    : AppException(code, StatusCodes.Status422UnprocessableEntity, message);
