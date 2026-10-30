using smart_pet_care_api.Common.Api;
using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using smart_pet_care_api.Infrastructure.Email;
using smart_pet_care_api.Models;
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

        private const int ConfirmationCodeMinutes = 15;
        private const int MaxConfirmationAttempts = 5;
        private const int ResendCooldownSeconds = 60;

        public AuthService(IJwtProvider jwt, IUserRepository userRepo, IAuthRepository authRepo, IGoogleOAuth googleOAuth, IEmailSender emailSender, IEmailTemplateProvider emailTemplates, IDeviceTokenService deviceTokenService, IPasswordResetLinkBuilder resetLinks, IOptions<PasswordResetOptions> resetOptions)
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
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private static string HashPassword(string password) =>
            BCrypt.Net.BCrypt.EnhancedHashPassword(password);

        public async Task RegisterAsync(RegisterRequest request)
        {
            var email = NormalizeEmail(request.Email);

            var existing = await _userRepo.GetTrackedByEmailAsync(email);
            if (existing is not null)
            {
                if (existing.EmailConfirmed)
                    throw new ConflictException(ErrorCodes.Auth.EmailAlreadyTaken, "Email is already taken");

                // Unconfirmed account: the previous registrant never proved
                // ownership of this mailbox, so let the new attempt take over
                // instead of locking the address forever.
                //
                // Hashed here rather than at the top of the method: an enhanced BCrypt hash is
                // deliberately expensive, and computing it before the lookup meant every
                // request that was about to be rejected still cost a full hash. That is the
                // work an attacker gets for free by posting addresses nobody owns.
                existing.PasswordHash = HashPassword(request.Password);
                existing.UpdatedAt = DateTime.UtcNow;
                await SendConfirmationCodeAsync(existing);
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

            await SendConfirmationCodeAsync(user);
        }

        public async Task ConfirmEmailAsync(ConfirmEmailRequest request)
        {
            var user = await _userRepo.GetTrackedByEmailAsync(NormalizeEmail(request.Email))
                ?? throw new EmailConfirmationException(EmailConfirmationError.CodeInvalid);

            if (user.EmailConfirmed)
                throw new EmailConfirmationException(EmailConfirmationError.AlreadyConfirmed);

            var code = await _authRepo.GetLatestConfirmationCodeAsync(user.Id);

            if (code is null || code.UsedAt is not null)
                throw new EmailConfirmationException(EmailConfirmationError.CodeInvalid);

            if (code.IsExpired)
                throw new EmailConfirmationException(EmailConfirmationError.CodeExpired);

            if (code.Attempts >= MaxConfirmationAttempts)
                throw new EmailConfirmationException(EmailConfirmationError.TooManyAttempts);

            if (code.CodeHash != _jwt.HashToken(request.Code))
            {
                code.Attempts++;
                await _authRepo.SaveChangesAsync();
                throw new EmailConfirmationException(EmailConfirmationError.CodeInvalid);
            }

            code.UsedAt = DateTime.UtcNow;
            user.EmailConfirmed = true;
            user.EmailConfirmedAt = DateTime.UtcNow;
            await _authRepo.SaveChangesAsync();
        }

        public async Task ResendConfirmationAsync(string email)
        {
            var user = await _userRepo.GetTrackedByEmailAsync(NormalizeEmail(email))
                ?? throw new NotFoundException(ErrorCodes.Auth.AccountNotFound, "No account found for this email");

            if (user.EmailConfirmed)
                throw new EmailConfirmationException(EmailConfirmationError.AlreadyConfirmed);

            await SendConfirmationCodeAsync(user);
        }

        private async Task SendConfirmationCodeAsync(User user)
        {
            var latest = await _authRepo.GetLatestConfirmationCodeAsync(user.Id);
            if (latest is not null && latest.CreatedAt > DateTime.UtcNow.AddSeconds(-ResendCooldownSeconds))
                throw new EmailConfirmationException(EmailConfirmationError.ResendTooSoon);

            // A new code supersedes any previous one.
            foreach (var old in await _authRepo.GetActiveConfirmationCodesAsync(user.Id))
                old.UsedAt = DateTime.UtcNow;

            var rawCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

            await _authRepo.AddConfirmationCodeAsync(new EmailConfirmationCode
            {
                UserId = user.Id,
                CodeHash = _jwt.HashToken(rawCode),
                ExpiresAt = DateTime.UtcNow.AddMinutes(ConfirmationCodeMinutes),
                CreatedAt = DateTime.UtcNow
            });

            await _authRepo.SaveChangesAsync();

            // Subject without the code in it on purpose: the subject is what a
            // lock screen shows.
            var body = _emailTemplates.ConfirmationCode(rawCode, ConfirmationCodeMinutes);
            await _emailSender.SendAsync(
                user.Email, "Confirm your Miso email", body.Text, body.Html);
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

            await _authRepo.SaveChangesAsync();

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
            var activeTokens = await _authRepo.GetActiveByUserAsync(userId);
            if (activeTokens.Count > 0)
            {
                foreach (var token in activeTokens)
                    token.RevokedAt = DateTime.UtcNow;

                await _authRepo.SaveChangesAsync();
            }

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

            if (token is null || !token.IsActive)
                return null;

            var user = await _userRepo.GetCredentialsByIdAsync(token.UserId);
            if (user is null) return null;

            token.RevokedAt = DateTime.UtcNow;

            return await IssueTokensAsync(user.Id, user.Email);
        }

        private Task<AuthTokenPair> IssueTokensAsync(User user) =>
            IssueTokensAsync(user.Id, user.Email);

        /// <summary>
        /// The id and the address are the whole of it — they are the access token's two
        /// claims and the two fields of the response — so the callers that have only those
        /// need not load a user row to issue a session.
        /// </summary>
        private async Task<AuthTokenPair> IssueTokensAsync(Guid userId, string email)
        {
            var accessToken = _jwt.GenerateAccessToken(userId, email);
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);

            var rawRefresh = _jwt.GenerateRefreshToken();
            var hash = _jwt.HashToken(rawRefresh);

            await _authRepo.AddAsync(new RefreshToken
            {
                UserId = userId,
                TokenHash = hash,
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
