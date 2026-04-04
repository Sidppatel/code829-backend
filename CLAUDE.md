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
- JWT secret stored in DB settings, cached in Redis (30s TTL)

### Key Patterns
- `ApiError(statusCode, message, traceId)` for consistent error responses
- Settings stored in DB (`AppSetting` entity), accessed via `ISettingsService`
- Security-critical settings (jwt_secret, stripe keys, smtp credentials, frontend_url, cors_origins) are **immutable via API** — only cosmetic/fee settings are mutable
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
