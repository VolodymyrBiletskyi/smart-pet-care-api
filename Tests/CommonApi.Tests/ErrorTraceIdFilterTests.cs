using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using smart_pet_care_api.Common.Api;
using Xunit;

namespace smart_pet_care_api.Common.Api.Tests;

public class ErrorTraceIdFilterTests
{
    [Fact]
    public void ErrorReturnedDirectly_GetsTheTraceId()
    {
        var error = ApiErrorResponse.FromMessage("Avatar not found", ErrorCodes.User.AvatarNotFound);

        var context = Execute(new NotFoundObjectResult(error));

        Assert.Equal(context.HttpContext.TraceIdentifier, error.TraceId);
    }

    [Fact]
    public void ATraceIdAlreadySet_IsLeftAlone()
    {
        var error = new ApiErrorResponse { Message = "x", TraceId = "from-the-handler" };

        Execute(new ObjectResult(error));

        Assert.Equal("from-the-handler", error.TraceId);
    }

    [Fact]
    public void SuccessfulResults_AreUntouched()
    {
        var payload = new { name = "Milo" };

        var context = Execute(new OkObjectResult(payload));

        Assert.Same(payload, Assert.IsType<OkObjectResult>(context.Result).Value);
    }

    private static ResultExecutingContext Execute(IActionResult result)
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-7" };
        var context = new ResultExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor()),
            [],
            result,
            controller: new object());

        new ErrorTraceIdFilter().OnResultExecuting(context);
        return context;
    }
}
