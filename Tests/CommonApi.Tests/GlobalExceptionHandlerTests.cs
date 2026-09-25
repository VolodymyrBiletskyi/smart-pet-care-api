using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Infrastructure.Classifier;
using Xunit;

namespace smart_pet_care_api.Common.Api.Tests;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task AppException_KeepsItsStatusCodeAliasAndParams()
    {
        var exception = new ValidationException(
            ErrorCodes.WeightLog.WeightTooLarge,
            "WeightKg cannot be greater than 230",
            new Dictionary<string, object?> { ["max"] = 230m });

        var (context, body) = await HandleAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(ErrorCodes.WeightLog.WeightTooLarge, body.GetProperty("code").GetString());
        Assert.Equal("230", body.GetProperty("params").GetProperty("max").ToString());
    }

    [Fact]
    public async Task AppException_MessageIsPunctuated()
    {
        var (_, body) = await HandleAsync(
            new NotFoundException(ErrorCodes.PetNotFound, "Pet not found"));

        Assert.Equal("Pet not found.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task EveryResponse_CarriesTheTraceId()
    {
        var (context, body) = await HandleAsync(
            new NotFoundException(ErrorCodes.PetNotFound, "Pet not found"));

        Assert.Equal(context.TraceIdentifier, body.GetProperty("traceId").GetString());
    }

    /// <summary>
    /// The point of the handler: an exception nobody designed for is a bug, and
    /// must not reach the client wearing a plausible business code or leaking
    /// the internal text that would let it pass for one.
    /// </summary>
    [Fact]
    public async Task UnexpectedException_Becomes500WithoutItsMessage()
    {
        var (context, body) = await HandleAsync(
            new InvalidOperationException("Sequence contains no elements"));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(ErrorCodes.Internal, body.GetProperty("code").GetString());
        Assert.DoesNotContain("Sequence contains no elements", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RateLimited_PassesTheClassifierCodeAndSetsRetryAfter()
    {
        var messageId = Guid.NewGuid();
        var exception = new ClassifierRateLimitedException(
            "internal detail", "rate_limit_exceeded", retryAfterSeconds: 30, messageId: messageId);

        var (context, body) = await HandleAsync(exception);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("rate_limit_exceeded", body.GetProperty("code").GetString());
        Assert.Equal(30, body.GetProperty("retryAfterSeconds").GetInt32());
        Assert.True(body.GetProperty("retryable").GetBoolean());
        Assert.Equal(messageId, body.GetProperty("messageId").GetGuid());
        Assert.Equal("30", context.Response.Headers.RetryAfter.ToString());
        Assert.DoesNotContain("internal detail", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Unavailable_FallsBackToTheGenericCode()
    {
        var (context, body) = await HandleAsync(
            new ClassifierUnavailableException("boom", HttpStatusCode.BadGateway));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal(ErrorCodes.Classifier.Unavailable, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ResponseAlreadyStarted_IsLeftToTheServer()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        var handled = await Handler().TryHandleAsync(
            context, new NotFoundException(ErrorCodes.PetNotFound, "Pet not found"), default);

        Assert.False(handled);
    }

    /// <summary>
    /// A caller that hung up has no one left to read a body, and counting the
    /// disconnect as a server fault would bury real 500s in noise.
    /// </summary>
    [Fact]
    public async Task AbortedRequest_IsNotReportedAsAFailure()
    {
        var context = new DefaultHttpContext();
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        context.RequestAborted = aborted.Token;

        var handled = await Handler().TryHandleAsync(
            context, new OperationCanceledException(), default);

        Assert.False(handled);
    }

    private static GlobalExceptionHandler Handler() =>
        new(NullLogger<GlobalExceptionHandler>.Instance);

    private static async Task<(HttpContext Context, JsonElement Body)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        using var body = new MemoryStream();
        context.Response.Body = body;

        var handled = await Handler().TryHandleAsync(context, exception, TestContext.Current.CancellationToken);
        Assert.True(handled);

        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            body, cancellationToken: TestContext.Current.CancellationToken);
        return (context, document.RootElement.Clone());
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => true;
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public string? ReasonPhrase { get; set; }
        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public void OnCompleted(Func<object, Task> callback, object state) { }
        public void OnStarting(Func<object, Task> callback, object state) { }
    }
}
