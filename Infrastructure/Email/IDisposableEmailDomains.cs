namespace smart_pet_care_api.Infrastructure.Email
{
    public interface IDisposableEmailDomains
    {
        /// <summary>
        /// Whether this domain, or a domain it is a subdomain of, is a throwaway mailbox
        /// provider.
        /// </summary>
        bool Contains(string domain);

        /// <summary>How many domains were loaded. 0 means the list never arrived.</summary>
        int Count { get; }
    }
}
