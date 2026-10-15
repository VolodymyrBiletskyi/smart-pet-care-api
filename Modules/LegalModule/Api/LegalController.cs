using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using smart_pet_care_api.Modules.LegalModule.Domain;

namespace smart_pet_care_api.Modules.LegalModule.Api
{
    /// <summary>
    /// The public home of the Terms and the Privacy Policy. Both routes sit at
    /// the root rather than under <c>/api</c>: they are URLs printed in app
    /// stores, linked from the sign-up screen and read by people, not endpoints
    /// a client consumes.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Tags("Legal")]
    public sealed class LegalController : ControllerBase
    {
        private readonly ILegalDocumentProvider _documents;

        public LegalController(ILegalDocumentProvider documents)
        {
            _documents = documents;
        }

        [HttpGet("/terms")]
        [HttpHead("/terms")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        public IActionResult Terms() => Document(LegalDocumentProvider.Terms, Negotiate());

        [HttpGet("/privacy")]
        [HttpHead("/privacy")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        public IActionResult Privacy() => Document(LegalDocumentProvider.Privacy, Negotiate());

        [HttpGet("/terms.pdf")]
        [HttpHead("/terms.pdf")]
        [Produces("application/pdf")]
        public IActionResult TermsPdf() => Document(LegalDocumentProvider.Terms, LegalFormat.Pdf);

        [HttpGet("/privacy.pdf")]
        [HttpHead("/privacy.pdf")]
        [Produces("application/pdf")]
        public IActionResult PrivacyPdf() => Document(LegalDocumentProvider.Privacy, LegalFormat.Pdf);

        [HttpGet("/terms.html")]
        [HttpHead("/terms.html")]
        [Produces("text/html")]
        public IActionResult TermsHtml() => Document(LegalDocumentProvider.Terms, LegalFormat.Html);

        [HttpGet("/privacy.html")]
        [HttpHead("/privacy.html")]
        [Produces("text/html")]
        public IActionResult PrivacyHtml() => Document(LegalDocumentProvider.Privacy, LegalFormat.Html);

        /// <summary>
        /// Which rendering an unsuffixed route should answer with.
        /// </summary>
        /// <remarks>
        /// Browsers and both platforms' WebViews ask for <c>text/html</c>, and
        /// they are the clients that cannot read a PDF well: the page reflows to
        /// the screen where a 595pt page would land at about a third of readable
        /// size on a phone, and Android's WebView will not render a PDF inline at
        /// all. Everything that does not ask for HTML -- curl, a store
        /// submission fetching the file, a client written against the original
        /// contract -- keeps getting the PDF these routes have always served.
        /// </remarks>
        private LegalFormat Negotiate()
        {
            foreach (var accepted in Request.GetTypedHeaders().Accept)
            {
                if (accepted.Quality == 0)
                    continue;

                if (accepted.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                    return LegalFormat.Html;

                // An explicit application/pdf outranks any text/html later in the
                // list; */* is not a preference and falls through to the default.
                if (accepted.MediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                    return LegalFormat.Pdf;
            }

            return LegalFormat.Pdf;
        }

        private IActionResult Document(string slug, LegalFormat format)
        {
            var document = _documents.Get(slug, format);

            if (format == LegalFormat.Pdf)
                // inline, not attachment: these routes are where the documents
                // live, so a viewer following the link should render the text
                // instead of dropping a file in the downloads folder.
                Response.Headers.ContentDisposition =
                    $"inline; filename=\"{document.FileName}\"";

            Response.Headers.CacheControl = "public, max-age=3600";

            // Without this a shared cache would hand the PDF to a browser, or
            // the page to a client that asked for the file: one URL, two bodies.
            Response.Headers.Vary = HeaderNames.Accept;

            // The ETag is over the file's bytes, so it differs per rendering and
            // a conditional request keeps answering 304 until a deploy actually
            // replaces the document.
            return File(
                document.Content,
                document.MediaType,
                document.LastModified,
                new EntityTagHeaderValue(document.ETag));
        }
    }
}
