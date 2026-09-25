using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Cloudinary;

namespace smart_pet_care_api.Common.Api;

/// <summary>
/// Turns an escaped exception into the one public error shape. Anything that is
/// not an <see cref="AppException"/> or a known classifier failure is reported as
/// <see cref="ErrorCodes.Internal"/> with its detail kept in the log: an
/// unexpected exception is a bug, and answering it with a plausible business
/// error is how bugs stay invisible.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Nothing can be rewritten once the status line is on the wire; let the
        // server abort the response rather than append a second body to it.
        if (httpContext.Response.HasStarted)
            return false;

        // The caller hung up. There is no one left to read a body, and reporting
        // it as a server fault would bury real 500s in disconnect noise.
        if (exception is OperationCanceledException
            && httpContext.RequestAborted.IsCancellationRequested)
            return false;

        var (statusCode, response) = Translate(exception);
        response.TraceId = httpContext.TraceIdentifier;

        // A deliberate upstream failure and a bug both deserve a log line, but not
        // the same one: only the second is worth waking someone up for, and
        // "Unhandled exception" is the string an alert would watch.
        if (response.Code == ErrorCodes.Internal)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. Trace: {TraceIdentifier}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        else if (statusCode >= StatusCodes.Status500InternalServerError
            || statusCode == StatusCodes.Status429TooManyRequests)
        {
            logger.LogWarning(
                exception,
                "Upstream failure {Code} ({StatusCode}) for {Method} {Path}. Trace: {TraceIdentifier}",
                response.Code,
                statusCode,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }

        if (response.RetryAfterSeconds is >= 0)
        {
            httpContext.Response.Headers.RetryAfter =
                response.RetryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    private static (int StatusCode, ApiErrorResponse Response) Translate(
        Exception exception) => exception switch
    {
        AppException app => (app.StatusCode, new ApiErrorResponse
        {
            Code = app.Code,
            Message = ApiErrorResponse.Normalize(app.Message),
            Params = app.Parameters,
            Retryable = app.Retryable,
            RetryAfterSeconds = app.RetryAfterSeconds
        }),

        // Photos exist only on pets today, so the alias the client already knows
        // is kept rather than generalised for an upload path that does not exist.
        CloudinaryUploadException => (
            StatusCodes.Status502BadGateway,
            new ApiErrorResponse
            {
                Code = ErrorCodes.Pet.PhotoUploadFailed,
                Message = "The photo could not be uploaded.",
                Retryable = true
            }),

        ClassifierRateLimitedException rateLimited => (
            StatusCodes.Status429TooManyRequests,
            new ApiErrorResponse
            {
                Code = rateLimited.Code,
                Message = "The assistant is busy. Please try again later.",
                Retryable = true,
                RetryAfterSeconds = rateLimited.RetryAfterSeconds,
                MessageId = rateLimited.MessageId
            }),

        ClassifierInvalidResponseException invalid => (
            StatusCodes.Status502BadGateway,
            new ApiErrorResponse
            {
                Code = ErrorCodes.Classifier.InvalidResponse,
                Message = "The assistant returned an invalid response.",
                Retryable = false,
                MessageId = invalid.MessageId
            }),

        ClassifierUnavailableException unavailable => (
            StatusCodes.Status503ServiceUnavailable,
            new ApiErrorResponse
            {
                Code = unavailable.Code ?? ErrorCodes.Classifier.Unavailable,
                Message = "The assistant is temporarily unavailable. Please try again later.",
                Retryable = true,
                RetryAfterSeconds = unavailable.RetryAfterSeconds,
                MessageId = unavailable.MessageId
            }),

        _ => (StatusCodes.Status500InternalServerError, new ApiErrorResponse
        {
            Code = ErrorCodes.Internal,
            Message = "An unexpected error occurred.",
            Retryable = false
        })
    };
}
