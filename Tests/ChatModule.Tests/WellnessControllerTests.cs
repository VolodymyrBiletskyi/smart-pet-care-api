using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.WellnessModule.Api;
using smart_pet_care_api.Modules.WellnessModule.Domain;
using smart_pet_care_api.Modules.WellnessModule.DTOs;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class WellnessControllerTests
{
    [Fact]
    public async Task GetOrCreateEvaluation_ReturnsWellnessAndForwardsAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var petId = Guid.NewGuid();
        var expected = CreateResponse();
        var service = new StubWellnessService { EvaluationResult = expected };
        var controller = CreateController(service, userId);

        var action = await controller.GetOrCreateEvaluation(
            petId,
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(action);
        Assert.Same(expected, ok.Value);
        Assert.Equal(petId, service.PetId);
        Assert.Equal(userId, service.UserId);
    }

    /// <summary>
    /// The status and alias travel on the exception, so the controller only has
    /// to stay out of the way. What each failure maps to is covered where it is
    /// decided: the wrapper in WellnessServiceTests and GlobalExceptionHandler.
    /// </summary>
    [Fact]
    public async Task GetOrCreateEvaluation_LetsInsufficientDataThrough()
    {
        var controller = CreateController(new StubWellnessService
        {
            EvaluationException = new WellnessInsufficientDataException()
        });

        var exception = await Assert.ThrowsAsync<WellnessInsufficientDataException>(() =>
            controller.GetOrCreateEvaluation(
                Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Wellness.InsufficientData, exception.Code);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
    }

    [Fact]
    public async Task History_ReturnsRequestedPage()
    {
        var expected = new WellnessHistoryResponseDto
        {
            Items = [CreateResponse()],
            Page = 2,
            PageSize = 5,
            TotalCount = 6
        };
        var service = new StubWellnessService { HistoryResult = expected };
        var controller = CreateController(service);

        var action = await controller.History(
            Guid.NewGuid(),
            page: 2,
            pageSize: 5,
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(action);
        Assert.Same(expected, ok.Value);
        Assert.Equal(2, service.Page);
        Assert.Equal(5, service.PageSize);
    }

    [Fact]
    public async Task History_LetsPaginationFailuresThrough()
    {
        var controller = CreateController(new StubWellnessService
        {
            HistoryException = new ValidationException(
                ErrorCodes.Wellness.HistoryQueryInvalid, "Page must be at least 1")
        });

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            controller.History(
                Guid.NewGuid(), page: 0, pageSize: 20, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Wellness.HistoryQueryInvalid, exception.Code);
    }

    private static WellnessController CreateController(
        IWellnessService service,
        Guid? userId = null)
    {
        var controller = new WellnessController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("userId", (userId ?? Guid.NewGuid()).ToString())],
                        "test"))
                }
            }
        };
        return controller;
    }

    private static WellnessResponseDto CreateResponse() => new()
    {
        WellnessScore = 82,
        Band = ClassifierWellnessBand.Excellent,
        ScoreStatus = ClassifierWellnessScoreStatus.Complete,
        States = new WellnessStatesDto
        {
            Activity = ClassifierWellnessReasonCode.ActivityTargetMet,
            Sleep = ClassifierWellnessReasonCode.SleepWithinRange,
            Diet = ClassifierWellnessReasonCode.DietTrackingStrong,
            Symptoms = ClassifierWellnessReasonCode.SymptomResultAvailable,
            PreventiveCare = ClassifierWellnessReasonCode.PreventiveCareCurrent,
            Baseline = ClassifierWellnessReasonCode.BaselineStable
        },
        Narrative = "Wellness is stable.",
        Recommendations = [],
        ReminderSuggestions = [],
        Disclaimer = "Not veterinary advice."
    };

    private sealed class StubWellnessService : IWellnessService
    {
        public WellnessResponseDto? EvaluationResult { get; init; }
        public WellnessHistoryResponseDto? HistoryResult { get; init; }
        public Exception? EvaluationException { get; init; }
        public Exception? HistoryException { get; init; }
        public Guid? PetId { get; private set; }
        public Guid? UserId { get; private set; }
        public int? Page { get; private set; }
        public int? PageSize { get; private set; }

        public Task<WellnessResponseDto> GetOrCreateEvaluationAsync(
            Guid petId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            PetId = petId;
            UserId = userId;
            if (EvaluationException is not null) throw EvaluationException;
            return Task.FromResult(EvaluationResult ?? CreateResponse());
        }

        public Task<WellnessHistoryResponseDto> GetHistoryAsync(
            Guid petId,
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            Page = page;
            PageSize = pageSize;
            if (HistoryException is not null) throw HistoryException;
            return Task.FromResult(HistoryResult ?? new WellnessHistoryResponseDto
            {
                Items = [],
                Page = page,
                PageSize = pageSize,
                TotalCount = 0
            });
        }
    }
}

