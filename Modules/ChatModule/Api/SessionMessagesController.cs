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
        var result = await chatService.GetMessagesAsync(
            sessionId,
            User.GetUserId(),
            limit,
            cursor,
            cancellationToken);

        return Ok(SessionMessagesPageResponseDto.FromResult(result));
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
            () => chatService.HandleUserMessageAsync(
                sessionId,
                User.GetUserId(),
                request.Text,
                request.ClientMessageId ?? Guid.Empty,
                cancellationToken));
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
            () => chatService.RetryUserMessageAsync(
                sessionId,
                User.GetUserId(),
                messageId,
                cancellationToken));
    }

    private async Task<IActionResult> ExecuteMessageOperationAsync(
        Func<Task<ClassifierChatResponse>> operation)
    {
        var response = await operation();
        return Ok(SessionMessageResponseDto.FromClassifier(response));
    }
}


