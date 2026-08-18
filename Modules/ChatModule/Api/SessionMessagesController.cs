using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.ChatModule.Domain;
using smart_pet_care_api.Modules.ChatModule.DTOs;

namespace smart_pet_care_api.Modules.ChatModule.Api;

[ApiController]
[Authorize]
[Route("api/sessions/{sessionId:guid}/messages")]
public sealed class SessionMessagesController(
    IChatService chatService,
    ILogger<SessionMessagesController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(
        typeof(SessionMessagesPageResponseDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        Guid sessionId,
        CancellationToken cancellationToken,
        [FromQuery] int limit = ChatService.DefaultMessagePageSize,
        [FromQuery] string? cursor = null)
    {
        try
        {
            var result = await chatService.GetMessagesAsync(
                sessionId,
                User.GetUserId(),
                limit,
                cursor,
                cancellationToken);

            return Ok(SessionMessagesPageResponseDto.FromResult(result));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(SessionMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ClassifierServiceErrorResponseDto),
        StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(
        typeof(ClassifierServiceErrorResponseDto),
        StatusCodes.Status502BadGateway)]
    [ProducesResponseType(
        typeof(ClassifierServiceErrorResponseDto),
        StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PostMessage(
        Guid sessionId,
        [FromBody] PostSessionMessageRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteMessageOperationAsync(
            sessionId,
            () => chatService.HandleUserMessageAsync(
                sessionId,
                User.GetUserId(),
                request.Text,
                request.ClientMessageId ?? Guid.Empty,
                cancellationToken),
            mapInvalidStateToConflict: true);
    }

    [HttpPost("{messageId:guid}/retry")]
    [ProducesResponseType(typeof(SessionMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        typeof(ClassifierServiceErrorResponseDto),
        StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(
        typeof(ClassifierServiceErrorResponseDto),
        StatusCodes.Status502BadGateway)]
    [ProducesResponseType(
        typeof(ClassifierServiceErrorResponseDto),
        StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RetryMessage(
        Guid sessionId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        return await ExecuteMessageOperationAsync(
            sessionId,
            () => chatService.RetryUserMessageAsync(
                sessionId,
                User.GetUserId(),
                messageId,
                cancellationToken),
            mapInvalidStateToConflict: true);
    }

    private async Task<IActionResult> ExecuteMessageOperationAsync(
        Guid sessionId,
        Func<Task<ClassifierChatResponse>> operation,
        bool mapInvalidStateToConflict = false)
    {
        try
        {
            var response = await operation();

            return Ok(SessionMessageResponseDto.FromClassifier(response));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception) when (mapInvalidStateToConflict)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ClassifierRateLimitedException exception)
        {
            logger.LogWarning(
                exception,
                "Classifier rate limit was exceeded for chat session {SessionId}. Trace: {TraceIdentifier}",
                sessionId,
                HttpContext.TraceIdentifier);

            return ClassifierError(
                StatusCodes.Status429TooManyRequests,
                exception.Code,
                "The pet-care assistant is busy. Please try again later.",
                retryable: true,
                exception.RetryAfterSeconds,
                exception.MessageId);
        }
        catch (ClassifierInvalidResponseException exception)
        {
            logger.LogError(
                exception,
                "Classifier returned an invalid response for chat session {SessionId}. Trace: {TraceIdentifier}",
                sessionId,
                HttpContext.TraceIdentifier);

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new ClassifierServiceErrorResponseDto
                {
                    MessageId = exception.MessageId,
                    Code = "classifier_invalid_response",
                    Message = "The pet-care assistant returned an invalid response.",
                    Retryable = false
                });
        }
        catch (ClassifierUnavailableException exception)
        {
            logger.LogWarning(
                exception,
                "Classifier is unavailable for chat session {SessionId}. Trace: {TraceIdentifier}",
                sessionId,
                HttpContext.TraceIdentifier);

            return ClassifierError(
                StatusCodes.Status503ServiceUnavailable,
                exception.Code ?? "service_unavailable",
                "The pet-care assistant is temporarily unavailable. Please try again later.",
                retryable: true,
                exception.RetryAfterSeconds,
                exception.MessageId);
        }
    }

    private ObjectResult ClassifierError(
        int statusCode,
        string code,
        string message,
        bool retryable,
        int? retryAfterSeconds,
        Guid? messageId)
    {
        if (retryAfterSeconds is >= 0)
        {
            Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(
                CultureInfo.InvariantCulture);
        }

        return StatusCode(
            statusCode,
            new ClassifierServiceErrorResponseDto
            {
                MessageId = messageId,
                Code = code,
                Message = message,
                Retryable = retryable,
                RetryAfterSeconds = retryAfterSeconds
            });
    }
}
