# ADR-0004: Magic-link passwordless auth for ticket buyers

- **Status:** Accepted
- **Date:** 2026-04-23
- **Tags:** auth, security, ux

## Context

Ticket buyers (`users`) interact with the platform sporadically — often once per event, sometimes with month-long gaps. Password-based auth for this population creates a predictable set of problems:

- Forgotten passwords → password-reset flows → essentially magic links anyway.
- Password reuse → breach exposure for a low-value account with credit card history.
- Password storage → bcrypt cost, breach liability, rotation policy.
- Signup friction → ~10% drop-off at the "create a password" step in a checkout flow.

Business users (Admin/Staff/Developer) have the opposite profile: daily usage, sensitive operations, MFA-compatible, familiar with password managers.

## Decision

**Users:** magic-link only. Email → 15-minute single-use token → session cookie. No password column on the `users` table.

**Business users:** email + password + (eventual) TOTP MFA. Password hashed via bcrypt, stored in `business_users.password_hash`.

Magic-link token validation happens atomically via `sp_consume_magic_link` — token is marked consumed in the same transaction that issues the session, preventing replay.

## Consequences

### Positive
- No password-storage liability for the large population.
- Checkout signup ≈ 2 fields (email, name) instead of 4 (email, name, password, confirm).
- Reset flow is the login flow — one code path, less testing surface.
- Single-use + 15-minute expiry + atomic consume protects against link-sharing and replay.

### Negative
- Every login requires email deliverability. Resend outage = Users cannot log in. Mitigated by MailKit SMTP fallback.
- Mobile email clients that preview links can consume tokens — mitigated by requiring explicit click from the recipient's device (token bound to IP + user-agent fingerprint for display-only warning).
- Email latency is user-visible — p95 delivery < 10s is a product requirement.

### Neutral
- Staff/Admin/Developer keep passwords — MFA roadmap lives there.

## Alternatives Considered

### Password for all users
Rejected: bad UX for sporadic buyers, breach liability, support cost on forgotten passwords.

### OAuth (Google/Apple/Facebook)
Rejected for launch: adds third-party dependency chain for the primary login, fragments account ownership. May revisit as an optional secondary flow.

### WebAuthn/passkeys
Rejected for launch: adoption still uneven on older mobile browsers; recovery UX is unsolved. Revisit after passkey adoption > 60%.

## References

- SP: `db/Sql/Procedures/sp_consume_magic_link.sql`
- Service: `api/Services/MagicLinkService.cs`
- Related: ADR-0002 (user vs business_users split)
