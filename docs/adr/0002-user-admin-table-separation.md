# ADR-0002: Separate `users` and `business_users` tables

- **Status:** Accepted
- **Date:** 2026-04-23
- **Tags:** backend, auth, schema

## Context

Platform has two populations with near-zero attribute overlap:

- **Users** (ticket buyers): magic-link passwordless login, no password column, minimal profile, high-volume self-service.
- **Business users** (Admin, Staff, Developer): password login + MFA, role field, lockout counters, device sessions, lower-volume staff population.

A single `users` table with a role column + nullable password column forced every query to filter by role and left half the columns NULL for the majority row class.

Early design explored a three-way split (`users`, `admin_users`, `staff_users`) — rejected because Admin/Staff/Developer differ only by the `role` enum, not by attribute set.

## Decision

Two tables: `users` (ticket buyers, no password) and `business_users` (Admin + Staff + Developer, with password + role enum).

Foreign keys from other tables (audit logs, events, device sessions) reference the **table name**, not the role alias. `events.organizer_id` references `business_users(id)`. `purchases.user_id` references `users(id)`.

## Consequences

### Positive
- Schema expresses the population boundary directly — no nullable-by-role columns.
- Auth pipeline is literally two different controllers (`AuthController` for magic-link, `AdminAuthController` for password) with no shared mutable state.
- Index tuning per table (`users` is read-heavy for email lookups; `business_users` is small + read-mostly with MFA state).

### Negative
- Joining "who did this" across both populations requires a union or an actor-type discriminator (see ADR for unified audit log, BE #19).
- FK from audit rows to "actor" is polymorphic — we store `actor_type` + `actor_id` rather than a constrained FK.

### Neutral
- A future refactor to add a third population (e.g., external partners) repeats the pattern — a new table, not a new role value.

## Alternatives Considered

### Single `users` table with role column
Rejected: nullable password column, role-filter on every query, unclear auth pipeline boundaries.

### Three-way split: users / admin_users / staff_users
Rejected: Admin/Staff/Developer share attribute set — only the role enum differs. Three tables with identical columns is worse than one with a role column.

### Shared `accounts` + per-role detail tables
Rejected: adds a join to every auth lookup for no schema-expression benefit.

## References

- Entities: `db/Entities/User.cs`, `db/Entities/BusinessUser.cs`
- Memory: `project_auth_identity_design.md`
- Related: ADR-0004 (magic-link passwordless for Users)
