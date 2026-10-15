namespace smart_pet_care_api.Modules.LegalModule.Domain
{
    /// <summary>
    /// Which rendering of a legal document to serve. Both are generated from the
    /// same markdown, so they are the same text in two shapes: the PDF is the
    /// one to download, print or hand to an app store, the page is the one that
    /// reflows on a phone.
    /// </summary>
    public enum LegalFormat
    {
        Pdf,
        Html
    }

    /// <summary>One rendering of one published legal document, read into memory.</summary>
    public sealed record LegalDocument(
        string Slug,
        LegalFormat Format,
        string FileName,
        string MediaType,
        byte[] Content,
        string ETag,
        DateTimeOffset LastModified);

    public interface ILegalDocumentProvider
    {
        /// <summary>
        /// The document behind a route slug ("terms", "privacy"), in the given
        /// format. Throws <see cref="FileNotFoundException"/> if the file is not
        /// in the deployed output, which is a packaging fault rather than a
        /// request the caller got wrong.
        /// </summary>
        LegalDocument Get(string slug, LegalFormat format);
    }
}
