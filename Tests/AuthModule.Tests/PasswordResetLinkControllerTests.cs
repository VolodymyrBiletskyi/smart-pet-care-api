using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Api;
using smart_pet_care_api.Modules.AuthModule.Domain;
using smart_pet_care_api.Modules.AuthModule.DTOs.Requests;
using Xunit;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

public class PasswordResetLinkControllerTests
{
    [Fact]
    public async Task Open_RendersTheAppLinkForAUsableToken()
    {
        var pages = new RecordingPageProvider();
        var controller = Controller(pages, tokenIsUsable: true);

        var result = Assert.IsType<ContentResult>(await controller.Open("good-token"));

        Assert.Equal("text/html; charset=utf-8", result.ContentType);
        Assert.Equal("miso://reset-password?token=good-token", pages.OpenedWith);
        Assert.Contains("good-token", result.Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("spent-token")]
    public async Task Open_FallsBackToTheExpiredPage(string? token)
    {
        var pages = new RecordingPageProvider();
        var controller = Controller(pages, tokenIsUsable: false);

        var result = Assert.IsType<ContentResult>(await controller.Open(token));

        Assert.Null(pages.OpenedWith);
        Assert.Equal(RecordingPageProvider.ExpiredBody, result.Content);
    }

    /// <summary>
    /// 200 for the expired page too: the status is about the request for a page, and
    /// some WebViews replace the body of an error status with their own.
    /// </summary>
    [Fact]
    public async Task Open_AnswersTwoHundredEvenForADeadLink()
    {
        var controller = Controller(new RecordingPageProvider(), tokenIsUsable: false);

        var result = Assert.IsType<ContentResult>(await controller.Open("spent"));

        Assert.True(result.StatusCode is null or StatusCodes.Status200OK);
    }

    /// <summary>
    /// The URL carries a bearer credential, so no cache may keep it, no crawler index
    /// it and no outbound request leak it in a Referer header.
    /// </summary>
    [Fact]
    public async Task Open_KeepsTheTokenOutOfCachesCrawlersAndReferrers()
    {
        var controller = Controller(new RecordingPageProvider(), tokenIsUsable: true);

        await controller.Open("good-token");

        var headers = controller.Response.Headers;
        Assert.Equal("no-store", headers.CacheControl);
        Assert.Equal("no-referrer", headers["Referrer-Policy"]);
        Assert.Equal("noindex, nofollow", headers["X-Robots-Tag"]);
    }

    private static PasswordResetLinkController Controller(
        IPasswordResetPageProvider pages, bool tokenIsUsable) =>
        new(new StubAuthService(tokenIsUsable), new StubResetLinkBuilder(), pages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private sealed class RecordingPageProvider : IPasswordResetPageProvider
    {
        public const string ExpiredBody = "<html>expired</html>";

        public string? OpenedWith { get; private set; }

        public string Open(string appLink)
        {
            OpenedWith = appLink;
            return $"<html>{appLink}</html>";
        }

        public string Expired() => ExpiredBody;
    }

    /// <summary>
    /// Only the one method the page calls; the rest of IAuthService has no business
    /// being reachable from a route that renders HTML.
    /// </summary>
    private sealed class StubAuthService(bool usable) : IAuthService
    {
        public Task<bool> IsPasswordResetTokenUsableAsync(string rawToken) =>
            Task.FromResult(usable);

        public Task RegisterAsync(RegisterRequest request) => throw new NotSupportedException();
        public Task ConfirmEmailAsync(ConfirmEmailRequest request) => throw new NotSupportedException();
        public Task ResendConfirmationAsync(string email) => throw new NotSupportedException();
        public Task ForgotPasswordAsync(string email) => throw new NotSupportedException();
        public Task ResetPasswordAsync(ResetPasswordRequest request) => throw new NotSupportedException();
        public Task<DTOs.Responses.AuthTokenPair> LoginAsync(LoginRequest request) => throw new NotSupportedException();
        public Task<DTOs.Responses.AuthTokenPair?> RefreshAsync(string rawRefreshToken) => throw new NotSupportedException();
        public Task LogoutAsync(Guid userId, string? deviceToken = null) => throw new NotSupportedException();
        public Task<DTOs.Responses.AuthTokenPair> GoogleLoginAsync(string code) => throw new NotSupportedException();
        public Task<DTOs.Responses.AuthTokenPair> GoogleMobileLoginAsync(string idToken) => throw new NotSupportedException();
    }
}
