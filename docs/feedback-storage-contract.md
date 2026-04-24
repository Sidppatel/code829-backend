# Feedback Storage & Rendering Contract

## Server behavior

`GET /v1/feedback` (admin-only) returns each feedback row with a `diagnostics`
field that is **raw JSON text** as submitted by the anonymous `POST /v1/feedback`
endpoint. The server:

- caps the stored diagnostics blob at **16 KB** (`FeedbackController.Submit`).
- stores the JSON verbatim (after size check); no structural validation beyond
  being parseable JSON at submit time.
- sets the response header `X-Feedback-Storage-Format: raw-json` on the list
  endpoint so the admin UI can treat the payload as attacker-controlled data.

## Frontend rendering requirements

Any admin client consuming `/v1/feedback` **must**:

1. Render `diagnostics` as **plain text** — never as HTML, never via
   `dangerouslySetInnerHTML` / `v-html` / `innerHTML`.
2. Wrap display in `String(value)` (or the framework equivalent) before
   inserting into the DOM so a caller that bypasses types cannot smuggle
   non-string content.
3. If showing the parsed object, parse defensively (`JSON.parse` in a
   try/catch) and render leaf values through the same plain-text path.
4. Truncate the display to a UI-appropriate length (e.g. 4 KB) with a
   "show full payload" toggle — the 16 KB server cap is the security limit,
   not a display budget.

The `X-Feedback-Storage-Format` header is advisory: if a future UI supports
multiple formats, branch on the header value. Today only `raw-json` is emitted.

## Why

The feedback form is unauthenticated and the diagnostics blob is carved out of
arbitrary client state (page URL, user agent, free-form reproduction steps).
Treating it as trusted structured data anywhere between the POST endpoint and
the admin render pipeline would hand attackers a stored-XSS sink.
