# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore dependencies
dotnet restore

# Run every test project
dotnet test smart-pet-care-api.sln

# Run one module's tests
dotnet test Tests/ChatModule.Tests/ChatModule.Tests.csproj --configuration Debug

# Run the application (dev)
dotnet run

# Apply database migrations
dotnet ef database update

# Build for production
dotnet publish -c Release -o /app/publish

# Run with Docker
docker-compose up --build
```

API docs (Scalar UI): `http://localhost:8080/scalar/v1`

All test projects live under `Tests/`, and `dotnet test smart-pet-care-api.sln`
runs every one of them.

Chat module tests are in `Tests/ChatModule.Tests/ChatModule.Tests.csproj`. They
cover the classifier client and configuration, session replacement,
eight-message history replay and pagination, ownership checks, response
mapping, and relational persistence.

Nutrition module tests are in `Tests/NutritionModule.Tests/`. They cover the
daily summary (day-window math, goal comparison) and the AI feeding analysis
(ownership, weight validation, classifier request building, caller-supplied
overrides, response validation, persistence and the two-analyses-per-pet
retention rule).

Reminder module tests are in `Tests/ReminderModule.Tests/`. They cover the
schedule calculator (interval arithmetic, week parity against the anchor,
month clamping, forward-only weekday alignment, local-vs-UTC date handling),
the recalculation service (completion closing an occurrence, early and
backdated completion, idempotency, end-of-series) and the completion service
(health record filing, type mapping, ownership).

Activity module tests are in `Tests/ActivityModule.Tests/`. They cover the
CRUD service (ownership, validation, UTC normalisation, persistence), the
repository (date-range and source filters, tracking behaviour), the
controller status mapping, the swappable activity source, the effort maths
(intensity weights, defaulting), the sleep log with its daily cap, and the
patch paths (partial application, clearing, whole-row revalidation, intensity
re-derivation, self-exclusion from the sleep day total).

Note module tests are in `Tests/NoteModule.Tests/`. They cover the service
(either field alone accepted, neither rejected, blanks normalised to null,
length ceilings measured after trimming, ownership, the patch revalidating the
whole row including the swap that clears one field while filling the other),
the mapper, the repository (pet filter, `UpdatedAt` ordering, tracking
behaviour) and the controller status mapping.

Auth module tests are in `Tests/AuthModule.Tests/`. They cover the password
reset end to end against fakes: that only the hash is stored and the raw
url-safe token is mailed, that an unknown address and the cooldown both answer
the same way as a sent link, that a new link supersedes the old one, that a
reset sets the password, spends the token once, revokes every live session
(including past the ten `GetActiveByUserAsync` returns), drops every push
registration but only on success, confirms an unconfirmed address and leaves an
already-set `EmailConfirmedAt` alone, the invalid/expired/superseded branches,
that the landing page's check does not spend the token it checks, and the link
builder's three sources of a base URL.

Notification module tests are in `Tests/NotificationModule.Tests/`. They cover
device token registration (blank rejected, trimming, the 60-day staleness
window, a token moving between accounts), unregistration scoped to the caller,
the account-wide unregistration a password reset calls, and logout dropping the
signing-out device's registration. Three of them need
Docker: the upsert is raw SQL resolved by a unique index, so `ON CONFLICT` and
the migration that creates its target can only be exercised against a real
Postgres.

### Activity logs

`ActivityLog` is one walk, play session or other activity record, with optional
steps, duration, intensity, place and free text. Wellness aggregates these
session logs directly; it does not read the legacy `ActivityDaily` model.

### Activity type, intensity and effort

A session also carries an optional `Type`, `Intensity` and `DurationMinutes`.
Only the last two are arithmetic: `ActivityEffort` turns them into
`ActiveMinutes` (duration × 0.4 / 0.7 / 1.0), which is the figure a wellness
score sums over a day. It is derived on read, never stored, so the weights can
be retuned without a backfill.

`ActivityType` is a label — for history and for chat context — and is
deliberately absent from that sum. Two consequences worth keeping:

- A walk done at a run is not a different type, it is a harder one, so
  obedience class and competition prep share `Training`, and the "we just went
  out and came back" walk shares `Walk` with the one that was all running.
- `Other` plus a `Note` costs nothing, because an activity the enum never
  anticipated still scores exactly like one it did. Rejecting unknown
  activities would buy no accuracy at all.

`Intensity` is intensity rather than difficulty on purpose: how hard a session
was *for this pet* depends on age, weight and condition, which is what the
score computes — asking the user for it would feed the answer back into
itself.

When a duration is present and no intensity was sent, one is derived from the
type (`Walk` → Low, `Run`/`Swimming` → High, everything else → Moderate).
Making the field required would buy a number the user picked to get past the
form; a duration with no intensity would silently weigh nothing.

### Editing logs

Both logs take a `PATCH` on the item route, using the repo's `PatchField<T>`
convention: absent leaves the field alone, `null` clears it, an empty body is a
400. `Source` is not patchable — it records which provider produced the row, not
what the row says, so a hand-edit cannot turn a typed note into a collar
reading.

A patch is validated as a whole row rather than field by field, through the same
`ValidateReading` the create path uses: the rules that matter are about the row
that results, and "a log has to record something" cannot be checked against a
single cleared field. The intensity derivation runs again on the result, so
adding a duration — or clearing an intensity that still has one — fills the
intensity back in rather than leaving the session unweighted.

The sleep day cap is re-checked on every edit with the edited row excluded from
the day's sum (`GetLoggedHoursAsync`'s `excludeSleepLogId`). Without that, a row
would be weighed against the version of itself still in the table and no
correction upwards would ever pass.

### Sleep logs

Sleep is `SleepLog` — `POST /api/pets/{petId}/sleep-logs` — and not an
`ActivityType`. It shares none of `ActivityLog`'s shape (no steps, no place, no
intensity) and the score reads it as a daily total rather than as a session, so
folding it in would make every reader of `ActivityLogs` filter sleep rows out.

A row is a local day plus hours; the day is stored as UTC midnight with the
caller's date taken at face value, since converting would move evening entries
onto the wrong date for half the world. Several rows may share a date — naps,
and eventually a collar feed — so the ceiling is on the day's **sum**, not on
the row. That sum check is what catches the same night entered twice, which is
the mistake a unique index would have caught, without banning nap-by-nap
logging. Wellness sums these rows per day and includes their average in the
classifier request.

Where the numbers come from is behind `IActivitySourceProvider`, picked per
request by `IActivitySourceResolver` from the `source` field. Only
`ManualActivitySourceProvider` is registered, so any other source is a 400
until its integration lands. A collar integration is one more provider
registration in `ActivityModuleExtensions`; `ActivityLogService` does not
change. Validation runs on the `ActivityReading` the provider returned, not on
the request body, so a device feed answers to the same rules as a typed-in
note.

### Reminder recalculation

`Reminder.RecalcStrategy` decides what a completion does to the schedule:

- `Calendar` — the completion is recorded and the calendar is left alone
  (brushing, weighing, activity).
- `FromCompletion` — next trigger is the performed date plus the interval,
  exactly. Forced on the server for Vaccination, ParasiteTreatment,
  Deworming and VetVisit, since the interval is a safety property there.
- `FromCompletionAlignedToWeekday` — same, then moved *forward* to the nearest
  selected weekday so habits like "bathing on Saturdays" survive.

All trigger dates come from `ReminderScheduleCalculator`, counted from
`Reminder.ScheduleAnchorAt` rather than from "now".

Recalculation has one implementation (`IReminderRecalculationService`) and
several callers, all keyed on the performed date so registering the same
completion twice changes nothing. Every type can be completed through
`POST /api/reminders/{id}/complete`; what the completion additionally files
depends on the type:

| Reminder type | `/complete` also files |
|---|---|
| Vaccination, ParasiteTreatment, Deworming, VetVisit | a HealthRecord |
| Grooming, Activity, Medication, anything else | nothing — the closed run is the log |
| Weighing, Feeding | nothing — the measurement is optional, see below |

Weighing and Feeding have a richer path as well: `POST
/api/pets/{petId}/weight-history` and `POST /api/pets/{petId}/feeding-logs`,
both with `reminderId`, close the occurrence *and* store the measurement.
That path is optional on purpose. Requiring it would mean a user answering a
push cannot tick the reminder off in one tap, and the portion they invent to
get past the form is worse than no portion at all — it reaches the classifier
and costs tokens to produce nonsense. A completion with no log stays out of
the nutrition summary and the feeding analysis, both of which read
`FeedingLogs`. A HealthRecord created by hand with a `reminderId` closes an
occurrence the same way, for treatments given with no rule behind them.

A completion that arrives before the occurrence ever fired has to create the
run itself. It is filed on the pending trigger only when the recalculated
trigger moves *past* that instant — "I did Saturday's bath on Thursday". When
the recalculation lands back on the pending instant, the completion was an
extra event rather than the coming occurrence done early, so the run is filed
at the time it happened and the pending trigger survives. Daily rules
confirmed the evening before hit this, and `Calendar` rules always do, since
they leave a future trigger untouched by definition. Filing on the pending
slot there would swallow a notification the user still wants and collide with
the scheduler's own row on the unique `(ReminderId, ScheduledFor)` index.

Because a completion can arrive twice from two different endpoints — Done on
the push, then the feeding log saved a minute later — the duplicate guard
matches on the **local day** of the performed date rather than the exact
instant. No rule the calculator can express fires twice in one local day, so
same day means same occurrence. Matching exactly would let the second call
through, and it would not merely duplicate history: it materialises the next
occurrence and closes that too, skipping a slot.

### Notes

`Note` — `POST /api/pets/{petId}/notes` — is free text the owner keeps about a
pet: a title, a body, nothing else. It is deliberately not a `JournalEntry`,
which carries a type, symptoms, a severity and an `ObservedAt`. Those fields
are what make the journal a health record other features can read, so filing
"the sitter's phone number" there would mean every reader of the journal has to
filter out rows that mean nothing to it.

Either field may be absent — a title with nothing under it and a body with no
heading are both notes a user means to keep — but a row with neither is not a
note at all, so that is the one case rejected (`note_empty`). The rule is about
the pair, so the `PATCH` is revalidated as a whole row the way the activity logs
are: `null` clears a field, and only clearing the last remaining one fails. A
blank string is stored as `null`, which keeps "absent" to a single check for
every reader. The list is ordered by `UpdatedAt`, which is set on insert as well
as on edit so that ordering reads one column and the client never meets a null.

### Device tokens and push targeting

A reminder's push goes to the device tokens of the pet's owner
(`NotificationService.SendReminderNotificationAsync`), so who receives a
notification is decided entirely by which account a `DeviceTokens` row belongs
to.

A token is therefore unique on `Token` alone, not on `(UserId, Token)`. An FCM
token names a device, not a session: keyed per user the same handset could hold
a row under several accounts at once, and registering under a new one did not
displace the old. The row left behind kept delivering the previous account's
reminders — pet name in the title, the reminder's description in the body — to a
phone that had been signed out of it, until staleness pruned it 60 days later.
Registration now reassigns the row (`ON CONFLICT ("Token") DO UPDATE SET
"UserId" = …`), so the registration follows whoever is signed in.

That covers switching accounts. Plain logout is the other half: `LogoutAsync`
takes an optional `deviceToken` and unregisters exactly that device — after the
session has been revoked, not before. The registration delete races the push
sender's own pruning of the same row, and a `DbUpdateConcurrencyException` there
used to abort logout with the refresh tokens still live for their full seven
days. For the same reason the delete is one `ExecuteDeleteAsync` statement
rather than a tracked load and `Remove`, and a session whose refresh tokens are
already gone no longer returns early. The field is
optional so a client that sends no body logs out as it always did — such a
client simply keeps relying on the reassignment above. Only the signing-out
device is dropped; wiping every token for the user would silence the pushes on
their other devices. A password reset is the one case where wiping all of them
is right, and it does — see the password reset section.

### Legal documents

`GET /terms` and `GET /privacy` serve the Terms and Conditions and the Privacy
Policy. Both sit at the root rather than under `/api`, and are anonymous: they
are URLs printed in app stores and linked from the sign-up screen, read by
people rather than consumed by a client.

Each document exists in two renderings, generated from one markdown source, and
the unsuffixed route negotiates on `Accept`:

| Route | Answers |
|---|---|
| `/terms`, `/privacy` | HTML if the client accepts `text/html`, otherwise the PDF |
| `/terms.pdf`, `/privacy.pdf` | always the PDF, `inline` so a viewer renders it |
| `/terms.html`, `/privacy.html` | always the page |

HTML is not the default for a client that states no preference, so `*/*` still
gets the PDF these routes originally served; browsers and both platforms'
WebViews ask for `text/html` and get the page. That matters because a PDF cannot
reflow: an A4 page squeezed onto a 360dp phone screen renders the 9.8pt body
text at around 6dp, a third of readable, and Android's WebView will not render a
PDF inline at all. The PDF stays the canonical artefact -- for stores, download
and print. `Vary: Accept` is set because `Cache-Control` is `public` and one URL
now has two bodies.

The source of each document is markdown in `docs/legal/`, and both renderings
are **committed artefacts** in `Legal/`, regenerated by hand:

```bash
python scripts/generate-legal-pdfs.py           # rewrite Legal/ from docs/legal/
python scripts/generate-legal-pdfs.py --check   # fail if they no longer match
```

Rendering per request was the alternative, and it would mean the bytes a user
was shown depend on when they asked -- for a document whose whole purpose is to
be the text that was in force at a point in time. Committing the output also
means the version served is reviewable in a diff.

The generator writes the PDF itself instead of pulling in a library: these are
headings and paragraphs, and a regeneration step that needs a working
`pip install` first is one nobody can run on the server. The costs are that
Helvetica's metrics are hardcoded there, so `docs/legal/*.md` has to stay inside
Latin-1 (the script folds the usual typographic characters and fails on
anything else), and that it supports only the markdown it needs: `#`/`##`/`###`,
paragraphs, `-` bullets and `**bold**`. Output is byte-identical across runs --
the PDF's creation date is the document's own effective date, not the clock --
so an unchanged source leaves git quiet and `--check` is meaningful.

The HTML is written for a WebView rather than for a desktop browser, which is
where it is actually read:

- One self-contained file, CSS inlined, **no external request of any kind** --
  no font, no stylesheet, no script. It renders offline and needs no JavaScript
  enabled, so an Android WebView left at its default settings shows it fully.
- `viewport-fit=cover` plus `env(safe-area-inset-*)` padding. Without them an
  edge-to-edge screen puts the first line under the notch and the last under
  the home indicator.
- `-webkit-text-size-adjust: none` so the WebView does not re-scale type that
  was already sized for it, `overscroll-behavior-y: contain` so the document
  does not rubber-band past its ends inside an app screen, and
  `-webkit-tap-highlight-color: transparent` because nothing but the links is
  tappable and a grey flash under every paragraph reads as a bug.
- 17px at a `max-width` of 38rem, which is about 40 characters a line on a
  phone, dropping to 16.5px and narrower gutters under 380px.
- A **light palette only**, with `color-scheme: light` and a `theme-color` meta
  so the scrollbars and the WebView's own chrome stay light around it on a
  phone set to dark mode. There is deliberately no `prefers-color-scheme`
  branch and no in-page theme switch: an app that wants its own dark theme
  should inject CSS through the WebView rather than have the document guess.
- A masthead block -- product name, document title, version line and the
  download -- and a `<details>` index of the sections, collapsed, so 18
  sections get a table of contents without five screens of one. `<details>`
  rather than script, for the same reason as above.
- The download is an `<a href="/terms.pdf" download="terms-and-conditions.pdf">`
  with an inline SVG glyph, repeated once in the footer. The `.pdf` route still
  sends `inline`, so the button downloads while the bare URL renders.
- Every `##`/`###` carries an id taken from its section number (`#s9`,
  `#s3-7`), so the app can deep-link to a clause. Numbers rather than a slug of
  the title: `#s9` still points at retention after the heading is reworded.
- Bare URLs in the source become links, and long ones wrap instead of forcing
  the page to scroll sideways.

Inside the app, link the suffixed routes rather than relying on negotiation:
`/terms.html` in the WebView, `/terms.pdf` behind a download or share action.
Which rendering `/terms` returns depends on an `Accept` header the HTTP library
picks, not on the app's code -- OkHttp sends none at all and would get the PDF.

The client-facing contract -- every route, the negotiation table, caching,
section anchors, the environments and what an existing integration has to change
-- is `docs/legal-documents-api.md`.

`LegalDocumentProvider` reads each file once and keeps the bytes, with an ETag
over them -- so it differs per rendering -- and a crawler revalidating gets a
304. It reads lazily rather than at startup on purpose: a packaging slip in a
static document should not stop the API from serving health records. A missing
file is therefore a 500 `internal_error`, which is what it is -- the
`Content Include` in the csproj is what puts these files in the publish output,
and the Web SDK's default globs cover neither a `.pdf` nor an `.html` outside
`wwwroot`.

The Terms are version 1.0 and the Privacy Policy 1.2; both describe the system
as it actually is -- the data inventory in the Privacy Policy was written off the
models and the infrastructure, so a feature that starts collecting something
new, or a change of processor, needs section 3, 7 or 9 updated with it.

1.2 added the password reset to sections 3.1, 7, 9 and 10: a new table of hashed
tokens with a 30-minute life, the reset link as something the service emails, and
the fact that a reset revokes the sessions on every device. It also records that
the endpoint does not reveal whether the address has an account, which is a
promise the implementation has to keep.

1.1 corrected sections 3.7 and 10, which described tokens as held in HTTP-only,
Secure, SameSite cookies "so page scripts cannot read them". The API has never
set a cookie: both tokens are returned in the response body and the refresh
token comes back in a request body. A published policy claiming a control that
does not exist is worse than one that claims less, so it now says what the API
does and leaves storage to the client. A version bump and a regeneration go
together -- the effective date is what the PDF's creation date is taken from.

### Transactional email

Two emails are sent today: the six-digit confirmation code, from
`AuthService.SendConfirmationCodeAsync` on register and on resend, and the
password reset link, from `AuthService.ForgotPasswordAsync`. Both go out as
**multipart/alternative** -- `IEmailSender.SendAsync` takes an optional
`htmlBody`, and `BodyBuilder` puts the text part first so a client picks the
HTML wherever it can render it. The text part is not a leftover: it is the
fallback for a plain-text client, and an HTML-only message looks like a blast to
a spam filter.

Every rendering lives in `Emails/` as a file, rendered by
`EmailTemplateProvider` (`{{code}}`, `{{minutes}}`, `{{link}}`, HTML-escaped into
the HTML variant). Files rather than strings in C# because email markup is tables
with every style inlined -- unreadable in a verbatim string, and editable by
someone who does not touch the service. Read once and cached; the
`Content Include` in the csproj is what puts them in the publish output.

The HTML is **email** HTML, which is a different craft from the legal pages:

- Tables for layout and every style inlined. Outlook renders with Word's engine,
  and several clients strip `<style>` from the head.
- **No image at all.** Most clients block remote images by default, so the
  wordmark is text -- a logo that always arrives beats one that usually does.
- `color-scheme: light only` plus `supported-color-schemes`. Without them Gmail
  and Outlook on iOS invert the card and the white design arrives grey.
- The code sits in its own bordered block with `bgcolor` as well as
  `background-color`, because Outlook ignores the CSS one.
- Preview text is a hidden div, and **it does not contain the code or the reset
  link** -- neither does either subject. Both land on a lock screen.
- The reset email's button is the one element the confirmation does not have. Its
  colour sits on the `<td>` as `bgcolor` as well as in CSS, since Outlook ignores
  the CSS one, and the rounded corner is what it degrades to a plain rectangle
  without. The full URL is repeated as text underneath, because a user reading
  mail on a desktop has to be able to copy it.

The sender name in the inbox is `EmailOptions.FromName`, defaulting to `Miso`
and overridden by `Email__FromName` / `EMAIL_FROM_NAME` in a deployed
environment, so changing the brand means changing `.env` on the host too.

The connection is encrypted or it does not happen: `EmailOptions.SocketOptions`
picks `SslOnConnect` for port 465 and `StartTls` for everything else, and
`SmtpEmailSender` asserts `client.IsSecure` before it authenticates. The old
`StartTlsWhenAvailable` carried on in cleartext whenever the server did not
advertise STARTTLS -- and a stripped capability line looks exactly like a server
that is simply plain -- so the SMTP password and the confirmation code could
both go out in the open. `Email:AllowInsecureConnection` restores the lenient
behaviour for a relay on loopback or a private network, where there is no
segment to intercept.

### Password reset

A forgotten password is recovered by link, not by code: `POST
/api/auth/forgot-password` mails one, `GET /reset-password?token=…` opens the app
at the new-password screen, and `POST /api/auth/reset-password` spends the token.
The client-facing contract is `docs/password-reset-flow.md`.

`forgot-password` answers **202 with the same body whether the address has an
account or not**, and whether a mail went out or was suppressed by the 60-second
per-account cooldown. This is the one anonymous endpoint where the difference
between two answers *is* the product — a free "does this person use Miso" lookup
against any list of addresses. `resend-confirmation` does return 404
`auth_account_not_found`, and it can: the caller there has just been told the
address is registered. The cooldown is therefore silent rather than a
`CONFIRMATION_RESEND_TOO_SOON`-style 429, and the per-address rate limiter is
what actually caps the endpoint.

The link in the email is **https to this API**, not `miso://`. A mail client
renders a custom scheme as plain text at best and strips it at worst, and the
message has to be readable on a desktop with no app installed. `/reset-password`
then serves a page that opens `PasswordReset:AppLink` — on load via a two-line
script, and from a visible button for when that is blocked. A deployment that
registers App Links or Universal Links for the host never reaches the route at
all, which is the better path; the page is what covers everything else, because
a 302 to a scheme nothing can open is a browser error rather than a sentence the
user can act on. It answers 200 even for a dead link: the status is about the
request for a page, and some WebViews replace the body of an error status with
their own.

Opening the link is a **read** — the token is spent by the POST and nothing else,
which is what makes a mail scanner's prefetch harmless. The route sets
`no-store`, `Referrer-Policy: no-referrer` and `X-Robots-Tag: noindex` because
the URL carries a bearer credential.

`PasswordResetToken` is its own table rather than a purpose column on
`EmailConfirmationCodes`: the two are looked up differently — a code by user, a
reset by the token in a URL — and sharing the table would put a row that can
change a password one wrong predicate away from the query that confirms an
address. 256 random bits, base64url rather than `GenerateRefreshToken`'s plain
base64 since it travels as a query parameter, stored as SHA256, single use, 30
minutes, and superseded by the next link for the same account.

What a reset does beyond the password:

- **Revokes every live refresh token**, through `GetAllActiveByUserAsync` rather
  than `GetActiveByUserAsync` — the latter takes the ten newest, which is enough
  to sign a session out but would leave an eleventh working for seven days. A
  reset is what a user does when they believe somebody else knows the password.
- **Deletes every push registration** (`IDeviceTokenService.UnregisterAllAsync`),
  which revoking the sessions does not cover: the recipient of a push is decided
  by the account a `DeviceTokens` row belongs to and by no session at all, so a
  device that registered while somebody else held the password would carry on
  receiving the owner's reminders — pet name in the title, the reminder in the
  body — and the 60-day pruning never arrives, because every delivered push
  refreshes `LastSeenAt`. It takes *all* of them, unlike logout: after a reset
  "the user's other devices are still theirs" is precisely the assumption that
  stopped holding, and each of the owner's devices has to sign in again anyway
  and registers on the way. Only on success — a rejected link must not become a
  way to silence somebody's notifications.
- **Confirms an unconfirmed address.** Following the link proved control of the
  mailbox, which is what the six-digit code proves; without it an account that
  never confirmed could reset its password and still not be able to log in.
- **Returns no session.** The link sat in a mailbox, so it buys one password
  change, not a signed-in client. The app logs in afterwards.

A Google-only account can use the flow and gains a password. It is not a new way
in — anyone holding the mailbox can already reset through Google — and refusing
would leave a user whose Google account is gone with nothing.

The token and the password are written in **one SaveChanges**: the tracked user
row is loaded, avatar bytes and all, instead of being projected and updated with
`ExecuteUpdate`, because a reset that burned the link without changing the
password would lock the user out of their own recovery.

`Pages/password-reset-*.html` are the two renderings, read and cached by
`PasswordResetPageProvider`, and in the publish output through the same
`Content Include` the email templates need. They follow the legal pages' rules
(self-contained, light-only, safe-area padding) with one exception: the automatic
hand-off needs script, so the link lives in the button's `href` and the script
reads it back off the element rather than being handed a second copy to escape.

Three findings from the security review were left in deliberately, and
`docs/password-reset-flow.md` records each with its fix:

- **Enumeration is detectable by timing.** The body is identical for an address
  with an account and one without; the response time is not, because the email
  is sent synchronously inside the request. Two orders of magnitude, one request
  per address. The identical body therefore raises the cost from reading the
  response to timing it, and no further — treat the registered-address set as
  obtainable. Not fixed because `SendConfirmationCodeAsync` on register leaks
  the same way for the same reason, and the fix is one change to how the
  application sends mail (a queue plus a `BackgroundService`) that should move
  both or neither. The Privacy Policy §3.1 is worded to claim no more than this.
- **The token reaches the nginx access log**, since it is in the query string and
  nginx logs the request line. Everything else about that URL is covered — the
  route sends `no-store`, `no-referrer` and `noindex`.
- **A completed reset notifies nobody.** Worst on a Google-only account, where
  the owner never meets a password prompt and so has no symptom at all.

### Errors

Every failure answers with one shape, `ApiErrorResponse`, carrying a stable
`code` the frontend localizes against. The public contract — the fields, the
full alias catalogue and which modules are covered so far — is
`docs/error-codes.md`.

The alias is set where the failure is raised, not derived from the message
afterwards: services throw an `AppException` carrying its `Code`, `StatusCode`
and optional `Params`, and `GlobalExceptionHandler` turns it into the response.
Deriving the code from the message text instead would mean that rewording a
message silently changes the code a client sees. The subclass picks the status,
so no controller decides one: `ValidationException` (400),
`UnauthorizedException` (401), `ForbiddenException` (403), `NotFoundException`
(404), `ConflictException` (409), `GoneException` (410),
`UnprocessableException` (422), `TooManyRequestsException` (429) and
`UpstreamException` (status as given, plus `Retryable`).

Three places put that shape on the wire, because a failure can reach the client
without ever being an exception:

- `GlobalExceptionHandler` — anything thrown.
- `ErrorTraceIdFilter` — stamps `TraceId` on an `ApiErrorResponse` a controller
  returned directly. Doing it at each call site would leave every new
  `NotFound(...)` one forgotten argument away from an error nobody can correlate
  with a log line.
- `JwtBearerEvents` in `AuthModuleExtensions` — the bearer scheme answers 401
  and 403 with an empty body by default, which would make the two statuses a
  client meets most often the only ones it cannot read a code from.

Anything that is not an `AppException` or a known classifier failure is reported
as `internal_error` with a 500 and its detail kept in the log. An exception
nobody designed for is a bug, and answering it with a plausible business error —
which is what a bare `catch (InvalidOperationException)` per controller did — is
how bugs stay invisible.

Every module answers this way now, so controllers carry no `try/catch` for
domain failures at all. Two places deliberately still catch, and both wrap
rather than translate: `WellnessService` turns classifier failures into its own
`wellness_service_*` aliases, because the client knows those and not the
classifier's, and `PetWeightLogService` turns a unique-index violation into the
same conflict its pre-check raises. Chat is the counter-example — it always
passed the classifier's own code through, so it wraps nothing.

`Tests/CommonApi.Tests/` covers the handler and keeps the catalogue from
drifting out of `docs/error-codes.md`: adding a constant without documenting it
fails the build.

Classifier integration documentation:

- `docs/chat-classifier-contract-v1.md`
- `docs/feeding-summary-contract-v1.md`
- `docs/wellness-contract-v1.md`

The classifier exposes four routes: `predict`, `chat`, `wellness` and
`feeding-summary`. The backend wires `chat`, `wellness` and `feeding-summary`.

### Wellness test fixture

A wellness score needs thirty days of several unrelated tables at once, so
`scripts/seed-wellness-test.sql` fills them for one existing pet:

```bash
docker compose exec -T db psql -U postgres -d smartPetCareDb \
  -v pet_id=<uuid> -v fill_pet_profile=1 < scripts/seed-wellness-test.sql
```

PowerShell has no input redirection for native commands, so there it is a pipe:

```powershell
Get-Content -Raw scripts/seed-wellness-test.sql |
  docker compose exec -T db psql -U postgres -d smartPetCareDb -v pet_id=<uuid> -v fill_pet_profile=1
```

It covers every dimension the aggregator reads — activity and sleep across the
whole window, two meals a day, weight history, preventive-care events, an active
condition, a medication with its reminder runs (26 of 30 doses taken, so
adherence is a ratio rather than a perfect score) and routine-care reminders
with a deliberate spread, including one never done and a grooming rule older
than the grooming event that has to override it.

Every row carries the marker `[wellness-e2e-seed:v1]` in a free-text column and a
deterministic ID derived from the pet, which is what makes a re-run replace the
previous fixture and leave user-created rows alone.
`scripts/cleanup-wellness-test.sql` removes it again, and takes an optional
`-v assessment_id=<uuid>` to drop the assessment a recalculation stored.

`-v fill_pet_profile=1` is the one thing that writes to the pet itself, and only
where a field is empty: species, breed, birth date, sex, weight and behavioural
notes. Age and weight decide what counts as enough activity for this pet, so a
name-only pet scores against defaults until they are set. The cleanup script
does not revert them — once set they are the pet's own profile.

## Architecture

This is a **.NET 10 Web API** for a pet care management system using a **feature-based modular structure**.

### Request Flow

```
HTTP Request → Controller → Service → Repository → AppDbContext (EF Core) → PostgreSQL
```

- **Controllers** (`Modules/*/Api/`) — thin, no business logic, delegate to services
- **Services** (`Modules/*/Domain/`) — business logic, depend on repository interfaces
- **Repositories** (`Modules/*/Repository/`) — data access only; read queries use `AsNoTracking()`
- **DTOs** (`Modules/*/DTOs/`) — separate request/response objects; never expose domain models directly
- **Mappers** (`Modules/*/Mapper/`) — convert between entities and DTOs

### Module Layout

Each feature module (e.g. `UserModule`, `AuthModule`) is self-contained:
```
Modules/<Feature>/
├── Api/           # Controller
├── Domain/        # IService + Service
├── Repository/    # IRepository + Repository
├── DTOs/          # Request + Response DTOs
└── Mapper/        # Entity ↔ DTO mapping
```

New modules should follow this layout and register their services in a dedicated `*ModuleExtensions.cs` file, then call it from `Program.cs`.

### Database

- **PostgreSQL 16** via EF Core (Npgsql provider), code-first
- `AppDbContext` is in `Data/AppDbContext.cs`
- Migrations are in `Migrations/`; the app runs `db.Database.Migrate()` at startup automatically
- Connection string comes from `appsettings.json` or the `DB_PASSWORD` env var in Docker

### Backups and rollback

Because migrations are applied automatically at startup, a deploy changes the
code and the schema in one step and neither reverts on its own. The procedure —
what is dumped when, and which of the three rollback cases applies (bad code /
bad migration / bad data) — is in `docs/backup-and-rollback.md`.

```bash
bash scripts/backup-db.sh [label]          # dump, verify, prune, upload to S3
bash scripts/restore-db.sh <dump> [--yes]  # drop, recreate, restore; leaves api stopped
bash scripts/rollback.sh --last            # back to the previously deployed commit
```

Dumps and per-release manifests live in `backups/` on the server (gitignored,
mounted into the db container as `/backups`). The deploy workflow takes one
before every release and records the commit it replaced in
`backups/.last-deployed-sha`.

The image is built in CI, not on the server: the `build` job pushes
`ghcr.io/volodymyrbiletskyi/smart-pet-care-api` tagged with the commit sha, and
the deploy pulls it. So the host never needs the ~3GB sdk image, a failed build
cannot take production down with it, and `rollback.sh` goes back to the tag for
a commit rather than rebuilding it. `docker compose up --build` locally is
unchanged — `api` carries both `image:` and `build:`. The ten most recent
versions are kept in the registry and older ones are deleted after each push,
since nothing there expires on its own.

On the host, the deploy removes the api images it replaced by name, keeping the
current release and the previous one. `docker image prune -f` cannot do this —
every release is tagged with its own commit sha, so nothing ever takes that tag
away and the replaced image is *unused* rather than *dangling*. Running the
plain prune on every deploy therefore reclaimed nothing for weeks, and the
accumulated images are what filled the root volume on 2026-10-10. The previous
release is kept so `rollback.sh` starts a local image instead of depending on
the registry mid-incident.

### Health and alerting

`/health` checks the database, not just the process (`DatabaseHealthCheck`).
Liveness alone used to be the whole check and was deliberate — a failed
migration means the server never listens, which is what `up -d --wait` gates
on, and that still holds. But it is also what let an outage run for 27 hours
unnoticed: the process was fine, Postgres was refusing every connection, and
the container healthcheck and the uptime probe both stayed green while every
request failed. The database is folded into the same route rather than put
behind a second readiness one because every consumer here wants "can this serve
a request", and nothing acts on liveness by itself — Docker does not restart an
unhealthy container, so an unreachable database turns the api unhealthy and
leaves it running.

Backups and free disk space are watched by **absence** of a signal, not by
failure reports: `backup-db.sh` pings `BACKUP_PING_URL` once a dump verifies,
`scripts/disk-space-ping.sh` pings hourly while free space is above its floor,
and a service off the host alerts when a ping stops arriving. The reasoning —
a cron on a full disk cannot mail anyone about the disk being full — is in
`docs/backup-and-rollback.md`, along with the floors and why they are absolute
rather than a percentage.

### Authentication

Dual auth strategy — JWT bearer tokens + Google OAuth:

- **JWT:** Access tokens (15 min) and refresh tokens (7 days) are returned in the response body and sent back as `Authorization: Bearer`. The API sets no cookie anywhere, so where a token is kept is the client's decision. Refresh tokens are persisted as SHA256 hashes in the `RefreshTokens` table, and `POST /api/auth/refresh` takes the raw token in the body.
- **Google OAuth:** Web (authorization code flow) and mobile (ID token validation) flows both supported via `Google.Apis.Auth`. A token is accepted only when Google reports the account's email as verified — the email is what `LoginOrRegisterOAuthUserAsync` matches an existing row on, so an unverified one naming somebody else's address would be a way into their account.
- `AuthMiddleware` validates that the JWT user still exists in the database; results are cached for 3 minutes to reduce DB hits.
- Protected endpoints use `[Authorize]`. Retrieve the current user ID via `ClaimsPrincipalExtensions.GetUserId()`.
- The anonymous endpoints — `login`, `register`, `confirm-email`, `resend-confirmation`, `forgot-password`, `reset-password` — are rate limited per client address (`AuthRateLimitPolicies`), and nginx limits the same six routes as a second layer. Each of them either burns an enhanced BCrypt hash or sends mail: without a cap `login` is a free guessing oracle and `register` reaches the SMTP sender once per request, since the 60-second resend cooldown is per account and a caller posting fresh addresses never meets it. `forgot-password` is the same and worse, since it answers identically for an address with an account and one without — the cap is the only thing between a list of addresses and a mailing. A rejection is `auth_too_many_requests` with `Retry-After`.
- `UseForwardedHeaders` is configured with `XForwardedFor` and an empty known-proxy list, because the limiter partitions on `Connection.RemoteIpAddress`: with the defaults (loopback only) nginx's headers were discarded and every client on earth shared the proxy's address and one partition.

JWT and OAuth settings are loaded from `appsettings.json` (`JwtOptions`, `GoogleOAuthOptions`) and can be overridden with env vars (see `.env`).

### Key Dependencies

| Package | Purpose |
|---|---|
| `BCrypt.Net-Next` | Password hashing |
| `Google.Apis.Auth` | Google OAuth token validation |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT bearer scheme |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core PostgreSQL provider |
| `Scalar.AspNetCore` | OpenAPI / interactive API docs |
