# Finance precision and rounding governance ledger — 1 October 2026

## Authority and exact base

- Objective: implement layered, auditable Finance precision and rounding governance without deriving quantity, unit-price, exchange-rate, tax, settlement, or display precision from currency minor units.
- Exact base: `fc4adb57cb47b7c95ca8dcdfd88732fcac189110` from `codex/finance-book-layout-followup-20261001` / PR #319 foundation.
- Branch: `codex/finance-precision-rounding-governance-20261001`.
- Isolated worktree: `C:/Users/Akwas/.codex/worktrees/c2cc/RHEMA-ERP`.
- Primary dirty checkout remains untouched.
- Local commits are authorized. No push, PR, merge, migration application, live database mutation, or accounting-data mutation is authorized.

## Scope and boundaries

- Finance owns the policy, settings, posting boundary, audit evidence, UI contracts, migrations, and Finance adapters.
- Inventory, Procurement, Sales, HR, Estate, Projects, and other module implementations remain read-only unless a separate user decision authorizes a named change.
- Currency ISO minor units remain authoritative and tenant read-only. Existing 0–4 decimal ledger storage is preserved.
- Historical records remain compatible; new settings use backward-compatible defaults.

## Precision domains

| Domain | Governing rule | Initial default | Accounting effect |
| --- | --- | --- | --- |
| Currency amount | ISO 4217 minor units | Per currency | Final ledger boundary |
| Unit price / cost | Tenant Finance calculation precision | 4 decimals | Intermediate calculation only |
| Quantity | Existing Unit Type decimal places plus additive increment contract | Existing UOM | Never currency-derived |
| Exchange rate | Tenant input/display precision | 10 / 6 decimals | Immutable posting evidence retains entered rate |
| Tax percentage | Separate rate precision | Existing stored rate | Never currency-derived |
| Tax amount | Method, scope, configurable increment | Nearest / Line / currency minor unit | Deterministic tax evidence |
| Invoice/cash rounding | Optional increment, method, gain/loss accounts | Disabled | Explicit balancing line only |
| Settlement tolerance | Independent amount/percentage tolerance | Zero | Never inferred from display rounding |
| Report display | Presentation precision | Currency decimals | Must not mutate stored values |

## Inventory classification

The repository-wide inventory is intentionally classified before edits. Generated migration designers, archived migrations, SQL verification snapshots, file sizes, percentages/scores, and non-Finance module UI formatting are not candidates for mechanical replacement.

| Class | Known Finance areas | Treatment |
| --- | --- | --- |
| Monetary posting amount | posting engine, journal entry/import, AP/AR, banking, allocations, parallel books, reversals | Route through currency boundary policy; preserve original evidence for replay/reversal |
| Unit price / cost | invoice lines, receipt distributions, asset/stock valuations | Preserve governed intermediate precision; round resulting money only |
| Quantity | Unit Type and source-document quantities | Enforce UOM precision/increment independently through additive Finance validation |
| Exchange rate | FX selection, conversions, parallel-book replication | Validate independent high precision; never use currency minor units |
| Percentage | tax rates, tolerances, variance and report percentages | Retain separate precision; do not convert merely because value displays two decimals |
| Reporting/display | `toFixed(2)` and report renderers | Replace only Finance monetary display paths with explicit display policy; no stored mutation |
| Statutory/legal rounding | tax calculation and invoice/cash rounding | Govern method, scope, increment, and immutable audit evidence |

## Work packages and dependencies

1. Policy contracts and focused pure unit tests.
2. Finance Settings entity/DTO/service validation, lifecycle governance, migration and model snapshot.
3. Posting, tax, invoice rounding, FX evidence, reversal/idempotency integration.
4. Finance Settings UI and API/UI contract tests.
5. Focused backend/frontend verification, independent read-only review, correction loop, local commits.

Shared model snapshot ownership stays with this branch. Any migration remains authored but unapplied.

## Checkpoint — branch creation and initial reconciliation

- Worktree was clean and detached at the exact required base before branch creation.
- Existing foundation includes `CurrencyMinorUnitPolicy`, ISO validation, 0–4 decimal support, ledger amount widening migration `20261001170752_FinanceCurrencyMinorUnitPrecision`, and Finance posting-engine currency-boundary usage.
- Initial broad search confirmed extensive two-decimal occurrences across generated/archived artifacts and other module ownership. These are evidence for classification, not authority for bulk edits.
- Current phase: inspect exact settings/service/posting/tax contracts, then implement the bounded Finance-owned policy layer.
- Tests: not yet run.
- Migrations/database: no migration authored or applied in this package; no database contacted.
- Review: independent review pending after implementation commits.
- Next safe action: add pure policy contracts and tests, then extend Finance Settings with validated backward-compatible defaults.

## Checkpoint — policy, settings, UI and migration tooling reconciliation

- Added a shared pure `PrecisionRoundingPolicy` covering unit price, exchange-rate and percentage precision, symmetric nearest/up/down increment rounding, monetary multiplication, tax, invoice rounding delta, UOM validation, and settlement tolerance.
- Added focused policy tests for 0-, 2-, and 3-decimal ISO currencies, high-precision FX, quantity × unit price, increment methods, tax scope arithmetic, invoice deltas, UOM increments, and settlement tolerances.
- Extended tenant Finance Settings across entity, DTO, service, API mapping and Finance UI with precision validation, backward-compatible defaults, accounting-activity locking, configured rounding accounts, and before/after Finance audit evidence.
- Currency decimal places remain sourced from the canonical ISO currency master and are read-only in the precision UI. Display settings are not used to mutate stored accounting values.
- Added an additive Unit Type quantity increment contract across entity, DTO, service validation and Finance Unit Type UI. It is governed independently of currency.
- Integrated the tax engine with tenant percentage precision, method, increment and line/tax-code-group/document scope. Aggregate residuals are assigned deterministically to the last contributing breakdown and surfaced as rounding evidence.
- EF tooling exposed a configuration-order mismatch where precision annotations retained legacy column type names. A final narrow model override now aligns runtime model, migration SQL type and snapshot metadata.
- Two failed scaffold attempts were discarded because the first retained legacy SQL types and the second used the repository's explicitly unsupported `TdcFastEfBuild` migration path and therefore proposed a full schema. The tracked base migration and model snapshot were restored exactly after each attempt. No migration was applied and no database was contacted.
- A normal full migration-metadata build is in progress before the final additive scaffold. This is a required repository constraint documented in `ErpSystem.Data.csproj`.
- API fast build: passed with 0 errors (existing warnings only) before the tax-engine integration. Data fast build: passed with 0 errors after the final precision override. Full post-integration verification remains pending.
- Review: independent read-only review remains pending after a coherent local commit.
- Next safe action: finish the normal migration scaffold, add targeted tax/settings coverage, run backend/frontend focused checks, then commit and dispatch independent review.

## Checkpoint — review candidate verification

- Authored additive migration `20261001213911_FinancePrecisionRoundingGovernance`; verified 16 additive columns, two indexes, four check constraints and two foreign keys with no table creation or destructive operation. The migration is not applied and no database was contacted.
- Normal full API build passed with 0 errors (existing repository warnings only).
- Pure precision policy suite passed: 15 passed, 0 failed.
- Focused Ghana statutory tax line-versus-document rounding regression passed: 1 passed, 0 failed.
- Changed-file frontend ESLint passed with no diagnostics. Full TypeScript validation still reports unrelated baseline errors in existing civil-engineering, medical, report-builder and legacy Finance fixture files; none reference this work package's changed files.
- Canonical AR/AP invoice posting behavior and gain/loss journal-line creation were not changed because that accounting side effect requires explicit user approval. Invoice-rounding configuration is therefore fail-closed: the UI cannot activate it and the API rejects activation until that integration is authorized.
- Review: coherent local commit and independent read-only accounting review are next.

## Checkpoint — independent review and correction loop

- Independent read-only review completed against commit `2dd80592c`. It confirmed the migration is additive/non-destructive and found no unrelated scope changes.
- The review identified that document/tax-code-group scope needs a document-wide AR/AP orchestrator, non-two-decimal tax posting needs widened posting/evidence storage, UOM increments need enforcement at every quantity write/post boundary, and invoice rounding needs canonical gain/loss posting. Those accounting behavior changes were not authorized in this work package.
- Corrective strategy is fail-closed rather than partial activation: aggregate tax scopes, non-two-decimal tax posting, UOM increment activation, and invoice rounding activation now reject explicitly. UI controls communicate/disable the unavailable choices, and database checks force Line tax scope plus disabled invoice rounding.
- Added service and database enum validation; tax increments must be whole multiples of the currency minor unit; tax percentage precision is capped at the existing four-decimal evidence capacity.
- Added stable tax component tie-breaking and corrected `TaxRoundingDelta` to compare governed results with raw tax for every supported scope.
- Precision lifecycle detection now considers posted/reversed journals rather than drafts. Precision policy changes require the Finance audit service and use a relational transaction so settings and audit evidence commit or roll back together.
- Correction verification: 15 focused core tests passed; corrected focused API tax regression passed; changed-file frontend ESLint passed; API compilation succeeded with existing warnings only.
- Deferred, approval-dependent adapters: document-wide AR/AP aggregate tax orchestration and allocation evidence; widening tax posting/snapshot storage for 0/3/4-decimal currencies and >4-decimal rates; canonical invoice rounding gain/loss lines; UOM enforcement at journal, budget and posting boundaries; explicit null-clear PATCH semantics; signed tax credit/reversal flow.
