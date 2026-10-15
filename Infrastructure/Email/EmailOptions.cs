namespace smart_pet_care_api.Infrastructure.Email
{
    public class EmailOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        // The name the inbox shows next to the message. Overridden by
        // Email__FromName / EMAIL_FROM_NAME in a deployed environment.
        public string FromName { get; set; } = "Miso";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
    }
}
