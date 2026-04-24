# Observability

Closes BE #8 (OpenTelemetry) + BE #12 (ship logs off Render).

## Stack

| Concern | Local (Development) | Staging / Production |
|---------|--------------------|-----------------------|
| Traces  | Jaeger all-in-one (in-memory) | Grafana Cloud Tempo |
| Metrics | Jaeger OTLP ingest (view in Jaeger) | Grafana Cloud Mimir |
| Logs    | Console + rolling files in `api/logs/` + OTLP → Jaeger | Console + OTLP → Grafana Cloud Loki |
| Errors (5xx) | Sentry (unchanged) | Sentry (unchanged) |

## Vendor decision — Grafana Cloud

Picked Grafana Cloud over Honeycomb. Reasons:

- Free tier: **50 GB logs + 50 GB traces + 10k metric series, 14-day retention** — well above our expected volume (current log footprint on Render ~2 GB/mo).
- Single vendor for logs + traces + metrics; logs-to-traces correlation is native because all three ship through the same OTLP endpoint with matching `trace_id` resource attribute.
- Honeycomb's 20M event/month cap is tighter for high-cardinality traces (each DB query span counts) — we'd blow past it during load tests.
- Grafana exposes an OTLP HTTP endpoint directly, no collector sidecar needed.

Config (prod): set in Render dashboard.

```
OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp-gateway-prod-us-east-0.grafana.net/otlp
OTEL_EXPORTER_OTLP_HEADERS=Authorization=Basic <base64(instance_id:api_token)>
```

Generate the token in Grafana Cloud → **Connections → Add connection → OpenTelemetry (OTLP)**. Scope: `logs:write`, `traces:write`, `metrics:write`.

## Local dev quickstart

```bash
# From code829-backend/
docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d

# Start API (picks up default OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318)
dotnet run --project api

# In another shell, generate a request
curl http://localhost:8000/events

# Open Jaeger UI
start http://localhost:16686
# service = "code829-api", click Find Traces
```

Each request should produce a trace with spans for: inbound HTTP → EF Core query (one span per query, with the SQL in `db.statement`) → Redis op (if cache miss) → outbound HTTP (Stripe / S3 / Resend / etc., if invoked).

## Log ↔ trace correlation

Every Serilog event is enriched with `TraceId` + `SpanId` (via `Api.Middleware.OpenTelemetryTraceEnricher` reading `Activity.Current`). Console lines carry `[trace=<id>]`, and the OTLP log sink propagates the same IDs as native OTLP log record fields. In Jaeger (local) or Grafana (prod) you can pivot from a trace to the matching log lines by trace ID.

## What is NOT replaced

- **Sentry** still captures unhandled 5xx exceptions and error breadcrumbs. OTEL is for traces + logs + metrics; Sentry remains the single source of truth for error grouping and alerting on exceptions.
- Serilog's rolling file sinks stay enabled in Development only — they're disabled in Production because Render's filesystem is ephemeral.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Jaeger UI shows no traces | Confirm Jaeger is up: `docker ps \| grep jaeger`. Confirm `OTEL_EXPORTER_OTLP_ENDPOINT` is unset or `http://localhost:4318`. |
| Grafana Cloud shows no data | Verify `OTEL_EXPORTER_OTLP_HEADERS` format (`Authorization=Basic <token>` — note `Basic `, space, then base64). |
| `TraceId` empty in logs | The log event fired outside an active `Activity` (e.g., app startup before `UseRouting`). Expected; ignore. |
| OTLP export errors in console | Set `OTEL_DIAGNOSTICS=1` env var to see OTEL SDK internal logs. |
