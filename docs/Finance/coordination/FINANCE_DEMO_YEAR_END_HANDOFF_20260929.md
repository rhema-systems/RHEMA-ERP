# D05/D06 year-end book close handoff — 2026-09-29

## Scope and base

- Branch: codex/finance-demo-year-end-20260929
- Exact base: 52ce3a71595ba18d26d40658c1a368743c44a853
- Scope: Finance year-end service, its internal posting-engine authority, fiscal-year endpoint/client contracts, new book-close cycle persistence, deletion guard and focused tests.
- No deployment, push, live database write or migration application performed.

## Implementation

Explicit tenant/book selection is required; UI defaults only to the real active primary/default book. Nominal balances are filtered by AccountingBookId and checked against the selected book's functional currency. Each independently approved book-period closure is captured in the immutable cycle. Year-end postings use a validated server-only cycle entry point, never a client replication-suppression flag; ordinary primary replication and the ordinary direct-parallel prohibition remain in place.

The SQL Server path wraps cycle creation, posting, balance updates and cycle completion in one serializable transaction with the same tenant posting-representation application lock as ordinary posting. Closing is idempotent by tenant/key and frozen request identity. Reopen targets an exact cycle and original posting evidence, uses its original book/currency and native line amounts, appends reversal evidence, and leaves period reopening to the existing approval workflow. Reclose requires a new key and creates a new cycle.

Legacy fiscal-year-wide closes are preserved and fail closed pending a separately approved book-authority reconciliation. New book closes do not overwrite global FiscalYear.IsClosed or its legacy journal fields. Fiscal-year deletion now checks cycle evidence even for a no-activity close.

## Migration and coordinator-owned snapshot

Migration 20260930000100_YearEndBookCloseCycles is hand-authored and UNAPPLIED. Root owns ApplicationDbContextModelSnapshot.cs, which this branch deliberately does not edit.

Snapshot integration must add:

- YearEndBookCloseCycle entity and all inherited TenantEntity columns, matching the entity and migration.
- FiscalYear alternate key (TenantId, Id).
- Cycle composite foreign keys to FiscalYear (TenantId, Id), AccountingBook (TenantId, Id, Code), retained earnings Account (TenantId, Id), and closing/reversal JournalEntry (TenantId, Id, AccountingBookId); restricted deletes.
- Unique tenant/year/book/cycle number and tenant/idempotency key indexes; unique tenant/year/book filtered to Status IN ('Closing', 'Closed').
- RowVersion concurrency token, column lengths/decimal type, four check constraints and trigger metadata TR_YearEndBookCloseCycles_ImmutableEvidence.
- Model/table configuration is in ApplicationDbContext.cs and is the exact source of truth for regeneration.

The migration adds the immutable-evidence trigger and fails downgrade if any evidence exists. It does not synthesize book-period approvals or convert legacy close history.

## Verification checkpoint

- PASS: git diff --check (line-ending conversion warnings only).
- PASS: TypeScript/TSX syntax transpilation for all four changed frontend files.
- PASS: existing frontend ESLint configuration over those four files: zero errors, zero warnings.
- Initial cold focused build compiled Core/Data/API but did not execute tests. Test compilation failed on one nullable-Guid assertion (corrected) and migration class absence from the build's file list (file was authored after initial evaluation began; it is normally included by Data's default compile glob).
- Incremental focused rerun is queued after other Finance workers and coordinator WHT; no passing runtime-test result is claimed at this checkpoint.
- Full client typecheck/browser UAT, SQL Server rollback/concurrent-close tests, trigger execution, migration replay and generated snapshot diff are not yet verified.

Focused tests cover BASE100 / parallel200 / delta30 isolation, independent USD parallel close, immutable reopen/reclose, retry, independent book-period approval, locked/legacy rejection, currency/retained-earnings errors, pending journals, forged leaf calls, closed-book ordinary posting, preserved normal replication, endpoint permissions, migration operation shape and no-activity cycles. FiscalYearDeletionGuardTests adds zero-journal cycle retention coverage.

Compatibility-only test edit: FxRealizedUnrealizedRevaluationTests.ThrowAfterSuccessfulRevaluationPostingEngine forwards the newly added IFinancePostingEngine.PostYearEndAsync member; its existing revaluation behavior is unchanged.

## Required release gates

Independent high-risk review is required before integration. Coordinator must integrate the model snapshot and validate migration/model consistency. Apply schema only with separate authorization; otherwise new-cycle queries will not operate. Read-only UAT currently reports zero AccountingBookPeriods, so this workflow correctly refuses close until the existing governed setup and independent approval workflow creates the required book-period authority. No fabricated approvals or silent global IFRS-to-BASE rewrite are included.

