# Runbook: Production bootstrap

One-shot seeding of a fresh production database. Idempotent — safe to re-run, but you should only ever need it once per environment.

## What it does

1. Applies all pending EF Core migrations.
2. Inserts default `AppSetting` rows (fees, expiry windows, log retention, etc.) for any key missing from the DB.
3. Creates the initial `BusinessUser` with role `Developer`, if and only if `business_users` is empty.

If every step finds existing data, the process logs "already bootstrapped" lines and exits `0` without writing anything.

## Prerequisites

- Production DB is provisioned and reachable via the `DB_HOST` / `DB_PORT` / `DB_USER` / `DB_NAME` / `DB_PASSWORD` components.
- EF migrations from `code829-db/src/Db/Migrations/` match the schema the API expects.
- You have shell/env access to the Render service.

## Procedure

1. **Set the bootstrap env vars on Render** (Service → Environment):

   | Var                           | Value                                          |
   |-------------------------------|------------------------------------------------|
   | `RUN_PROD_BOOTSTRAP`          | `true`                                         |
   | `BOOTSTRAP_DEVELOPER_EMAIL`   | owner email, e.g. `owner@code829.com`          |
   | `BOOTSTRAP_DEVELOPER_PASSWORD`| strong password, ≥ 12 chars (changed post-run) |
   | `BOOTSTRAP_DEVELOPER_FIRST_NAME` | optional, default `Platform`                |
   | `BOOTSTRAP_DEVELOPER_LAST_NAME`  | optional, default `Owner`                   |
   | `FRONTEND_URL`                | prod URL, e.g. `https://code829.com`           |
   | `CORS_ORIGINS`                | comma-separated prod portal URLs               |

2. **Trigger a deploy** (Render auto-deploys on env change, or click "Manual Deploy").

3. **Watch logs.** Expected sequence:
   ```
   [ProdBootstrap] Starting
   [ProdBootstrap] Migrations applied
   [ProdBootstrap] Settings: 15 added, rest already present
   [ProdBootstrap] Created initial developer: owner@code829.com
   [ProdBootstrap] Complete
   [ProdBootstrap] Exiting 0 — unset RUN_PROD_BOOTSTRAP and redeploy to start server
   ```
   The process exits — Render will mark the deploy as failed because the web port never opens. That is expected.

4. **Unset `RUN_PROD_BOOTSTRAP`** (delete the env var) and all four `BOOTSTRAP_DEVELOPER_*` vars.

5. **Redeploy.** The server starts normally this time.

6. **Log in** with the seeded developer credentials at the Developer portal, then **rotate the password** via the UI immediately.

## Re-running

Safe. Re-triggering with `RUN_PROD_BOOTSTRAP=true` against a populated DB logs:
```
[ProdBootstrap] Settings: 0 added, rest already present
[ProdBootstrap] Initial developer: already present
```
and exits `0` without writing.

## Troubleshooting

- **Process starts the web server instead of bootstrapping:** `ASPNETCORE_ENVIRONMENT` must be `Production`. Bootstrap guard requires both `IsProduction()` and `RUN_PROD_BOOTSTRAP=true`.
- **Throws `BOOTSTRAP_DEVELOPER_EMAIL must be set`:** you set `RUN_PROD_BOOTSTRAP` without the `BOOTSTRAP_DEVELOPER_*` vars. Unset `RUN_PROD_BOOTSTRAP` or set the vars.
- **Throws `password must be at least 12 characters`:** use a stronger password.
- **Migration fails:** bootstrap aborts with non-zero exit. Check migration state; fix in a code deploy first, then retry bootstrap.
