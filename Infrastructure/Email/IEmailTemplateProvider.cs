namespace smart_pet_care_api.Infrastructure.Email
{
    /// <summary>
    /// Both renderings of one email. Sent as multipart/alternative: the client
    /// shows the HTML, and the text part is what a plain-text client falls back
    /// to -- and what keeps the message from looking like an HTML-only blast to
    /// a spam filter.
    /// </summary>
    public sealed record EmailBody(string Text, string Html);

    public interface IEmailTemplateProvider
    {
        EmailBody ConfirmationCode(string code, int expiresInMinutes);

        EmailBody PasswordReset(string link, int expiresInMinutes);

        /// <summary>
        /// Sent after a reset succeeds. It carries no link and no code: it is the one
        /// email whose whole job is to be read by a person who may not have asked for
        /// anything, and a credential in it would be worth intercepting.
        /// </summary>
        EmailBody PasswordChanged();
    }
}
