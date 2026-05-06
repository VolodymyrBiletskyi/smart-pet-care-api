namespace smart_pet_care_api.Modules.AuthModule.OAuth
{
    public class GoogleOAuthOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
    }
}