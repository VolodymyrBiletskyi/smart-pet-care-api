#!/usr/bin/env python3
"""Render the documents in docs/legal/ into the PDFs served by /terms and /privacy.

    python scripts/generate-legal-pdfs.py [--check]

The PDFs are committed artefacts -- the endpoint reads a file, it does not build
one per request -- so this script runs by hand whenever the markdown changes, and
`--check` fails if the committed PDFs no longer match their source.

It writes the PDF by hand rather than through a library: a regeneration step that
needs a working `pip install` first is one nobody can run on the server, and the
documents are text and headings only. The trade-off is Helvetica with the metrics
hardcoded below, so the source files have to stay in the Latin-1 range; see
normalise() for the handful of typographic characters that are folded.

Output is byte-for-byte deterministic -- the creation date comes from the
document's own effective date, not from the clock -- so an unchanged source
re-renders to an unchanged file and git stays quiet.
"""

import hashlib
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# source markdown, output stem, route slug
DOCUMENTS = [
    ("docs/legal/terms-and-conditions.md", "Legal/terms-and-conditions", "terms"),
    ("docs/legal/privacy-policy.md", "Legal/privacy-policy", "privacy"),
]

# A4, in PDF points.
PAGE_W, PAGE_H = 595.28, 841.89
MARGIN_X = 56.0
MARGIN_TOP = 64.0
MARGIN_BOTTOM = 62.0
TEXT_W = PAGE_W - 2 * MARGIN_X

REGULAR, BOLD = "F1", "F2"

# Widths of ASCII 32..126 in 1/1000 em, from the Adobe AFM metrics for the
# standard Helvetica fonts. Needed because wrapping has to happen here: there is
# no layout engine downstream to fix a line that turned out too long.
W_REGULAR = [
    278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
    556, 556, 556, 556, 556, 556, 556, 556, 556, 556,
    278, 278, 584, 584, 584, 556, 1015,
    667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
    667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611,
    278, 278, 278, 469, 556, 333,
    556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
    556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500,
    334, 260, 334, 584,
]
W_BOLD = [
    278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
    556, 556, 556, 556, 556, 556, 556, 556, 556, 556,
    333, 333, 584, 584, 584, 611, 975,
    722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
    667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611,
    333, 278, 333, 584, 556, 333,
    556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
    611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500,
    389, 280, 389, 584,
]
assert len(W_REGULAR) == 95 and len(W_BOLD) == 95

# Typographic characters that survive a copy-paste into the source but have no
# place in a WinAnsi-encoded Helvetica string.
FOLD = {
    "—": "-", "–": "-", "‘": "'", "’": "'",
    "“": '"', "”": '"', "…": "...", " ": " ",
    "•": "-", "→": "->",
}

MONTHS = ("january", "february", "march", "april", "may", "june",
          "july", "august", "september", "october", "november", "december")


def normalise(text):
    for bad, good in FOLD.items():
        text = text.replace(bad, good)
    stray = {c for c in text if ord(c) > 126 and c not in "\n\r\t"}
    if stray:
        raise SystemExit(
            "source contains characters this renderer cannot encode: "
            + " ".join(sorted("U+%04X (%s)" % (ord(c), c) for c in stray))
        )
    return text


def width(text, bold, size):
    table = W_BOLD if bold else W_REGULAR
    total = 0
    for ch in text:
        code = ord(ch)
        total += table[code - 32] if 32 <= code <= 126 else 556
    return total * size / 1000.0


# ---------------------------------------------------------------- markdown ---

class Block:
    def __init__(self, style, units, indent=0.0, hanging=0.0, bullet=None):
        self.style = style
        self.units = units
        self.indent = indent
        self.hanging = hanging
        self.bullet = bullet


# style -> (font size, leading, space before, space after, bold, keep with next)
STYLES = {
    "h1":      (19.0, 24.0, 0.0, 10.0, True, True),
    "h2":      (12.5, 16.0, 17.0, 5.0, True, True),
    "h3":      (10.5, 14.0, 11.0, 3.0, True, True),
    "body":    (9.8, 13.6, 0.0, 7.0, False, False),
    "bullet":  (9.8, 13.6, 0.0, 4.0, False, False),
    "meta":    (9.0, 12.0, 0.0, 10.0, False, False),
}


def tokenize(text, default_bold):
    """Split a line into unbreakable units of (word, bold) runs.

    A unit is what a line break may not fall inside. Whitespace in the source
    decides where one ends, which is why the parts of a **bold** span are not
    simply words: the comma after `**Privacy Policy**,` has no space in front of
    it and has to stay welded to the word, in the other font.
    """
    units = []
    gap = False  # whitespace seen since the previous run

    for index, part in enumerate(re.split(r"\*\*", text)):
        bold = default_bold or index % 2 == 1
        part = part.replace("`", "")
        if not part:
            continue

        if part[0].isspace():
            gap = True

        words = part.split()
        for word in words:
            if gap or not units:
                units.append([(word, bold)])
            else:
                units[-1].append((word, bold))
            gap = True

        gap = part[-1].isspace() or not words

    return units


def parse(markdown):
    blocks = []
    paragraph = []
    bullet = None

    def flush():
        nonlocal paragraph, bullet
        if bullet is not None:
            text, level = bullet
            size = STYLES["bullet"][0]
            blocks.append(Block(
                "bullet", tokenize(text, False),
                indent=14.0 * (level + 1), hanging=width("-   ", False, size),
                bullet="-"))
            bullet = None
        elif paragraph:
            style = "meta" if len(blocks) == 1 and blocks[0].style == "h1" else "body"
            blocks.append(Block(style, tokenize(" ".join(paragraph), False)))
        paragraph = []

    for raw in markdown.splitlines():
        line = raw.rstrip()
        stripped = line.strip()

        if not stripped or stripped == "---":
            flush()
            continue

        heading = re.match(r"^(#{1,3})\s+(.*)$", stripped)
        if heading:
            flush()
            style = "h%d" % len(heading.group(1))
            blocks.append(Block(style, tokenize(heading.group(2), True)))
            continue

        item = re.match(r"^(\s*)[-*]\s+(.*)$", line)
        if item:
            flush()
            bullet = (item.group(2), len(item.group(1)) // 2)
            continue

        if bullet is not None:  # continuation line of the current bullet
            bullet = (bullet[0] + " " + stripped, bullet[1])
        else:
            paragraph.append(stripped)

    flush()
    return blocks


# ------------------------------------------------------------------ layout ---

def wrap(units, bold_default, size, max_width):
    """Lay units out into lines of (text, bold, space_before) runs."""
    lines, current, used = [], [], 0.0

    def push():
        nonlocal current, used
        if current:
            lines.append(current)
        current, used = [], 0.0

    for unit in units:
        lead = width(" ", unit[0][1], size) if current else 0.0
        unit_width = sum(width(text, bold, size) for text, bold in unit)

        if unit_width > max_width:  # a URL with no break in it
            push()
            for text, bold in unit:
                chunk = ""
                for ch in text:
                    if chunk and used + width(chunk + ch, bold, size) > max_width:
                        current.append((chunk, bold, False))
                        push()
                        chunk = ""
                    chunk += ch
                if chunk:
                    current.append((chunk, bold, False))
                    used += width(chunk, bold, size)
            continue

        if current and used + lead + unit_width > max_width:
            push()
            lead = 0.0

        for index, (text, bold) in enumerate(unit):
            current.append((text, bold, index == 0 and lead > 0.0))
        used += lead + unit_width

    push()
    return lines or [[("", bold_default, False)]]


class Line:
    def __init__(self, runs, x, y, size):
        self.runs = runs
        self.x = x
        self.y = y
        self.size = size


def paginate(blocks):
    pages, page = [], []
    y = PAGE_H - MARGIN_TOP
    floor = MARGIN_BOTTOM

    def new_page():
        nonlocal pages, page, y
        if page:
            pages.append(page)
        page = []
        y = PAGE_H - MARGIN_TOP

    for block in blocks:
        size, leading, before, after, bold, keep = STYLES[block.style]
        indent = block.indent + block.hanging
        lines = wrap(block.units, bold, size, TEXT_W - indent)

        # Keeping a heading with the first two lines under it: a section title
        # alone at the foot of a page reads as the end of the document.
        needed = leading * (len(lines) + (2 if keep else 0))
        if page and y - before - needed < floor:
            new_page()
        elif page:
            y -= before

        for index, runs in enumerate(lines):
            if y - leading < floor:
                new_page()
            y -= leading
            x = MARGIN_X + block.indent + (0.0 if index == 0 else block.hanging)
            if block.bullet is not None and index == 0:
                page.append(Line([(block.bullet, False, False)], x, y, size))
                x += block.hanging
            page.append(Line(runs, x, y, size))

        y -= after

    if page:
        pages.append(page)
    return pages


# --------------------------------------------------------------- pdf output ---

def escape(text):
    return text.replace("\\", r"\\").replace("(", r"\(").replace(")", r"\)")


def content_stream(lines, footer):
    out = []
    for line in lines:
        out.append("BT")
        out.append("1 0 0 1 %.2f %.2f Tm" % (line.x, line.y))
        font = None
        for text, bold, space in line.runs:
            wanted = BOLD if bold else REGULAR
            if wanted != font:
                out.append("/%s %.2f Tf" % (wanted, line.size))
                font = wanted
            out.append("(%s) Tj" % escape(" " + text if space else text))
        out.append("ET")

    out.append("BT")
    out.append("0.45 0.45 0.45 rg")
    out.append("/%s 7.8 Tf" % REGULAR)
    out.append("1 0 0 1 %.2f %.2f Tm" % (
        MARGIN_X + (TEXT_W - width(footer, False, 7.8)) / 2.0, MARGIN_BOTTOM - 24.0))
    out.append("(%s) Tj" % escape(footer))
    out.append("ET")
    return "\n".join(out).encode("cp1252")


def build_pdf(pages, title, subject, created):
    objects = []  # 1-based; body objects are appended in order

    def add(payload):
        objects.append(payload)
        return len(objects)

    catalog = add(None)   # patched below, once the page tree id is known
    pages_id = add(None)
    fonts = {
        REGULAR: add(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica "
                     b"/Encoding /WinAnsiEncoding >>"),
        BOLD: add(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold "
                  b"/Encoding /WinAnsiEncoding >>"),
    }
    info = add(
        ("<< /Title (%s) /Author (Smart Pet Care App project team) /Subject (%s) "
         "/Producer (scripts/generate-legal-pdfs.py) /Creator (scripts/generate-legal-pdfs.py) "
         "/CreationDate (D:%s) /ModDate (D:%s) >>"
         % (escape(title), escape(subject), created, created)).encode("cp1252"))

    resources = "<< /Font << %s >> >>" % " ".join(
        "/%s %d 0 R" % (name, oid) for name, oid in fonts.items())

    page_ids = []
    for number, lines in enumerate(pages, start=1):
        stream = content_stream(lines, "Page %d of %d" % (number, len(pages)))
        content_id = add(b"<< /Length %d >>\nstream\n" % len(stream) + stream + b"\nendstream")
        page_ids.append(add(
            ("<< /Type /Page /Parent %d 0 R /MediaBox [0 0 %.2f %.2f] /Resources %s "
             "/Contents %d 0 R >>" % (pages_id, PAGE_W, PAGE_H, resources, content_id)
             ).encode("cp1252")))

    objects[catalog - 1] = b"<< /Type /Catalog /Pages %d 0 R >>" % pages_id
    objects[pages_id - 1] = (
        "<< /Type /Pages /Count %d /Kids [%s] >>"
        % (len(page_ids), " ".join("%d 0 R" % pid for pid in page_ids))).encode("cp1252")

    out = bytearray(b"%PDF-1.4\n%\xc3\xa4\xc3\xbc\xc3\xb6\n")
    offsets = []
    for oid, payload in enumerate(objects, start=1):
        offsets.append(len(out))
        out += b"%d 0 obj\n" % oid + payload + b"\nendobj\n"

    xref = len(out)
    out += b"xref\n0 %d\n" % (len(objects) + 1)
    out += b"0000000000 65535 f \n"
    for offset in offsets:
        out += b"%010d 00000 n \n" % offset

    doc_id = hashlib.md5(bytes(out)).hexdigest().upper().encode("ascii")
    out += (b"trailer\n<< /Size %d /Root %d 0 R /Info %d 0 R /ID [<%s> <%s>] >>\n"
            % (len(objects) + 1, catalog, info, doc_id, doc_id))
    out += b"startxref\n%d\n%%%%EOF\n" % xref
    return bytes(out)


def plain(units):
    return " ".join("".join(text for text, _ in unit) for unit in units)


# -------------------------------------------------------------- html output ---

# The page the routes serve to a browser or a WebView. A PDF cannot reflow: a
# 595pt page squeezed onto a 360dp phone screen renders 9.8pt body text at
# around 6dp, which is a third of what is readable, and the zoom that fixes it
# brings horizontal panning with it. Same source, same wording, laid out by the
# client instead.
HTML_STYLE = """
:root {
  /* Light, and only light: `color-scheme: light` keeps the scrollbars and the
     WebView's own chrome from turning dark around a light page when the phone
     is in dark mode. There is deliberately no prefers-color-scheme branch. */
  color-scheme: light;
  --bg: #ffffff;
  --surface: #f6f7f9;
  --fg: #1d1d23;
  --muted: #6a6a75;
  --accent: #2a6bb0;
  --rule: #e6e7ec;
  --pad-x: 1.25rem;
}
* { box-sizing: border-box; }
html {
  /* A WebView must not re-scale the text we sized for it, and the document
     must not rubber-band past its own ends inside an app screen. */
  -webkit-text-size-adjust: none;
  overscroll-behavior-y: contain;
}
body {
  margin: 0;
  background: var(--bg);
  color: var(--fg);
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto,
               "Helvetica Neue", Arial, sans-serif;
  font-size: 17px;
  line-height: 1.65;
  /* Nothing here is tappable except the links, so the grey flash on every
     paragraph touch reads as a bug. */
  -webkit-tap-highlight-color: transparent;
}
main {
  max-width: 38rem;
  margin: 0 auto;
  /* env() needs viewport-fit=cover, which the meta tag sets: an edge-to-edge
     WebView otherwise puts the first line under the notch and the last one
     under the home indicator. */
  padding-top: calc(2.25rem + env(safe-area-inset-top));
  padding-right: calc(var(--pad-x) + env(safe-area-inset-right));
  padding-bottom: calc(4rem + env(safe-area-inset-bottom));
  padding-left: calc(var(--pad-x) + env(safe-area-inset-left));
}
h1 { font-size: 1.6rem; line-height: 1.25; letter-spacing: -0.015em; margin: 0 0 0.5rem; }
h2 { font-size: 1.14rem; line-height: 1.35; margin: 2.3rem 0 0.6rem; }
h3 { font-size: 1rem; margin: 1.7rem 0 0.4rem; }
h1, h2, h3 { text-wrap: balance; scroll-margin-top: 1rem; }
p, li { text-wrap: pretty; }
p { margin: 0 0 1rem; }
ul { margin: 0 0 1.2rem; padding-left: 1.2rem; }
li { margin: 0 0 0.55rem; }
li::marker { color: var(--muted); }
strong { font-weight: 600; }
a { color: var(--accent); overflow-wrap: anywhere; }
::selection { background: #dbe8f6; }
a:focus-visible, summary:focus-visible {
  outline: 2px solid var(--accent); outline-offset: 2px; border-radius: 4px;
}
.meta { color: var(--muted); font-size: 0.93rem; }

/* The masthead: title, version and the download, as one block rather than
   three paragraphs that happen to come first. */
.masthead {
  background: var(--surface);
  border: 1px solid var(--rule);
  border-radius: 14px;
  padding: 1.4rem 1.25rem 1.3rem;
  margin-bottom: 1.9rem;
}
.masthead p { margin: 0; }
.eyebrow {
  margin: 0 0 0.4rem;
  color: var(--muted);
  font-size: 0.78rem;
  font-weight: 600;
  letter-spacing: 0.09em;
  text-transform: uppercase;
}
.action {
  display: inline-flex;
  align-items: center;
  gap: 0.55rem;
  margin-top: 1.2rem;
  padding: 0.62rem 1rem;
  background: var(--bg);
  border: 1px solid var(--rule);
  border-radius: 10px;
  color: var(--fg);
  font-size: 0.95rem;
  font-weight: 500;
  text-decoration: none;
}
.action:hover { border-color: var(--accent); color: var(--accent); }
.action svg { width: 1.05em; height: 1.05em; flex: none; }

/* Collapsed by default, and built from <details> so the page still needs no
   JavaScript: 18 sections are worth an index, but not five screens of one. */
.toc { margin: 0 0 2.4rem; }
.toc summary { cursor: pointer; color: var(--accent); font-size: 0.95rem; font-weight: 500; }
.toc ul { list-style: none; margin: 0; padding: 0.9rem 0 0; }
.toc li { margin: 0 0 0.55rem; }
.toc a { color: var(--fg); text-decoration: none; }
.toc a:hover { color: var(--accent); }

footer { margin-top: 3rem; padding-top: 1.1rem; border-top: 1px solid var(--rule); }
footer p { margin: 0; }

/* Small phones: the gutters cost more than they give at this width. */
@media (max-width: 380px) {
  :root { --pad-x: 1rem; }
  body { font-size: 16.5px; }
  h1 { font-size: 1.45rem; }
  .action { width: 100%; justify-content: center; }
}
""".strip()

DOWNLOAD_ICON = (
    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" '
    'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">'
    '<path d="M12 3v12"/><path d="m7 11 5 5 5-5"/><path d="M4 20h16"/></svg>')

TRAILING = ".,;:)\"'"


def escape_html(text):
    return (text.replace("&", "&amp;")
                .replace("<", "&lt;")
                .replace(">", "&gt;")
                .replace('"', "&quot;"))


def word_html(text):
    """One word, escaped, with a bare URL turned into a link."""
    if not text.startswith(("http://", "https://")):
        return escape_html(text)

    url, tail = text, ""
    while url and url[-1] in TRAILING:
        tail = url[-1] + tail
        url = url[:-1]

    return '<a href="%s">%s</a>%s' % (escape_html(url), escape_html(url), escape_html(tail))


def inline_html(units, emphasise=True):
    """Units as inline HTML. One <strong> per bold span, not per bold word.

    `emphasise` is off for headings: the tokenizer marks every word in one bold
    so the PDF picks the bold font, and h1/h2/h3 already carry the weight.
    """
    pieces = []  # (html, bold), coalesced as we go

    for index, unit in enumerate(units):
        for position, (text, bold) in enumerate(unit):
            bold = bold and emphasise
            # Units are the unbreakable groups the PDF wrapper needs; here they
            # only say where the spaces go, which is the same question.
            lead = " " if index and position == 0 else ""
            html = word_html(text)

            if pieces and pieces[-1][1] == bold:
                pieces[-1] = (pieces[-1][0] + lead + html, bold)
                continue

            if lead and pieces:  # the space belongs outside the tag about to open
                pieces[-1] = (pieces[-1][0] + lead, pieces[-1][1])

            pieces.append((html, bold))

    return "".join("<strong>%s</strong>" % html if bold else html for html, bold in pieces)


def heading_id(text):
    """A stable anchor for a section, so the app can deep-link into one.

    Taken from the section number rather than the words -- `#s9` keeps pointing
    at retention after the heading gets reworded, which a slug of the title
    would not.
    """
    numbered = re.match(r"^(\d+(?:\.\d+)*)\.?\s", text)
    if numbered:
        return "s" + numbered.group(1).replace(".", "-")

    return re.sub(r"-+", "-", re.sub(r"[^a-z0-9]+", "-", text.lower())).strip("-")


def render_html(blocks, title, slug, stem):
    body, in_list = [], False

    # The title and the version line are the document's masthead, not two
    # paragraphs that happen to come first. The product name is split off the
    # title so the heading is the document, not the sentence.
    if len(blocks) > 1 and blocks[0].style == "h1" and blocks[1].style == "meta":
        product, _, heading = title.partition(" - ")
        if not heading:
            product, heading = "", title

        body.append('<header class="masthead">')
        if product:
            body.append('<p class="eyebrow">%s</p>' % escape_html(product))
        body.append("<h1>%s</h1>" % escape_html(heading))
        body.append('<p class="meta">%s</p>' % inline_html(blocks[1].units))
        body.append(
            '<a class="action" href="/%s.pdf" download="%s.pdf">%s Download PDF</a>'
            % (slug, stem, DOWNLOAD_ICON))
        body.append("</header>")
        blocks = blocks[2:]

    sections = [(heading_id(plain(b.units)), plain(b.units))
                for b in blocks if b.style == "h2"]
    if len(sections) > 3:
        body.append('<nav class="toc" aria-label="Contents">')
        body.append("<details><summary>Contents</summary>")
        body.append("<ul>")
        body.extend('  <li><a href="#%s">%s</a></li>' % (anchor, escape_html(text))
                    for anchor, text in sections)
        body.append("</ul>")
        body.append("</details>")
        body.append("</nav>")

    for block in blocks:
        if block.style == "bullet":
            if not in_list:
                body.append("<ul>")
                in_list = True
            body.append("  <li>%s</li>" % inline_html(block.units))
            continue

        if in_list:
            body.append("</ul>")
            in_list = False

        if block.style in ("h1", "h2", "h3"):
            tag = block.style
            body.append('<%s id="%s">%s</%s>' % (
                tag, heading_id(plain(block.units)),
                inline_html(block.units, emphasise=False), tag))
        elif block.style == "meta":
            body.append('<p class="meta">%s</p>' % inline_html(block.units))
        else:
            body.append("<p>%s</p>" % inline_html(block.units))

    if in_list:
        body.append("</ul>")

    body.append(
        '<footer><p class="meta">The same document as a PDF: '
        '<a href="/%s.pdf" download="%s.pdf">/%s.pdf</a></p></footer>'
        % (slug, stem, slug))

    page = (
        '<!doctype html>\n<html lang="en">\n<head>\n'
        '<meta charset="utf-8">\n'
        '<meta name="theme-color" content="#ffffff">\n'
        '<meta name="viewport" '
        'content="width=device-width, initial-scale=1, viewport-fit=cover">\n'
        '<title>@TITLE@</title>\n'
        '<style>\n@STYLE@\n</style>\n'
        '</head>\n<body>\n<main>\n@BODY@\n</main>\n</body>\n</html>\n')

    return (page.replace("@TITLE@", escape_html(title))
                .replace("@STYLE@", HTML_STYLE)
                .replace("@BODY@", "\n".join(body)))


def effective_date(markdown, source):
    """PDF date stamp taken from the document's own effective date."""
    match = re.search(r"Effective\s+(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})", markdown)
    if not match:
        raise SystemExit("%s: no 'Effective <d> <Month> <yyyy>' line to date the PDF with" % source)
    day, month, year = match.groups()
    if month.lower() not in MONTHS:
        raise SystemExit("%s: unrecognised month '%s'" % (source, month))
    return "%s%02d%02d000000Z" % (year, MONTHS.index(month.lower()) + 1, int(day))


def render(source, stem, slug):
    """Both representations of one document, as (path, bytes) pairs."""
    markdown = normalise((ROOT / source).read_text(encoding="utf-8"))
    blocks = parse(markdown)
    if not blocks or blocks[0].style != "h1":
        raise SystemExit("%s: must start with a single '# Title' heading" % source)

    title = plain(blocks[0].units)
    subject = plain(blocks[1].units) if len(blocks) > 1 else title

    return [
        (stem + ".pdf",
         build_pdf(paginate(blocks), title, subject, effective_date(markdown, source))),
        (stem + ".html",
         render_html(blocks, title, slug, Path(stem).name).encode("utf-8")),
    ]


def main():
    check = "--check" in sys.argv[1:]
    stale = []

    for source, stem, slug in DOCUMENTS:
        for target, content in render(source, stem, slug):
            path = ROOT / target
            current = path.read_bytes() if path.exists() else None

            if current == content:
                print("unchanged  %s" % target)
                continue
            if check:
                stale.append(target)
                print("STALE      %s" % target)
                continue

            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
            print("wrote      %s (%d bytes)" % (target, len(content)))

    if stale:
        raise SystemExit(
            "\n%d document(s) no longer match docs/legal/. Run:\n"
            "    python scripts/generate-legal-pdfs.py" % len(stale))


if __name__ == "__main__":
    main()
