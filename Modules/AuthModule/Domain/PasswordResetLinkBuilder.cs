using Microsoft.Extensions.Options;

namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    public sealed class PasswordResetLinkBuilder : IPasswordResetLinkBuilder
    {
        public const string Path = "/reset-password";

        private readonly PasswordResetOptions _options;
        private readonly IHttpContextAccessor _httpContext;

        public PasswordResetLinkBuilder(
            IOptions<PasswordResetOptions> options, IHttpContextAccessor httpContext)
        {
            _options = options.Value;
            _httpContext = httpContext;
        }

        public string EmailLink(string rawToken) =>
            $"{BaseUrl()}{Path}?token={Uri.EscapeDataString(rawToken)}";

        public string AppLink(string rawToken) =>
            _options.AppLink.Replace("{token}", Uri.EscapeDataString(rawToken));

        /// <summary>
        /// The request's own scheme and host are the last resort rather than a
        /// hardcoded production URL: a developer running this locally would otherwise
        /// get an email pointing at the live server, and the token in it would be one
        /// the live database has never seen.
        /// </summary>
        private string BaseUrl()
        {
            if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
                return _options.PublicBaseUrl.TrimEnd('/');

            var request = _httpContext.HttpContext?.Request;
            return request is null ? string.Empty : $"{request.Scheme}://{request.Host}";
        }
    }
}
