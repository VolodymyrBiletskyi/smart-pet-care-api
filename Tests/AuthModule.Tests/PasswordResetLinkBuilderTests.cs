using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using smart_pet_care_api.Modules.AuthModule.Domain;
using Xunit;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

public class PasswordResetLinkBuilderTests
{
    private const string Token = "abc-123_XYZ";

    [Fact]
    public void EmailLink_UsesTheConfiguredBase()
    {
        var builder = Builder(new PasswordResetOptions { PublicBaseUrl = "https://api.example.com" });

        Assert.Equal(
            "https://api.example.com/reset-password?token=abc-123_XYZ",
            builder.EmailLink(Token));
    }

    [Fact]
    public void EmailLink_DoesNotDoubleTheSlash()
    {
        var builder = Builder(new PasswordResetOptions { PublicBaseUrl = "https://api.example.com/" });

        Assert.Equal(
            "https://api.example.com/reset-password?token=abc-123_XYZ",
            builder.EmailLink(Token));
    }

    /// <summary>
    /// A developer running this locally must not be mailed a link into production,
    /// where the token does not exist — so with nothing configured the link points
    /// back at the host the request arrived on.
    /// </summary>
    [Fact]
    public void EmailLink_FallsBackToTheRequestsOwnHost()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 8080);

        var builder = Builder(new PasswordResetOptions(), context);

        Assert.Equal(
            "http://localhost:8080/reset-password?token=abc-123_XYZ",
            builder.EmailLink(Token));
    }

    [Fact]
    public void AppLink_SubstitutesTheToken()
    {
        var builder = Builder(new PasswordResetOptions
        {
            AppLink = "miso://auth/new-password?t={token}"
        });

        Assert.Equal("miso://auth/new-password?t=abc-123_XYZ", builder.AppLink(Token));
    }

    private static PasswordResetLinkBuilder Builder(
        PasswordResetOptions options, HttpContext? context = null) =>
        new(Options.Create(options), new HttpContextAccessor { HttpContext = context });
}
