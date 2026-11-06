using smart_pet_care_api.Common.Api;
using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using smart_pet_care_api.Infrastructure.Email;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.Infrastructure;
using smart_pet_care_api.Modules.AuthModule.DTOs.Requests;
using smart_pet_care_api.Modules.AuthModule.DTOs.Responses;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.AuthModule.OAuth;
using smart_pet_care_api.Modules.AuthModule.Repository;
using smart_pet_care_api.Modules.NotificationModule.Domain;
using smart_pet_care_api.Modules.UserModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    public class AuthService : IAuthService
    {
        private readonly IJwtProvider _jwt;
        private readonly IUserRepository _userRepo;
        private readonly IAuthRepository _authRepo;
        private readonly IGoogleOAuth _googleOAuth;
        private readonly IEmailSender _emailSender;
        private readonly IEmailTemplateProvider _emailTemplates;
        private readonly IDeviceTokenService _deviceTokenService;
        private readonly IPasswordResetLinkBuilder _resetLinks;
        private readonly PasswordResetOptions _resetOptions;
        private readonly IAuthStateCache _authState;
        private readonly ILogger<AuthService> _logger;

        private const int ConfirmationCodeMinutes = 15;
        private const int ResendCooldownSeconds = 60;

        /// <summary>
        /// Wrong confirmation codes one account may be sent in
        /// <see cref="ConfirmationAttemptWindow"/>. It used to be five <em>per code</em>,
        /// which capped nothing: a sixth guess was a resend away, and the resend reset the
        /// counter. Ten an hour leaves room for a person mistyping a six-digit code and
        /// takes a guessing budget of a million down to something that never arrives.
        /// </summary>
        private const int MaxConfirmationAttempts = 10;
        private static readonly TimeSpan ConfirmationAttemptWindow = TimeSpan.FromHours(1);

        /// <summary>
        /// How soon after a refresh token was replaced its predecessor coming back is read
        /// as a client asking twice rather than as a replay. Either way the request is
        /// refused; this only decides whether the whole session dies with it, and a
        /// mobile client with two requests in flight should not be signed out for it.
        /// </summary>
        private static readonly TimeSpan RefreshRetryGrace = TimeSpan.FromSeconds(10);

        public AuthService(IJwtProvider jwt, IUserRepository userRepo, IAuthRepository authRepo, IGoogleOAuth googleOAuth, IEmailSender emailSender, IEmailTemplateProvider emailTemplates, IDeviceTokenService deviceTokenService, IPasswordResetLinkBuilder resetLinks, IOptions<PasswordResetOptions> resetOptions, IAuthStateCache authState, ILogger<AuthService> logger)
        {
            _jwt = jwt;
            _userRepo = userRepo;
            _authRepo = authRepo;
            _googleOAuth = googleOAuth;
            _emailSender = emailSender;
            _emailTemplates = emailTemplates;
            _deviceTokenService = deviceTokenService;
            _resetLinks = resetLinks;
            _resetOptions = resetOptions.Value;
            _authState = authState;
            _logger = logger;
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private static string HashPassword(string password) =>
            BCrypt.Net.BCrypt.EnhancedHashPassword(password);

        public async Task RegisterAsync(RegisterRequest request)
        {
            var email = NormalizeEmail(request.Email);

            // The four columns this needs, not the row: a tracked load drags the avatar
            // bytes along, and nothing here writes to the user any more.
            var existing = await _userRepo.GetCredentialsByEmailAsync(email);
            if (existing is not null)
            {
                if (existing.EmailConfirmed)
                    throw new ConflictException(ErrorCodes.Auth.EmailAlreadyTaken, "Email is already taken");

                // Unconfirmed account: the previous registrant never proved ownership of
                // this mailbox, so let the new attempt claim it instead of locking the
                // address forever. What the attempt no longer does is set the password on
                // the row — it rides along on the confirmation code and is applied by
                // whoever proves the mailbox.
                //
                // Writing it here meant anyone who posted this address while its owner was
                // half way through signing up replaced the password behind them. The code
                // still went to the real mailbox, so the owner confirmed, carried on into
                // an account that answered to somebody else's password, and met nothing
                // but a failed login to tell them.
                //
                // Hashed after the lookup rather than at the top of the method: an enhanced
                // BCrypt hash is deliberately expensive, and computing it first meant every
                // request that was about to be rejected still cost a full hash. That is the
                // work an attacker gets for free by posting addresses nobody owns.
                await SendConfirmationCodeAsync(
                    existing.Id, existing.Email, HashPassword(request.Password));
                return;
            }

            var user = await _userRepo.AddAsync(new User
            {
                Email = email,
                PasswordHash = HashPassword(request.Password),
                TermsAccepted = true,
                TermsAcceptedAt = DateTime.UtcNow,
                EmailConfirmed = false,
                CreatedAt = DateTime.UtcNow
            });

            // No pending hash: this row was just created for this sign-up, so the password
            // on it is already this caller's and nobody else has a claim on the address.
            await SendConfirmationCodeAsync(user.Id, user.Email, pendingPasswordHash: null);
        }

        public async Task ConfirmEmailAsync(ConfirmEmailRequest request)
        {
            var user = await _userRepo.GetTrackedByEmailAsync(NormalizeEmail(request.Email))
                ?? throw new EmailConfirmationException(EmailConfirmationError.CodeInvalid);

            if (user.EmailConfirmed)
                throw new EmailConfirmationException(EmailConfirmationError.AlreadyConfirmed);

            var now = DateTime.UtcNow;

            if (user.ConfirmationAttemptsStartedAt is { } windowStart
                && windowStart + ConfirmationAttemptWindow <= now)
            {
                user.ConfirmationAttempts = 0;
                user.ConfirmationAttemptsStartedAt = null;
            }

            if (user.ConfirmationAttempts >= MaxConfirmationAttempts)
                throw new EmailConfirmationException(EmailConfirmationError.TooManyAttempts);

            // Every live code, not the newest one. Two sign-ups for one unconfirmed
            // address leave two open offers, and the second arriving must not invalidate
            // the code the first registrant is holding — see SendConfirmationCodeAsync.
            var active = await _authRepo.GetActiveConfirmationCodesAsync(user.Id);

            if (active.Count == 0)
            {
                // Nothing live. "Expired" rather than "invalid" only when that is what
                // happened, because it is the one case where the client can say something
                // better than "wrong code": ask for a new one.
                var latest = await _authRepo.GetLatestConfirmationCodeAsync(user.Id);

                throw new EmailConfirmationException(
                    latest is { UsedAt: null, IsExpired: true }
                        ? EmailConfirmationError.CodeExpired
                        : EmailConfirmationError.CodeInvalid);
            }

            var codeHash = _jwt.HashToken(request.Code);
            var match = active.FirstOrDefault(c => c.CodeHash == codeHash);

            if (match is null)
            {
                user.ConfirmationAttempts++;
                user.ConfirmationAttemptsStartedAt ??= now;

                // Diagnostic: a wrong guess cannot be attributed to one of several live
                // codes, so it is recorded against each that was live when it arrived.
                foreach (var code in active)
                    code.Attempts++;

                await _authRepo.SaveChangesAsync();
                throw new EmailConfirmationException(EmailConfirmationError.CodeInvalid);
            }

            // The password the confirmed sign-up supplied, applied by the only party who
            // could have read the code out of the mailbox.
            if (match.PendingPasswordHash is not null)
                user.PasswordHash = match.PendingPasswordHash;

            // Every other live code dies with it: an open offer to set this account's
            // password is only open while the account has no proven owner.
            foreach (var code in active)
                code.UsedAt = now;

            user.EmailConfirmed = true;
            user.EmailConfirmedAt = now;
            user.ConfirmationAttempts = 0;
            user.ConfirmationAttemptsStartedAt = null;
            user.UpdatedAt = now;

            await _authRepo.SaveChangesAsync();
        }

        public async Task ResendConfirmationAsync(string email)
        {
            var user = await _userRepo.GetCredentialsByEmailAsync(NormalizeEmail(email))
                ?? throw new NotFoundException(ErrorCodes.Auth.AccountNotFound, "No account found for this email");

            if (user.EmailConfirmed)
                throw new EmailConfirmationException(EmailConfirmationError.AlreadyConfirmed);

            // The resend repeats the newest sign-up, password binding and all. This
            // endpoint is given an address and nothing that says which sign-up the caller
            // is waiting on, and a code carrying no binding would hand the account to
            // whatever hash the row happens to hold.
            var latest = await _authRepo.GetLatestConfirmationCodeAsync(user.Id);

            await SendConfirmationCodeAsync(user.Id, user.Email, latest?.PendingPasswordHash);
        }

        /// <param name="pendingPasswordHash">
        /// The password this code applies when it is used, or null for a code that leaves
        /// the account's own password alone.
        /// </param>
        private async Task SendConfirmationCodeAsync(
            Guid userId, string email, string? pendingPasswordHash)
        {
            var latest = await _authRepo.GetLatestConfirmationCodeAsync(userId);
            if (latest is not null && latest.CreatedAt > DateTime.UtcNow.AddSeconds(-ResendCooldownSeconds))
                throw new EmailConfirmationException(EmailConfirmationError.ResendTooSoon);

            // A new code supersedes an earlier one only when the two carry the same
            // pending password — that is, when this is a resend of the same sign-up.
            //
            // Superseding unconditionally would hand the whole takeover back: a second
            // sign-up would kill the code its owner is holding, the owner would ask for
            // another, and a resend inherits the newest binding, which would be the
            // second caller's. Leaving both live costs nothing now that the guessing
            // ceiling is on the account rather than on the code.
            foreach (var old in await _authRepo.GetActiveConfirmationCodesAsync(userId))
                if (string.Equals(old.PendingPasswordHash, pendingPasswordHash, StringComparison.Ordinal))
                    old.UsedAt = DateTime.UtcNow;

            var rawCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

            await _authRepo.AddConfirmationCodeAsync(new EmailConfirmationCode
            {
                UserId = userId,
                CodeHash = _jwt.HashToken(rawCode),
                PendingPasswordHash = pendingPasswordHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(ConfirmationCodeMinutes),
                CreatedAt = DateTime.UtcNow
            });

            await _authRepo.SaveChangesAsync();

            // Subject without the code in it on purpose: the subject is what a
            // lock screen shows.
            var body = _emailTemplates.ConfirmationCode(rawCode, ConfirmationCodeMinutes);
            await _emailSender.SendAsync(
                email, "Confirm your Miso email", body.Text, body.Html);
        }

        /// <summary>
        /// Mails a reset link. Answers identically for an address with an account and one
        /// without: this is the one anonymous endpoint where the difference between the
        /// two answers is itself the product — a free "does this person use Miso" lookup
        /// against any list of addresses. <c>resend-confirmation</c> does return a 404,
        /// and it can: the caller there has just been told the address is registered.
        /// </summary>
        public async Task ForgotPasswordAsync(string email)
        {
            // Not the tracked row: nothing here writes to the user, and the tracked load
            // drags the avatar bytes along with it.
            var user = await _userRepo.GetCredentialsByEmailAsync(NormalizeEmail(email));
            if (user is null)
                return;

            // A per-account cooldown like the confirmation code's, but silent. Raising
            // ResendTooSoon would answer the question the 404 above refuses to, so the
            // second request inside the window is accepted and simply sends nothing. The
            // per-address rate limiter is what actually caps the endpoint.
            var latest = await _authRepo.GetLatestPasswordResetTokenAsync(user.Id);
            if (latest is not null && latest.CreatedAt > DateTime.UtcNow.AddSeconds(-ResendCooldownSeconds))
                return;

            // A new link supersedes any previous one, so a user who clicks the older mail
            // is told the link is spent rather than quietly handed a second live
            // credential for the same account.
            foreach (var old in await _authRepo.GetActivePasswordResetTokensAsync(user.Id))
                old.UsedAt = DateTime.UtcNow;

            var rawToken = GeneratePasswordResetToken();

            await _authRepo.AddPasswordResetTokenAsync(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = _jwt.HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddMinutes(_resetOptions.LinkLifetimeMinutes),
                CreatedAt = DateTime.UtcNow
            });

            await _authRepo.SaveChangesAsync();

            var body = _emailTemplates.PasswordReset(
                _resetLinks.EmailLink(rawToken), _resetOptions.LinkLifetimeMinutes);

            await _emailSender.SendAsync(
                user.Email, "Reset your Miso password", body.Text, body.Html);
        }

        public async Task<bool> IsPasswordResetTokenUsableAsync(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
                return false;

            var token = await _authRepo.GetPasswordResetTokenAsync(HashResetToken(rawToken));
            return token?.IsActive == true;
        }

        public async Task ResetPasswordAsync(ResetPasswordRequest request)
        {
            var token = await _authRepo.GetPasswordResetTokenAsync(HashResetToken(request.Token));

            if (token is null || token.UsedAt is not null)
                throw new ValidationException(
                    ErrorCodes.Auth.ResetTokenInvalid, "This reset link is not valid");

            if (token.IsExpired)
                throw new GoneException(
                    ErrorCodes.Auth.ResetTokenExpired, "This reset link has expired, request a new one");

            // Tracked, avatar bytes and all, rather than a projection plus an
            // ExecuteUpdate: spending the token and setting the password have to be one
            // statement batch, and a reset that burned the link without changing the
            // password would lock the user out of their own recovery.
            var user = await _userRepo.GetTrackedByIdAsync(token.UserId)
                ?? throw new ValidationException(
                    ErrorCodes.Auth.ResetTokenInvalid, "This reset link is not valid");

            token.UsedAt = DateTime.UtcNow;
            user.PasswordHash = HashPassword(request.Password);
            user.UpdatedAt = DateTime.UtcNow;

            // Following the link proved control of the mailbox, which is exactly what the
            // six-digit code proves. Without this an account that never confirmed could
            // reset its password and still be unable to log in.
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                user.EmailConfirmedAt = DateTime.UtcNow;
            }

            // Every session goes with the password. A reset is what a user does when they
            // believe somebody else knows it, and a refresh token left alive keeps that
            // somebody signed in for its remaining seven days.
            foreach (var refresh in await _authRepo.GetAllActiveByUserAsync(user.Id))
                refresh.RevokedAt = DateTime.UtcNow;

            // And the access token already in hand, which revoking refresh tokens does
            // not reach: it is signed, unexpired and looked up nowhere, so it carried on
            // reading and writing this account's data for up to another fifteen minutes.
            // AuthMiddleware compares this against the token's own issue time.
            user.TokensValidFrom = DateTime.UtcNow;

            await _authRepo.SaveChangesAsync();

            // The middleware caches that cutoff per user for three minutes, which is how
            // long "signed out at once" would otherwise take.
            _authState.Invalidate(user.Id);

            // And every push registration, which revoking the sessions does not cover:
            // who receives a notification is decided by the account a DeviceTokens row
            // belongs to, not by any session, so a device that registered while somebody
            // else held this password keeps receiving the owner's reminders — pet name in
            // the title, the reminder in the body — until the 60-day pruning, which a
            // delivered push keeps pushing back. Unlike logout this takes all of them:
            // after a reset, "the user's other devices are still theirs" is exactly the
            // assumption that no longer holds. Each of the owner's own devices has to log
            // in again anyway, and registers on the way.
            //
            // After the save, like logout, and one ExecuteDelete for the same reason: the
            // password change is the part that must not be lost to a row the push sender
            // pruned at the same moment.
            await _deviceTokenService.UnregisterAllAsync(user.Id);

            await NotifyPasswordChangedAsync(user.Email);
        }

        /// <summary>
        /// Tells the mailbox that the password changed. Until this existed a completed
        /// takeover notified nobody: the owner's only symptom was a login that stopped
        /// working, and on a Google-only account — which never meets a password prompt —
        /// there was no symptom at all.
        /// <para>
        /// Best effort, and deliberately after the save. The password has already changed
        /// by this point, so a dead SMTP server must not turn into a failure that tells
        /// the client the reset did not happen. The reset is the product; the notice is
        /// about it.
        /// </para>
        /// </summary>
        private async Task NotifyPasswordChangedAsync(string email)
        {
            try
            {
                var body = _emailTemplates.PasswordChanged();
                await _emailSender.SendAsync(
                    email, "Your Miso password was changed", body.Text, body.Html);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Password changed, but the notification email could not be sent.");
            }
        }

        /// <summary>
        /// 256 bits, base64url. URL-safe rather than <see cref="IJwtProvider.GenerateRefreshToken"/>'s
        /// plain base64, because this one travels as a query parameter through a mail
        /// client, and <c>+</c> surviving that round trip is not something to rely on.
        /// </summary>
        private static string GeneratePasswordResetToken()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Base64Url.EncodeToString(bytes);
        }

        /// <summary>
        /// Trimmed, because base64url contains no whitespace and an app that pulls the
        /// token out of a deep link can easily arrive with a trailing newline — which
        /// would otherwise read as a link that does not exist.
        /// </summary>
        private string HashResetToken(string rawToken) => _jwt.HashToken(rawToken.Trim());

        public async Task<AuthTokenPair> LoginAsync(LoginRequest request)
        {
            // The four columns sign-in reads, rather than the row: a user carries their
            // avatar bytes, and a login has no use for a megabyte of them.
            var user = await _userRepo.GetCredentialsByEmailAsync(NormalizeEmail(request.Email))
                ?? throw new UnauthorizedException(ErrorCodes.Auth.InvalidCredentials, "Invalid email or password");

            // OAuth-only accounts have no password hash.
            if (user.PasswordHash is null)
                throw new UnauthorizedException(ErrorCodes.Auth.InvalidCredentials, "Invalid email or password");

            var isValid = BCrypt.Net.BCrypt.EnhancedVerify(request.Password, user.PasswordHash);
            if (!isValid)
                throw new UnauthorizedException(ErrorCodes.Auth.InvalidCredentials, "Invalid email or password");

            if (!user.EmailConfirmed)
                throw new EmailConfirmationException(EmailConfirmationError.EmailNotConfirmed);

            return await IssueTokensAsync(user.Id, user.Email);
        }

        public async Task LogoutAsync(Guid userId, string? deviceToken = null)
        {
            var now = DateTime.UtcNow;

            // All of them. This used to take GetActiveByUserAsync's ten newest, so an
            // eleventh live refresh token outlived the sign-out and kept working for its
            // remaining seven days.
            foreach (var token in await _authRepo.GetAllActiveByUserAsync(userId))
                token.RevokedAt = now;

            await _authRepo.SaveChangesAsync();

            // The access token goes too, the same way a reset ends it: revoking refresh
            // tokens stops a session continuing, but says nothing about the fifteen
            // minutes the token in hand still has on it.
            await _userRepo.SetTokensValidFromAsync(userId, now);
            _authState.Invalidate(userId);

            // Dropped alongside the session rather than left to the client's own call to
            // DELETE /api/notifications/device-token: a registration that outlives the
            // session keeps delivering this account's reminders — pet name and all — to a
            // device somebody else may now be signed in on. Only the device that is signing
            // out is unregistered, so the user's other devices keep their pushes.
            //
            // It runs after the revocation, not before: the registration delete races the
            // push sender's own pruning of the same row, and a failure there must not leave
            // the session alive for its full seven days.
            if (!string.IsNullOrWhiteSpace(deviceToken))
                await _deviceTokenService.UnregisterAsync(userId, deviceToken);
        }

        public async Task<AuthTokenPair?> RefreshAsync(string rawRefreshToken)
        {
            var hash = _jwt.HashToken(rawRefreshToken);
            var token = await _authRepo.GetByHashAsync(hash);

            if (token is null)
                return null;

            if (token.RevokedAt is { } revokedAt)
            {
                // A token that was already spent has come back. Rotation alone cannot
                // tell that from a client asking twice, so the question is who else is
                // holding this chain — and the honest answer is that somebody might be.
                // The family is one sign-in on one device, so ending it signs that device
                // out and leaves the user's other devices alone.
                //
                // Inside the grace it is read as the client's own retry and only refused.
                // A thief gains nothing from the difference: either way no tokens are
                // issued for a token that has been spent.
                if (token.ReplacedByTokenHash is null
                    || revokedAt + RefreshRetryGrace <= DateTime.UtcNow)
                {
                    await _authRepo.RevokeFamilyAsync(token.TokenFamilyId, DateTime.UtcNow);

                    _logger.LogWarning(
                        "A spent refresh token was presented; revoked session family {Family}.",
                        token.TokenFamilyId);
                }

                return null;
            }

            if (!token.IsActive)
                return null;

            var user = await _userRepo.GetCredentialsByIdAsync(token.UserId);
            if (user is null) return null;

            return await IssueTokensAsync(user.Id, user.Email, token);
        }

        private Task<AuthTokenPair> IssueTokensAsync(User user) =>
            IssueTokensAsync(user.Id, user.Email);

        /// <summary>
        /// The id and the address are the whole of it — they are the access token's two
        /// claims and the two fields of the response — so the callers that have only those
        /// need not load a user row to issue a session.
        /// </summary>
        /// <param name="rotated">
        /// The refresh token this one replaces, on a refresh. It is revoked here and
        /// hands its family down, so the new token belongs to the same session; null from
        /// a sign-in, which starts a family of its own.
        /// </param>
        private async Task<AuthTokenPair> IssueTokensAsync(
            Guid userId, string email, RefreshToken? rotated = null)
        {
            var accessToken = _jwt.GenerateAccessToken(userId, email);
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);

            var rawRefresh = _jwt.GenerateRefreshToken();
            var hash = _jwt.HashToken(rawRefresh);

            if (rotated is not null)
            {
                rotated.RevokedAt = DateTime.UtcNow;
                rotated.ReplacedByTokenHash = hash;
            }

            await _authRepo.AddAsync(new RefreshToken
            {
                UserId = userId,
                TokenHash = hash,
                TokenFamilyId = rotated?.TokenFamilyId ?? Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });

            await _authRepo.SaveChangesAsync();

            return new AuthTokenPair
            {
                Auth = new AuthResponse
                {
                    Id = userId,
                    Email = email,
                    AccessToken = accessToken,
                    ExpiresAtUtc = expiresAt
                },
                RefreshToken = rawRefresh
            };
        }
        public async Task<AuthTokenPair> GoogleLoginAsync(string code)
        {
            var userInfo = await _googleOAuth.ExchangeCodeAsync(code);
            return await LoginOrRegisterOAuthUserAsync(userInfo);
        }

        public async Task<AuthTokenPair> GoogleMobileLoginAsync(string idToken)
        {
            var userInfo = await _googleOAuth.ValidateIdTokenAsync(idToken);
            return await LoginOrRegisterOAuthUserAsync(userInfo);
        }

        private static void SyncGoogleProfile(User user, GoogleUserInfo userInfo)
        {
            user.DisplayName ??= userInfo.Name;
            user.AvatarUrl ??= userInfo.Picture;
        }

        private async Task<AuthTokenPair> LoginOrRegisterOAuthUserAsync(GoogleUserInfo userInfo)
        {
            var existingLogin = await _userRepo.GetExternalLoginAsync(AuthProvider.Google, userInfo.GoogleId);
            if (existingLogin is not null)
            {
                var existingUser = await _userRepo.GetTrackedByIdAsync(existingLogin.UserId);
                SyncGoogleProfile(existingUser!, userInfo);
                await _userRepo.SaveChangesAsync();
                return await IssueTokensAsync(existingUser!);
            }

            var email = NormalizeEmail(userInfo.Email);

            var user = await _userRepo.GetTrackedByEmailAsync(email);
            if (user is null)
            {
                user = await _userRepo.AddAsync(new User
                {
                    Email = email,
                    PasswordHash = null,
                    TermsAccepted = true,
                    TermsAcceptedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Google has already verified this email.
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                user.EmailConfirmedAt = DateTime.UtcNow;
            }

            SyncGoogleProfile(user, userInfo);

            await _userRepo.AddExternalLoginAsync(new ExternalLogin
            {
                UserId = user.Id,
                Provider = AuthProvider.Google,
                ProviderUserId = userInfo.GoogleId,
                ProviderEmail = userInfo.Email
            });

            return await IssueTokensAsync(user);
        }
    }
}
