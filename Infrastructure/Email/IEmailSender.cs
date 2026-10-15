namespace smart_pet_care_api.Infrastructure.Email
{
    public interface IEmailSender
    {
        /// <summary>
        /// Sends one message. <paramref name="htmlBody"/> is optional: with it the
        /// message goes out as multipart/alternative and the text stays the
        /// fallback, without it as plain text exactly as before.
        /// </summary>
        Task SendAsync(string toAddress, string subject, string textBody, string? htmlBody = null);
    }
}
