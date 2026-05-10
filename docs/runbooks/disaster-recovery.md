# Runbook: Disaster recovery

Per-vendor failure procedures for production. Assumes multi-vendor stack (ADR-0003): Supabase (DB), Upstash (Redis), Cloudflare R2 (storage), Render (compute), Cloudflare (DNS/CDN), Stripe (payments).

## RTO / RPO targets (free tier)

- **RTO (Recovery Time Objective):** 1 hour — time to restore read-only service.
- **RPO (Recovery Point Objective):** 24 hours — worst-case data loss window, bounded by Supabase daily backup.

These tighten to RTO 15 min / RPO 5 min on Supabase Pro (PITR) + Fly.io multi-region — see `docs/ha-strategy.md`.

## 1. Supabase database lost or corrupted

**Symptoms:** API boots fail at migration with Npgsql connection errors, or every query returns zero rows, or 5xx spike in Sentry with "relation does not exist".

1. **Freeze writes.** Scale Render service to 0 instances (Render dashboard → Settings → Instance Count). Prevents partial data being written to a wounded DB.
2. **Open Supabase → Project → Database → Backups.** Pick the most recent daily backup before corruption window.
3. **Restore in place** (Supabase restores to the same project; host/credentials stay the same) OR **restore to new project** (host/credentials change — update the `DB_HOST` / `DB_USER` / `DB_PASSWORD` env vars on Render afterwards).
4. If restored to new project: update the affected `DB_*` env vars on Render. Do not restart yet.
5. **Verify migrations match code.** Compare `__EFMigrationsHistory` count vs `code829-db/src/Db/Migrations/` files. If code is ahead of restored DB, run the migrate workflow in the `code829-db` repo (`.github/workflows/migrate.yml`) — manual `workflow_dispatch` is the path here, since the change isn't a normal push to master.
6. **Scale Render back to 1 instance.** Watch logs — expect `Database migrations applied` and healthy `/health/live`.
7. **Run ProdBootstrap if the restore was to an empty DB** (shouldn't happen if backup was valid, but the bootstrap is idempotent — safe to re-run).
8. **Reconcile Stripe** (see section 4) for the RPO window between backup time and failure.

## 2. Redis flushed / Upstash outage

**Symptoms:** Cache miss rate spikes; no functional impact unless sessions live in Redis.

1. **Check session store.** Current posture: sessions are JWT cookies (stateless), not Redis-backed. Magic-link tokens live in Postgres (`magic_link_tokens` table), not Redis. Redis holds rate-limit counters + query cache only.
2. **No action needed for data integrity.** Cache regenerates on next hit. Rate-limit counters reset to 0 — briefly permissive, but not catastrophic.
3. **If Upstash is fully down:** the app degrades but does not fail. StackExchange.Redis returns timeouts; callers should treat cache reads as miss. Verify `RedisConnectionRetryPolicy` in `Program.cs` does not deadlock.
4. **Recovery:** Upstash restores automatically. No manual step.

Future change (sessions → Redis): update this runbook and define a rehydration path before cutover.

## 3. Render outage

**Symptoms:** Render status page red, health checks failing, 502 from Cloudflare.

Current posture: single-region Render deploy. No hot standby. Manual contingency = deploy to Fly.io.

1. **Confirm it's Render, not us.** Check https://status.render.com and Cloudflare Analytics (5xx origin vs 5xx Cloudflare).
2. **If expected recovery < 30 min:** wait it out. Cloudflare returns a branded 503 page (configure via Custom Error Pages).
3. **If > 30 min or indefinite — failover to Fly.io:**
   a. `fly deploy` from the `code829-backend` repo (Fly.io app is pre-provisioned as cold standby; Dockerfile already suited).
   b. Fly.io app needs env vars mirrored from Render. Copy via `fly secrets set` or Infisical sync.
   c. Update Cloudflare DNS `api.code829.com` CNAME to the Fly.io hostname. TTL of 60s means cutover in ~1 min.
   d. Verify `/health/live` on new origin.
4. **Back to Render** once it recovers: flip DNS back, keep Fly.io warm for 24h before scaling to 0.

## 4. Stripe reconciliation

**When to run:** after a DB restore (section 1), after a suspected billing incident, or monthly as a baseline.

1. **Export Stripe PaymentIntents** for the period from Stripe Dashboard → Payments → Export.
2. **Query our record:**
   ```sql
   SELECT stripe_payment_intent_id, amount_cents, status, created_at
   FROM stripe_transactions
   WHERE created_at >= '<start>' AND created_at < '<end>'
   ORDER BY created_at;
   ```
3. **Diff.** Look for:
   - PaymentIntent `succeeded` in Stripe, no matching `stripe_transactions` row → missed webhook.
   - `stripe_transactions` row with `status = 'pending'` older than 1 hour → orphaned intent; check Stripe for actual state.
   - Amount mismatch → `PAYMENT_AMOUNT_MISMATCH` should have logged at booking time; investigate fraud/bug.
4. **Replay missed webhooks** with Stripe CLI:
   ```bash
   stripe events resend evt_XXX
   ```
   Our webhook handler is idempotent (deduped by `stripe_event_id`).
5. **Manual correction:** if an intent succeeded in Stripe but the purchase is missing, do NOT insert the purchase manually. Contact the customer, confirm the charge, refund or re-issue a ticket via the admin flow.

## Post-incident

- Update `docs/performance-baseline.md` and `docs/ha-strategy.md` if the incident surfaced a gap.
- Write a blameless postmortem: what broke, what the detection time was, what the RTO actually was, what we'd change.
- If Stripe reconciliation found drift, file a ticket to tighten webhook monitoring.
