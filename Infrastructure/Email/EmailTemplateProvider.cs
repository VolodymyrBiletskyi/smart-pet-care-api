using System.Collections.Concurrent;
using System.Net;

namespace smart_pet_care_api.Infrastructure.Email
{
    /// <summary>
    /// Renders the templates in <c>Emails/</c>. They are files rather than
    /// strings in C# because email HTML is tables with every style inlined --
    /// unreadable inside a verbatim string, and something a designer should be
    /// able to edit without touching the service.
    /// </summary>
    public sealed class EmailTemplateProvider : IEmailTemplateProvider
    {
        private const string Directory = "Emails";
        private const string ConfirmationCodeTemplate = "email-confirmation";

        private readonly IWebHostEnvironment _environment;
        private readonly ConcurrentDictionary<string, string> _cache = new();

        public EmailTemplateProvider(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public EmailBody ConfirmationCode(string code, int expiresInMinutes)
        {
            var values = new Dictionary<string, string>
            {
                ["code"] = code,
                ["minutes"] = expiresInMinutes.ToString()
            };

            return new EmailBody(
                Render(ConfirmationCodeTemplate + ".txt", values, escape: false),
                Render(ConfirmationCodeTemplate + ".html", values, escape: true));
        }

        private string Render(string fileName, Dictionary<string, string> values, bool escape)
        {
            // Read once and keep it: the files never change between deploys, and
            // a confirmation email is on the critical path of signing up.
            var template = _cache.GetOrAdd(fileName, name => File.ReadAllText(
                Path.Combine(_environment.ContentRootPath, Directory, name)));

            foreach (var (key, value) in values)
                template = template.Replace(
                    "{{" + key + "}}",
                    escape ? WebUtility.HtmlEncode(value) : value);

            return template;
        }
    }
}
