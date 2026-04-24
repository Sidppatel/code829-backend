# ADR-0009: Accept OpenTelemetry beta instrumentation packages

- **Status:** Accepted
- **Date:** 2026-04-24
- **Tags:** observability, dependencies, risk

## Context

The backend uses the OpenTelemetry .NET SDK (tracing + metrics + logs via OTLP) to feed Grafana Cloud in production and Jaeger in dev. Two of the instrumentation packages the API depends on ship only as `-beta.1` on NuGet:

- `OpenTelemetry.Instrumentation.EntityFrameworkCore` `1.15.1-beta.1`
- `OpenTelemetry.Instrumentation.StackExchangeRedis` `1.15.1-beta.1`

The rest of the OTEL surface (`OpenTelemetry.Api`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`) is available at a stable `1.15.x` release and is pinned to one.

Upstream plan: per the [OpenTelemetry .NET repository](https://github.com/open-telemetry/opentelemetry-dotnet-contrib), these two instrumentations are planned to exit beta in the `1.16.0` GA wave. No concrete date is published.

Our dependency-pinning policy (see security remediation roadmap S4, BE #92) requires concrete versions — no wildcards, no floating ranges. The question is whether beta packages are acceptable inputs to that policy.

## Decision

**Accept both OTEL instrumentation packages at their `1.15.1-beta.1` pin for now.** Track for the `1.16.0` GA bump as a post-publish chore.

- Exact pins in `api/api.csproj`:
  - `OpenTelemetry.Instrumentation.EntityFrameworkCore` `1.15.1-beta.1`
  - `OpenTelemetry.Instrumentation.StackExchangeRedis` `1.15.1-beta.1`
- NuGet lock file (`packages.lock.json`) captures the exact resolved transitive graph and `dotnet restore --locked-mode` is enforced in CI (BE #78/#79 in the same session).
- When either package publishes a non-beta `1.16.0` (or later stable) release, bump both in a single PR and remove this ADR's "tracking" footnote.

## Consequences

### Positive
- Keeps full database + cache tracing (EF Core + Redis spans) in Grafana / Jaeger — these instrumentations are where our highest-value DB-latency signal comes from.
- Honors the no-wildcard rule: pins are concrete and reproducible via the lock file.
- Single-PR upgrade path when stable ships — no churn on surrounding OTEL packages.

### Negative / Trade-offs
- `-beta.1` means the upstream API surface is technically subject to break between betas and GA. Mitigation: we pin and lock, so nothing changes under us until we choose to bump.
- Security scanners that flag any pre-release tag as "unstable" may grumble; that's a policy-level acceptance, recorded here.

### Neutral
- If an urgent CVE lands on `1.15.1-beta.1`, the upgrade target is whatever the latest beta or GA is at that moment — treat like any other dep.

## Alternatives Considered

### Alternative A — Drop both instrumentations until GA ships
- Rejected. Losing DB + Redis spans blinds us to the class of latency issues that drive most production investigations. The observability value outweighs the beta-label cost.

### Alternative B — Use a stable third-party EF Core / Redis instrumentation
- Rejected. No drop-in replacement exists that emits OTEL-compatible spans with the same detail. Building or maintaining one is well outside the scope of this platform.

### Alternative C — Pin to a `2.x` preview wave
- Rejected. The surrounding `OpenTelemetry.*` packages we ship are on `1.15.x`; mixing major lines would risk version-skew warnings and runtime incompatibilities.

## Tracking

Bump target: `1.16.0` GA (or next stable after that) for both packages.

- Check cadence: reviewed at the start of each quarterly dependency sweep.
- Owner: whoever touches `api/api.csproj` next for any OTEL-related change.
- Trigger to bump immediately: a `GHSA-*` advisory affecting `1.15.1-beta.1`.

## References

- Security remediation roadmap session S4 (BE #92).
- [OpenTelemetry .NET contrib repo](https://github.com/open-telemetry/opentelemetry-dotnet-contrib)
- [NuGet packages.lock.json docs](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies)
