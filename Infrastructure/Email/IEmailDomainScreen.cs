namespace smart_pet_care_api.Infrastructure.Email
{
    /// <summary>
    /// Screens the domain of an address before anything is mailed to it: a typo and a
    /// made-up domain both bounce, and a sending account whose bounce rate goes over
    /// the provider's threshold is throttled or suspended.
    /// </summary>
    /// <remarks>
    /// Only the domain. Whether the mailbox exists is what the confirmation code
    /// proves, and the one protocol-level way to ask -- an SMTP <c>RCPT TO</c> probe --
    /// is answered honestly by neither Gmail nor any catch-all domain, while putting
    /// this server's address on blocklists for asking.
    /// </remarks>
    public interface IEmailDomainScreen
    {
        /// <summary>
        /// Throws <see cref="Common.Api.ValidationException"/> with
        /// <c>auth_email_domain_invalid</c> or <c>auth_email_domain_disposable</c>, and
        /// returns for an address that may be mailed.
        /// </summary>
        Task ScreenAsync(string email, CancellationToken cancellationToken = default);
    }
}
