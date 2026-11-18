namespace smart_pet_care_api.Infrastructure.Email
{
    /// <summary>
    /// The throwaway-mailbox domains in <c>Emails/disposable-domains.txt</c>, read once
    /// and held as a set.
    /// </summary>
    /// <remarks>
    /// A file rather than a service call: the list changes on the order of weeks, and a
    /// sign-up that depends on a third party answering is a sign-up that breaks when
    /// they do not.
    /// </remarks>
    public sealed class DisposableEmailDomains : IDisposableEmailDomains
    {
        private const string FileName = "disposable-domains.txt";
        private const string Directory = "Emails";

        /// <summary>
        /// Forwarding relays, which must never be refused however the list is refreshed.
        /// They are deliverable addresses -- they do not bounce and cost no reputation,
        /// which is the whole case for this check -- and they are what a privacy-minded
        /// user signs up with. Today's list contains none of them; this is what keeps a
        /// future copy of it from quietly locking those users out.
        /// </summary>
        private static readonly string[] KnownRelays =
        [
            "duck.com",
            "simplelogin.com",
            "simplelogin.io",
            "slmail.me",
            "anonaddy.com",
            "anonaddy.me",
            "addy.io",
            "mozmail.com",
            "relay.firefox.com",
            "privaterelay.appleid.com",
            "33mail.com",
            "passinbox.com"
        ];

        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DisposableEmailDomains> _logger;
        private readonly Lazy<HashSet<string>> _domains;

        public DisposableEmailDomains(
            IWebHostEnvironment environment, ILogger<DisposableEmailDomains> logger)
        {
            _environment = environment;
            _logger = logger;
            _domains = new Lazy<HashSet<string>>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public int Count => _domains.Value.Count;

        public bool Contains(string domain)
        {
            var domains = _domains.Value;

            // Up the labels, so a provider that hands out addresses under subdomains --
            // mailinator does -- is caught by its own entry. A parent only matches when
            // the parent itself is listed, so a listed subdomain of a legitimate
            // dynamic-DNS provider stays a single entry.
            for (var candidate = domain; candidate.Contains('.');)
            {
                if (domains.Contains(candidate))
                    return true;

                candidate = candidate[(candidate.IndexOf('.') + 1)..];
            }

            return false;
        }

        private HashSet<string> Load()
        {
            var path = Path.Combine(_environment.ContentRootPath, Directory, FileName);

            try
            {
                var domains = new HashSet<string>(StringComparer.Ordinal);

                foreach (var line in File.ReadLines(path))
                {
                    var domain = line.Trim();
                    if (domain.Length == 0 || domain[0] == '#')
                        continue;

                    domains.Add(domain.ToLowerInvariant());
                }

                foreach (var relay in KnownRelays)
                    if (domains.Remove(relay))
                        _logger.LogWarning(
                            "Disposable domain list contains the forwarding relay {Domain}; ignoring it",
                            relay);

                _logger.LogInformation("Loaded {Count} disposable e-mail domains", domains.Count);
                return domains;
            }
            catch (Exception exception)
            {
                // Fails open, like the DNS check and for the same reason: a file missing
                // from the publish output is a packaging slip, and it must not be able to
                // stop people from signing up. Logged as an error because the only other
                // symptom is a check that silently passes everything.
                _logger.LogError(exception, "Could not read {Path}; disposable domains are not screened", path);
                return [];
            }
        }
    }
}
