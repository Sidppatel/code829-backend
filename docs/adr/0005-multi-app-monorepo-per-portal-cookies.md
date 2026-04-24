# ADR-0005: Multi-app frontend monorepo with per-portal session cookies

- **Status:** Accepted
- **Date:** 2026-04-23
- **Tags:** frontend, auth, security

## Context

Four distinct frontends share UI primitives, data models, and a single backend:

- **Public** (`:5173`) — ticket buyers, unauthenticated + User role.
- **Admin** (`:5174`) — event organizers, Admin role.
- **Staff** (`:5175`) — on-site ops, Staff role.
- **Developer** (`:5176`) — platform owner, Developer role.

Options for packaging:
1. One SPA with role-based routing. Single bundle, but every visitor downloads staff + developer screens they'll never see.
2. Four separate repos. No shared code reuse.
3. One monorepo, four apps, shared package.

Auth surface: originally a single JWT-in-Authorization-header scheme with token in `localStorage`. Session 9 hardening removed body-JWT and moved to HttpOnly session cookies.

## Decision

**pnpm workspace monorepo**, four Vite apps, shared logic in `packages/shared` (auth, axios, queries, stores, components, types).

**Per-portal session cookies.** Each app sets its own cookie at login:
- `admin_session`, `staff_session`, `developer_session` — `SameSite=Strict`.
- `user_session` — `SameSite=Lax` (top-level nav attaches for email-link UX; cross-origin fetch/XHR/beacon still won't).

**CSRF mitigation via `X-Portal` header.** Every request carries `X-Portal: admin|staff|developer|user`, set by `configureApiClient` at boot. Being a custom header, it forces a CORS preflight on every cross-origin request; the preflight is rejected by our strict `CORS_ORIGINS` allow-list. Anti-forgery tokens are therefore redundant.

## Consequences

### Positive
- **Bundle size:** each app ships only its screens; public bundle is not polluted by admin tooling.
- **Attack surface isolation:** cookie theft in one portal does not authorize requests to another.
- **Independent deploys:** four Cloudflare Worker deployments, each with its own cache/KV.
- **Shared types stay honest:** `packages/shared` forces one source of truth for DTOs and business rules.

### Negative
- Four `vite.config.ts` files to keep in sync — mitigated by sharing base config from `packages/shared`.
- CORS config must list every portal's origin — four origins per environment.
- Beacons (`navigator.sendBeacon`) rely on the session cookie (the browser attaches it automatically). Beacon endpoints must be careful with SameSite=Lax on the user cookie — only User-facing beacons attach; cross-origin fetch POSTs do not.

### Neutral
- `X-Portal` header doubles as a telemetry attribute (which portal issued the request).

## Alternatives Considered

### Single SPA with role-based routing
Rejected: bundle bloat, shared cookie surface enlarges CSRF blast radius.

### Four separate repos
Rejected: no structural enforcement of shared DTO types; four dependency-upgrade PRs per library bump.

### Single shared session cookie with role claim
Rejected: cross-portal CSRF blast radius. Strict SameSite on Admin/Staff/Developer cookies is a strong defense — a shared cookie would have to be the weakest of the four (User's Lax).

### Anti-forgery token (double-submit cookie)
Rejected: `X-Portal` custom header achieves the same guarantee (forced preflight + origin allow-list) with zero bookkeeping.

## References

- `packages/shared/src/lib/axios.ts`
- `api/Services/AdminAuthController.cs` — `SetSessionCookie`
- Memory: `session_8_portal_cookie_audit.md`, `session_9_auth_dedupe.md`
- Related: ADR-0006 (pnpm workspace layout)
