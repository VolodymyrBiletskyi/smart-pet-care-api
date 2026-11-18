using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.AuthModule.Domain;
using smart_pet_care_api.Modules.AuthModule.DTOs.Requests;
using Xunit;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

/// <summary>
/// Sign-up and confirmation against fakes. What these pin down is the account the
/// code belongs to: a second sign-up for an unconfirmed address used to overwrite
/// the password on the row, so whoever posted an address during somebody else's
/// sign-up had the real owner confirm an account that answered to the poster's
/// password. The password now rides on the code, and the party who proves the
/// mailbox decides which sign-up won.
/// </summary>
public class RegistrationConfirmationTests
{
    private const string Email = "owner@example.com";
    private const string OwnerPassword = "Owner-pass1!";
    private const string AttackerPassword = "Attacker-pass1!";

    // ---- who owns the account the code confirms ---------------------------

    [Fact]
    public async Task Register_OnAnUnconfirmedAccount_LeavesTheStoredPasswordAlone()
    {
        var (authRepo, userRepo, service) = Fixture();

        await service.RegisterAsync(Request(OwnerPassword));
        var user = Assert.Single(userRepo.Users);
        var originalHash = user.PasswordHash;

        Backdate(authRepo);
        await service.RegisterAsync(Request(AttackerPassword));

        Assert.Equal(originalHash, user.PasswordHash);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task Confirm_WithTheFirstSignupsCode_KeepsTheFirstPassword()
    {
        var (authRepo, userRepo, service) = Fixture();
        var mail = new FakeEmailSender();
        service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));
        var ownersCode = CodeFrom(mail, index: 0);

        Backdate(authRepo);
        await service.RegisterAsync(Request(AttackerPassword));

        await service.ConfirmEmailAsync(new ConfirmEmailRequest { Email = Email, Code = ownersCode });

        var user = Assert.Single(userRepo.Users);
        Assert.True(user.EmailConfirmed);
        Assert.True(Verify(OwnerPassword, user.PasswordHash));
        Assert.False(Verify(AttackerPassword, user.PasswordHash));
    }

    /// <summary>
    /// The other direction, which is the honest use of a second sign-up: the address
    /// was never confirmed, nobody is waiting on the first attempt, and the password
    /// this caller chose is the one they will sign in with.
    /// </summary>
    [Fact]
    public async Task Confirm_WithASecondSignupsCode_AppliesThatSignupsPassword()
    {
        var (authRepo, userRepo, service) = Fixture();
        var mail = new FakeEmailSender();
        service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));
        Backdate(authRepo);
        await service.RegisterAsync(Request(AttackerPassword));
        var secondCode = CodeFrom(mail, index: 1);

        await service.ConfirmEmailAsync(new ConfirmEmailRequest { Email = Email, Code = secondCode });

        var user = Assert.Single(userRepo.Users);
        Assert.True(Verify(AttackerPassword, user.PasswordHash));
    }

    /// <summary>
    /// The second sign-up must not kill the code the first one is holding. If it did,
    /// the owner would ask for a new code — and a resend inherits the newest binding,
    /// which would hand the account back to the second caller.
    /// </summary>
    [Fact]
    public async Task Register_DoesNotSupersedeAnotherSignupsLiveCode()
    {
        var (authRepo, userRepo, service) = Fixture();

        await service.RegisterAsync(Request(OwnerPassword));
        Backdate(authRepo);
        await service.RegisterAsync(Request(AttackerPassword));

        Assert.Equal(2, authRepo.ConfirmationCodes.Count);
        Assert.All(authRepo.ConfirmationCodes, code => Assert.True(code.IsActive));
    }

    [Fact]
    public async Task Confirm_ClosesEveryOtherLiveCode()
    {
        var (authRepo, userRepo, service) = Fixture();
        var mail = new FakeEmailSender();
        service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));
        var ownersCode = CodeFrom(mail, index: 0);
        Backdate(authRepo);
        await service.RegisterAsync(Request(AttackerPassword));

        await service.ConfirmEmailAsync(new ConfirmEmailRequest { Email = Email, Code = ownersCode });

        Assert.All(authRepo.ConfirmationCodes, code => Assert.NotNull(code.UsedAt));
    }

    [Fact]
    public async Task Resend_RepeatsTheNewestSignupsBinding()
    {
        var (authRepo, userRepo, service) = Fixture();
        var mail = new FakeEmailSender();
        service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));
        Backdate(authRepo);

        await service.ResendConfirmationAsync(Email);
        var resentCode = CodeFrom(mail, index: 1);

        // The resend replaced its own attempt's code rather than adding a third offer.
        Assert.Single(authRepo.ConfirmationCodes, code => code.IsActive);

        await service.ConfirmEmailAsync(new ConfirmEmailRequest { Email = Email, Code = resentCode });

        var user = Assert.Single(userRepo.Users);
        Assert.True(Verify(OwnerPassword, user.PasswordHash));
    }

    [Fact]
    public async Task Register_OnAConfirmedAccount_IsAConflict()
    {
        var (authRepo, userRepo, service) = Fixture();
        await service.RegisterAsync(Request(OwnerPassword));
        userRepo.Users[0].EmailConfirmed = true;

        Backdate(authRepo);
        var failure = await Assert.ThrowsAsync<ConflictException>(
            () => service.RegisterAsync(Request(AttackerPassword)));

        Assert.Equal(ErrorCodes.Auth.EmailAlreadyTaken, failure.Code);
    }

    // ---- the guessing ceiling --------------------------------------------

    [Fact]
    public async Task Confirm_WithAWrongCode_CountsAgainstTheAccount()
    {
        var (authRepo, userRepo, service) = Fixture();
        await service.RegisterAsync(Request(OwnerPassword));
        PinCode(authRepo, index: 0);

        await AssertConfirmFails(service, "000000", EmailConfirmationError.CodeInvalid);

        Assert.Equal(1, userRepo.Users[0].ConfirmationAttempts);
        Assert.NotNull(userRepo.Users[0].ConfirmationAttemptsStartedAt);
    }

    /// <summary>
    /// The point of the whole change: a fresh code used to arrive with a fresh counter,
    /// so five guesses per code was a budget of five per minute for as long as anyone
    /// cared to keep asking.
    /// </summary>
    [Fact]
    public async Task Confirm_AFreshCodeDoesNotResetTheAccountsBudget()
    {
        var (authRepo, userRepo, service) = Fixture();
        var mail = new FakeEmailSender();
        service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));
        PinCode(authRepo, index: 0);

        for (var attempt = 0; attempt < 10; attempt++)
            await AssertConfirmFails(service, "00000" + attempt % 10, EmailConfirmationError.CodeInvalid);

        Backdate(authRepo);
        await service.ResendConfirmationAsync(Email);

        // Even the right code is refused while the account is over its ceiling, so a
        // guess cannot slip through on a code that was only just issued.
        await AssertConfirmFails(
            service, CodeFrom(mail, index: 1), EmailConfirmationError.TooManyAttempts);
    }

    [Fact]
    public async Task Confirm_TheCeilingLapsesWithItsWindow()
    {
        var (authRepo, userRepo, service) = Fixture();
        var mail = new FakeEmailSender();
        service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));
        var code = CodeFrom(mail, index: 0);

        userRepo.Users[0].ConfirmationAttempts = 10;
        userRepo.Users[0].ConfirmationAttemptsStartedAt = DateTime.UtcNow.AddHours(-2);

        await service.ConfirmEmailAsync(new ConfirmEmailRequest { Email = Email, Code = code });

        Assert.True(userRepo.Users[0].EmailConfirmed);
        Assert.Equal(0, userRepo.Users[0].ConfirmationAttempts);
        Assert.Null(userRepo.Users[0].ConfirmationAttemptsStartedAt);
    }

    [Fact]
    public async Task Confirm_ASpentBudgetIsClearedByASuccess()
    {
        var (authRepo, userRepo, service) = Fixture();
        var mail = new FakeEmailSender();
        service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));
        await AssertConfirmFails(service, "000000", EmailConfirmationError.CodeInvalid);

        await service.ConfirmEmailAsync(
            new ConfirmEmailRequest { Email = Email, Code = CodeFrom(mail, index: 0) });

        Assert.Equal(0, userRepo.Users[0].ConfirmationAttempts);
    }

    // ---- the answers that were already right -----------------------------

    [Fact]
    public async Task Confirm_AnExpiredCodeStillSaysExpired()
    {
        var (authRepo, userRepo, service) = Fixture();
        var mail = new FakeEmailSender();
        service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));
        authRepo.ConfirmationCodes[0].ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        await AssertConfirmFails(
            service, CodeFrom(mail, index: 0), EmailConfirmationError.CodeExpired);
    }

    [Fact]
    public async Task Confirm_AnUnknownAddressIsAnInvalidCode()
    {
        var (authRepo, userRepo, service) = Fixture();

        await AssertConfirmFails(service, "123456", EmailConfirmationError.CodeInvalid,
            email: "nobody@example.com");
    }

    [Fact]
    public async Task Confirm_AnAlreadyConfirmedAccountSaysSo()
    {
        var (authRepo, userRepo, service) = Fixture();
        await service.RegisterAsync(Request(OwnerPassword));
        userRepo.Users[0].EmailConfirmed = true;

        await AssertConfirmFails(service, "123456", EmailConfirmationError.AlreadyConfirmed);
    }

    [Fact]
    public async Task Register_InsideTheCooldownIsRefused()
    {
        var (authRepo, userRepo, service) = Fixture();
        await service.RegisterAsync(Request(OwnerPassword));

        await Assert.ThrowsAsync<EmailConfirmationException>(
            () => service.RegisterAsync(Request(AttackerPassword)));
    }

    // ---- what an unconfirmed account can do -------------------------------

    /// <summary>
    /// Nothing, and that is where the ticket's fourth item is answered: an unconfirmed
    /// account is issued no token, so every <c>[Authorize]</c> route — the classifier,
    /// Cloudinary uploads, push registration — is out of reach without a per-module
    /// check. The password is verified first, so this is not a way to learn whether an
    /// address is registered either.
    /// </summary>
    [Fact]
    public async Task Login_BeforeConfirming_IssuesNoTokens()
    {
        var (authRepo, userRepo, service) = Fixture();

        await service.RegisterAsync(Request(OwnerPassword));

        var failure = await Assert.ThrowsAsync<EmailConfirmationException>(
            () => service.LoginAsync(new LoginRequest { Email = Email, Password = OwnerPassword }));

        Assert.Equal(EmailConfirmationError.EmailNotConfirmed, failure.Error);
        Assert.Equal(ErrorCodes.EmailConfirmation.NotConfirmed, failure.Code);
        Assert.Empty(authRepo.RefreshTokens);
    }

    [Fact]
    public async Task Login_AfterConfirming_IssuesTokens()
    {
        var (authRepo, userRepo, service) = Fixture();
        var mail = new FakeEmailSender();
        service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));
        await service.ConfirmEmailAsync(
            new ConfirmEmailRequest { Email = Email, Code = CodeFrom(mail, index: 0) });

        var tokens = await service.LoginAsync(
            new LoginRequest { Email = Email, Password = OwnerPassword });

        Assert.NotNull(tokens.Auth.AccessToken);
        Assert.Single(authRepo.RefreshTokens);
    }

    // ---- what is screened before a letter goes out ------------------------

    /// <summary>
    /// The screen runs before the row, the hash and the SMTP call, so a refused domain
    /// leaves nothing at all behind — no account for its owner to meet later, and no
    /// bounce against this domain's sending reputation.
    /// </summary>
    [Fact]
    public async Task Register_WithARefusedDomain_CreatesNothingAndMailsNobody()
    {
        var authRepo = new FakeAuthRepository();
        var userRepo = new FakeUserRepository();
        var mail = new FakeEmailSender();
        var service = AuthServiceFactory.Create(
            authRepo, userRepo, mail,
            emailDomains: new FakeEmailDomainScreen { RejectWith = ErrorCodes.Auth.EmailDomainInvalid });

        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => service.RegisterAsync(Request(OwnerPassword)));

        Assert.Equal(ErrorCodes.Auth.EmailDomainInvalid, failure.Code);
        Assert.Empty(mail.Sent);
        Assert.Empty(userRepo.Users);
        Assert.Empty(authRepo.ConfirmationCodes);
    }

    /// <summary>
    /// A resend is not screened: it presupposes an account that already passed the
    /// check and already received a letter, so the only thing a second check could do
    /// is keep a real user from their code while their provider's DNS is unreachable.
    /// </summary>
    [Fact]
    public async Task Resend_DoesNotScreenTheDomainAgain()
    {
        var authRepo = new FakeAuthRepository();
        var userRepo = new FakeUserRepository();
        var screen = new FakeEmailDomainScreen();
        var service = AuthServiceFactory.Create(authRepo, userRepo, emailDomains: screen);

        await service.RegisterAsync(Request(OwnerPassword));
        Backdate(authRepo);
        await service.ResendConfirmationAsync(Email);

        Assert.Equal([Email], screen.Screened);
    }

    /// <summary>
    /// The sixty-second cooldown caps the rate and nothing else: a caller willing to
    /// wait a minute between calls can mail sixty letters an hour to an address its
    /// owner never typed. The rate limiter cannot see that address — it partitions on
    /// the client's IP, before any field of the body is bound.
    /// </summary>
    [Fact]
    public async Task Resend_PastTheHourlyCeiling_IsRefused()
    {
        var authRepo = new FakeAuthRepository();
        var userRepo = new FakeUserRepository();
        var mail = new FakeEmailSender();
        var service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));

        for (var sent = 1; sent < 5; sent++)
        {
            Backdate(authRepo);
            await service.ResendConfirmationAsync(Email);
        }

        Assert.Equal(5, mail.Sent.Count);

        Backdate(authRepo);
        var failure = await Assert.ThrowsAsync<EmailConfirmationException>(
            () => service.ResendConfirmationAsync(Email));

        Assert.Equal(EmailConfirmationError.ResendTooSoon, failure.Error);
        Assert.Equal(5, mail.Sent.Count);
    }

    /// <summary>
    /// The ceiling is a window, not a total: the five letters aging out past the hour
    /// leaves the address askable again.
    /// </summary>
    [Fact]
    public async Task Resend_OnceTheWindowHasPassed_IsAllowedAgain()
    {
        var authRepo = new FakeAuthRepository();
        var userRepo = new FakeUserRepository();
        var mail = new FakeEmailSender();
        var service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.RegisterAsync(Request(OwnerPassword));

        for (var sent = 1; sent < 5; sent++)
        {
            Backdate(authRepo);
            await service.ResendConfirmationAsync(Email);
        }

        foreach (var code in authRepo.ConfirmationCodes)
            code.CreatedAt = code.CreatedAt.AddHours(-2);

        await service.ResendConfirmationAsync(Email);

        Assert.Equal(6, mail.Sent.Count);
    }

    // ---- helpers ---------------------------------------------------------

    private static (FakeAuthRepository, FakeUserRepository, AuthService) Fixture()
    {
        var authRepo = new FakeAuthRepository();
        var userRepo = new FakeUserRepository();

        return (authRepo, userRepo, AuthServiceFactory.Create(authRepo, userRepo));
    }

    private static RegisterRequest Request(string password) => new()
    {
        Email = Email,
        Password = password,
        PasswordConfirm = password,
        TermsAccepted = true
    };

    /// <summary>
    /// Pushes every code's creation time past the sixty-second resend cooldown, which
    /// is the only thing standing between two sign-ups in a test.
    /// </summary>
    private static void Backdate(FakeAuthRepository authRepo)
    {
        foreach (var code in authRepo.ConfirmationCodes)
            code.CreatedAt = code.CreatedAt.AddMinutes(-2);
    }

    /// <summary>The six digits out of the nth confirmation email the fake provider rendered.</summary>
    private static string CodeFrom(FakeEmailSender mail, int index) =>
        mail.Sent[index].Text.Split(' ')[1];

    /// <summary>
    /// Replaces a stored code with a known one, so a test that guesses wrong cannot
    /// guess right by luck.
    /// </summary>
    private static void PinCode(FakeAuthRepository authRepo, int index) =>
        authRepo.ConfirmationCodes[index].CodeHash = new FakeJwtProvider().HashToken("999999");

    private static bool Verify(string password, string? hash) =>
        hash is not null && BCrypt.Net.BCrypt.EnhancedVerify(password, hash);

    private static async Task AssertConfirmFails(
        AuthService service, string code, EmailConfirmationError expected, string? email = null)
    {
        var failure = await Assert.ThrowsAsync<EmailConfirmationException>(
            () => service.ConfirmEmailAsync(
                new ConfirmEmailRequest { Email = email ?? Email, Code = code }));

        Assert.Equal(expected, failure.Error);
    }
}
