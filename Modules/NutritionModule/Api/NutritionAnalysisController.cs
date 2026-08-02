using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.NutritionModule.Domain;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.NutritionModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets/{petId:guid}/nutrition-summary/analysis")]
    public class NutritionAnalysisController : ControllerBase
    {
        private readonly INutritionAnalysisService _service;
        private readonly ILogger<NutritionAnalysisController> _logger;

        public NutritionAnalysisController(
            INutritionAnalysisService service,
            ILogger<NutritionAnalysisController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>Asks the AI to grade the pet's nutrition for a day and advise on it.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(NutritionAnalysisResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(
            typeof(NutritionAnalysisErrorResponseDto),
            StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(
            typeof(NutritionAnalysisErrorResponseDto),
            StatusCodes.Status502BadGateway)]
        [ProducesResponseType(
            typeof(NutritionAnalysisErrorResponseDto),
            StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Analyze(
            Guid petId,
            CancellationToken cancellationToken,
            [FromQuery] DateOnly? date,
            [FromQuery] int utcOffsetMinutes = 0)
        {
            try
            {
                var analysis = await _service.AnalyzeAsync(
                    petId, User.GetUserId(), date, utcOffsetMinutes, cancellationToken);
                return Ok(analysis);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ClassifierRateLimitedException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Classifier rate limit was exceeded for nutrition analysis of pet {PetId}. Trace: {TraceIdentifier}",
                    petId,
                    HttpContext.TraceIdentifier);

                return ClassifierError(
                    StatusCodes.Status429TooManyRequests,
                    ex.Code,
                    "The nutrition assistant is busy. Please try again later.",
                    retryable: true,
                    ex.RetryAfterSeconds);
            }
            catch (ClassifierInvalidResponseException ex)
            {
                // The reason names the offending field only, never its value,
                // so it is safe for normal application logs.
                _logger.LogError(
                    ex,
                    "Classifier returned an invalid nutrition analysis for pet {PetId}: {ValidationReason}. Trace: {TraceIdentifier}",
                    petId,
                    ex.ValidationReason ?? "unspecified",
                    HttpContext.TraceIdentifier);

                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    new NutritionAnalysisErrorResponseDto
                    {
                        Code = "classifier_invalid_response",
                        Message = "The nutrition assistant returned an invalid response.",
                        Retryable = false
                    });
            }
            catch (ClassifierUnavailableException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Classifier is unavailable for nutrition analysis of pet {PetId}. Trace: {TraceIdentifier}",
                    petId,
                    HttpContext.TraceIdentifier);

                return ClassifierError(
                    StatusCodes.Status503ServiceUnavailable,
                    ex.Code ?? "service_unavailable",
                    "The nutrition assistant is temporarily unavailable. Please try again later.",
                    retryable: true,
                    ex.RetryAfterSeconds);
            }
        }

        /// <summary>Returns the stored latest analysis and the one before it.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(NutritionAnalysisHistoryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRecent(Guid petId)
        {
            try
            {
                return Ok(await _service.GetRecentAsync(petId, User.GetUserId()));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
        }

        private ObjectResult ClassifierError(
            int statusCode,
            string code,
            string message,
            bool retryable,
            int? retryAfterSeconds)
        {
            if (retryAfterSeconds is >= 0)
            {
                Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(
                    CultureInfo.InvariantCulture);
            }

            return StatusCode(
                statusCode,
                new NutritionAnalysisErrorResponseDto
                {
                    Code = code,
                    Message = message,
                    Retryable = retryable,
                    RetryAfterSeconds = retryAfterSeconds
                });
        }
    }
}
