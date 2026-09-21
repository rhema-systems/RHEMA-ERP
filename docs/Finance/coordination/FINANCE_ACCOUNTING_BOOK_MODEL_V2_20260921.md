# Finance Accounting Book Model V2

## Objective

Implement the stakeholder-approved accounting-book model:

- exactly one perpetual tenant Primary book (`BASE`, GHS, Ghana Statutory);
- adjustment-only Delta layers with live inherited base presentation;
- foreign-currency Parallel books populated only by atomic translated replicas of Primary postings;
- tenant fiscal periods as the sole posting-period authority;
- governed Parallel initialization (zero, translated opening, or historical replay);
- direct report selection for Primary, multi-Delta, Parallel, and Delta-only views.

## Approved boundaries

- Finance-owned entities, services, migrations, seeding, APIs, UI, and tests.
- No database reset or migration application without a separate explicit cutover action.
- No remote push or PR mutation.
- Preserve the dirty demo worktree and its running services.

## Repository state

- Authoritative demo source: `codex/finance-gl-latest-master-20260916` at `f734091b`, with a large verified-but-uncommitted demo-hardening overlay.
- Isolated implementation worktree: `RHEMA-ERP-finance-accounting-book-model-v2`.
- Branch: `codex/finance-accounting-book-model-v2`.
- Baseline snapshot commit: `3db18679` (captures the demo-hardening overlay without altering the source worktree).

## Phase plan

1. Domain/schema and invariant foundation.
2. Tenant-period-only validation and simplified lifecycle.
3. Primary-to-Parallel atomic posting/reversal replication.
4. Delta inheritance, derived account structure, and reporting.
5. Governed initialization and FX translation controls.
6. Seed replacement and frontend UX.
7. Focused tests, full build, migration review, and clean-reset rehearsal plan.

## Current status

`IMPLEMENTING_PHASE_5`

## Migration state

Migration `20260921154920_AccountingBookModelV2` has been authored and reviewed but not applied. It fails closed when live accounting books already exist, requiring either the approved fresh-database cutover or a separately reviewed data-conversion plan. Database state is unchanged.

## Implemented

- Enforced one perpetual, always-active Primary book and removed Primary replacement/lifecycle operations.
- Restricted ordinary applicability routing to Primary; Delta is explicit-adjustment only and Parallel rejects direct posting.
- Added atomic Primary-to-Parallel journal replication with immutable rate evidence, inherited dimensions, mapping checks, rounding control, and original-rate reversals.
- Made tenant fiscal periods the sole posting/readiness period authority; per-book period UX is removed from the active flow.
- Added derived structure inheritance, preserved local-only accounts, and automatic protected Parallel CTA/rounding accounts.
- Replaced standard seed books with `BASE`, `IFRS_ADJUSTMENTS`, and `USD_PARALLEL`.
- Added Trial Balance Base + multi-Delta selection, aggregation, PDF parameter support, and historical Delta selection.
- Added a combined Delta ledger inquiry and UI that labels posted base journals as `Inherited` and Delta journals as `Adjustment`, without duplicating base postings.
- Added V2 create/edit UX for Delta posting windows and Parallel replication/opening/translation settings.
- Corrected Primary-to-Parallel conversion to persist and apply the source-to-target multiplier (`InverseRate` under the current legacy exchange-rate storage contract).

## Verification evidence

- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore`: passed, 0 errors (repository warnings remain).
- Frontend `npm run type-check` filtered to changed Finance files: no changed-file errors; the repository-wide command still reports unrelated baseline TypeScript failures.
- V2-focused backend suite: 6 passed, covering Primary creation prohibition, multi-Delta aggregation/order, historical Delta ledger labels, protected Parallel accounts, missing-rate atomic rollback, and immutable translated replication.
- Delta ledger/report frontend tests: 4 passed across 2 files.
- Targeted `AccountingBookPeriodInitializationC4Tests`: 20 passed; 3 legacy assertions fail because they require removed behavior (per-book close readiness, Primary lifecycle transitions, and inactive derived mappings). These tests must be replaced with V2 assertions.
- No migration was applied and no database was reset.

## Open implementation slices

- Complete the source-to-target exchange-rate convention migration across every foreign-currency consumer, not only Parallel replication.
- Implement governed Parallel opening conversion and historical replay execution (configuration shape is present; zero opening is supported).
- Replace remaining legacy accounting-book assertions and add reversal-rate coverage.
- Perform a fresh-database migration/seeding rehearsal only after explicit cutover authorization.

## Next authorized action

Finish FX convention/opening execution and V2-focused tests in the isolated worktree; do not apply the migration.
