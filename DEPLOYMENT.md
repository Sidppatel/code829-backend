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

### 2.2 Get Your Connection Strings

Go to **Project Settings → Database → Connection string**

You need **two** connection strings:

| Use | Mode | Port | When to use |
|---|---|---|---|
| EF Core Migrations | Session | 5432 | Running `dotnet ef` locally |
| API at runtime | Transaction (Pooler) | 6543 | Render env var |

Obtain both strings from the Supabase dashboard — never paste them into this document, a chat log, or a commit. The shapes are:

```
# Session (migrations)
postgresql://<user>:<password>@<host>.pooler.supabase.com:5432/postgres

# Transaction Pooler (API runtime)
postgresql://<user>:<password>@<host>.pooler.supabase.com:6543/postgres?pgbouncer=true
```

Concrete values (project ref, password, regional host) live only in Supabase + the Render env-var settings + the runbook at `docs/deployment-internal.md` (untracked).

### 2.3 Run EF Core Migrations

Run this **locally**, pointed at your Supabase **Session** connection string (port 5432). Obtain the string from the Supabase dashboard; never paste credentials into source control or chat:

```bash
# From the repo root
cd code829-backend

# Source the string from Supabase (dashboard → Project Settings → Database)
export DATABASE_URL="<supabase-session-connection-string>"

# Run migrations
dotnet ef database update --project db --startup-project api
```

> ⚠️ Use port **5432** (session mode) for migrations — PgBouncer transaction mode doesn't support the `SET` commands EF Core uses.

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
3. Obtain the **Redis URL** from the Upstash dashboard. The shape is:
   ```
   redis://default:<password>@<host>.upstash.io:6379
   ```
   Or for TLS (recommended):
   ```
   rediss://default:<password>@<host>.upstash.io:6380
   ```
   Concrete host + password values live in Upstash + the Render env-var settings — never in this repo.

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

> ⚠️ Use the Supabase **Transaction Pooler** connection string (port **6543**) in `DATABASE_URL` — NOT the session mode URL.

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
| `DATABASE_URL` | `postgresql://...supabase.com:6543/postgres?pgbouncer=true` | Transaction pooler, port 6543 |
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
| `REDIS_URL` | Upstash `rediss://…:6380`. Omit to skip cache (not recommended for prod) |
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
