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
├── api/                # ASP.NET Web API — controllers, services, middleware, workers, seeding, validators
├── contracts/          # Shared DTOs and Enums (no dependencies)
├── db/                 # EF Core DbContext, entities, migrations, views, SP wrappers, raw SQL
├── tests/Api.Tests/    # xUnit + Moq
└── tools/Analyzers/    # Roslyn analyzers (EP0001 enforces Data Access Rule)
```

## Build & Run

```bash
dotnet build                          # Build entire solution (analyzer EP0001 fires here)
dotnet test                           # Run xUnit tests
dotnet run --project api              # Run API (listens on http://localhost:8000)
dotnet ef migrations add <Name> --project db --startup-project api
dotnet ef database update --project db --startup-project api
```

The boot scripts at the monorepo root (`..\start.ps1`, `..\start-backend.ps1`) handle Docker + secrets + migrations + run in one shot. Use them for normal dev — invoke `dotnet run` directly only when you need a debugger or detailed control.

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
- All authenticated requests use **session cookies** (HttpOnly, set at login by `SetSessionCookie`). No JWT is emitted in the response body; no `Authorization: Bearer` header is read by the server for normal requests.
- **CSRF mitigation:** Every frontend request sends `X-Portal: admin|staff|developer|user` (set at app boot via `configureApiClient`). This custom header makes every request non-simple under CORS, triggering a preflight. CORS is locked to `CORS_ORIGINS` env var — unknown origins fail preflight and never reach the server. Anti-forgery tokens are therefore not required.
- Admin/staff/developer cookies use `SameSite=Strict` — cross-site requests never attach these cookies at all.
- User cookie uses `SameSite=Lax` — attaches on top-level navigations (safe) but not on cross-origin fetch/XHR/sendBeacon POST. All state-mutating endpoints are POST/PUT/PATCH/DELETE, so a cross-site GET cannot trigger mutations.
- **Beacon endpoints** (`POST /purchases/cancel-beacon`, `POST /tables/release-beacon`) authenticate via session cookie. `navigator.sendBeacon` sends cookies automatically (browser-managed). Both endpoints use `[Authorize]` + `[RequireRole(UserRole.User)]` and read `userId` from `User.FindFirst(ClaimTypes.NameIdentifier)`.

### [AllowAnonymous] endpoint audit

### [AllowAnonymous] endpoint audit
Every endpoint without `[RequireRole]` has been reviewed and is intentionally public:
- `GET /events`, `GET /events/{id}`, `GET /events/{id}/tables`, `GET /events/{id}/ticket-types`, `GET /events/facets`, `GET /events/schema-list` — public catalog; no PII, aggregate/display pricing only.
- (Beacons are now `[Authorize]` — removed from this list in Session 9.)
- `GET /bookings/stripe-config` — publishable key only; env-gated (503 if unconfigured; live keys blocked outside production).
- `GET /developer/logo` — public branding asset.
- `POST /feedback` — public form, rate-limited via default bucket.
- `GET /tickets/claim` — claim token is the authenticator; tokens expire.
- `POST /webhooks/stripe` — Stripe HMAC signature validates authenticity.
- `/auth/*` — magic link / dev-login / admin-login; each rate-limited 5/min.

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
- **Secrets** sourced from Infisical (`infisical export --env=dev --format=dotenv`) — no `.env` files in the repo
- **Local-only config** (docker creds, localhost URLs) read from monorepo-root `.env.local`
- Connection strings: `DefaultConnection` (Postgres), `Redis`
- Docker Compose for local Postgres + Redis (`docker-compose.yml`)
- Production: env vars set on Render (backend) + Supabase (DB)

## Conventions

- Async all the way — all service methods are async, suffixed with `Async`
- Nullable reference types enabled — use `!` only inside async closures where null is already checked
- Use typed generics in service responses, not `any`/`object`
- All API calls from frontend go through domain-specific service files
- Keep controllers thin — business logic belongs in services
- Ownership checks required on all admin mutation endpoints (compare `OrganizerId` to current user)

## Data Access Rule (architectural)

**The API must never read or write tables directly via EF Core LINQ.** All data access goes through:
- Stored Procedures (`sp_*`) called via `SqlQueryRaw<T>()` / `ExecuteSqlRawAsync()` — wrapped in `db/Repositories/StoredProcedures/`
- SQL Functions (`SELECT * FROM sp_foo(...)`)
- Views (keyless entities mapped in `OnModelCreating`, e.g., `context.EventViews`, `context.UserProfileViews` — these ARE views and are fine)

Forbidden examples on non-view DbSets: `context.Users.FirstOrDefaultAsync(...)`, `context.Events.Add(...)`, `context.Bookings.AnyAsync(...)`, `context.Tables.Where(...).ToListAsync()`, `context.AdminUsers.CountAsync()`.

**Exceptions:** `api/Seeding/**`, `tests/**`, and `db/Repositories/*.cs` (legacy low-level adapters, excluding the `StoredProcedures/` subfolder) are path-whitelisted. For a specific site, annotate with `[AllowDirectDbAccess("reason")]` on the method/class, or put `// ARCH-EXCEPTION: <reason>` on the invocation line.

**Roslyn analyzer `EP0001`** in `tools/Analyzers/` enforces this at build time at **Error** severity — new direct-DbSet access fails `dotnet build`. Intentional exceptions use inline `// ARCH-EXCEPTION: <reason>` comments (dozens of these exist in legacy controllers where the read+mutate pattern would require deep SP redesign; grep for them).

**The analyzer allows**: `FromSqlRaw`, `FromSqlInterpolated`, and `FromSql` on any DbSet (that's the SP-call escape hatch). View DbSets (property name ends in `Views`) are always allowed.

**When adding new data access:**
- For reads that need entity materialization: create an `sp_*` function returning `SETOF <tablename>` and call via `context.Entities.FromSqlRaw("SELECT * FROM sp_foo({0})", arg).Include(...).FirstOrDefaultAsync()`.
- For projections/aggregations: prefer a view (`v_*`) registered in `OnModelCreating` and queried through its DbSet.
- For writes: create `sp_create_*` / `sp_update_*` / `sp_delete_*` returning whatever the caller needs (uuid of new row, void, etc.).
- For existence checks: prefer `SELECT EXISTS(...)` via `sp_*_exists_*` returning `bool`.

**PR checklist:** reviewer confirms no new `context.<NonViewTable>.<EFMethod>` outside `Seeding/` or `Tests/`.

## Roslyn Analyzers (`tools/Analyzers/`)

Wired into `api.csproj` as `<ProjectReference ... OutputItemType="Analyzer">`. Builds a custom DLL referenced by the API project.

- **EP0001** — enforces the Data Access Rule (above). Severity: Error. Fails `dotnet build` on violation.
- Add new analyzers in this project; document the rule + escape hatch here when you do.

## Required Skills

To work on the backend you need fluency in:

- **C# 13 / .NET 10** — async/await, nullable reference types, records, pattern matching
- **ASP.NET Web API** — controllers, model binding, attribute routing, action filters, middleware pipeline
- **EF Core 10** — code-first migrations, `FromSqlRaw`/`FromSqlInterpolated`, keyless entities for views, `Include`
- **PostgreSQL 16** — writing stored procedures (PL/pgSQL), functions, views, row-level locking (`FOR UPDATE`), `SETOF` returns
- **Roslyn analyzers** (only when extending `tools/Analyzers/`) — `DiagnosticAnalyzer`, syntax/symbol walking
- **xUnit + Moq** — `Theory`, `InlineData`, `Setup(...).Returns(...)`, `Verify(...)`
- **JWT / OAuth** — bearer tokens, claims, custom auth handlers
- **Stripe.net** — `PaymentIntentService`, webhook signature verification (`EventUtility.ConstructEvent`)
- **Serilog** — structured logging, sinks, enrichers
- **StackExchange.Redis** — `IConnectionMultiplexer`, key conventions, TTLs
- **FluentValidation** — `AbstractValidator<T>`, rule chains, custom validators
- **Docker Compose** — for local Postgres + Redis
- **Infisical CLI** — `export`, `secrets set`

Domain-specific knowledge that compounds: Stripe payment lifecycle (intent → charge → webhook → reconciliation), magic-link auth issuance/expiry, S3 presigned URLs, QR-code claim tokens.

See [../SKILLS.md](../SKILLS.md) for the full list of project-specific (non-generic) backend skills.

## Application Map (graphify) — required workflow

The dependency map for this repo lives in `graphify-out/` (and a monorepo-wide one at `..\graphify-out\`). Index: [../APPLICATION_MAP.md](../APPLICATION_MAP.md).

**Before changes:**
- Read the wiki pages for files you'll touch (`graphify-out/wiki/<Name>.md`).
- Note god nodes (highest connectivity) in `GRAPH_REPORT.md` — they have non-obvious downstream consumers.
- Use `graphify explain "<NodeName>"` or `graphify query "<question>"` for targeted lookups.

**After every commit:**
- `graphify update .` (run automatically by post-commit hook if installed via `graphify hook install`).
- The map being out of sync = unfinished change.
