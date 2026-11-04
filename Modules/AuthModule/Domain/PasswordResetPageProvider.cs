using System.Collections.Concurrent;

namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    public sealed class PasswordResetPageProvider : IPasswordResetPageProvider
    {
        private const string Directory = "Pages";

        private readonly IWebHostEnvironment _environment;
        private readonly ConcurrentDictionary<string, string> _cache = new();

        public PasswordResetPageProvider(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public string Open() => Read("password-reset-open.html");

        public string Expired() => Read("password-reset-expired.html");

        private string Read(string fileName) =>
            _cache.GetOrAdd(fileName, name => File.ReadAllText(
                Path.Combine(_environment.ContentRootPath, Directory, name)));
    }
}
