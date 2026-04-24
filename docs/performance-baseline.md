# Performance baseline

Target for this doc: record current prod/staging performance so regressions are caught before they ship. Fill in with numbers from running `tests/load/scenarios/*.js` against staging.

## Method

- **Tool:** k6 (`tests/load/`).
- **Environment:** staging (Render backend, Supabase DB, Upstash Redis) — mirror of prod shape.
- **Dataset:** seeded fixtures — 500 events, 10k ticket types, 1 k active users.
- **Run cadence:** before every release tagged `vX.Y.0`; results appended below as dated rows.

## Results — YYYY-MM-DD (placeholder — run once + fill in)

### Scenario: browse-events.js (100 VUs × 5 min)

| Metric                              | Value | Threshold    |
|-------------------------------------|-------|--------------|
| http_req_duration p50               | TBD   | —            |
| http_req_duration p95               | TBD   | < 500 ms     |
| http_req_duration p99               | TBD   | —            |
| http_req_failed                     | TBD   | < 1 %        |
| Total requests                      | TBD   | —            |
| RPS avg                             | TBD   | —            |

### Scenario: read-heavy.js (1000 req/min × 5 min)

| Metric              | Value | Threshold |
|---------------------|-------|-----------|
| http_req_duration p95 | TBD | < 500 ms |
| http_req_duration p99 | TBD | < 1000 ms |
| http_req_failed       | TBD | < 1 % |

### Scenario: checkout.js (20 VUs × 5 min)

| Metric                        | Value | Threshold |
|-------------------------------|-------|-----------|
| ep_quote_ms p95               | TBD   | —         |
| ep_purchase_ms p95            | TBD   | —         |
| ep_confirm_ms p95             | TBD   | —         |
| http_req_duration p95         | TBD   | < 1500 ms |
| http_req_failed               | TBD   | < 2 %     |
| ep_purchases_ok               | TBD   | —         |
| ep_purchases_fail             | TBD   | —         |

## Resource utilization (record alongside each run)

| Resource                          | Observed | Free-tier cap | 70 % alert threshold |
|-----------------------------------|----------|---------------|----------------------|
| Render backend memory (MB)        | TBD      | 512 MB        | 358 MB               |
| Render backend CPU (%)            | TBD      | 0.5 vCPU      | 35 %                 |
| Supabase DB connections (avg/max) | TBD      | 60            | 42                   |
| Supabase DB CPU (%)               | TBD      | shared        | 70 %                 |
| Upstash Redis commands/hour       | TBD      | 10 000        | 7 000                |
| Upstash Redis memory (MB)         | TBD      | 256 MB        | 179 MB               |

Feed the 70 % thresholds into UptimeRobot / Sentry Performance / Grafana alerts so we know when to upgrade the tier before load starts failing requests.

## Interpretation checklist (per run)

- [ ] All k6 thresholds passed (non-zero exit = fail).
- [ ] p95 latency ≤ previous run + 10 %.
- [ ] No endpoint above 1 s p95 on browse scenarios.
- [ ] DB pool saturation < 70 %; no `too many connections` errors in Render logs.
- [ ] Redis command rate projected under free-tier cap for prod traffic.
- [ ] Open a ticket if any threshold regresses or tier headroom drops below 30 %.
