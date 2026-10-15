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
    }
}
