# High-availability strategy

Current posture, upgrade path, cost curve. Paired with `docs/runbooks/disaster-recovery.md` (recovery procedures) and ADR-0003 (vendor choices).

## Current posture: single-region, free tier

| Layer          | Vendor             | Region       | Redundancy             |
|----------------|--------------------|--------------|------------------------|
| Backend API    | Render             | Oregon (US-W)| None — single instance |
| Postgres       | Supabase           | US-East-1    | Daily backup, no PITR  |
| Redis          | Upstash            | Global edge  | Vendor-managed replica |
| Object storage | Cloudflare R2      | Cloudflare network (11 regions) | Vendor-managed |
| DNS / CDN      | Cloudflare         | Global       | 300+ POPs              |
| Frontend       | Cloudflare Workers | Global edge  | 300+ POPs              |
| Payments       | Stripe             | Stripe-managed | Stripe SLA           |

**Blast radius:**
- Render region-out → API down until failover (manual, see DR runbook §3).
- Supabase region-out → DB unreachable; we read-only fail, no auto-failover.
- Upstash → degraded cache, no data loss (not authoritative for anything).
- R2 / Cloudflare → edge serves most content; origin-requested assets 503.
- Stripe → checkout disabled; catalog browsing works.

**Effective availability:** ~99.5% (single-region compute) at $0/mo base. Free-tier RTO 1h / RPO 24h.

## Upgrade path

Each row is independent — adopt in any order based on which failure modes matter most.

### Tier 1 — $25–50/mo: eliminate the 24h RPO

| Change                          | Vendor            | Monthly cost | Buys you                              |
|---------------------------------|-------------------|--------------|---------------------------------------|
| Supabase Pro                    | Supabase          | $25          | PITR (7-day), daily backups, 8 GB DB  |
| Upstash Pro (regional replica)  | Upstash           | $0 free tier supports this | Regional HA for cache    |
| Render Starter (2× instances)   | Render            | $7/instance  | HA within region; rolling deploys     |

After Tier 1: RPO ≈ 5 min, RTO ≈ 15 min for single-region failures.

### Tier 2 — $75–150/mo: multi-region backend

| Change                          | Vendor            | Monthly cost | Buys you                              |
|---------------------------------|-------------------|--------------|---------------------------------------|
| Fly.io hot standby (2 regions)  | Fly.io            | $30–50       | Active-passive; Anycast traffic route |
| Cloudflare Load Balancer        | Cloudflare        | $5 + $0.50/origin | Auto DNS failover with health checks |
| Supabase read replicas          | Supabase (Team)   | $599+        | Regional read locality — probably defer |

After Tier 2: single-region-of-cloud-provider outage is survivable. DB remains single-region — still a failure mode, but now the long pole.

### Tier 3 — $600+/mo: multi-region DB

Supabase Team / self-hosted CockroachDB / Neon with regional replicas. Defer until revenue justifies.

## DNS failover procedure (manual, Tier 0)

No auto-failover on free tier. Manual DNS swap is the contingency.

1. **Monitor:** UptimeRobot (free) hits `https://api.code829.com/health/live` every 5 min. Alert to email + Slack on 2 consecutive failures.
2. **Trigger:** on alert, on-call runs DR runbook §3 (Fly.io failover).
3. **DNS swap:** in Cloudflare DNS, edit `api.code829.com` CNAME to point at the Fly.io hostname. TTL is 60s — propagation ≤ 2 min.
4. **Verify:** `curl https://api.code829.com/health/live` from two networks.
5. **Revert:** when Render recovers, flip CNAME back; keep Fly.io warm for 24h.

## DNS failover test procedure (document, do not execute in prod)

Run against staging quarterly:

1. In Cloudflare DNS for staging, change `api-staging.code829.com` CNAME from Render → Fly.io staging.
2. Verify propagation: `dig api-staging.code829.com +short` from three networks (home, mobile, CI runner).
3. Hit `/health/live` against the CNAME — expect 200.
4. Run smoke-test Playwright suite against the staging URL.
5. Flip CNAME back. Record elapsed time in `docs/performance-baseline.md`.

This test validates (a) Fly.io staging actually boots, (b) env-var parity with Render, (c) DNS TTL is honored. Skipping it risks discovering the failover doesn't work during a real outage.

## Session / auth HA notes

Sessions are JWT cookies (stateless) — no sticky-session requirement, any instance can serve any request. This is what makes the multi-region story cheap: no session store to replicate.

If we adopt a Redis-backed session store later, revisit this doc and the DR runbook — Redis becomes part of the critical path.

## Open questions

- Fly.io hot standby: pre-provisioned? Currently assumed in the DR runbook; verify before declaring Tier-2 ready.
- Stripe idempotency key TTL: 24h on Stripe side; if we failover and replay a webhook > 24h later, idempotency protection lapses. Monitor.
- R2 region: R2 is globally replicated by Cloudflare; no action needed. But uploads pinned to a specific region would need regional failover — not a current concern.
