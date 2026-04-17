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

### [AllowAnonymous] endpoint audit
Every endpoint without `[RequireRole]` has been reviewed and is intentionally public:
- `GET /events`, `GET /events/{id}`, `GET /events/{id}/tables`, `GET /events/{id}/ticket-types`, `GET /events/facets`, `GET /events/schema-list` — public catalog; no PII, aggregate/display pricing only.
- `POST /bookings/cancel-beacon`, `POST /tables/release-beacon` — JWT in body, explicit ownership re-check at controller boundary, rate-limited (20/min), log `AUDIT beacon_*_ownership_mismatch` if misused.
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


