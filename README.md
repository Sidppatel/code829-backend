# Code829 Backend — .NET 10 REST API

The server-side API for the Code829 Event Platform. Handles authentication, event management, table bookings, payments, check-in, and all admin/developer operations.

---

## Tech Stack

| Technology | Purpose |
|---|---|
| .NET 10 (C#) | Web API framework |
| Entity Framework Core 10 | ORM + migrations |
| PostgreSQL 16 | Primary database |
| Redis 7 | Caching + rate limiting |
| Stripe.net | Payment processing |
| Serilog | Structured logging |
| FluentValidation | Request validation |
| QRCoder | QR code generation |
| MailKit / Resend | Email delivery |
| AWSSDK.S3 | File storage (production) |
| ClosedXML / CsvHelper | Data export (CSV, XLSX) |
| Scalar | OpenAPI documentation |

---

## Solution Structure

```
backend.slnx
├── api/                    # ASP.NET Web API
│   ├── Controllers/        # 24 controllers (public, admin, developer)
│   ├── Services/           # Business logic (interfaces + implementations)
│   ├── Middleware/          # Error handling, rate limiting, CORS, security headers, correlation IDs
│   ├── Validators/         # FluentValidation request validators
│   ├── Workers/            # Background services (hold cleanup, log cleanup, scheduled publish)
│   ├── Seeding/            # Data seeder (runs on startup)
│   └── Program.cs          # Application entry point & DI configuration
├── contracts/              # Shared DTOs and Enums (no dependencies)
│   ├── DTOs/               # Data transfer objects by domain
│   └── Enums/              # 12 enum definitions
├── db/                     # Data access layer
│   ├── Entities/           # 19 EF Core entity classes
│   ├── Views/              # Database view entities (read-only)
│   ├── Migrations/         # EF Core migrations
│   ├── Repositories/       # Data access repositories
│   ├── Interceptors/       # Change tracking interceptor
│   └── EventPlatformDbContext.cs
├── tests/                  # xUnit test project
│   └── Api.Tests/
├── docker-compose.yml      # Local PostgreSQL + Redis
└── Dockerfile              # Production multi-stage build
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Git

### 1. Clone and Start Infrastructure

```bash
git clone <your-backend-repo-url> code829-backend
cd code829-backend

# Start PostgreSQL and Redis via Docker
docker compose up -d
```

### Boot scripts (monorepo-wide)

Run from the **monorepo root** (`event-platform/`):

| OS            | Full stack              | Backend only                  | Stop                  | Full reset                       |
|---------------|-------------------------|-------------------------------|-----------------------|----------------------------------|
| Windows       | `.\start.ps1`           | `.\start-backend.ps1`         | `.\stop.ps1`          | `.\stop-clear-start.ps1`         |
| Linux / macOS | `./code829-backend/scripts/start.sh` | `./code829-backend/scripts/start-backend.sh` | `./code829-backend/scripts/stop.sh` | `./code829-backend/scripts/stop-clear-start.sh` |

The bash scripts at `code829-backend/scripts/` are feature-equivalent to the PowerShell siblings at the monorepo root. Both read secrets via `infisical export --env=dev` and local-only config from `.env.local`.


Docker Compose provisions:
- **PostgreSQL 16** on port `5432` (user: `ep_dev`, password: `ep_dev_password`, db: `event_platform`)
- **Redis 7** on port `6379`

### 2. Configure Environment

Local dev sources secrets from Infisical (`infisical export --env=dev`) and non-secret docker creds from a gitignored `.env.local` at the monorepo root. Required keys:

| Variable | Purpose |
|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Superuser role for the dev container |
| `REDIS_PASSWORD` | Dev Redis auth |
| `EP_APP_PASSWORD` | Password for the runtime `ep_app` role provisioned by `docker-init/01-extensions-and-roles.sql` |
| `EP_READONLY_PASSWORD` | Password for the reporting `ep_readonly` role |
| `DATABASE_URL`, `REDIS_URL` | Connection strings consumed by the API |

`EP_APP_PASSWORD` and `EP_READONLY_PASSWORD` are read by the Postgres container on first boot via `psql \getenv` — the init SQL never hardcodes values. They must be present before `docker compose up` or init aborts. Rotate by resetting the volume (`stop-clear-start.ps1`) after updating `.env.local`.

### 3. Run Migrations and Start

```bash
# Apply database migrations
dotnet ef database update --project db --startup-project api

# Run the API
dotnet run --project api
```

The API starts at **http://localhost:8000**. OpenAPI docs are available at `/scalar`.

### 4. First Run — Seeded Data

On first startup, `DataSeeder` creates:

| Email | Role | Purpose |
|---|---|---|
| `developer@code829.local` | Developer | Full platform access |
| `admin@code829.local` | Admin | Event organizer |
| `staff@code829.local` | Staff | Check-in operations |
| `user1@code829.local` | User | Test attendee |
| `user2@code829.local` | User | Test attendee |
| `user3@code829.local` | User | Test attendee |
| `organizer@code829.local` | Admin | Secondary organizer |

Plus default app settings (JWT secret, Stripe mock keys, etc.) and 4 table templates.

### 5. Install Git Hooks (recommended)

Install the pre-commit hooks so gitleaks + detect-secrets run on every commit:

```powershell
# Windows
pwsh ./scripts/setup-hooks.ps1
```

```bash
# Linux / macOS
./scripts/setup-hooks.sh
```

Both scripts require `pre-commit` on PATH (`pip install pre-commit detect-secrets`).

---

## Build and Test

```bash
dotnet build              # Build entire solution
dotnet test               # Run xUnit tests
dotnet run --project api  # Start API server
```

### EF Core Migrations

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project db --startup-project api

# Apply pending migrations
dotnet ef database update --project db --startup-project api
```

---

## Architecture

### Request Pipeline (Middleware Order)

1. **SecurityHeadersMiddleware** — X-Content-Type-Options, X-Frame-Options, CSP, HSTS
2. **CORS** — Origins loaded from DB settings
3. **CorrelationIdMiddleware** — Attaches unique ID to every request + Serilog context
4. **RateLimitingMiddleware** — Redis-based (200 req/15min general, 5 req/1min auth)
5. **ErrorHandlingMiddleware** — Catches exceptions, logs to `developer_logs`, returns `ApiError`
6. **Authentication** — JWT Bearer validation
7. **Authorization** — Standard ASP.NET authorization
8. **RoleAuthorizationMiddleware** — `[RequireRole]` attribute enforcement

### Authentication Flow

1. User requests magic link via email (`POST /auth/magic-link`)
2. Backend generates SHA256-hashed token, stores in DB, emails raw token
3. User clicks link → `POST /auth/magic-link/verify` with raw token
4. Backend returns JWT (24h expiry) + refresh token (30d expiry)
5. Refresh tokens support rotation — old token invalidated on use

No passwords are ever stored. First-time users are auto-created on login.

### Role Hierarchy

```
Developer (3) > Admin (2) > Staff (1) > User (0)
```

Higher roles inherit all lower-role permissions. Enforced by `[RequireRole(UserRole.X)]` attribute.

### Payment Flow (Stripe)

1. User creates booking → backend creates Stripe PaymentIntent
2. Frontend collects card via Stripe Elements → confirms payment
3. Stripe webhook notifies backend of payment success/failure
4. For Stripe Connect: destination charges route funds to organizer, platform keeps application fee

### Event Layout Modes

- **Grid**: Predefined table layout. Users lock a table (10-min hold) → book → pay.
- **Open**: Capacity-based. Users select seat count → book → pay.

### Background Workers

| Worker | Schedule | Purpose |
|---|---|---|
| `HoldCleanupWorker` | Every 60s | Releases expired table locks, marks expired bookings |
| `LogCleanupWorker` | Every 24h | Purges old log entries |
| `ScheduledPublishWorker` | Every 60s | Publishes events at their scheduled publish time |

### File Storage

- **Development**: Local filesystem (`uploads/` directory, served as static files)
- **Production**: AWS S3 (configurable bucket, endpoint, access keys)

### Caching Strategy

Redis is used for:
- JWT secret caching (30s TTL)
- App settings caching (30s TTL)
- Rate limiting buckets (per-IP, per-email)
- Table lock coordination

---

## Environment Variables

### Required (Production)

| Variable | Description |
|---|---|
| `DATABASE_URL` | PostgreSQL connection string (use Supabase transaction pooler, port 6543) |
| `DATABASE_SSL_MODE` | `Require` for Supabase |
| `REDIS_URL` | Redis connection URL (`redis://` or `rediss://` for TLS) |
| `JWT_SECRET` | 64-char hex string for JWT signing. Generate: `openssl rand -hex 32` |
| `RESEND_API_KEY` | Resend email API key (starts with `re_`) |
| `EMAIL_FROM_ADDRESS` | Sender email (must match verified Resend domain) |

### Optional (Stripe Connect — Express onboarding)

| Variable | Description |
|---|---|
| `FRONTEND_URL_ADMIN` | Origin of the admin SPA (e.g. `https://admin.code829.com` in prod, `http://localhost:5174` in dev). Used by `StripeConnectService` to build Stripe AccountLink `return_url` / `refresh_url` so admins land back on the settings page after onboarding. Defaults to `http://localhost:5174` if unset. |

### Optional (Production — for S3 file storage)

| Variable | Description |
|---|---|
| `S3_ACCESS_KEY` | S3-compatible access key |
| `S3_SECRET_KEY` | S3-compatible secret key |
| `S3_BUCKET` | S3 bucket name |
| `S3_ENDPOINT_URL` | S3-compatible endpoint URL |
| `CDN_BASE_URL` | CDN URL prefix for serving uploaded images |

### Set in Dockerfile (do not override)

| Variable | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PORT` | `10000` |

### DB-Stored Settings (managed via Developer UI)

These are stored encrypted in the `app_settings` table and managed through the Developer Settings page:

| Key | Default | Mutable via API |
|---|---|---|
| `jwt_secret` | Auto-generated | No |
| `stripe_secret_key` | `MOCK_DEV` | No |
| `stripe_publishable_key` | `MOCK_DEV` | No |
| `stripe_webhook_secret` | `MOCK_DEV` | No |
| `stripe_connected_account_id` | `MOCK_DEV` | No |
| `frontend_url` | `http://localhost:5173` | No |
| `cors_origins` | `http://localhost:5173,http://localhost:5174` | No |
| `resend_api_key` | From env var | No |
| `email_from_address` | From env var | Yes |
| `magic_link_expiry_minutes` | `15` | Yes |
| `hold_expiry_minutes` | `10` | Yes |
| `default_platform_fee_cents` | `1500` ($15) | Yes |
| `platform_fee_percent` | `8` | Yes |
| `brand_name` | `Code829` | Yes |
| `max_tickets_per_booking` | `10` | Yes |
| `search_results_per_page` | `20` | Yes |

---

## Logging

Serilog writes structured logs to:

| Sink | Path | Content |
|---|---|---|
| Console | stdout | All logs |
| Main log file | `logs/log-YYYYMMDD.txt` | All API activity |
| Error log file | `logs/errors-YYYYMMDD.txt` | Warnings + errors only |
| Seeding log file | `logs/seeding-YYYYMMDD.txt` | Startup seeding activity |

Additionally, logs are persisted to database tables:
- `developer_logs` — Unhandled exceptions with stack traces
- `admin_logs` — Audit trail for admin mutations
- `system_logs` — Background worker activity
- `email_logs` — Email delivery tracking

---

## Docker

### Local Development

```bash
docker compose up -d      # Start Postgres + Redis
docker compose down        # Stop containers
docker compose down -v     # Stop + delete volumes (reset data)
```

### Production Build

```bash
docker build -t code829-backend .
docker run -p 10000:10000 --env-file .env code829-backend
```

---

## Deployment

See [DEPLOYMENT.md](./DEPLOYMENT.md) or the root-level [DEPLOYMENT_GUIDE.md](../DEPLOYMENT_GUIDE.md) for full production deployment instructions covering Render, Supabase, Upstash, Resend, and Cloudflare.

### Quick Reference

- **Hosting**: Render (Docker web service)
- **Database**: Supabase PostgreSQL
- **Cache**: Upstash Redis (or Render Redis)
- **Email**: Resend HTTP API
- **Health check**: `GET /health/live`
- **CI/CD**: GitHub Actions (`.github/workflows/ci.yml`) — build + test on push/PR to main

---

## API Documentation

- **Interactive docs**: Available at `/scalar` when running the API
- **Full endpoint reference**: See [docs/API_REFERENCE.md](../docs/API_REFERENCE.md)
- **Database schema**: See [docs/DATABASE.md](../docs/DATABASE.md)

## Architecture & Operations

- **ADRs**: [docs/adr/](./docs/adr/) — architectural decisions (stored-procedure data access, user/admin split, multi-vendor stack, magic-link auth, multi-app frontend, pnpm workspaces, no-FE-calculation rule).
- **HA strategy**: [docs/ha-strategy.md](./docs/ha-strategy.md)
- **Runbooks**: [docs/runbooks/](./docs/runbooks/) — prod bootstrap, staging reset, disaster recovery, secret rotation.
- **Observability**: [docs/observability.md](./docs/observability.md)
