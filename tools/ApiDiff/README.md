# ApiDiff

Parity harness for the SQL-extraction refactor. Captures normalized JSON responses from the running API, then compares two captures (or a captured baseline vs a live API) to confirm DTO byte-for-byte equivalence after a refactor.

## Files

- `ApiDiff.ps1` — main script (PowerShell 7+)
- `endpoints.json` — endpoint catalog (auth role, params, tier)
- `baseline/` — committed master baseline (created by Capture)

## Tiers

- **detail** — full normalized body diff. Used for the 12 endpoints touched by the refactor.
- **smoke** — status code + item count only. Used for 40+ unrelated endpoints to catch incidental breakage.

## Volatile fields stripped before diff

`createdAt`, `updatedAt`, `deletedAt`, `expiresAt`, `lastActivityAt`, `traceId`, `etag`/`eTag`/`ETag`, `requestId`, `ip`, `ipAddress`, `userAgent`, `deviceName`, `magicLinkToken`, `sessionToken`, `accessToken`, `refreshToken`, `jwt`, `qrToken`, `claimToken`. Replaced with the literal string `<<VOLATILE>>` so structural drift still shows up but timestamps don't.

## Usage

### Capture against running API

```powershell
pwsh -File tools/ApiDiff/ApiDiff.ps1 -Mode Capture `
  -BaseUrl http://localhost:8000 `
  -OutDir tools/ApiDiff/baseline
```

Logs in as developer/admin/organizer/staff/user using seeded creds in `endpoints.json`. Writes per-endpoint files plus `_manifest.json` and `_fixtures.json`.

### Compare baseline vs current

```powershell
pwsh -File tools/ApiDiff/ApiDiff.ps1 -Mode Compare `
  -BaselineDir tools/ApiDiff/baseline `
  -CurrentUrl http://localhost:8001 `
  -ReportPath tools/ApiDiff/parity-report.md
```

Captures current internally, then diffs against the baseline. Exits non-zero if any row fails.

## Workflow during refactor

1. Boot master cleanly (`..\start-backend.ps1`), API on `:8000`.
2. Capture baseline once: writes to `tools/ApiDiff/baseline/`. Commit on the refactor branch.
3. After each Phase A/B commit: run Compare against the refactor stack on `:8001`. Zero `BODY_MISMATCH` allowed for detail-tier rows.
4. Final dual-stack run: parallel master+refactor stacks per the plan, single Compare call generates `parity-report.md`.

## Requirements

- PowerShell 7+ (`pwsh`)
- Master DB seeded by `..\stop-clear-start.ps1`. Both stacks must run on identical seeds.
- API in `Development` mode (uses `/auth/dev-login`, exposes seeded magic creds).

## Limitations

- POST/PUT/PATCH/DELETE not currently exercised — high regression risk lives in writes, but capturing them safely needs idempotent fixtures. Out of scope for this refactor since it is read-only.
- Fixture resolution is best-effort: `eventId`/`purchaseId` come from list endpoints. If those endpoints break, downstream rows skip with a warning.
