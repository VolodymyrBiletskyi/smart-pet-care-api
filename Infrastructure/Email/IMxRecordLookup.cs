namespace smart_pet_care_api.Infrastructure.Email
{
    /// <summary>What the DNS answered about a domain's mail exchangers.</summary>
    public enum MailExchangers
    {
        /// <summary>The domain publishes at least one MX record.</summary>
        Present,

        /// <summary>The domain does not exist, or exists and publishes no MX record.</summary>
        Absent,

        /// <summary>
        /// The question was not answered -- a timeout, a refusing resolver, a socket
        /// error. Callers let the address through on this: a user must not be unable to
        /// sign up because our DNS is having a bad minute.
        /// </summary>
        Unknown
    }

    public interface IMxRecordLookup
    {
        Task<MailExchangers> LookupAsync(string domain, CancellationToken cancellationToken = default);
    }
}
