namespace smart_pet_care_api.Modules.AuthModule.OAuth
{
    public class GoogleUserInfo
    {
        public string Email { get; set; } = string.Empty;
        public string GoogleId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
        public string? Picture { get; set; }
    }
}