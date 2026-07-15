using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Infrastructure.Classifier;
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
    [HttpPost]
    [ProducesResponseType(typeof(SessionMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PostMessage(
        Guid sessionId,
        [FromBody] PostSessionMessageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await chatService.HandleUserMessageAsync(
                sessionId,
                User.GetUserId(),
                request.Text,
                cancellationToken);

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
        catch (ClassifierInvalidResponseException exception)
        {
            logger.LogError(
                exception,
                "Classifier returned an invalid response for chat session {SessionId}. Trace: {TraceIdentifier}",
                sessionId,
                HttpContext.TraceIdentifier);

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = "The pet-care assistant returned an invalid response." });
        }
        catch (ClassifierUnavailableException exception)
        {
            logger.LogWarning(
                exception,
                "Classifier is unavailable for chat session {SessionId}. Trace: {TraceIdentifier}",
                sessionId,
                HttpContext.TraceIdentifier);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message = "The pet-care assistant is temporarily unavailable. Please retry.",
                    retryable = true
                });
        }
    }
}
