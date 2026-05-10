# SQL Extraction Worklist

Source of truth for the `refactor/sql-extraction-multi-agg` branch. Each row converts inline LINQ aggregation in a controller/service into a Postgres view or function, called via existing `db/Sql/...` + `db/Repositories/StoredProcedures/...` pattern. DTO response shapes are preserved byte-for-byte; verified by `tools/ApiDiff/`.

**Counts:** 12 Tier 1 / 6 Tier 2-keep-as-is / 5 already extracted (reference).

## Already extracted (reference — do not redo)

| File | Method | Backing |
|---|---|---|
| `api/Controllers/AdminLogsController.cs` | `GetAll` | `sp_get_admin_logs` |
| `api/Controllers/DeveloperLogsController.cs` | `GetAll` | `sp_get_admin_logs` |
| `api/Controllers/DeveloperController.cs` | `GetEmailLogs` | `sp_get_email_logs` |
| `api/Controllers/DeveloperController.cs` | `GetDeveloperLogs` | `sp_get_developer_logs` |
| `api/Controllers/DeveloperController.cs` | `GetSystemLogs` | `sp_get_system_logs` |

## Tier 1 — extract

| # | File:Lines | Method | Pattern | Q# | Target | Proposed name | Cx | Status |
|---|---|---|---|---|---|---|---|---|
| 1 | AdminDashboardController.cs:21–59 | GetDashboard | Multi-Count(5) + GroupBy(3) + ToList+Sum | 8 | views | `v_admin_dashboard_stats`, `v_top_events_revenue`, `v_purchases_by_status`, `v_events_by_category` | M | ☐ |
| 2 | AdminDashboardController.cs:61–128 | GetNextEvent | ToList+Count×8 / Sum×N | 10 | function | `sp_get_next_event_stats(p_event_id uuid)` | M | ☐ |
| 3 | DeveloperDashboardController.cs:21–59 | GetDashboard | dup of #1 | 8 | reuse | reuse `v_admin_dashboard_stats` (+ siblings) | S | ☐ |
| 4 | DeveloperDashboardController.cs:62–103 | GetMonthlyReport | ToList+Sum×7 + GroupBy(1) | 8 | function | `sp_get_monthly_report(p_year int, p_month int)` | M | ☐ |
| 5 | DeveloperDashboardController.cs:105+ | GetNextEvent | dup of #2 | 10 | reuse | reuse `sp_get_next_event_stats` | S | ☐ |
| 6 | AdminEventsController.cs:48–134 | GetAll | GroupBy(1) + Sum×2 | 3 | view/fn | `v_event_with_table_stats` (or extend `v_event_tables_summary`) | M | ☐ |
| 7 | AdminLayoutController.cs:535–550 | GetPurchaseInfoForEvent | GroupBy + Sum/Count×3 | 4 | function | `sp_get_purchase_info_for_event(p_event_id uuid)` | S | ☐ |
| 8 | AdminLayoutController.cs:505–530 | GetEventTableListAndState | ToList + in-memory Count + GroupBy | 3 | view/fn | `v_event_table_layout_state` | M | ☐ |
| 9 | AdminPurchasesController.cs:117–146 | GetStats | Multi-Count(3) + Sum | 4 | view | `v_purchase_statistics` | S | ☐ |
| 10 | EventsController.cs:159–195 | GetFacets | GroupBy + Min/Max + Distinct×4 | 5 | view | `v_event_facets_summary` | S | ☐ |
| 11 | AdminLayoutController.cs:552–572 | MapEventTables | in-memory GroupBy after ToList | 2 | merge into #7 | (refactor) | S | ☐ |
| 12 | AdminEventsController.cs:107–122 | GetAll (table-stats inner) | GroupBy + Sum×2 | 3 | merge into #6 | (embedded) | S | ☐ |

## Tier 2 — keep-as-is

LINQ chain has `read → claims/role check → scoped read`. Flattening risks ACL leakage. Document but do not flatten.

| # | File | Method | Reason |
|---|---|---|---|
| 13 | AdminPurchasesController.cs:55–115 | GetAll | `GetCallerScopeAsync` between query + scoped read |
| 14 | AdminEventsController.cs:151–164 | GetById::TicketTypes | ownership check then view read |
| 15 | AdminEventsController.cs:166–180 | GetById::TableTypes | ownership check then view read |
| 16 | AdminLayoutController.cs:380–440 | GetEventTableLayout | multi-stage ACL + lock-state compute |
| 17 | DeveloperEventsController.cs (inherits) | GetAll | inherits AdminEventsController behaviour |
| 18 | DeveloperPurchasesController.cs (inherits) | GetAll | inherits AdminPurchasesController behaviour |

## Conversion recipe (per row)

1. Author SQL → `db/Sql/Views/v_*.sql` or `db/Sql/Procedures/sp_*.sql`. Read-only fns marked `STABLE`. `LANGUAGE sql` preferred.
2. (View) keyless entity → `db/Entities/Views/<Name>View.cs`. Register in `db/EventPlatformDbContext.OnModelCreating()` (lines 730–854) + DbSet (12–76).
3. (Function) C# wrapper → `db/Repositories/StoredProcedures/<Domain>Procedures.cs` (interface + impl). DI in `api/Program.cs`.
4. EF migration (in the sibling `code829-db` repo): `dotnet ef migrations add Extract_<Artifact> --project src/Db --startup-project src/MigrationRunner`. Up: `migrationBuilder.Sql(MigrationSqlLoader.Load("file.sql"))`. Down: `DROP VIEW/FUNCTION ... IF EXISTS`.
5. Controller swap to `_ctx.<Views>.AsNoTracking().FirstOrDefaultAsync()` or `_procs.<Method>Async(...)`. EP0001 enforces.
6. Update integration tests in `tests/`. Add SQL-level test for non-trivial fns.
7. `dotnet build` clean → `dotnet test` → ApiDiff harness vs baseline → commit (one artifact per commit when feasible).

## Risks

- **Indexes** — verify `purchases(event_id, status)`, `purchases(created_at)`, `events(category, status)` are indexed; add via migration if missing.
- **DTO drift** — column names + casing + nullability + ordering must match existing JSON. ApiDiff harness catches.
- **Function drop signatures** — Postgres requires full arg list (`DROP FUNCTION sp_x(uuid, int)`).
- **No `SECURITY DEFINER`** — codebase doesn't use it; introducing requires separate review.
- **Naming collisions** — search existing `db/Sql/Procedures/` before authoring; e.g. `sp_event_stats` (existing) vs new `sp_get_next_event_stats`.
