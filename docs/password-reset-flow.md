# Password reset v1

A user who has forgotten their password gives their address, gets a link by
email, and the link opens the app at a "choose a new password" screen. The app
then posts the new password together with the token the link carried.

Three pieces are involved, and only two of them are endpoints a client calls:

| Step | Route | Caller |
|---|---|---|
| 1. Ask for a link | `POST /api/auth/forgot-password` | the app |
| 2. Open the link | `GET /reset-password?token=…` | the user's mail client / the OS |
| 3. Set the password | `POST /api/auth/reset-password` | the app |

## 1. Ask for a link

```http
POST /api/auth/forgot-password
Content-Type: application/json

{ "email": "owner@example.com" }
```

```http
202 Accepted

{ "message": "If that address has an account, a reset link is on its way" }
```

**This endpoint answers the same way for an address that has an account and one
that does not**, and the same way whether a mail actually went out or was
suppressed by the per-account cooldown. There is deliberately no success/failure
distinction to branch on: the difference between the two answers is a lookup
telling anyone with a list of addresses which of them are registered. Show
"check your inbox" on every `202`.

The *body* is identical; the **response time is not**, and that is a known gap
rather than a solved problem — see [Known limitations](#known-limitations)
below. Do not describe this endpoint to users, or in store copy, as proof that
the service will not reveal whether an address is registered.

The only errors are `request_validation_failed` (`auth_email_required`,
`auth_email_invalid`) and `auth_too_many_requests` with `Retry-After`. The
address is trimmed and lower-cased, so the casing the user typed does not
matter.

A second request inside **60 seconds** is accepted and sends nothing. Past that
it sends a new link, and **the previous link stops working** — a user who goes
back to the older mail gets `auth_reset_token_invalid`, which is the point: at
most one live link per account.

## 2. Opening the link

The email carries an **https** link to this API — `https://<api>/reset-password?token=…`
— not a `miso://` one. A mail client renders a custom scheme as plain text at
best and strips it at worst, and the same message has to be readable on a
desktop with no app installed.

What that URL does:

- **On Android**, the OS hands the app the https URL directly and the server is
  never reached. This is the path; everything below is what happens when it does
  not apply. It works because
  `https://smart-pet-care.duckdns.org/.well-known/assetlinks.json` states that
  `com.misopets.app` owns this domain, and the app declares `autoVerify` for
  `/reset-password*`. Android checks that itself — which is exactly the guarantee
  a custom scheme cannot give.
- **Everywhere else** — a desktop browser, a phone without the app, a mail
  scanner prefetching — the API serves a page that says the link is live and to
  open it on a phone with Miso installed. It carries **no token and no way into
  the app**, because a device that reaches it cannot finish the reset anyway.

The app receives the **raw token** from the link itself. It does not need to call
the API to find out whether the token is good — just show the form and let step 3
answer.

> **The page used to hand off to `miso://reset-password?token=…`, and that was a
> vulnerability.** A custom scheme has no owner: any app on the phone could
> register it, receive the token and change the password on the account. It
> applied to every account whether or not its owner had Miso installed, since
> `forgot-password` is public and an attacker can have the email sent to the
> victim themselves. The hand-off, the button and the `PasswordReset:AppLink`
> setting behind them are gone; App Links replace them.

The page itself is a `200` in both cases, including for a dead link: the status
describes the request for a page, and some WebViews replace the body of an error
status with their own. A spent or expired link renders a "this link no longer
works" page instead of opening the app.

The route is a **read**. Nothing is consumed by opening it, which is what makes
a mail scanner's prefetch harmless. The token is spent by step 3 and by nothing
else.

## 3. Set the password

```http
POST /api/auth/reset-password
Content-Type: application/json

{
  "token": "FJ8c…",
  "password": "New-pass1!",
  "passwordConfirm": "New-pass1!"
}
```

```http
204 No Content
```

The password rules are the register form's, unchanged:
`auth_password_too_short` (under 6), `auth_password_too_weak` (needs a letter, a
digit and a symbol), `auth_passwords_do_not_match`.

| Code | HTTP | Meaning for the screen |
|---|---|---|
| `auth_reset_token_invalid` | 400 | Unknown, already used, or superseded by a newer link. Send the user back to **Forgot password**. |
| `auth_reset_token_expired` | 410 | The link aged out. Same screen, friendlier message. |
| `auth_reset_token_required` | — | Inside `errors.token`: the client posted no token. |

**No session comes back.** The link is a bearer credential that sat in a mailbox,
so it buys one password change and not a signed-in client — call
`POST /api/auth/login` with the new password afterwards.

Five things happen alongside the password:

- **Every refresh token for the account is revoked.** A reset is what a user does
  when they think somebody else knows their password, so other devices are
  signed out and have to log in again. Expect this in the UI: a second device
  will get `auth_refresh_token_invalid` on its next refresh and must clear its
  session rather than retry.
- **Every access token issued before the reset stops being accepted.** Revoking
  refresh tokens only stops a session continuing; the access token already in a
  client's hand is signed, unexpired and looked up nowhere, so it went on reading
  and writing the account for up to another fifteen minutes. A request with one now
  gets `401 auth_session_revoked` — the same handling as
  `auth_account_no_longer_exists`: clear the session, go to login, do not refresh.
- **Every push registration for the account is deleted.** Sessions do not decide
  who receives a notification — the account a device token belongs to does — so a
  device that registered while somebody else held the password would keep getting
  this account's reminders otherwise. **The client must re-register its device
  token after signing in again** (`POST /api/notifications/device-token`); if it
  only registers on first install, the user silently stops getting notifications
  after a reset.
- **An unconfirmed address becomes confirmed.** Following the link proved control
  of the mailbox, which is exactly what the six-digit code proves. Without this
  a user who never confirmed could reset their password and still be unable to
  log in.
- **A notice is mailed to the address**, saying the password changed and every
  device was signed out. It carries no link and no code: it goes to a mailbox
  whose owner may not have asked for anything. Best effort and sent after the
  password is already stored, so a dead SMTP server cannot turn a completed reset
  into a failure the client reports.

A Google-only account — one that has never had a password — can use this flow,
and it gains one. It is not a new way in: anyone holding the mailbox can already
reset through Google itself, and blocking it would leave a user whose Google
account is gone with nothing.

## Token properties

| | |
|---|---|
| Size | 256 bits of CSPRNG output, base64url — URL-safe, since it travels as a query parameter through a mail client |
| Lifetime | 30 minutes (`PasswordReset:LinkLifetimeMinutes`) |
| Uses | One |
| Stored as | SHA256; the raw value exists only in the email |
| Superseded by | The next link for the same account |

## Configuration

| Setting | Env var | Default |
|---|---|---|
| `PasswordReset:LinkLifetimeMinutes` | — | `30` |
| `PasswordReset:PublicBaseUrl` | — | falls back to `ApiPublicUrl`, then to the host the request arrived on |

`PublicBaseUrl` is where the link in the email points. It follows `ApiPublicUrl`
rather than being a second variable to keep in step, and falls back to the
request's own scheme and host so that a developer running the API locally is
mailed a link to localhost — not one into production, where the token does not
exist.

Both endpoints are rate limited per client address, in the app and again at
nginx: `forgot-password` with the email policy (5 per 10 minutes), since it
spends someone else's inbox, and `reset-password` with the credentials policy
(10 per minute), since it burns an enhanced BCrypt hash.

## Known limitations

Three things found in review. Two are fixed and kept here struck through, because
what was wrong and why it was wrong is worth more than a shorter section; the one
that is still open is first.

### Account enumeration is detectable by timing

`forgot-password` returns the same body either way, but not in the same time. An
address with no account is one indexed `SELECT` and a return — a few
milliseconds. An address with one also writes a token row and **sends the email
synchronously inside the request**, which is a few hundred milliseconds to a
couple of seconds against an external SMTP relay. The gap is two orders of
magnitude and needs no statistics to read: one request per address, a threshold
anywhere between 50 and 200 ms.

The per-account cooldown does not help. It makes the *second* request for an
address fast, and an attacker working through a list sends one.

So the identical body raises the cost of enumeration from "read the response"
to "time the response", and no further. **Treat the set of registered addresses
as obtainable by anyone who wants it**, and do not build anything on the
assumption that it is secret. The Privacy Policy is worded to match (§3.1) and
deliberately claims no more than this.

The fix is to take the send off the request path — a `Channel<T>` plus a
`BackgroundService`, so the response is written before MailKit opens a
connection. It was not done here because it is a change to how the whole
application sends mail: `SendConfirmationCodeAsync` on register leaks the same
way for the same reason, and moving one and not the other would be worse than
moving neither. Whoever does it should do both.

### ~~The token is written to the nginx access log~~ — fixed

`location = /reset-password` now sets `access_log off`, so the request line — and
the token in its query string — is no longer written anywhere. It had been
readable in nginx's access log for as long as the log was kept: single-use and
good for 30 minutes, but a usable password-reset credential for anyone who could
read those files or received a copy.

The same applies to any future route that takes a credential in a URL: it needs
its own `location` with logging off, because the default format logs the whole
request line.

Everything else about that URL is already handled: the route answers with
`Cache-Control: no-store`, `Referrer-Policy: no-referrer` and
`X-Robots-Tag: noindex, nofollow`, so the token does not reach a shared cache, a
`Referer` header or a search index.

### ~~A completed reset sends no notification~~ — fixed

`ResetPasswordAsync` now mails a "your password was changed" notice
(`Emails/password-changed.*`) after the reset lands. Until it did, nothing told
the account holder their password had changed: the symptom they were left with was
a login that stopped working, which reads as your own mistake long before it reads
as an intrusion. It was worst on a **Google-only account**, where there was no
symptom at all — the owner signs in with Google and never meets a password prompt,
so a password set by somebody with brief access to their mailbox stayed invisible
indefinitely.

Two properties of the notice are deliberate. It carries **no link and no code**,
because it is the one email sent to someone who may not have asked for anything.
And it is **best effort, after the save**: the password has already changed by then,
so a refused send is logged rather than raised — answering the client that the
reset failed would leave the user holding a password they believe was rejected.
