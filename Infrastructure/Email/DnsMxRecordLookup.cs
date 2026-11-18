using DnsClient;
using DnsClient.Protocol;
using Microsoft.Extensions.Caching.Memory;

namespace smart_pet_care_api.Infrastructure.Email
{
    /// <summary>
    /// The MX question, asked over DNS and memoised per domain.
    /// </summary>
    /// <remarks>
    /// The lookup sits on the critical path of signing up, in front of a request that
    /// also hashes a password and talks to an SMTP server, so two things are not
    /// optional: a short timeout with no retries, and a cache. Without the cache every
    /// sign-up on <c>gmail.com</c> -- which is most of them -- is a network round trip
    /// before the account can be created.
    /// </remarks>
    public sealed class DnsMxRecordLookup : IMxRecordLookup
    {
        /// <summary>
        /// Long enough for a resolver that is working and short enough that a resolver
        /// that is not does not hold the sign-up open. A miss fails open, so the cost of
        /// being wrong here is a letter sent to a domain we could not check.
        /// </summary>
        private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(2);

        private static readonly TimeSpan CachePresent = TimeSpan.FromHours(6);

        /// <summary>
        /// Shorter than <see cref="CachePresent"/>: a domain whose records have just
        /// been set up should not be refused for the rest of the afternoon, and the
        /// reverse mistake only costs a letter.
        /// </summary>
        private static readonly TimeSpan CacheAbsent = TimeSpan.FromMinutes(15);

        private readonly ILookupClient _dns;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DnsMxRecordLookup> _logger;

        public DnsMxRecordLookup(
            ILookupClient dns, IMemoryCache cache, ILogger<DnsMxRecordLookup> logger)
        {
            _dns = dns;
            _cache = cache;
            _logger = logger;
        }

        public async Task<MailExchangers> LookupAsync(
            string domain, CancellationToken cancellationToken = default)
        {
            var key = $"mx:{domain}";
            if (_cache.TryGetValue(key, out MailExchangers cached))
                return cached;

            var answer = await QueryAsync(domain, cancellationToken);

            // Unknown is deliberately not cached: it says nothing about the domain, only
            // that this one query failed, and holding it would turn a blip into a window
            // in which the check does not run at all.
            if (answer is not MailExchangers.Unknown)
                _cache.Set(key, answer,
                    answer is MailExchangers.Present ? CachePresent : CacheAbsent);

            return answer;
        }

        private async Task<MailExchangers> QueryAsync(
            string domain, CancellationToken cancellationToken)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(QueryTimeout);

                var response = await _dns.QueryAsync(
                    domain, QueryType.MX, cancellationToken: timeout.Token);

                if (response.Header.ResponseCode is DnsHeaderResponseCode.NotExistentDomain)
                    return MailExchangers.Absent;

                // Any other error code is the resolver failing to answer rather than the
                // domain answering "no" -- SERVFAIL from a lame delegation, REFUSED from a
                // resolver that does not serve us.
                if (response.HasError)
                {
                    _logger.LogDebug(
                        "MX lookup for {Domain} answered {Code}", domain, response.Header.ResponseCode);
                    return MailExchangers.Unknown;
                }

                // No MX and no error is the domain stating it takes no mail. A host with an
                // A record but no MX is still technically a mail destination by RFC 5321's
                // implicit-MX rule, and that is the one class of address this refuses
                // wrongly -- it is also what a parked typo domain looks like, which is the
                // case worth catching.
                return response.Answers.OfType<MxRecord>().Any()
                    ? MailExchangers.Present
                    : MailExchangers.Absent;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Every failure here is ours, not the caller's: a timeout, no route to the
                // resolver, a malformed response. Logged at warning because a resolver that
                // is down turns the whole check off silently.
                _logger.LogWarning(exception, "MX lookup for {Domain} failed", domain);
                return MailExchangers.Unknown;
            }
        }
    }
}
