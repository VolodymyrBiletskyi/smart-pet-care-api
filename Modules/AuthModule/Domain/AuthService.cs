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

        public AuthService(IJwtProvider jwt, IUserRepository userRepo, IAuthRepository authRepo, IGoogleOAuth googleOAuth)
        {
            _jwt = jwt;
            _userRepo = userRepo;
            _authRepo = authRepo;
            _googleOAuth = googleOAuth;
        }

        public async Task<AuthTokenPair> LoginAsync(LoginRequest request)
        {
            var user = await _userRepo.GetByEmailAsync(request.Email)
                ?? throw new InvalidOperationException("Invalid email or password");

            var isValid = BCrypt.Net.BCrypt.EnhancedVerify(request.Password, user.PasswordHash);
            if (!isValid)
                throw new InvalidOperationException("Invalid email or password");

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

        private async Task<AuthTokenPair> LoginOrRegisterOAuthUserAsync(GoogleUserInfo userInfo)
        {
            // 1. check if oauth account already linked
            var existingLogin = await _userRepo.GetExternalLoginAsync(AuthProvider.Google, userInfo.GoogleId);
            if (existingLogin is not null)
            {
                var existingUser = await _userRepo.GetByIdAsync(existingLogin.UserId);
                return await IssueTokensAsync(existingUser!);
            }

            // 2. check if user exists with same email
            var user = await _userRepo.GetByEmailAsync(userInfo.Email);
            if (user is null)
            {
                // 3. create new user
                user = await _userRepo.AddAsync(new User
                {
                    Email = userInfo.Email,
                    PasswordHash = null
                });
            }

            // 4. link external login to user
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