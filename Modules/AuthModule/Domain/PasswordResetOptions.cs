namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    public class PasswordResetOptions
    {
        /// <summary>
        /// Absolute base of the link that goes in the email. Falls back to
        /// <c>ApiPublicUrl</c>, and then to the host the forgot-password request itself
        /// arrived on -- which is the right one in every deployment, since the user came
        /// from the same app that is about to mail them.
        /// </summary>
        public string PublicBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// How long a link stays usable. Short because it is a bearer credential sitting
        /// in a mailbox, and long enough that a user who reads mail on another device
        /// still gets there.
        /// </summary>
        public int LinkLifetimeMinutes { get; set; } = 30;
    }
}
