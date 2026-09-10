using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Modules.WellnessModule.Api;
using smart_pet_care_api.Modules.WellnessModule.Domain;
using smart_pet_care_api.Modules.WellnessModule.DTOs;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class WellnessControllerTests
{
    [Fact]
    public async Task Recalculate_ReturnsWellnessAndForwardsAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var petId = Guid.NewGuid();
        var expected = CreateResponse();
        var service = new StubWellnessService { RecalculateResult = expected };
        var controller = CreateController(service, userId);

        var action = await controller.Recalculate(
            petId,
            new WellnessRecalculationRequestDto { CurrentSymptoms = "low appetite" },
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(action);
        Assert.Same(expected, ok.Value);
        Assert.Equal(petId, service.PetId);
        Assert.Equal(userId, service.UserId);
        Assert.Equal("low appetite", service.CurrentSymptoms);
    }

    [Fact]
    public async Task Recalculate_WhenRateLimited_Returns429AndRetryAfter()
    {
        var controller = CreateController(new StubWellnessService
        {
            RecalculateException = new ClassifierRateLimitedException(
                "internal details",
                "rate_limit_exceeded",
                retryAfterSeconds: 30)
        });

        var action = await controller.Recalculate(
            Guid.NewGuid(),
            null,
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status429TooManyRequests, result.StatusCode);
        Assert.Equal("30", controller.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task Recalculate_WhenClassifierResponseIsInvalid_Returns502()
    {
        var controller = CreateController(new StubWellnessService
        {
            RecalculateException = new ClassifierInvalidResponseException(
                "internal details",
                validationReason: "breakdown.activity.reasonCodes is empty")
        });

        var action = await controller.Recalculate(
            Guid.NewGuid(),
            null,
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
    }

    [Fact]
    public async Task Recalculate_WhenClassifierUnavailable_Returns503AndRetryAfter()
    {
        var controller = CreateController(new StubWellnessService
        {
            RecalculateException = new ClassifierUnavailableException(
                "internal details",
                retryAfterSeconds: 20)
        });

        var action = await controller.Recalculate(
            Guid.NewGuid(),
            null,
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("20", controller.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task Current_WhenAssessmentDoesNotExist_Returns404()
    {
        var controller = CreateController(new StubWellnessService());

        var action = await controller.Current(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundObjectResult>(action);
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
    public async Task History_WhenPaginationIsInvalid_Returns400()
    {
        var controller = CreateController(new StubWellnessService
        {
            HistoryException = new ArgumentException("Page must be at least 1")
        });

        var action = await controller.History(
            Guid.NewGuid(),
            page: 0,
            pageSize: 20,
            TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    private static WellnessController CreateController(
        IWellnessService service,
        Guid? userId = null)
    {
        var controller = new WellnessController(
            service,
            NullLogger<WellnessController>.Instance)
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
        public WellnessResponseDto? RecalculateResult { get; init; }
        public WellnessHistoryResponseDto? HistoryResult { get; init; }
        public Exception? RecalculateException { get; init; }
        public Exception? HistoryException { get; init; }
        public Guid? PetId { get; private set; }
        public Guid? UserId { get; private set; }
        public string? CurrentSymptoms { get; private set; }
        public int? Page { get; private set; }
        public int? PageSize { get; private set; }

        public Task<WellnessResponseDto> RecalculateAsync(
            Guid petId,
            Guid userId,
            string? currentSymptoms,
            CancellationToken cancellationToken = default)
        {
            PetId = petId;
            UserId = userId;
            CurrentSymptoms = currentSymptoms;
            if (RecalculateException is not null) throw RecalculateException;
            return Task.FromResult(RecalculateResult ?? CreateResponse());
        }

        public Task<WellnessResponseDto?> GetCurrentAsync(
            Guid petId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<WellnessResponseDto?>(null);

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
