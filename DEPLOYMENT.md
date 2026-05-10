# 🚀 Production Deployment Guide

> **Stack**: React (Vite) → Cloudflare Workers | ASP.NET Core (.NET 10) → Render | PostgreSQL → Supabase | Redis → Upstash | Keepalive → UptimeRobot

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Supabase — Database Setup](#2-supabase--database-setup)
3. [Upstash — Redis Setup](#3-upstash--redis-setup)
4. [Render — API Hosting](#4-render--api-hosting)
5. [Cloudflare Workers — Frontend](#5-cloudflare-workers--frontend)
6. [UptimeRobot — Prevent Cold Starts](#6-uptimerobot--prevent-cold-starts)
7. [Environment Variables Reference](#7-environment-variables-reference)
8. [Post-Deploy Checklist](#8-post-deploy-checklist)

---

## 1. Prerequisites

Make sure you have accounts on all of the following (all free tiers):

| Service | URL | Purpose |
|---|---|---|
| Supabase | https://supabase.com | PostgreSQL database |
| Upstash | https://upstash.com | Redis (serverless) |
| Render | https://render.com | ASP.NET Core API hosting |
| Cloudflare | https://cloudflare.com | Frontend + CDN |
| UptimeRobot | https://uptimerobot.com | API keepalive pings |

You will also need:
- [.NET 10 SDK](https://dotnet.microsoft.com/download) installed locally (for running migrations)
- [Node.js 20+](https://nodejs.org) installed locally

---

## 2. Supabase — Database Setup

### 2.1 Create a Project

1. Go to [supabase.com](https://supabase.com) → **New Project**
2. Set a strong **database password** — save it somewhere safe
3. Choose a region closest to your users (e.g. `us-east-1`)
4. Wait ~2 minutes for provisioning

### 2.2 Get Your Connection Components

Go to **Project Settings → Database → Connection string** and decompose the
pooler endpoint into the five env-var components the system uses everywhere.
**No URL form is ever stored** — neither in source, env files, nor any single
secret value.

| Component | Migrations (Session pooler) | API runtime (Transaction pooler) |
|---|---|---|
| `DB_HOST` | `<host>.pooler.supabase.com` | same |
| `DB_PORT` (this doc / runtime) / `DB_PORT_SESSION` (migrate workflow) | `5432` | `6543` |
| `DB_USER` | `postgres.<project-ref>` | same |
| `DB_NAME` | `postgres` | same |
| `DB_PASSWORD` | (Supabase-issued) | same |

Concrete values live only in Supabase + Render env vars + the `code829-db` repo's `production` GitHub Environment + the runbook at `docs/deployment-internal.md` (untracked) — never in this document, this repo, or chat.

### 2.3 Run Schema Migrations

Migrations are **owned by the sibling `code829-db` repo**. The backend never
applies schema. Production migrations run via that repo's
`.github/workflows/migrate.yml`, gated on the `production` GitHub
Environment's required-reviewer rule. To run locally against a dev DB:

```bash
cd ../code829-db
# Components (DB_HOST etc.) sourced from your local .env.local + Infisical;
# DB_PORT_SESSION = 5432 for DDL.
dotnet run --project src/MigrationRunner
```

> ⚠️ The session pooler (port 5432) is required for DDL. Transaction-mode
> pooling (port 6543) does not support the `SET` commands EF Core uses.

### 2.4 Create SQL Views Manually

EF Core maps views with `ToView()` but doesn't auto-create the SQL. Go to **Supabase Dashboard → SQL Editor** and run your view creation scripts for:
- `v_events`
- `v_event_summary`
- `v_tables`

These are defined in your migrations folder — look for any `CREATE OR REPLACE VIEW` statements.

### 2.5 Disable RLS (for Service Role Access)

Your ASP.NET API uses the service role (server-side), so run this in the **SQL Editor** for each table:

```sql
-- Repeat for each table: users, events, venues, tables, bookings, payments, etc.
ALTER TABLE users DISABLE ROW LEVEL SECURITY;
ALTER TABLE events DISABLE ROW LEVEL SECURITY;
ALTER TABLE venues DISABLE ROW LEVEL SECURITY;
ALTER TABLE tables DISABLE ROW LEVEL SECURITY;
ALTER TABLE bookings DISABLE ROW LEVEL SECURITY;
ALTER TABLE payments DISABLE ROW LEVEL SECURITY;
ALTER TABLE addresses DISABLE ROW LEVEL SECURITY;
ALTER TABLE table_types DISABLE ROW LEVEL SECURITY;
ALTER TABLE magic_link_tokens DISABLE ROW LEVEL SECURITY;
ALTER TABLE refresh_tokens DISABLE ROW LEVEL SECURITY;
ALTER TABLE app_settings DISABLE ROW LEVEL SECURITY;
```

### 2.6 Enable pg_cron for Token Cleanup (Optional but Recommended)

Go to **Database → Extensions** → enable `pg_cron`, then run:

```sql
-- Clean up expired magic link tokens nightly at 2am UTC
SELECT cron.schedule('cleanup-magic-tokens', '0 2 * * *',
  $$DELETE FROM magic_link_tokens WHERE "ExpiresAt" < now() AND "IsUsed" = false$$
);

-- Clean up old refresh tokens nightly
SELECT cron.schedule('cleanup-refresh-tokens', '0 2 * * *',
  $$DELETE FROM refresh_tokens WHERE "ExpiresAt" < now()$$
);
```

---

## 3. Upstash — Redis Setup

Your API uses Redis (see `docker-compose.yml`). Upstash provides a **free serverless Redis** instance.

1. Go to [upstash.com](https://upstash.com) → **Create Database**
2. Choose **Redis** → Region closest to your Render deployment
3. From the Upstash dashboard, decompose the connection details into the five
   component env vars the backend reads (no URL form anywhere):

   | Component | TLS (recommended) | Plaintext |
   |---|---|---|
   | `REDIS_HOST` | `<host>.upstash.io` | same |
   | `REDIS_PORT` | `6380` | `6379` |
   | `REDIS_USER` | `default` | `default` |
   | `REDIS_PASSWORD` | (Upstash token) | same |
   | `REDIS_TLS` | `true` | `false` |

   Concrete host + password values live only in Upstash + the Render env-var
   settings — never in this repo or chat.

> Free tier: 10,000 commands/day, 256MB storage — sufficient for session/cache usage.

---

## 4. Render — API Hosting

Two setup paths. The Blueprint path is preferred — it's IaC and reproducible.

### 4.1 (Preferred) Blueprint via `render.yaml`

`code829-backend/render.yaml` declares the service, Docker build, health check, and required env vars. To provision:

1. In Render → **New → Blueprint** → connect the `code829-backend` repo.
2. Render discovers `render.yaml` and creates the service.
3. Fill in the env vars marked `sync: false` (all secrets — see the file for the list).

Subsequent deploys happen automatically on pushes to `master`.

### 4.2 (Alternative) Manual Web Service

Use this only if you don't want to use the Blueprint.

1. Go to [render.com](https://render.com) → **New → Web Service**
2. Connect your GitHub account and select `Sidppatel/code829-backend`
3. Configure:

| Setting | Value |
|---|---|
| **Name** | `code829-backend` |
| **Region** | Same as Supabase (e.g. Oregon / US East) |
| **Branch** | `master` |
| **Runtime** | **Docker** (auto-detected from `Dockerfile`) |
| **Instance Type** | Free |
| **Health Check Path** | `/health/live` |

4. Click **Advanced** → set the environment variables listed in [Section 7](#7-environment-variables-reference).

> ⚠️ Set `DB_PORT=6543` (Transaction Pooler) on Render — NOT the session-mode 5432 port.

### 4.3 Deploy

Render will:
1. Pull your repo
2. Build the Docker image using `Dockerfile`
3. Run the container on port `10000` (set via `PORT` env var in `render.yaml` and Dockerfile)
4. Start health checks against `/health/live`

First deploy takes ~3–5 minutes. Once live, copy your service URL:
```
https://code829-backend.onrender.com
```

---

## 5. Cloudflare Workers — Frontend

The frontend is **four separate Workers** (one per SPA), each bound to its own subdomain. Deploys are driven by [`code829-frontend/.github/workflows/deploy.yml`](../code829-frontend/.github/workflows/deploy.yml) — pushing to `master` auto-deploys only the apps whose files changed.

| App | Domain | Worker name | Config |
|---|---|---|---|
| Public | `code829.com` | `code829-public` | `apps/public/wrangler.toml` |
| Admin | `admin.code829.com` | `code829-admin` | `apps/admin/wrangler.toml` |
| Developer | `developer.code829.com` | `code829-developer` | `apps/developer/wrangler.toml` |
| Staff | `staff.code829.com` | `code829-staff` | `apps/staff/wrangler.toml` |

SPA routing is handled by Workers' `not_found_handling = "single-page-application"` — no `_redirects` file needed.

Each Worker's `/api/*` path proxies to the Render backend (shared proxy in `tools/cf-worker/apiProxy.ts`). The `public` Worker additionally serves a dynamic `/sitemap.xml`.

### 5.1 Prerequisites (one time)

1. Add your four domains to Cloudflare DNS (orange cloud on each).
2. Create a Cloudflare API token with **Workers Scripts: Edit** + **Account: Read** + **Zone: Read** scopes.
3. Set the following **GitHub Actions secrets** on `code829-frontend`:
   - `CLOUDFLARE_API_TOKEN`
   - `CLOUDFLARE_ACCOUNT_ID`
   - `VITE_API_URL` → `https://code829-backend.onrender.com` (the Render URL from Section 4)

### 5.2 First deploy

Push to `master` (or trigger the workflow manually via the Actions tab — the `workflow_dispatch` input lets you deploy a specific subset).

Workers bind their custom domains automatically from each `wrangler.toml`'s `[[routes]]` block, so no dashboard clicks are required after the initial DNS setup.

> ⚠️ All `VITE_` prefixed vars are baked into the bundle at build time — they are **not secret**. Never put API keys or secrets in `VITE_` variables.

### 5.3 Local dev

Each app runs on its own port (5173/5174/5175/5176) via `pnpm dev:<app>`. Vite proxies `/api/*` to `http://localhost:8000` (the local backend) for same-origin cookies.

---

## 6. UptimeRobot — Prevent Cold Starts

Render's free tier spins down your API after **15 minutes of inactivity**, causing a ~30–50 second cold start on the next request. UptimeRobot pings your API every 5 minutes to keep it alive.

### 6.1 Create a Free Account

1. Go to [uptimerobot.com](https://uptimerobot.com) → **Register for FREE**

### 6.2 Add a Monitor

1. Click **+ Add New Monitor**
2. Configure:

| Field | Value |
|---|---|
| **Monitor Type** | HTTP(s) |
| **Friendly Name** | `code829-backend keepalive` |
| **URL** | `https://code829-backend.onrender.com/health/live` |
| **Monitoring Interval** | **5 minutes** |

3. Click **Create Monitor**

That's it. UptimeRobot will now ping your API every 5 minutes 24/7 — keeping Render from spinning it down. It also sends you an **email alert** if your API actually goes down.

> Free tier includes 50 monitors at 5-minute intervals — more than enough.

---

## 7. Environment Variables Reference

### Backend (Render)

Required (fail-fast — service won't start without these):

| Variable | Example | Notes |
|---|---|---|
| `DB_HOST` | `<host>.pooler.supabase.com` | Supabase pooler hostname |
| `DB_PORT` | `6543` | Transaction pooler — runtime queries |
| `DB_USER` | `postgres.<project-ref>` | |
| `DB_NAME` | `postgres` | |
| `DB_PASSWORD` | (Supabase-issued) | |
| `JWT_SECRET` | 64-char hex string | Generate: `openssl rand -hex 32` |
| `STRIPE_SECRET_KEY` | `sk_live_…` / `sk_test_…` | Live keys required in Production, blocked outside it |

Runtime defaults (set automatically via `render.yaml` / Dockerfile):

| Variable | Value | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | |
| `PORT` | `10000` | Render binds this automatically |
| `FRONTEND_URL` | `https://code829.com` | Used in magic-link emails |
| `CORS_ORIGINS` | `https://code829.com,https://admin.code829.com,...` | All four subdomains |
| `TRUSTED_PROXIES` | Cloudflare IPv4 CIDRs | For `X-Forwarded-For` trust |

Optional (set as needed):

| Variable | Notes |
|---|---|
| `REDIS_HOST` / `REDIS_PORT` / `REDIS_USER` / `REDIS_PASSWORD` / `REDIS_TLS` | Upstash components (host, `6380`, `default`, token, `true`). Omit to skip cache (not recommended for prod) |
| `STRIPE_PUBLISHABLE_KEY` | Client-side key |
| `STRIPE_WEBHOOK_SECRET` | Needed for `/webhooks/stripe` signature verification |
| `RESEND_API_KEY` | Email sending |
| `S3_ACCESS_KEY`, `S3_SECRET_KEY`, `S3_BUCKET`, `S3_ENDPOINT_URL`, `CDN_BASE_URL` | File storage (falls back to local disk if unset) |
| `DATABASE_SSL_MODE` | Override the default `VerifyFull` (non-dev) / `Disable` (dev) |

### Frontend (Cloudflare Workers — set in GitHub Actions secrets)

| Variable | Example | Notes |
|---|---|---|
| `VITE_API_URL` | `https://code829-backend.onrender.com` | No trailing slash. Baked into the JS bundle at build time via `env.VITE_API_URL` in `.github/workflows/deploy.yml`; **never** passed as a Worker runtime var (`wrangler --var` values are readable from the Cloudflare dashboard and must not carry secrets). |
| `VITE_APP_NAME` | `Code829` | |
| `VITE_DEFAULT_THEME` | `system` | |
| `CLOUDFLARE_API_TOKEN` | (secret) | Needs `Workers Scripts: Edit`, `Account: Read`, `Zone: Read` |
| `CLOUDFLARE_ACCOUNT_ID` | (secret) | From Cloudflare dashboard sidebar |

---

## 8. Post-Deploy Checklist

- [ ] Supabase migrations ran successfully (tables visible in Table Editor)
- [ ] SQL Views created manually (`v_events`, `v_event_summary`, `v_tables`)
- [ ] RLS disabled on all tables
- [ ] pg_cron cleanup jobs scheduled
- [ ] Render API is live at `/health/live` → returns `200`
- [ ] All four Cloudflare Workers deployed (`code829-{public,admin,developer,staff}`)
- [ ] All four custom domains respond (`code829.com`, `admin.code829.com`, `developer.code829.com`, `staff.code829.com`)
- [ ] `VITE_API_URL` secret in GitHub points to Render API URL (no 404s on API calls)
- [ ] `/api/events` via each subdomain proxies through to the backend successfully
- [ ] UptimeRobot monitor is active and showing **Up** status
- [ ] Test a full flow: Register → Browse Events → Book → Payment

---

## Architecture Diagram

```
User Browser
     │
     ▼
Cloudflare Workers (4 × code829-frontend)
  public / admin / developer / staff  |  Static Assets + /api proxy
     │                                     │
     │ same-origin /api/* forwarded by Worker
     ▼
Render Web Service (code829-backend)
  ASP.NET Core .NET 10 | Docker | Free
     │                    ▲
     │                    │ ping /health/live every 5 min
     │               UptimeRobot (free keepalive)
     │
     ├──► Supabase PostgreSQL (port 6543 pooler)
     │     EF Core | Free tier | 500MB
     │
     └──► Upstash Redis (TLS port 6380)
           Sessions / Cache | Free tier | 10k req/day
```

---

*Last updated: April 2026*
