using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.AuthModule.Jwt
{
    public class JwtProvider : IJwtProvider
    {
        private static readonly JwtSecurityTokenHandler TokenHandler = new();
        private readonly JwtOptions _options;
        private readonly byte[] _key;

        public JwtProvider(IOptions<JwtOptions> options)
        {
            _options = options.Value;
            _key = Encoding.UTF8.GetBytes(_options.SecretKey);
        }

        public int AccessTokenMinutes => _options.AccessTokenMinutes;

        public string GenerateAccessToken(Guid userId, string email)
        {
            // The issue time is a claim of our own rather than the handler's iat, because
            // AuthMiddleware compares it against User.TokensValidFrom to refuse tokens
            // from a session that has since been signed out. A claim this API writes
            // itself cannot stop being written by an SDK default.
            Claim[] claims = [
                new(AuthClaims.UserId, userId.ToString()),
                new(AuthClaims.Email, email ?? string.Empty),
                // Milliseconds, not seconds: the comparison is against a stored instant,
                // and at second granularity a token issued in the same second as a
                // sign-out either survives it for a full fifteen minutes or — if the
                // comparison rounds the other way — a sign-in immediately after a
                // sign-out is refused for just as long.
                new(AuthClaims.TokenIssuedAt,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture))];

            var signingCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_key),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                signingCredentials: signingCredentials,
                issuer: _options.Issuer,
                audience: _options.Audience,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes));

            return TokenHandler.WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        public string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }
    }
}