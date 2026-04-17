# Code829 Backend

## Tech Stack

- **.NET 10** (net10.0), C# with nullable enabled, implicit usings
- **Entity Framework Core 10** + Npgsql (PostgreSQL)
- **Serilog** for logging (console + file sinks)
- **Stripe.net** for payments
- **MailKit** / Resend for email
- **StackExchange.Redis** for caching
- **FluentValidation** for request validation
- **QRCoder** for QR code generation
- **AWSSDK.S3** for file storage (with local fallback)
- **Scalar** for API docs (OpenAPI)

## Solution Structure

```
backend.slnx
├── api/           # ASP.NET Web API — controllers, services, middleware, workers, seeding, validators
├── contracts/     # Shared DTOs and Enums (no dependencies)
├── db/            # EF Core DbContext, entities, migrations, views
└── tests/         # Api.Tests (xUnit + Moq)
```

## Build & Run

```bash
dotnet build                          # Build entire solution
dotnet test                           # Run tests
dotnet run --project api              # Run API (listens on http://localhost:8000)
dotnet ef migrations add <Name> --project db --startup-project api
```

## Architecture

### Layers
- **Controllers** — Thin, handle HTTP concerns only. Delegate to services.
- **Services** — Business logic. Each has an interface (`IXxxService`) and implementation.
- **Entities** — EF Core models in `db/Entities/`. `BaseEntity` provides `Id`, `CreatedAt`, `UpdatedAt`.
- **DTOs** — In `contracts/DTOs/`, organized by domain. Never expose entities directly.
- **Validators** — FluentValidation validators in `api/Validators/`.
- **Middleware** — Custom middleware for error handling, CORS, rate limiting, role auth, correlation IDs, security headers.
- **Workers** — Background services: `HoldCleanupWorker`, `LogCleanupWorker`, `ScheduledPublishWorker`.

### Auth & Roles
- JWT Bearer authentication configured in `Program.cs`
- Magic link passwordless login (no passwords stored)
- Role hierarchy: **Developer > Admin > Staff > User**
- `[RequireRole(UserRole.X)]` attribute for endpoint authorization
- `[AllowAnonymous]` only where explicitly needed (public endpoints, beacon)
- JWT secret loaded from `JWT_SECRET` environment variable via `ISecretsProvider`

### CSRF posture
- All authenticated requests use `Authorization: Bearer <jwt>` headers — **no auth cookies**.
  Beacon endpoints pass the JWT in the request body (unavoidable: `navigator.sendBeacon` can't set headers) and each beacon delegates to a service that re-validates ownership.
- Because the API doesn't accept session cookies, classic CSRF (cookie auto-attach) is not applicable and anti-forgery tokens are not required.
- If a cookie-based auth path is ever added (e.g., admin SSR), add anti-forgery then.

### Payment integrity
- Stripe is mandatory in every environment (no mock service). Missing `STRIPE_SECRET_KEY` fails startup; live keys are required in `Production` and blocked outside it.
- `BookingService.ConfirmPaymentAsync` and `WebhooksController.HandlePaymentIntentSucceeded` both fetch the PaymentIntent and reject if `AmountReceived != StripeTransaction.AmountCents` — logged as `PAYMENT_AMOUNT_MISMATCH`.
- Pricing is computed exclusively by `PricingService`; `BookingService` and `POST /bookings/quote` both call it, so quote math and booking math are guaranteed identical.
- Open-capacity bookings use `sp_reserve_open_capacity` which takes a row-level lock on the event and validates capacity + ticket-type quota atomically.

### Key Patterns
- `ApiError(statusCode, message, traceId)` for consistent error responses
- **Secrets** (JWT, Stripe, Resend, S3/CDN keys) are in environment variables, accessed via `ISecretsProvider` (singleton)
- **Runtime config** (app_name, fees, feature flags, URLs) stored in DB (`AppSetting` entity), accessed via `ISettingsService`
- Only non-sensitive settings are mutable via the Developer API — secrets require env var changes + restart
- Stripe PaymentIntents for payment flow; webhook at `/webhooks/stripe`
- File storage abstracted behind `IFileStorageService` (local dev / S3 prod)
- Admin log auditing via `IAdminLogService`

### Controller Naming
- `EventsController` — Public endpoints
- `AdminEventsController` — Admin (organizer) endpoints with ownership checks
- `DeveloperController` — Platform-wide developer endpoints
- `DeveloperEventsController` — Developer event management (cross-organizer)

### Database
- PostgreSQL 16 via Docker
- EF Core code-first migrations in `db/Migrations/`
- DB views in `db/Entities/Views/` (EventView, EventSummaryView, TableView)
- Seeding in `api/Seeding/` — runs on startup in development

### Environment
- Config via `.env` file (see `.env.example`)
- Connection strings: `DefaultConnection` (Postgres), `Redis`
- Docker Compose for local Postgres + Redis

## Conventions

- Async all the way — all service methods are async, suffixed with `Async`
- Nullable reference types enabled — use `!` only inside async closures where null is already checked
- Use typed generics in service responses, not `any`/`object`
- All API calls from frontend go through domain-specific service files
- Keep controllers thin — business logic belongs in services
- Ownership checks required on all admin mutation endpoints (compare `OrganizerId` to current user)

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **code829-backend** (1600 symbols, 4051 relationships, 129 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> If any GitNexus tool warns the index is stale, run `npx gitnexus analyze` in terminal first.

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `gitnexus_impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `gitnexus_detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `gitnexus_query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `gitnexus_context({name: "symbolName"})`.

## When Debugging

1. `gitnexus_query({query: "<error or symptom>"})` — find execution flows related to the issue
2. `gitnexus_context({name: "<suspect function>"})` — see all callers, callees, and process participation
3. `READ gitnexus://repo/code829-backend/process/{processName}` — trace the full execution flow step by step
4. For regressions: `gitnexus_detect_changes({scope: "compare", base_ref: "main"})` — see what your branch changed

## When Refactoring

- **Renaming**: MUST use `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` first. Review the preview — graph edits are safe, text_search edits need manual review. Then run with `dry_run: false`.
- **Extracting/Splitting**: MUST run `gitnexus_context({name: "target"})` to see all incoming/outgoing refs, then `gitnexus_impact({target: "target", direction: "upstream"})` to find all external callers before moving code.
- After any refactor: run `gitnexus_detect_changes({scope: "all"})` to verify only expected files changed.

## Never Do

- NEVER edit a function, class, or method without first running `gitnexus_impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `gitnexus_rename` which understands the call graph.
- NEVER commit changes without running `gitnexus_detect_changes()` to check affected scope.

## Tools Quick Reference

| Tool | When to use | Command |
|------|-------------|---------|
| `query` | Find code by concept | `gitnexus_query({query: "auth validation"})` |
| `context` | 360-degree view of one symbol | `gitnexus_context({name: "validateUser"})` |
| `impact` | Blast radius before editing | `gitnexus_impact({target: "X", direction: "upstream"})` |
| `detect_changes` | Pre-commit scope check | `gitnexus_detect_changes({scope: "staged"})` |
| `rename` | Safe multi-file rename | `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` |
| `cypher` | Custom graph queries | `gitnexus_cypher({query: "MATCH ..."})` |

## Impact Risk Levels

| Depth | Meaning | Action |
|-------|---------|--------|
| d=1 | WILL BREAK — direct callers/importers | MUST update these |
| d=2 | LIKELY AFFECTED — indirect deps | Should test |
| d=3 | MAY NEED TESTING — transitive | Test if critical path |

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/code829-backend/context` | Codebase overview, check index freshness |
| `gitnexus://repo/code829-backend/clusters` | All functional areas |
| `gitnexus://repo/code829-backend/processes` | All execution flows |
| `gitnexus://repo/code829-backend/process/{name}` | Step-by-step execution trace |

## Self-Check Before Finishing

Before completing any code modification task, verify:
1. `gitnexus_impact` was run for all modified symbols
2. No HIGH/CRITICAL risk warnings were ignored
3. `gitnexus_detect_changes()` confirms changes match expected scope
4. All d=1 (WILL BREAK) dependents were updated

## Keeping the Index Fresh

After committing code changes, the GitNexus index becomes stale. Re-run analyze to update it:

```bash
npx gitnexus analyze
```

If the index previously included embeddings, preserve them by adding `--embeddings`:

```bash
npx gitnexus analyze --embeddings
```

To check whether embeddings exist, inspect `.gitnexus/meta.json` — the `stats.embeddings` field shows the count (0 means no embeddings). **Running analyze without `--embeddings` will delete any previously generated embeddings.**

> Claude Code users: A PostToolUse hook handles this automatically after `git commit` and `git merge`.

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
