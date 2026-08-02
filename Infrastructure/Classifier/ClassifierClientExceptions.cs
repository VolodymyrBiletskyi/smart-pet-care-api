using System.Net;

namespace smart_pet_care_api.Infrastructure.Classifier;

public sealed class ClassifierInvalidResponseException : Exception
{
    public ClassifierInvalidResponseException(
        string message,
        HttpStatusCode? statusCode = null,
        string? responseContent = null,
        Exception? innerException = null,
        string? validationReason = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
        ValidationReason = validationReason;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ResponseContent { get; }

    /// <summary>
    /// Which part of the contract the response broke, as field-level metadata
    /// only. Safe to log: it names fields and JSON paths, never their values.
    /// </summary>
    public string? ValidationReason { get; }
}

public sealed class ClassifierUnavailableException : Exception
{
    public ClassifierUnavailableException(
        string message,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null,
        string? code = null,
        int? retryAfterSeconds = null,
        Guid? messageId = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        RetryAfterSeconds = retryAfterSeconds;
        MessageId = messageId;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? Code { get; }

    public int? RetryAfterSeconds { get; }

    public Guid? MessageId { get; }
}

public sealed class ClassifierRateLimitedException : Exception
{
    public ClassifierRateLimitedException(
        string message,
        string code,
        int? retryAfterSeconds = null,
        Exception? innerException = null,
        Guid? messageId = null)
        : base(message, innerException)
    {
        Code = code;
        RetryAfterSeconds = retryAfterSeconds;
        MessageId = messageId;
    }

    public string Code { get; }

    public int? RetryAfterSeconds { get; }

    public Guid? MessageId { get; }
}
