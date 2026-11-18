using smart_pet_care_api.Common.Api;

namespace smart_pet_care_api.Infrastructure.Email
{
    /// <summary>
    /// The two checks, in the order of what they cost: the list is a set lookup, the MX
    /// query is a network round trip.
    /// </summary>
    public sealed class EmailDomainScreen : IEmailDomainScreen
    {
        private readonly IDisposableEmailDomains _disposable;
        private readonly IMxRecordLookup _mx;

        public EmailDomainScreen(IDisposableEmailDomains disposable, IMxRecordLookup mx)
        {
            _disposable = disposable;
            _mx = mx;
        }

        public async Task ScreenAsync(string email, CancellationToken cancellationToken = default)
        {
            var domain = DomainOf(email);

            if (domain is null)
                throw new ValidationException(
                    ErrorCodes.Auth.EmailDomainInvalid, "That e-mail address has no valid domain");

            if (_disposable.Contains(domain))
                throw new ValidationException(
                    ErrorCodes.Auth.EmailDomainDisposable,
                    "Disposable e-mail addresses cannot be used to sign up");

            // Unknown goes through. The alternative is an outage of our resolver being an
            // outage of registration.
            if (await _mx.LookupAsync(domain, cancellationToken) is MailExchangers.Absent)
                throw new ValidationException(
                    ErrorCodes.Auth.EmailDomainInvalid, "That e-mail domain does not accept mail");
        }

        /// <summary>
        /// The domain part, lower-cased and without a trailing root dot, or null for
        /// something that is not an address. DataAnnotations has already had a say on the
        /// shape by the time this runs; this is what keeps the lookup from being handed
        /// whatever got past it.
        /// </summary>
        private static string? DomainOf(string email)
        {
            var at = email.LastIndexOf('@');
            if (at <= 0 || at == email.Length - 1)
                return null;

            var domain = email[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant();

            // A dot is required: a single-label domain is either a local hostname or a
            // typo, and neither has a mailbox worth a letter.
            return domain.Contains('.') && !domain.Any(char.IsWhiteSpace) ? domain : null;
        }
    }
}
