# ADR-0010: CI Coverage Ratchet

- Status: Accepted
- Date: 2026-04-24
- Deciders: Sidppatel
- Tags: ci, testing, quality

## Context and Problem Statement

Session S1 (Testcontainers, FE #33 / BE #54) added the first real integration
test suite and wired code-coverage collection into `.github/workflows/ci.yml`
via ReportGenerator. S2 bootstrapped smoke tests for 19 of the 26 controllers,
landing the repo at roughly 15% line coverage across the `api` project.

CI initially gated at **15%** — low enough to pass the smoke baseline, but it
meant any regression below that number (or any large addition of untested code)
would not fail the build. BE #7 sets a medium-term target of **60%**, while
backlog work expands controller happy-path coverage incrementally.

A static gate drifts: landing one test file without moving the number risks
the gate staying at its historical floor forever. A per-PR delta check is
noisy (branch builds fluctuate). What we want is a **monotonic floor** that
steps up as coverage grows, with an explicit calendar cadence so the team
knows when the next step lands.

## Decision

Adopt a **monthly ratchet** of +5 percentage points until the gate reaches the
BE #7 target of 60%, then switch to a linear +1 pt/month schedule until 80%.

The first step pins the floor **just below the current measured line coverage**
rather than a flat "+5 pt of the old floor". CI measured 19.4% actual line
coverage on master at the time this ADR landed; 20% would have tripped the
very first build, and a pure "+5 pt of the old floor" arithmetic run from
15% would have chased a number the suite has not yet reached.

| Window | Floor | Rationale |
|---|---|---|
| 2026-04 — initial S5 bump | **19%** | current-actual (19.4%) minus 0.4 pt safety margin for variance |
| 2026-05 | 24% | +5 pt ratchet |
| 2026-06 | 29% | +5 pt ratchet |
| 2026-07 | 34% | +5 pt ratchet |
| 2026-08 | 39% | +5 pt ratchet |
| 2026-09 | 44% | +5 pt ratchet |
| 2026-10 | 49% | +5 pt ratchet |
| 2026-11 | 54% | +5 pt ratchet |
| 2026-12 | 59% | +5 pt ratchet |
| 2027-01 | 60% | BE #7 target hit |
| 2027-02+ | +1 pt/month | fine-grained ratchet to 80% |

### How each bump lands

1. First workday of the month, open a PR bumping the threshold in
   `.github/workflows/ci.yml` (the `awk` comparison + the adjacent comment).
2. If actual coverage is below the new floor, the PR is blocked until
   additional tests land. The author pairs the bump with the tests.
3. If the ratchet would exceed the current actual, delay the bump by one
   month and open a GitHub issue naming the controllers that need coverage.
4. Never lower the floor. A regression must be fixed by adding tests.

## Consequences

Positive:

- Monotonic improvement: the gate can only rise.
- Predictable cadence: every month has a known target.
- Forces a conscious decision when coverage stalls (delayed bump + issue).

Negative:

- Adds a recurring operational task (~15 min/month).
- A PR author whose change has low coverage may be blocked unexpectedly.

## Links

- BE #100 — *raise coverage gate from 15%* (closed by this ADR + CI bump)
- BE #7 — *reach 60% line coverage on `api`*
- `.github/workflows/ci.yml` (coverage gate + ReportGenerator step)
