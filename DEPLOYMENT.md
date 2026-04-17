# 🚀 Production Deployment Guide

> **Stack**: React (Vite) → Cloudflare Pages | ASP.NET Core (.NET 10) → Render | PostgreSQL → Supabase | Redis → Upstash | Keepalive → UptimeRobot

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Supabase — Database Setup](#2-supabase--database-setup)
3. [Upstash — Redis Setup](#3-upstash--redis-setup)
4. [Render — API Hosting](#4-render--api-hosting)
5. [Cloudflare Pages — Frontend](#5-cloudflare-pages--frontend)
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

Copy both. They look like:
```
# Session (migrations)
postgresql://postgres.[ref]:[password]@aws-0-us-east-1.pooler.supabase.com:5432/postgres

# Transaction Pooler (API runtime)
postgresql://postgres.[ref]:[password]@aws-0-us-east-1.pooler.supabase.com:6543/postgres?pgbouncer=true
```

### 2.3 Run EF Core Migrations

Run this **locally**, pointed at your Supabase **Session** connection string (port 5432):

```bash
# From the repo root
cd code829-backend

# Set the connection string temporarily
export DATABASE_URL="postgresql://postgres.[ref]:[password]@...supabase.com:5432/postgres"

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
3. Copy the **Redis URL** — it looks like:
   ```
   redis://default:[password]@[host].upstash.io:6379
   ```
   Or for TLS (recommended):
   ```
   rediss://default:[password]@[host].upstash.io:6380
   ```

> Free tier: 10,000 commands/day, 256MB storage — sufficient for session/cache usage.

---

## 4. Render — API Hosting

### 4.1 Create a Web Service

1. Go to [render.com](https://render.com) → **New → Web Service**
2. Connect your GitHub account and select `Sidppatel/code829-backend`
3. Configure:

| Setting | Value |
|---|---|
| **Name** | `code829-api` |
| **Region** | Same as Supabase (e.g. Oregon / US East) |
| **Branch** | `master` |
| **Runtime** | **Docker** (auto-detected from `Dockerfile`) |
| **Instance Type** | Free |

4. Click **Advanced** → set the following **Environment Variables**:

```
DATABASE_URL         = postgresql://postgres.[ref]:[password]@...supabase.com:6543/postgres?pgbouncer=true
REDIS_URL            = rediss://default:[password]@[host].upstash.io:6380
JWT_SECRET           = <run: openssl rand -hex 32>
ASPNETCORE_ENVIRONMENT = Production
PORT                 = 8000
```

> ⚠️ Use the **Transaction Pooler** connection string (port **6543**) here — NOT the session mode URL.

### 4.2 Health Check Configuration

Render auto-detects the health check from your `Dockerfile`. To confirm:

- Go to your service → **Settings → Health & Alerts**
- Set **Health Check Path** to: `/health/live`

### 4.3 Deploy

Click **Create Web Service** — Render will:
1. Pull your repo
2. Build the Docker image using your `Dockerfile`
3. Run the container on port `8000`
4. Start health checks against `/health/live`

First deploy takes ~3–5 minutes. Once live, copy your service URL:
```
https://code829-api.onrender.com
```

---

## 5. Cloudflare Pages — Frontend

### 5.1 Connect Your Repo

1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com) → **Pages → Create a project**
2. Connect GitHub → select `Sidppatel/code829-frontend`
3. Configure build settings:

| Setting | Value |
|---|---|
| **Framework preset** | Vite |
| **Build command** | `npm run build` |
| **Build output directory** | `dist` |
| **Node.js version** | `20` |

### 5.2 Set Environment Variables

In **Settings → Environment Variables → Production**, add:

```
VITE_API_URL       = https://code829-api.onrender.com
VITE_APP_NAME      = Code829
VITE_DEFAULT_THEME = system
```

> ⚠️ All `VITE_` prefixed vars are baked into the bundle at build time — they are **not secret**. Never put API keys or secrets in `VITE_` variables.

### 5.3 Deploy

Click **Save and Deploy**. Cloudflare will install deps and run `npm run build`. Your site goes live at:
```
https://code829-frontend.pages.dev
```

You can add a custom domain later under **Pages → Custom Domains**.

### 5.4 Handle SPA Routing

React Router requires a `_redirects` file so direct URL visits don't 404. Create this file at `public/_redirects`:

```
/*    /index.html    200
```

This tells Cloudflare Pages to serve `index.html` for all routes and let React Router handle navigation.

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
| **Friendly Name** | `code829-api keepalive` |
| **URL** | `https://code829-api.onrender.com/health/live` |
| **Monitoring Interval** | **5 minutes** |

3. Click **Create Monitor**

That's it. UptimeRobot will now ping your API every 5 minutes 24/7 — keeping Render from spinning it down. It also sends you an **email alert** if your API actually goes down.

> Free tier includes 50 monitors at 5-minute intervals — more than enough.

---

## 7. Environment Variables Reference

### Backend (Render)

| Variable | Example | Notes |
|---|---|---|
| `DATABASE_URL` | `postgresql://...supabase.com:6543/postgres?pgbouncer=true` | Transaction pooler, port 6543 |
| `REDIS_URL` | `rediss://default:pass@host.upstash.io:6380` | Upstash TLS URL |
| `JWT_SECRET` | 64-char hex string | Generate: `openssl rand -hex 32` |
| `ASPNETCORE_ENVIRONMENT` | `Production` | |
| `PORT` | `8000` | Already set in Dockerfile |

### Frontend (Cloudflare Pages)

| Variable | Example | Notes |
|---|---|---|
| `VITE_API_URL` | `https://code829-api.onrender.com` | No trailing slash |
| `VITE_APP_NAME` | `Code829` | |
| `VITE_DEFAULT_THEME` | `system` | |

---

## 8. Post-Deploy Checklist

- [ ] Supabase migrations ran successfully (tables visible in Table Editor)
- [ ] SQL Views created manually (`v_events`, `v_event_summary`, `v_tables`)
- [ ] RLS disabled on all tables
- [ ] pg_cron cleanup jobs scheduled
- [ ] Render API is live at `/health/live` → returns `200`
- [ ] Cloudflare Pages build succeeded and site loads
- [ ] `VITE_API_URL` points to Render API URL (no 404s on API calls)
- [ ] `public/_redirects` file added for SPA routing
- [ ] UptimeRobot monitor is active and showing **Up** status
- [ ] Test a full flow: Register → Browse Events → Book → Payment

---

## Architecture Diagram

```
User Browser
     │
     ▼
Cloudflare Pages (code829-frontend)
  React + Vite | Global CDN | Free
     │
     │ HTTPS API calls to VITE_API_URL
     ▼
Render Web Service (code829-backend/api)
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
