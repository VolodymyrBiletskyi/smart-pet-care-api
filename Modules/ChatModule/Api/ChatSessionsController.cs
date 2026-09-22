using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.ChatModule.Domain;
using smart_pet_care_api.Modules.ChatModule.DTOs;

namespace smart_pet_care_api.Modules.ChatModule.Api;

[ApiController]
[Authorize]
[Route("api/sessions")]
public sealed class ChatSessionsController(IChatService chatService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<ChatSessionResponseDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSessions(
        CancellationToken cancellationToken)
    {
        var sessions = await chatService.GetSessionsAsync(
            User.GetUserId(),
            cancellationToken);

        return Ok(sessions.Select(ChatSessionResponseDto.FromResult));
    }

    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(
        typeof(ChatSessionDetailsResponseDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await chatService.GetSessionAsync(
                sessionId,
                User.GetUserId(),
                cancellationToken);

            return Ok(ChatSessionDetailsResponseDto.FromResult(session));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(Error(exception.Message, "chat_session_not_found"));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ChatSessionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateChatSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await chatService.CreateSessionAsync(
                User.GetUserId(),
                request.PetId,
                cancellationToken);
            var response = ChatSessionResponseDto.FromResult(result);

            return Created($"/api/sessions/{result.SessionId:D}", response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Error(exception.Message, "chat_pet_id_required"));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(Error(exception.Message, "pet_not_found"));
        }
    }

    private static ApiErrorResponse Error(string message, string code) =>
        ApiErrorResponse.FromMessage(message, code);
}
