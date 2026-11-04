namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    /// <summary>
    /// The two pages <c>/reset-password</c> can answer with. Files in <c>Pages/</c>
    /// rather than strings in C#, for the same reason the email templates are: markup
    /// with its CSS inlined is unreadable inside a verbatim string.
    /// </summary>
    public interface IPasswordResetPageProvider
    {
        /// <summary>
        /// Shown for a live link. It carries no token and no way into the app: with the
        /// domain verified, Android opens the link in the app and never reaches this
        /// page, so whoever lands here is on a device that cannot continue anyway.
        /// </summary>
        string Open();

        /// <summary>Shown for a link that is spent, expired or was never ours.</summary>
        string Expired();
    }
}
