using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace smart_pet_care_api.Modules.AuthModule.OAuth
{
    public class GoogleOAuth : IGoogleOAuth
    {
        private readonly GoogleOAuthOptions _options;
        private readonly HttpClient _httpClient;

        public GoogleOAuth(IOptions<GoogleOAuthOptions> options, HttpClient httpClient)
        {
            _options = options.Value;
            _httpClient = httpClient;
        }

        public string GetAuthorizationUrl()
        {
            var queryParams = new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["redirect_uri"] = _options.RedirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["access_type"] = "offline"
            };

            var query = string.Join("&", queryParams.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            return $"https://accounts.google.com/o/oauth2/v2/auth?{query}";
        }

        public async Task<GoogleUserInfo> ExchangeCodeAsync(string code)
        {
            var tokenResponse = await _httpClient.PostAsync("https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["redirect_uri"] = _options.RedirectUri,
                    ["grant_type"] = "authorization_code"
                }));

            var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            var idToken = tokenJson.GetProperty("id_token").GetString()!;

            return await ValidateIdTokenAsync(idToken);
        }

        public async Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken)
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken);

            return new GoogleUserInfo
            {
                Email = payload.Email,
                GoogleId = payload.Subject,
                Name = payload.Name,
                GivenName = payload.GivenName,
                FamilyName = payload.FamilyName,
                Picture = payload.Picture
            };
        }
    }
}