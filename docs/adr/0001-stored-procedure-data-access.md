# ADR-0001: Stored-procedure-only data access

- **Status:** Accepted
- **Date:** 2026-04-23
- **Tags:** backend, database, architecture

## Context

API layer originally used EF Core LINQ for reads + writes. This coupled query shape to C# call sites, made it hard to audit what SQL the DB actually ran, fragmented authorization/row-level-security logic across controllers, and produced N+1 patterns that only surfaced under load.

Postgres has first-class support for functions and stored procedures. PL/pgSQL is a stable surface we can version independently from the C# assembly and that DBAs can profile directly.

## Decision

All data access from `api/` goes through stored procedures (`sp_*`), SQL functions, or keyless views — never direct EF `DbSet<T>` LINQ.

Enforced at build time by Roslyn analyzer **EP0001** at `Error` severity (`tools/Analyzers/`). Violations fail `dotnet build`.

**Escape hatches:**
- Path whitelist: `api/Seeding/**`, `tests/**`, `db/Repositories/*.cs` (excluding `StoredProcedures/` subfolder).
- `[AllowDirectDbAccess("reason")]` attribute on method or class.
- Inline `// ARCH-EXCEPTION: <reason>` comment on invocation line.
- `FromSqlRaw` / `FromSqlInterpolated` on any DbSet (SP-call escape hatch) — always allowed.
- View DbSets (property name ends in `Views`) — always allowed.

## Consequences

### Positive
- Single audit surface for every query (grep `sp_*` in `db/Sql/Procedures/`).
- Row-level constraints (capacity locks, magic-link single-consume) live atomically in SQL with `FOR UPDATE`.
- DB-side profiling and indexing is straightforward — query shape is fixed.
- Migration discipline: changing SP signature forces callsite update.

### Negative
- Higher ceremony for new reads: author SQL + wrapper interface + DI registration.
- PL/pgSQL debugging tooling weaker than C# debugger.
- Inventory of `// ARCH-EXCEPTION` comments exists in legacy controllers; burn-down tracked in BE #18.

### Neutral
- Seeders + tests bypass the rule by path whitelist — intentional, they need bulk setup.

## Alternatives Considered

### EF Core LINQ everywhere
Rejected: original state. Coupled query shape to C#, fragmented auth logic, no central audit.

### Repository pattern over EF
Rejected: still runs LINQ; doesn't solve audit or atomicity. Adds indirection without moving logic to DB.

### Dapper + raw SQL in C#
Rejected: SQL lives in C# string literals — same audit problem, no DB-side function catalog.

## References

- Analyzer: `tools/Analyzers/DataAccessRuleAnalyzer.cs`
- SP wrappers: `db/Repositories/StoredProcedures/`
- SP SQL: `db/Sql/Procedures/`
- Memory: `feedback_backend_data_access.md`
