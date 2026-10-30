using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using smart_pet_care_api.Modules.AuthModule.Domain;

namespace smart_pet_care_api.Modules.AuthModule.Api
{
    /// <summary>
    /// Where the link in the reset email lands. At the root rather than under
    /// <c>/api</c>, and anonymous, for the same reason <c>/terms</c> is: it is a URL a
    /// person opens from their mail client, not an endpoint a client consumes.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Tags("Auth")]
    public sealed class PasswordResetLinkController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IPasswordResetLinkBuilder _links;
        private readonly IPasswordResetPageProvider _pages;

        public PasswordResetLinkController(
            IAuthService authService,
            IPasswordResetLinkBuilder links,
            IPasswordResetPageProvider pages)
        {
            _authService = authService;
            _links = links;
            _pages = pages;
        }

        /// <summary>
        /// Opens the app at the reset screen, carrying the token across.
        /// </summary>
        /// <remarks>
        /// A page rather than a 302 to the app's own scheme: a mail scanner prefetching
        /// the link, a desktop browser and a phone with no app installed all arrive
        /// here, and a redirect to a scheme nothing can open is a browser error rather
        /// than a sentence the user can act on. A deployment that registers App Links or
        /// Universal Links for this URL never reaches this method at all — the OS hands
        /// the app the https link directly.
        ///
        /// It is a read, which is what makes the prefetch harmless: the token is spent
        /// by <c>POST /api/auth/reset-password</c> and by nothing else.
        /// </remarks>
        [HttpGet(PasswordResetLinkBuilder.Path)]
        [Produces("text/html")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Open([FromQuery] string? token)
        {
            var usable = token is not null
                && await _authService.IsPasswordResetTokenUsableAsync(token);

            // The URL carries a credential: no cache may keep it, no crawler may index
            // it, and no outbound request may leak it in a Referer header.
            Response.Headers.CacheControl = "no-store";
            Response.Headers[HeaderNames.Pragma] = "no-cache";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

            var html = usable ? _pages.Open(_links.AppLink(token!)) : _pages.Expired();

            // 200 for the expired page too. The status describes the request for a page,
            // and the page is exactly what a person who clicked a stale link needs to
            // read; a 410 here would have some WebViews show their own error instead.
            return Content(html, "text/html; charset=utf-8");
        }
    }
}
