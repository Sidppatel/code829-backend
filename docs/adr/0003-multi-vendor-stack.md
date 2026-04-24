# ADR-0003: Multi-vendor hosting stack

- **Status:** Accepted
- **Date:** 2026-04-23
- **Tags:** infrastructure, hosting, cost

## Context

Pre-revenue product. Needs production-grade Postgres, Redis, object storage, DNS/CDN, backend compute, frontend hosting — all within free or near-free tiers. Must be portable enough to swap any single vendor without rewriting application code.

Single-cloud options (AWS, GCP, Azure) offer tight integration but force a $20–100/mo floor even idle, and lock business logic into vendor primitives (RDS Proxy, CloudFront Functions, IAM roles) that are painful to migrate off.

## Decision

Best-of-breed managed vendors on free tier, wired together by environment variables:

| Concern           | Vendor             | Why                                                   |
|-------------------|--------------------|-------------------------------------------------------|
| Postgres          | Supabase           | Free tier with daily backups, PITR on paid, pgvector. |
| Redis             | Upstash            | Pay-per-request, generous free tier, REST fallback.   |
| Object storage    | Cloudflare R2      | Zero egress fees — critical for ticket-image CDN.     |
| Backend compute   | Render             | Native .NET support, free web service tier.          |
| Frontend          | Cloudflare Workers | Edge-rendered SPAs, free requests tier.               |
| DNS + CDN + WAF   | Cloudflare         | Anchors the multi-vendor story.                       |
| Error tracking    | Sentry             | Free tier covers our volume.                          |
| Observability     | Grafana Cloud      | OTLP ingest, free tier (50 GB logs, 10k metrics).     |
| Email             | Resend             | Free tier, modern API.                                |
| Secrets           | Infisical          | Self-hosted fallback if SaaS goes away.               |

All vendor coupling is env-var level (`DATABASE_URL`, `REDIS_URL`, `S3_*`, `STRIPE_*`, `OTEL_*`). App code talks to standard protocols (Postgres wire, Redis RESP, S3 API, OTLP).

## Consequences

### Positive
- **Cost:** ~$0/mo up to first real traffic; linear scaling per vendor.
- **Portability:** Each vendor has at least one drop-in alternative (Supabase → Neon/RDS; Upstash → ElastiCache; R2 → S3; Render → Fly.io).
- **Vendor leverage:** No single vendor can raise prices 10× and hold the product hostage.
- **Failure isolation:** Supabase outage doesn't take down Redis or object storage.

### Negative
- **Operational surface:** Seven dashboards, seven support tiers, seven status pages.
- **Egress coordination:** Backend → Postgres crosses cloud boundaries (Render ↔ Supabase) — latency + data-transfer billing at scale.
- **No single-pane-of-glass billing.**

### Neutral
- Observability cross-cuts every vendor — OTEL + Sentry mitigate.

## Alternatives Considered

### AWS-only (RDS + ElastiCache + S3 + ECS + CloudFront)
Rejected: $20–100/mo floor, IAM role complexity for hobby-scale, CloudFront egress fees hurt image-heavy ticket product.

### Fly.io full stack
Rejected: Postgres-on-Fly had durability concerns at time of decision; Redis story weaker than Upstash.

### Self-hosted on a VPS
Rejected: backup/restore discipline cost exceeds free-tier savings; no inherent HA.

## References

- `docs/ha-strategy.md` — HA upgrade path per vendor.
- `docs/runbooks/disaster-recovery.md` — per-vendor failure procedures.
- Memory: `project_hosting_stack.md`
