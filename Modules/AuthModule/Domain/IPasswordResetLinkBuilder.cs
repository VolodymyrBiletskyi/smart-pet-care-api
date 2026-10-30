namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    public interface IPasswordResetLinkBuilder
    {
        /// <summary>
        /// The https link the email carries. Not the app's own scheme: a mail client
        /// renders <c>miso://</c> as plain text at best and strips it at worst, and the
        /// same message has to be readable on a desktop with no app installed.
        /// </summary>
        string EmailLink(string rawToken);

        /// <summary>The app link that <c>/reset-password</c> opens.</summary>
        string AppLink(string rawToken);
    }
}
