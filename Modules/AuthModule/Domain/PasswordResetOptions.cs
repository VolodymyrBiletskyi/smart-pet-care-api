namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    public class PasswordResetOptions
    {
        public const string DefaultAppLink = "miso://reset-password?token={token}";

        /// <summary>
        /// Absolute base of the link that goes in the email. Falls back to
        /// <c>ApiPublicUrl</c>, and then to the host the forgot-password request itself
        /// arrived on -- which is the right one in every deployment, since the user came
        /// from the same app that is about to mail them.
        /// </summary>
        public string PublicBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Where <c>/reset-password</c> hands the browser off to, with <c>{token}</c>
        /// substituted. A custom scheme rather than an https link because this is the
        /// side the app owns: a deployment that registers Android App Links or iOS
        /// Universal Links for the https URL never reaches the server at all, and this
        /// value stops mattering.
        /// </summary>
        public string AppLink { get; set; } = DefaultAppLink;

        /// <summary>
        /// How long a link stays usable. Short because it is a bearer credential sitting
        /// in a mailbox, and long enough that a user who reads mail on another device
        /// still gets there.
        /// </summary>
        public int LinkLifetimeMinutes { get; set; } = 30;
    }
}
