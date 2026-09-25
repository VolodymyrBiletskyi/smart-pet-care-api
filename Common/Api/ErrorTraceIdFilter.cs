using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace smart_pet_care_api.Common.Api;

/// <summary>
/// Stamps the trace id on error bodies a controller returns directly, so it is
/// present whether the failure travelled as an exception or as a plain result.
/// Doing it at each call site would mean every new <c>NotFound(...)</c> is one
/// forgotten argument away from an error nobody can correlate with a log line.
/// </summary>
public sealed class ErrorTraceIdFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: ApiErrorResponse error }
            && string.IsNullOrEmpty(error.TraceId))
        {
            error.TraceId = context.HttpContext.TraceIdentifier;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
