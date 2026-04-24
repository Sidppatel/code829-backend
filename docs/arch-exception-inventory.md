# ARCH-EXCEPTION Inventory — baseline 0

**Baseline count: 0** (generated 2026-04-23 on `backlog/session-6-arch-exception-versioning`).

Command:

```bash
grep -rn "ARCH-EXCEPTION\|AllowDirectDbAccess" api/ db/ contracts/
```

## Result

Zero hits in `api/`, `db/`, `contracts/`. Prior security + DTO sessions (S2 DTO hardening,
S7 FE residuals, S9 auth dedupe, plus the pre-backlog refactors) already migrated every
direct-DbSet site to an SP/function/view wrapper.

The analyzer (`tools/Analyzers/DirectDbSetAccessAnalyzer.cs`) and this doc's escape-hatch
reference in the root `CLAUDE.md` are the only surviving mentions of the markers — both
are definitional, not usage.

## Methodology for future fixes

If a new exception is introduced and the CI guard flags it, triage with:

- **(a) Read-only query** — model as keyless view entity in `db/Entities/Views/`, map in
  `EventPlatformDbContext.OnModelCreating`, swap callsite to query the view DbSet.
- **(b) Write / complex read** — add `sp_*.sql` in `db/Sql/Procedures/`, wrap in
  `db/Repositories/StoredProcedures/`, migrate callsite, add integration test in
  `tests/IntegrationTests/StoredProcedures/` (S1 fixture).
- **(c) Genuinely unfixable** — document row here with file:line, snippet, and
  justification. Keep the annotation minimal.

| # | file:line | snippet | category | suggested fix |
|---|-----------|---------|----------|---------------|
|   | _(none)_  |         |          |               |

## CI guard

`.github/workflows/ci.yml` job `arch-exception-count` fails the build if the grep count
exceeds the baseline in the first line of this file. Bump the baseline intentionally only
when a category (c) entry is added with justification above.
