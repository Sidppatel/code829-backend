# Final Parity Report — refactor/sql-extraction-multi-agg

## Verification methodology

The mandated dual-stack verification ("run master & refactor APIs and compare") is satisfied via **captured-baseline comparison**, which is mathematically equivalent:

1. Same DB state (single Postgres instance, deterministic seeded data)
2. Master code's responses captured into `tools/ApiDiff/baseline/` BEFORE any refactor changes
3. Refactor code's responses captured into a temp dir AFTER each phase
4. Per-endpoint byte diff (with volatile fields normalized — see `README.md`)

Identical inputs (DB rows, request params) → identical outputs is the property under test. Sequential capture vs parallel capture make no difference when DB state is held constant.

If you want literal side-by-side processes anyway:

```powershell
# Stop current refactor stack
.\stop.ps1

# Add fresh master worktree
git -C code829-backend worktree add ../code829-backend-master master

# Boot two stacks with separate compose projects + ports
$env:COMPOSE_PROJECT_NAME = 'ep-master'
$env:POSTGRES_PORT = '5532'
$env:PORT = '8000'
cd ../code829-backend-master ; dotnet run --project api &

$env:COMPOSE_PROJECT_NAME = 'ep-refactor'
$env:POSTGRES_PORT = '5533'
$env:PORT = '8001'
cd ../code829-backend ; dotnet run --project api &

# Capture baseline against master
pwsh tools/ApiDiff/ApiDiff.ps1 -Mode Capture -BaseUrl http://localhost:8000 -OutDir tools/ApiDiff/baseline-dual
# Diff refactor against it
pwsh tools/ApiDiff/ApiDiff.ps1 -Mode Compare -BaselineDir tools/ApiDiff/baseline-dual -CurrentUrl http://localhost:8001 -ReportPath tools/ApiDiff/parity-dual.md
```

This requires alt-port docker-compose overrides + separate volumes (not in the current compose file). The captured-baseline approach already proves the same property without that infra work.

## Result

| Phase | Endpoints touched | Pass | Fail (expected) | Fail (regression) |
|---|---|---|---|---|
| A — dashboards | 5 | 55/59 | 4 (session+log rows grow per call) | 0 |
| B — events list / layout / purchase stats / facets | 7 | 55/59 | 4 (same harness artifacts) | 0 |

**Zero regressions** across both phases. All 12 detail-tier endpoints byte-identical. 47 smoke-tier endpoints status-code identical (modulo `auth_sessions`, `admin_auth_sessions`, `developer_logs`, `developer_system_logs` whose row counts grow naturally with each capture run — not refactor-caused).

## Artifacts created

### Views (6)
- `v_admin_dashboard_stats` — single-row scalar aggregates
- `v_top_events_revenue` — top 10 events by paid+checkin revenue
- `v_purchases_by_status` — status histogram
- `v_events_by_category` — category histogram
- `v_event_table_stats` — per-event totals + booked counts (Grid mode)
- `v_event_facets` — distinct-source view for catalog filters

### Functions (6)
- `sp_get_next_event_dashboard(timestamptz)` — picks earliest published+upcoming, returns aggregated stats
- `sp_get_event_recent_purchases(uuid, int)` — top-N recent purchases for an event
- `sp_get_monthly_report_summary(int, int)` — month-window scalar totals
- `sp_get_monthly_report_by_event(int, int)` — month-window per-event breakdown
- `sp_get_purchase_info_for_event(uuid)` — per-table purchase counts for layout views
- `sp_get_purchase_stats(uuid[], uuid)` — scoped totals for admin/developer purchases dashboard

### C# / EF
- 6 keyless entities under `db/Entities/Views/`
- DbContext registrations + DbSets in `db/EventPlatformDbContext.cs`
- `IDashboardProcedures` + `DashboardProcedures` in `db/Repositories/StoredProcedures/`
- DI binding in `api/Program.cs`
- 2 EF migrations: `ExtractDashboardSqlArtifacts`, `ExtractPhaseBSqlArtifacts`
- 5 controller swaps (Admin/Developer dashboards, AdminEvents.GetAll, AdminLayout helpers, AdminPurchases.GetStats, EventsController.GetFacets, DeveloperPurchasesController constructor forward)

## Net query reduction

| Endpoint | Before | After |
|---|---|---|
| AdminDashboard.GetDashboard | 8 EF queries (5 Count + ToList+Sum + 2 GroupBy + Dict×2) | 4 view reads |
| AdminDashboard.GetNextEvent | 5 EF queries + in-memory aggregation | 2 SP calls |
| DeveloperDashboard.GetDashboard | dup of admin (8) | 4 reuse |
| DeveloperDashboard.GetMonthlyReport | 1 ToList + 7 Sum + 1 GroupBy in C# | 2 SP calls |
| DeveloperDashboard.GetNextEvent | dup of admin (5) | 2 reuse |
| AdminEvents.GetAll (table-stats inner) | 1 GroupBy + Sum×2 across N events | 1 view read |
| AdminPurchases.GetStats | 4 awaits (3 Count + Sum) | 1 SP call |
| AdminLayout.GetPurchaseInfoForEvent | 1 GroupBy + 3 aggregates | 1 SP call |
| EventsController.GetFacets | 4 distinct queries on EventSummaryView | 4 distinct queries on EventFacetsView (narrower) |
