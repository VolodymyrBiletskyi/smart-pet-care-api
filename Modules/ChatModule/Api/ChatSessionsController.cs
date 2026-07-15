using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ChatSessionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
            return BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }
}
