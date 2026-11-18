using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Infrastructure.Email;
using Xunit;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

/// <summary>
/// The check that runs before a confirmation email is sent. What it is for is the
/// bounce: a provider throttles or suspends a sending account whose bounce rate goes
/// over their threshold, so a typo'd domain costs more than the one letter.
/// </summary>
public class EmailDomainScreenTests
{
    [Fact]
    public async Task ADomainWithMailExchangers_IsAccepted()
    {
        var screen = Screen(MailExchangers.Present);

        await screen.ScreenAsync("owner@example.com", Token);
    }

    /// <summary>
    /// <see cref="MailExchangers.Absent"/> is both halves of the ticket's first rule —
    /// a domain that does not exist and one that exists with no MX record. Neither can
    /// receive the letter, and the client's next step is the same.
    /// </summary>
    [Fact]
    public async Task ADomainWithNoMailExchangers_IsRefused()
    {
        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => Screen(MailExchangers.Absent).ScreenAsync("owner@no-such-domain-xyz123.com", Token));

        Assert.Equal(ErrorCodes.Auth.EmailDomainInvalid, failure.Code);
        Assert.Equal(400, failure.StatusCode);
    }

    /// <summary>
    /// The one that matters most: our resolver timing out must not be our registration
    /// closing. An address we could not check is mailed, which is what the service did
    /// before this check existed at all.
    /// </summary>
    [Fact]
    public async Task AnUnansweredLookup_LetsTheAddressThrough()
    {
        await Screen(MailExchangers.Unknown).ScreenAsync("owner@example.com", Token);
    }

    [Fact]
    public async Task ADisposableDomain_IsRefused()
    {
        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => Screen(MailExchangers.Present, "mailinator.com")
                .ScreenAsync("owner@mailinator.com", Token));

        Assert.Equal(ErrorCodes.Auth.EmailDomainDisposable, failure.Code);
    }

    /// <summary>
    /// The disposable alias is separate from the invalid one because the address is
    /// real: "that domain does not exist" is the wrong thing to show a user holding a
    /// working mailbox.
    /// </summary>
    [Fact]
    public async Task ADisposableDomain_IsRefusedWithoutAskingTheDns()
    {
        var lookup = new FakeMxLookup(MailExchangers.Present);
        var screen = new EmailDomainScreen(new StubDisposableDomains("mailinator.com"), lookup);

        await Assert.ThrowsAsync<ValidationException>(
            () => screen.ScreenAsync("owner@mailinator.com", Token));

        Assert.Empty(lookup.Asked);
    }

    [Theory]
    [InlineData("owner@MailInator.com")]
    [InlineData("owner@mailinator.com.")]
    [InlineData("owner@team.mailinator.com")]
    public async Task ADisposableDomain_IsRefusedHoweverItIsWritten(string address)
    {
        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => Screen(MailExchangers.Present, "mailinator.com").ScreenAsync(address, Token));

        Assert.Equal(ErrorCodes.Auth.EmailDomainDisposable, failure.Code);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("owner@")]
    [InlineData("@example.com")]
    [InlineData("owner@localhost")]
    [InlineData("owner@exa mple.com")]
    public async Task SomethingThatIsNotAnAddress_IsRefused(string address)
    {
        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => Screen(MailExchangers.Present).ScreenAsync(address, Token));

        Assert.Equal(ErrorCodes.Auth.EmailDomainInvalid, failure.Code);
    }

    [Fact]
    public async Task TheDomainAskedAboutIsTheOneAfterTheLastAt()
    {
        var lookup = new FakeMxLookup(MailExchangers.Present);
        var screen = new EmailDomainScreen(new StubDisposableDomains(), lookup);

        await screen.ScreenAsync("Owner+pets@Example.COM", Token);

        Assert.Equal("example.com", Assert.Single(lookup.Asked));
    }

    // ---- the real list ----------------------------------------------------

    /// <summary>
    /// Reads the file that ships with the application, which is the only thing standing
    /// between the ticket's acceptance criterion and a blocklist that was never
    /// published.
    /// </summary>
    [Fact]
    public void TheShippedList_RejectsThrowawayProviders()
    {
        var domains = Domains();

        Assert.True(domains.Count > 1000, $"only {domains.Count} domains loaded");

        foreach (var domain in new[]
                 { "mailinator.com", "guerrillamail.com", "10minutemail.com", "yopmail.com" })
            Assert.True(domains.Contains(domain), domain);
    }

    /// <summary>
    /// Forwarding relays are deliverable addresses: they do not bounce and cost no
    /// sending reputation, which is the entire case for this check, and they are what a
    /// privacy-minded user signs up with. A refreshed list that starts naming them
    /// would lock those users out, so the provider subtracts them.
    /// </summary>
    [Fact]
    public void TheShippedList_KeepsForwardingRelaysAndRealProviders()
    {
        var domains = Domains();

        foreach (var domain in new[]
                 {
                     "duck.com", "simplelogin.io", "addy.io", "anonaddy.com", "mozmail.com",
                     "privaterelay.appleid.com", "gmail.com", "icloud.com", "proton.me",
                     "outlook.com", "mail.ru", "example.com"
                 })
            Assert.False(domains.Contains(domain), domain);
    }

    // ---- helpers ----------------------------------------------------------

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static EmailDomainScreen Screen(MailExchangers answer, params string[] disposable) =>
        new(new StubDisposableDomains(disposable), new FakeMxLookup(answer));

    private static DisposableEmailDomains Domains() =>
        new(Environment(), NullLogger<DisposableEmailDomains>.Instance);

    /// <summary>
    /// The directory the file is read from at runtime, found by walking up from the
    /// test output the way the template tests do.
    /// </summary>
    private static IWebHostEnvironment Environment()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Emails", "disposable-domains.txt")))
                return new StubEnvironment(directory.FullName);

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Emails/disposable-domains.txt was not found above the test output.");
    }

    private sealed class FakeMxLookup(MailExchangers answer) : IMxRecordLookup
    {
        public List<string> Asked { get; } = [];

        public Task<MailExchangers> LookupAsync(
            string domain, CancellationToken cancellationToken = default)
        {
            Asked.Add(domain);
            return Task.FromResult(answer);
        }
    }

    private sealed class StubDisposableDomains(params string[] domains) : IDisposableEmailDomains
    {
        public int Count => domains.Length;

        public bool Contains(string domain) =>
            domains.Any(listed =>
                domain == listed || domain.EndsWith("." + listed, StringComparison.Ordinal));
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
