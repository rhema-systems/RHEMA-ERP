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

`CUTOVER_CANDIDATE_PROVISIONED`

## Migration state

Migrations `20260921154920_AccountingBookModelV2` and `20260921174134_AccountingBookTranslationEvidence` have been authored and reviewed. They remain unapplied to the live/demo database. Both were successfully applied through the complete migration chain to the isolated local rehearsal database `RHEMAERP_GL_REHEARSAL_BOOKV2_20260921_A4`. The first fails closed when live accounting books already exist, requiring either the approved fresh-database cutover or a separately reviewed data-conversion plan. The second adds immutable opening-translation evidence and normalizes existing rate rows to the canonical `1 source/base = Rate target` direction.

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
- Standardized exchange-rate storage and the public contract to `1 source/base = Rate target`, with the reciprocal in `InverseRate`; updated posting, AP, AR, cash, fixed assets, revaluation, supplier debit notes, GL conversion, and opening-balance consumers.
- Made zero-opening Parallel evidence use the Parallel book currency rather than the tenant currency, and added explicit UI warnings explaining the historical transactions excluded by each replication cutoff/opening choice.
- Implemented governed Parallel opening conversion with immutable rate id/value/date/type/source per account, supporting single-approved-rate and classification-driven methods plus protected CTA balancing.
- Implemented idempotent historical replay during governed activation. Every eligible Primary journal is translated at its accounting-date approved rate, linked to its source journal, and projected into exact-book balances; missing rates or mappings roll back activation.
- Reversal replicas reuse the original immutable Parallel rate even when later rates exist.

## Verification evidence

- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore`: passed, 0 errors (repository warnings remain).
- Frontend `npm run type-check` filtered to changed Finance files: no changed-file errors; the repository-wide command still reports unrelated baseline TypeScript failures.
- V2/FX/Parallel/reversal/protected-layout backend sweep: 51 passed. Coverage includes single-rate and classification-driven governed conversion snapshots, CTA balancing, historical replay idempotency, canonical rate direction, Primary creation prohibition, multi-Delta aggregation/order, historical Delta ledger labels, protected Parallel accounts, protected layout idempotency, missing-rate atomic rollback, immutable translated replication, direct-Parallel rejection, and original-rate reversals.
- Accounting-book readiness frontend suite: 11 passed. Legacy per-book period expectations were removed in favor of the tenant fiscal-calendar model.
- Delta ledger/report frontend tests: 4 passed across 2 files.
- Full Next.js production build passed and generated all Finance administration routes, including currencies and both dimension pages.
- Idempotent migration SQL generation passed. Review confirmed `decimal(18,6)` translation evidence, the tenant-scoped exchange-rate foreign key, evidence check constraints, and the one-time `Rate`/`InverseRate` normalization statement.
- Fresh A4 migration plus `seed-db` passed. Direct SQL verification found exactly 3 canonical active books, 3 approved balanced initialization packages, 2 protected non-postable Parallel control accounts, 1 approved default routing policy with 10 rules, and 6 active protected statement layouts.
- `seed-finance-baseline` reran successfully without duplicates. `seed-finance-demo-dimensions` reran twice and produced 22 values across 6 dimensions with zero duplicate codes.
- Rehearsal correction: Finance protected layouts were previously filtered to retired book codes and invoked before baseline activation. The canonical codes are now `BASE`, `IFRS_ADJUSTMENTS`, and `USD_PARALLEL`, and protected-layout seeding runs after baseline activation.
- Read-only assessment of the retained `RhemaERP` development/demo database found four applied migrations, five active legacy books, two balanced draft journals with 12 draft transaction lines, no posting events, five approved zero-balance initialization packages, and legacy exchange-rate direction such as `GHS/USD = 12.5`. Because there is no posted Finance activity, a fresh database is safer than an in-place conversion; the two drafts remain preserved in the original database.
- Provisioned the separate non-destructive cutover candidate `RHEMAERP_BOOKV2_CUTOVER_20260922`; the existing `RhemaERP` database was not reset, dropped, migrated, or repointed.
- The cutover candidate passed the complete seven-migration chain and `seed-db`. Finance-specific baseline provisioning passed twice and Finance demo-dimension provisioning passed twice, demonstrating idempotency.
- Direct cutover-candidate reconciliation found exactly three active canonical books (`BASE`, `IFRS_ADJUSTMENTS`, `USD_PARALLEL`), three approved and balanced zero-opening packages, two protected non-direct-posting USD Parallel control accounts, one approved routing policy with ten rules, six active protected statement layouts, 22 values across six transaction dimensions with no duplicate codes, canonical `GHS/USD = 0.08` with reciprocal `12.5`, and zero journals, transactions, or posting events.
- Post-provisioning source verification reconfirmed that `RhemaERP` remains at four migrations with five books and its two draft journals intact.
- Targeted `AccountingBookPeriodInitializationC4Tests`: 20 passed; 3 legacy assertions fail because they require removed behavior (per-book close readiness, Primary lifecycle transitions, and inactive derived mappings). These tests must be replaced with V2 assertions.
- No migration was applied to the retained `RhemaERP` database and no database was reset or dropped.

## Open implementation slices

- Replace the remaining legacy accounting-book assertions that intentionally encode removed per-book period and Primary lifecycle behavior.
- Repoint and restart a deliberately selected API/frontend runtime against the verified cutover candidate only after confirming that the operator wants to leave the preserved legacy demo database and its two drafts behind.

## Next authorized action

Use `RHEMAERP_BOOKV2_CUTOVER_20260922` as the verified fresh cutover candidate. Keep `RhemaERP` preserved as rollback/reference evidence. Runtime repointing and restart remain a separate, explicit operational action; neither has been performed.
