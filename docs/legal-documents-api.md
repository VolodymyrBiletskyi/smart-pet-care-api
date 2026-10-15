# Legal documents API

The Terms and Conditions and the Privacy Policy. Public: no bearer token, no
cookie, `GET` and `HEAD` only. These are the URLs to put in the app, on the web
front end and in app store listings.

Each document is published in two renderings generated from one markdown source,
so the wording is always identical between them:

- an **HTML page**, for showing the document to a person;
- a **PDF**, for downloading, printing and submitting to an app store.

## Endpoints

| Route | Returns |
|---|---|
| `GET /terms.html` | the page, always |
| `GET /privacy.html` | the page, always |
| `GET /terms.pdf` | the PDF, always |
| `GET /privacy.pdf` | the PDF, always |
| `GET /terms` | negotiated on `Accept` (see below) |
| `GET /privacy` | negotiated on `Accept` |

**Use the suffixed routes from the app.** `/terms` and `/privacy` decide what to
return from the request's `Accept` header, and that header is set by the HTTP
library, not by your code: an OkHttp upgrade, an added interceptor or a proxy
rewriting it changes the response with no change on your side. Keep `/terms` and
`/privacy` for links you hand to a browser — a store listing, an email, a
`<a href>` — where they are the nicer URL and the browser always asks for HTML.

## Content negotiation on the unsuffixed routes

Measured against the running service:

| Request `Accept` | Response |
|---|---|
| `text/html,application/xhtml+xml,...` (browser, WebView) | HTML page |
| absent (OkHttp/Retrofit send no `Accept`) | PDF |
| `*/*` (URLSession, Ktor, RN `fetch`) | PDF |
| `application/json, text/plain, */*` (axios, Dio) | PDF |
| `application/pdf` | PDF |

HTML is returned only when the client explicitly accepts `text/html`; anything
else gets the PDF. `text/html` anywhere in the list wins over `*/*`
(`*/*, text/html;q=0.9` returns the page), `q=0` is honoured
(`text/html;q=0, */*` returns the PDF), and an `application/pdf` listed ahead of
`text/html` wins. `Vary: Accept` is sent, so shared caches keep the two bodies
apart.

## Caching

Both renderings answer with:

```http
Cache-Control: public, max-age=3600
ETag: "<32 hex chars>"
Last-Modified: <file timestamp>
Vary: Accept
```

The `ETag` is a hash of the file's bytes, so it differs per rendering and only
changes when a deploy actually replaces the document. Send it back as
`If-None-Match` and the service answers `304 Not Modified`. Nothing needs to be
re-fetched between releases.

## Linking to a section

Every numbered section in the HTML carries an id derived from its number, so the
app can open the document at a clause instead of at the top:

```
/privacy.html#s9     retention periods
/privacy.html#s11    the user's GDPR rights
/privacy.html#s7     who the data is shared with
/terms.html#s3       "not veterinary advice"
/terms.html#s10      deleting an account
```

The anchor follows the section number, not the heading text, so `#s9` keeps
pointing at retention after the heading is reworded. `#s3-7` style ids address
subsections (`3.7`).

## What the HTML page is

Relevant if you embed it in a WebView or an iframe:

- **One self-contained file.** CSS inlined, no script, no external request of any
  kind — no web font, no stylesheet, no image. It renders with JavaScript
  disabled, which is the Android WebView default, and it renders offline.
- **Light palette, fixed.** No `prefers-color-scheme` branch: the page stays
  light on a device in dark mode. `color-scheme: light` and a `theme-color` meta
  keep the surrounding chrome light too. If the app has its own dark theme,
  inject CSS through the WebView rather than expecting the document to follow.
- **Sized for a phone.** `width=device-width, viewport-fit=cover`, safe-area
  insets so nothing sits under a notch or a home indicator, 17px type in a 38rem
  column (about 40 characters a line), 16.5px below 380px wide.
- **A masthead** with the product name, the document title, the version and
  effective date, and a **Download PDF** button, plus a collapsed `<details>`
  index of the sections.
- Long URLs wrap; the page never scrolls sideways.

The in-page links are either fragments (`#s4`) or root-relative (`/terms.pdf`),
so they inherit the scheme and host the page was served from. Served over HTTPS,
the download link is HTTPS. There is no mixed-content risk because there is no
external resource at all.

## Offering the PDF

The page's own button is:

```html
<a href="/terms.pdf" download="terms-and-conditions.pdf">Download PDF</a>
```

Add your own the same way if you build the screen natively. The `.pdf` route
sends `Content-Disposition: inline`, so the bare URL renders in a viewer while
the `download` attribute saves a file — the link chooses, not the server.

On Android a WebView will not render a PDF inline: pointing a WebView at
`/terms.pdf` fires the download listener and leaves the screen blank. Show
`/terms.html` and send the PDF to the system viewer or a share intent. iOS
`WKWebView` renders PDFs natively.

## Environments

```
production   https://smart-pet-care.duckdns.org/terms.html
development   http://localhost:8080/terms.html
```

Take the base URL from configuration rather than hardcoding it.

- **Production is HTTPS.** nginx terminates TLS and port 80 answers `301` to
  HTTPS. A front end served over HTTPS must call HTTPS or the browser blocks the
  request as mixed content.
- **There is no HTTPS on localhost.** TLS lives only in nginx, which is not part
  of a local run; `https://localhost:8080` fails to connect. Use `http` in
  development — nothing redirects it.
- **A device cannot reach `localhost`.** The compose file publishes the API on
  `127.0.0.1:8080`, so use `http://10.0.2.2:8080` from an Android emulator. A
  physical device on the same Wi-Fi cannot reach it at all while the port is
  bound to loopback. Android API 28+ also blocks cleartext by default, so an
  `http://` base URL needs an exception in the network security config.

## Recording acceptance

Acceptance is recorded on the account, not by visiting these routes.

- `POST /api/auth/register` requires `"termsAccepted": true`. Sending `false` or
  omitting it fails validation with `auth_terms_not_accepted` inside the usual
  `request_validation_failed` 400 response, so the checkbox has to be ticked
  before the request is made.
- Google sign-up sets acceptance implicitly. **Show the links next to the Google
  button too** — that flow has no checkbox, so the links are the only place the
  user is given the documents before the account exists.
- `termsAccepted` is part of `UserResponseDto`, which today is only returned by
  `PATCH /api/users`. There is no `GET` endpoint for the current user, so a
  client cannot read the flag back without editing the profile.

There is currently **no endpoint for re-accepting** a new version of the Terms.
The documents carry a version and an effective date on their first line
(`Version 1.0. Effective 15 October 2026.`), and if re-acceptance is ever needed
after a material change, that is a backend change rather than something the
client can drive today.

## Changing an existing integration

If the front end already points at `/terms` or `/privacy`:

| Current usage | What happens now | Action |
|---|---|---|
| `<a href>` or `WebView.loadUrl` | the HTML page | none needed; switching to `/terms.html` makes it deterministic |
| a "download PDF" link or button | **the HTML page, not a file** | point it at `/terms.pdf` |
| `fetch` / `axios` / Retrofit | PDF bytes | point it at `/terms.html` |

The second row is the only breaking change: before these routes gained an HTML
rendering, `/terms` always returned the PDF.

## Editing the documents

The text lives in `docs/legal/terms-and-conditions.md` and
`docs/legal/privacy-policy.md`. Both renderings are committed artefacts under
`Legal/` and are regenerated by hand:

```bash
python scripts/generate-legal-pdfs.py           # rewrite Legal/ from docs/legal/
python scripts/generate-legal-pdfs.py --check   # fail if they no longer match
```

A wording change is therefore a backend commit and a deploy, not a content
update the client can pick up on its own.
