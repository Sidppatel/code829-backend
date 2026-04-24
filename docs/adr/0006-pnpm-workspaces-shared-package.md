# ADR-0006: pnpm workspaces with `@code829/shared`

- **Status:** Accepted
- **Date:** 2026-04-23
- **Tags:** frontend, tooling, monorepo

## Context

Four Vite apps (ADR-0005) need to share: DTO types, axios instance config, Zustand stores, query hooks, UI primitives, and business utilities. Options:

1. Copy-paste between apps. Entropy wins.
2. Publish a private npm package. Publish-per-change ceremony.
3. Workspace with path-mapped internal package.

## Decision

**pnpm workspaces.** `pnpm-workspace.yaml` declares `apps/*` and `packages/*`. Shared logic lives in `packages/shared` and is consumed as `@code829/shared` via workspace protocol (`"@code829/shared": "workspace:*"`).

Apps import from `@code829/shared` as if it were a published package — no relative `../../packages/shared/src` paths. TypeScript path mapping + Vite alias resolve the import at build time to the source files (no pre-build step for the shared package).

## Consequences

### Positive
- **Atomic cross-package changes:** changing a shared DTO + all four apps in one PR, one test run, one review.
- **No publish step:** `pnpm install` links source directly. HMR works across package boundaries in dev.
- **Strict version parity:** every app runs the same version of every shared dependency by construction.
- **CI parallelism:** `pnpm -r build` and `pnpm -r test` fan out per-package.

### Negative
- **Shared package is a god node.** A breaking change ripples to four apps simultaneously. Mitigated by type-level enforcement — breakage surfaces at `tsc`, not runtime.
- **pnpm-specific features** (workspace protocol, hoisting config) — migrating to npm/yarn would require surgery.
- **Tooling that doesn't understand workspaces** (some older linters, ad-hoc scripts) needs per-package invocation.

### Neutral
- `@code829/ui` package exists but is empty as of 2026-04-23 — decision deferred in Session 10 memo.

## Alternatives Considered

### npm workspaces
Rejected at time of decision: dependency hoisting was less strict; disk usage higher. Acceptable fallback if pnpm maintenance becomes a concern.

### yarn workspaces (classic or v3)
Rejected: v3's PnP caused tool compatibility issues; classic is in maintenance.

### Private npm package (GitHub Packages)
Rejected: publish-per-change friction on internal code, no HMR across package boundary.

### Relative imports from a `shared/` directory
Rejected: no enforced boundary, TypeScript happily imports `../../apps/admin/src/internals`.

## References

- `pnpm-workspace.yaml` (frontend repo)
- `packages/shared/package.json`
- Related: ADR-0005 (multi-app monorepo)
