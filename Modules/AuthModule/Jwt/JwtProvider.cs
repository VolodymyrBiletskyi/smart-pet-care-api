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

        public string GenerateAccessToken(User user)
        {
            Claim[] claims = [
                new("userId", user.Id.ToString()),
                new("email", user.Email ?? string.Empty)];

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