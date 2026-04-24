# Runbook: Staging database reset

Wipe staging user data + re-run bootstrap. Preserves schema and migration history.

## When to use

- Staging has accumulated flaky test data that breaks reproducibility.
- A new feature needs a clean baseline for QA.
- You need to re-demo a flow from scratch.

**Never run this against production.** The script is safe only because it targets staging DSN + tables.

## Procedure

1. **Confirm you are pointing at staging.** Double-check the Supabase project name in the URL bar. Screenshot it. No other confirmation step exists — there is no script-level guard.

2. **Open Supabase SQL editor** for the staging project.

3. **Run the wipe script.** Copy-paste the block below. It truncates user-data tables in FK-safe order; `RESTART IDENTITY` resets sequences; `CASCADE` covers any FKs we missed.

   ```sql
   BEGIN;

   -- Purchases + tickets (leaf tables first)
   TRUNCATE TABLE
       purchase_tickets,
       purchase_tables,
       stripe_transactions,
       purchases
   RESTART IDENTITY CASCADE;

   -- Events + layout
   TRUNCATE TABLE
       event_tables,
       event_ticket_types,
       event_images,
       events,
       tables,
       venue_images,
       venues,
       addresses
   RESTART IDENTITY CASCADE;

   -- Auth + sessions
   TRUNCATE TABLE
       magic_link_tokens,
       user_email_verification_tokens,
       user_password_reset_tokens,
       business_password_reset_tokens,
       device_sessions,
       invitations,
       users,
       business_users,
       business_user_events
   RESTART IDENTITY CASCADE;

   -- Logs + feedback
   TRUNCATE TABLE
       audit_logs,
       business_logs,
       developer_logs,
       system_logs,
       email_logs,
       feedback
   RESTART IDENTITY CASCADE;

   -- Images (leave platform_images if you want branding preserved)
   TRUNCATE TABLE images RESTART IDENTITY CASCADE;

   COMMIT;
   ```

4. **Re-bootstrap.** Follow `docs/runbooks/prod-bootstrap.md`, but against the staging Render service. You should get a clean "Settings: 15 added / Created initial developer" log.

5. **Optionally re-seed demo events.** Staging can set `SEED_DATA=true` to run `DataSeeder` + `VenueEventSeeder` + `LayoutSeeder` + `PurchaseSeeder` on next boot. Unset afterwards.

## What is preserved

- Schema (tables, indexes, constraints, views, stored procedures).
- `__EFMigrationsHistory` — you keep migration state.
- `app_settings` — these are repopulated by `ProdBootstrap` only for missing keys.

## What is lost

- All user-generated content: purchases, events, venues, tickets, logs, uploaded images referenced from `images`.
- All sessions — every staff/user will be logged out.
