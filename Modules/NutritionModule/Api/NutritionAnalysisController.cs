using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.NutritionModule.Domain;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Requests;
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

        /// <summary>
        /// Asks the AI to grade a day's feeding against the calorie target it
        /// derives from the pet's body data. Answers <c>400</c> when no usable
        /// weight is available, which the grading needs.
        /// </summary>
        /// <remarks>
        /// The body is optional and every field in it is an override. Omit it
        /// and the pet's stored species, breed, weight and age are used, with
        /// the products built from the analysed day's feeding logs.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(NutritionAnalysisResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(
            typeof(ApiErrorResponse),
            StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(
            typeof(ApiErrorResponse),
            StatusCodes.Status502BadGateway)]
        [ProducesResponseType(
            typeof(ApiErrorResponse),
            StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Analyze(
            Guid petId,
            CancellationToken cancellationToken,
            [FromQuery] DateOnly? date,
            [FromQuery] int utcOffsetMinutes = 0,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
            NutritionAnalysisRequestDto? request = null)
        {
            try
            {
                var analysis = await _service.AnalyzeAsync(
                    petId, User.GetUserId(), date, utcOffsetMinutes, request, cancellationToken);
                return Ok(analysis);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message, ErrorCodes.PetNotFound));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(Error(ex.Message, ErrorCodes.Nutrition.AnalysisInvalid));
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
                    new ApiErrorResponse
                    {
                        Code = ErrorCodes.Classifier.InvalidResponse,
                        Message = "The nutrition assistant returned an invalid response.",
                        TraceId = HttpContext.TraceIdentifier,
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
                    ex.Code ?? ErrorCodes.Classifier.Unavailable,
                    "The nutrition assistant is temporarily unavailable. Please try again later.",
                    retryable: true,
                    ex.RetryAfterSeconds);
            }
        }

        /// <summary>Returns the stored latest analysis and the one before it.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(NutritionAnalysisHistoryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRecent(Guid petId)
        {
            try
            {
                return Ok(await _service.GetRecentAsync(petId, User.GetUserId()));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message, ErrorCodes.PetNotFound));
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
                new ApiErrorResponse
                {
                    Code = code,
                    Message = message,
                    TraceId = HttpContext.TraceIdentifier,
                    Retryable = retryable,
                    RetryAfterSeconds = retryAfterSeconds
                });
        }

        private static ApiErrorResponse Error(string message, string code) =>
            ApiErrorResponse.FromMessage(message, code);
    }
}


