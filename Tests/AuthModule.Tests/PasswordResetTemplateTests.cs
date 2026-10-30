using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using smart_pet_care_api.Infrastructure.Email;
using smart_pet_care_api.Modules.AuthModule.Domain;
using Xunit;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

/// <summary>
/// Renders the real files in <c>Emails/</c> and <c>Pages/</c>. A placeholder that
/// stops matching its template is otherwise invisible until a user is mailed
/// <c>{{link}}</c>.
/// </summary>
public class PasswordResetTemplateTests
{
    private const string Link = "https://api.test/reset-password?token=abc123";

    [Fact]
    public void PasswordResetEmail_SubstitutesEveryPlaceholder()
    {
        var body = new EmailTemplateProvider(Environment()).PasswordReset(Link, 30);

        foreach (var rendering in new[] { body.Text, body.Html })
        {
            Assert.DoesNotContain("{{", rendering);
            Assert.Contains(Link, rendering);
            Assert.Contains("30", rendering);
        }
    }

    /// <summary>
    /// Neither the subject nor the preview text carries the link, since both land on
    /// a lock screen — so the hidden preview div must not be the place it leaks.
    /// </summary>
    [Fact]
    public void PasswordResetEmail_KeepsTheLinkOutOfThePreviewText()
    {
        var html = new EmailTemplateProvider(Environment()).PasswordReset(Link, 30).Html;

        var preview = html.Split("max-height:0")[1].Split("</div>")[0];
        Assert.DoesNotContain("token", preview);
    }

    [Fact]
    public void OpenPage_CarriesTheAppLinkAndNoPlaceholder()
    {
        var html = new PasswordResetPageProvider(Environment())
            .Open("miso://reset-password?token=abc123");

        Assert.DoesNotContain("{{", html);
        Assert.Contains("miso://reset-password?token=abc123", html);
    }

    [Fact]
    public void ExpiredPage_RendersWithoutAnyLink()
    {
        var html = new PasswordResetPageProvider(Environment()).Expired();

        Assert.DoesNotContain("{{", html);
        Assert.DoesNotContain("miso://", html);
    }

    /// <summary>
    /// The repository root, found by walking up from the test output the way
    /// ErrorCodeCatalogueTests finds docs/ — the templates are read from the content
    /// root at runtime, and that is what this stands in for.
    /// </summary>
    private static IWebHostEnvironment Environment()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Emails"))
                && Directory.Exists(Path.Combine(directory.FullName, "Pages")))
                return new StubEnvironment(directory.FullName);

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Emails/ and Pages/ were not found above the test output.");
    }

    private sealed class StubEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = contentRoot;
        public string WebRootPath { get; set; } = contentRoot;
        public string ApplicationName { get; set; } = "tests";
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
