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
        private readonly IPasswordResetPageProvider _pages;

        public PasswordResetLinkController(
            IAuthService authService,
            IPasswordResetPageProvider pages)
        {
            _authService = authService;
            _pages = pages;
        }

        /// <summary>
        /// Tells the reader whether their link is still live, and nothing more.
        /// </summary>
        /// <remarks>
        /// This method is the fallback, not the path: with the domain verified through
        /// <c>/.well-known/assetlinks.json</c>, Android hands the app the https link
        /// directly and never comes here. Whoever does arrive — a desktop browser, a
        /// phone without the app, a mail scanner prefetching — is on a device that
        /// cannot finish the reset anyway.
        ///
        /// So the page carries **no token and no hand-off**. It used to open
        /// <c>miso://reset-password?token=…</c>, and any app on the phone could claim
        /// that scheme and take the token: a custom scheme has no owner, which is the
        /// whole difference between it and a verified App Link. The link in the email is
        /// public — anyone can have one mailed to any address — so that was a way into
        /// any account whose owner could be made to tap once.
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

            var html = usable ? _pages.Open() : _pages.Expired();

            // 200 for the expired page too. The status describes the request for a page,
            // and the page is exactly what a person who clicked a stale link needs to
            // read; a 410 here would have some WebViews show their own error instead.
            return Content(html, "text/html; charset=utf-8");
        }
    }
}
