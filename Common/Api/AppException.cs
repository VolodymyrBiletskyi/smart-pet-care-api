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
    bool? retryable = null,
    int? retryAfterSeconds = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public IDictionary<string, object?>? Parameters { get; } = parameters;
    public bool? Retryable { get; } = retryable;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
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

/// <summary>
/// A dependency failed. Modules wrap their own alias around it — the client is
/// told which feature is degraded, not which internal service broke.
/// </summary>
public class UpstreamException(
    string code,
    int statusCode,
    string message,
    bool retryable,
    int? retryAfterSeconds = null,
    Exception? innerException = null)
    : AppException(
        code, statusCode, message,
        retryable: retryable,
        retryAfterSeconds: retryAfterSeconds,
        innerException: innerException);
