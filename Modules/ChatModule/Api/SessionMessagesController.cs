using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
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
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
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
            return NotFound(Error(exception.Message, NotFoundErrorCode(exception.Message)));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Error(exception.Message, RequestErrorCode(exception.Message)));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(SessionMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
        StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
        StatusCodes.Status502BadGateway)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
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
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
        StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
        StatusCodes.Status502BadGateway)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
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
            return NotFound(Error(exception.Message, NotFoundErrorCode(exception.Message)));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Error(exception.Message, RequestErrorCode(exception.Message)));
        }
        catch (InvalidOperationException exception) when (mapInvalidStateToConflict)
        {
            return Conflict(Error(exception.Message, ConflictErrorCode(exception.Message)));
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
                new ApiErrorResponse
                {
                    MessageId = exception.MessageId,
                    Code = ErrorCodes.Classifier.InvalidResponse,
                    Message = "The pet-care assistant returned an invalid response.",
                    TraceId = HttpContext.TraceIdentifier,
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
                exception.Code ?? ErrorCodes.Classifier.Unavailable,
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
            new ApiErrorResponse
            {
                MessageId = messageId,
                Code = code,
                Message = message,
                TraceId = HttpContext.TraceIdentifier,
                Retryable = retryable,
                RetryAfterSeconds = retryAfterSeconds
            });
    }

    private static ApiErrorResponse Error(string message, string code) =>
        ApiErrorResponse.FromMessage(message, code);

    private static string NotFoundErrorCode(string message) => message switch
    {
        "The chat message was not found." => "chat_message_not_found",
        _ => "chat_session_not_found"
    };

    private static string RequestErrorCode(string message)
    {
        if (message.StartsWith("Limit must be between", StringComparison.Ordinal))
            return "chat_page_limit_invalid";
        if (message.StartsWith("Message text cannot exceed", StringComparison.Ordinal))
            return "chat_message_text_too_long";

        return message switch
        {
            "The message cursor is invalid." => "chat_cursor_invalid",
            "Client message ID is required." => "chat_client_message_id_required",
            "Message text is required." => "chat_message_text_required",
            _ => "chat_request_invalid"
        };
    }

    private static string ConflictErrorCode(string message) => message switch
    {
        "Only a failed retryable user message can be retried." => "chat_message_not_retryable",
        "The client message ID has already been used with different text." => "chat_client_message_id_conflict",
        "The client message is already being processed or must be retried." => "chat_message_processing_or_retry_required",
        "The stored chat response is invalid." => "chat_stored_response_invalid",
        _ => "chat_state_conflict"
    };
}


