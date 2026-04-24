# ADR-0008: Render service plan for `code829-backend`

- **Status:** Accepted
- **Date:** 2026-04-24
- **Deciders:** Sid Patel
- **Tags:** infra, hosting, render, cost

## Context

`code829-backend` currently ships on Render's `plan: free` tier. The free plan:

- Spins the instance down after 15 min idle → first request after idle takes 30–60 s (cold start).
- Has only 512 MB RAM and 0.1 CPU — insufficient once real traffic or background workers run.
- Does not run on a persistent disk — uploads and logs are ephemeral (partially fine because S3 is used for media, but `logs/` and any local caches vanish).
- Provides no SLA and no dashboard access to logs beyond 24 h.
- Blocks outbound egress at ~100 GB/month, which is tight once webhook retries + OTLP log/metric export are enabled.

For a production ticketing workload with Stripe webhooks + magic-link email issuance, cold starts on the checkout path are an unacceptable UX. Webhook delivery also retries exponentially when the instance is spun down, which can mask real failures.

Render's published tiers as of April 2026 (`https://render.com/pricing`):

| Tier       | $/mo   | RAM    | CPU  | Spin-down | Notes                          |
|------------|--------|--------|------|-----------|--------------------------------|
| `free`     | 0      | 512 MB | 0.1  | Yes       | Current tier                   |
| `starter`  | 7      | 512 MB | 0.5  | No        | Persistent, no spin-down       |
| `standard` | 25     | 2 GB   | 1.0  | No        | Suitable for launch            |
| `pro`      | 85     | 4 GB   | 2.0  | No        | Horizontal scale upgrades here |

## Decision

Move `code829-backend` from `plan: free` to **`plan: starter`** for the initial production cutover.

`starter` is the cheapest tier that eliminates cold-start spin-down, which is the single most user-visible problem with the free tier. RAM and CPU budget at 512 MB / 0.5 CPU is enough for the current workload (one web service + three background workers, modest Stripe webhook volume). When a single metric crosses into `standard` territory (memory > 400 MB sustained p95, CPU > 70% sustained p95, response time p95 > 500 ms), upgrade to `standard` — the switch is a one-line `plan:` change and a no-downtime Render restart.

## Consequences

### Positive

- No cold starts on the public checkout path.
- Stripe webhook retries succeed on first delivery in normal operation (no 30s spin-up).
- Log retention and observability via Grafana Cloud (see OTLP export) is uninterrupted.

### Negative / Trade-offs

- $7/mo recurring cost per environment (production, eventually staging).
- `starter` still caps RAM at 512 MB — if background workers balloon (e.g. image processing spikes), we will need a quick move to `standard`.
- No horizontal autoscaling on `starter`; single instance is a SPOF. This is accepted because Render provides instance-level restart + Supabase + Upstash cover the durable state. See [docs/ha-strategy.md](../ha-strategy.md).

### Neutral

- Plan changes take effect on next deploy; no code changes required.
- Spending is predictable and easy to roll back by editing `render.yaml` and redeploying.

## Alternatives Considered

### Keep `free`

Rejected — cold starts on Stripe webhook + checkout make this a non-starter for production. Free tier is acceptable only for the per-PR review environment (if we add one later).

### Jump straight to `standard` ($25/mo)

Rejected for launch — we do not yet have measurements that justify the 2 GB RAM headroom. Starter gives us the no-spin-down property at 1/3 the cost, and the upgrade path is trivial if metrics demand it.

### Fly.io / Railway / self-host on a VPS

Rejected for launch — migrating off Render would bundle infra work with the public launch, raising risk. Render is already wired for blueprint deploys, Docker, health checks, and secret management. Revisit after 6 months of production if Render cost or feature gaps become real.

## References

- `render.yaml:19` — plan declaration.
- Render pricing: https://render.com/pricing
- [docs/ha-strategy.md](../ha-strategy.md) — HA posture acceptance for single-instance starter.
- GitHub issue: [BE #73](https://github.com/Sidppatel/code829-backend/issues/73)
