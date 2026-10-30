namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    /// <summary>
    /// The two pages <c>/reset-password</c> can answer with. Files in <c>Pages/</c>
    /// rather than strings in C#, for the same reason the email templates are: markup
    /// with its CSS inlined is unreadable inside a verbatim string.
    /// </summary>
    public interface IPasswordResetPageProvider
    {
        /// <summary>The page that hands the browser off to the app.</summary>
        string Open(string appLink);

        /// <summary>Shown for a link that is spent, expired or was never ours.</summary>
        string Expired();
    }
}
