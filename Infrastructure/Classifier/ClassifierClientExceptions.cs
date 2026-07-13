using System.Net;

namespace smart_pet_care_api.Infrastructure.Classifier;

public sealed class ClassifierInvalidResponseException : Exception
{
    public ClassifierInvalidResponseException(
        string message,
        HttpStatusCode? statusCode = null,
        string? responseContent = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ResponseContent { get; }
}

public sealed class ClassifierUnavailableException : Exception
{
    public ClassifierUnavailableException(
        string message,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
