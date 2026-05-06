namespace smart_pet_care_api.Modules.AuthModule.OAuth
{
    public interface IGoogleOAuth
    {
        string GetAuthorizationUrl();
        Task<GoogleUserInfo> ExchangeCodeAsync(string code);
        Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken);
    }
}