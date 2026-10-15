using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace smart_pet_care_api.Modules.LegalModule.Domain
{
    /// <summary>
    /// Serves the files in <c>Legal/</c>, which are committed artefacts generated
    /// from <c>docs/legal/*.md</c> by <c>scripts/generate-legal-pdfs.py</c>. The
    /// documents are a published legal text, so rendering one per request would
    /// mean the bytes a user was shown depend on when they asked.
    /// </summary>
    public sealed class LegalDocumentProvider : ILegalDocumentProvider
    {
        public const string Terms = "terms";
        public const string Privacy = "privacy";

        private const string Directory = "Legal";

        private static readonly Dictionary<string, string> Stems = new()
        {
            [Terms] = "terms-and-conditions",
            [Privacy] = "privacy-policy"
        };

        private static readonly Dictionary<LegalFormat, (string Extension, string MediaType)> Formats = new()
        {
            [LegalFormat.Pdf] = ("pdf", "application/pdf"),
            [LegalFormat.Html] = ("html", "text/html; charset=utf-8")
        };

        private readonly IWebHostEnvironment _environment;
        private readonly ConcurrentDictionary<(string, LegalFormat), LegalDocument> _cache = new();

        public LegalDocumentProvider(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public LegalDocument Get(string slug, LegalFormat format)
        {
            if (!Stems.TryGetValue(slug, out var stem))
                throw new KeyNotFoundException($"No legal document is published as '{slug}'.");

            // Read once and keep the bytes: the files are small, immutable
            // between deploys, and these are unauthenticated routes a crawler can
            // hit as often as it likes. Loading them at startup instead would let
            // a packaging slip in a static document take the whole API down.
            return _cache.GetOrAdd((slug, format), key =>
            {
                var (extension, mediaType) = Formats[key.Item2];
                var fileName = $"{stem}.{extension}";
                var path = Path.Combine(_environment.ContentRootPath, Directory, fileName);
                var content = File.ReadAllBytes(path);

                return new LegalDocument(
                    slug,
                    key.Item2,
                    fileName,
                    mediaType,
                    content,
                    ETag: $"\"{Convert.ToHexString(SHA256.HashData(content))[..32].ToLowerInvariant()}\"",
                    LastModified: new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
            });
        }
    }
}
