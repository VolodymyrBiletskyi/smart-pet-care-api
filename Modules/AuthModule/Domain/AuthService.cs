using System.Security.Cryptography;
using smart_pet_care_api.Infrastructure.Email;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.DTOs.Requests;
using smart_pet_care_api.Modules.AuthModule.DTOs.Responses;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.AuthModule.OAuth;
using smart_pet_care_api.Modules.AuthModule.Repository;
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

        private const int ConfirmationCodeMinutes = 15;
        private const int MaxConfirmationAttempts = 5;
        private const int ResendCooldownSeconds = 60;

        public AuthService(IJwtProvider jwt, IUserRepository userRepo, IAuthRepository authRepo, IGoogleOAuth googleOAuth, IEmailSender emailSender)
        {
            _jwt = jwt;
            _userRepo = userRepo;
            _authRepo = authRepo;
            _googleOAuth = googleOAuth;
            _emailSender = emailSender;
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        public async Task RegisterAsync(RegisterRequest request)
        {
            var email = NormalizeEmail(request.Email);
            var passwordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(request.Password);

            var existing = await _userRepo.GetTrackedByEmailAsync(email);
            if (existing is not null)
            {
                if (existing.EmailConfirmed)
                    throw new InvalidOperationException("Email is already taken");

                // Unconfirmed account: the previous registrant never proved
                // ownership of this mailbox, so let the new attempt take over
                // instead of locking the address forever.
                existing.PasswordHash = passwordHash;
                existing.UpdatedAt = DateTime.UtcNow;
                await SendConfirmationCodeAsync(existing);
                return;
            }

            var user = await _userRepo.AddAsync(new User
            {
                Email = email,
                PasswordHash = passwordHash,
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
                ?? throw new InvalidOperationException("No account found for this email");

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

            await _emailSender.SendAsync(
                user.Email,
                "Your Smart Pet Care confirmation code",
                $"Your confirmation code is: {rawCode}\n\nIt expires in {ConfirmationCodeMinutes} minutes. " +
                "If you did not request this, you can ignore this email.");
        }

        public async Task<AuthTokenPair> LoginAsync(LoginRequest request)
        {
            var user = await _userRepo.GetByEmailAsync(NormalizeEmail(request.Email))
                ?? throw new InvalidOperationException("Invalid email or password");

            // OAuth-only accounts have no password hash.
            if (user.PasswordHash is null)
                throw new InvalidOperationException("Invalid email or password");

            var isValid = BCrypt.Net.BCrypt.EnhancedVerify(request.Password, user.PasswordHash);
            if (!isValid)
                throw new InvalidOperationException("Invalid email or password");

            if (!user.EmailConfirmed)
                throw new EmailConfirmationException(EmailConfirmationError.EmailNotConfirmed);

            return await IssueTokensAsync(user);
        }

        public async Task LogoutAsync(Guid userId)
        {
            var activeTokens = await _authRepo.GetActiveByUserAsync(userId);
            if (activeTokens.Count == 0) return;

            foreach (var token in activeTokens)
                token.RevokedAt = DateTime.UtcNow;

            await _authRepo.SaveChangesAsync();
        }

        public async Task<AuthTokenPair?> RefreshAsync(string rawRefreshToken)
        {
            var hash = _jwt.HashToken(rawRefreshToken);
            var token = await _authRepo.GetByHashAsync(hash);

            if (token is null || !token.IsActive)
                return null;

            var user = await _userRepo.GetByIdAsync(token.UserId);
            if (user is null) return null;

            token.RevokedAt = DateTime.UtcNow;

            return await IssueTokensAsync(user);
        }

        private async Task<AuthTokenPair> IssueTokensAsync(User user)
        {
            var accessToken = _jwt.GenerateAccessToken(user);
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);

            var rawRefresh = _jwt.GenerateRefreshToken();
            var hash = _jwt.HashToken(rawRefresh);

            await _authRepo.AddAsync(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = hash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });

            await _authRepo.SaveChangesAsync();

            return new AuthTokenPair
            {
                Auth = new AuthResponse
                {
                    Id = user.Id,
                    Email = user.Email,
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
                var existingUser = await _userRepo.GetByIdAsync(existingLogin.UserId);
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