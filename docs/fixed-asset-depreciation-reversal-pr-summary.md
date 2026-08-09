# Fixed Asset Depreciation Reversal And Correction - PR Summary

Date: 2026-08-09

## Delivered

- Maker-checker depreciation reversal requests with separate permissions.
- Shared reversal-date/closed-period policy enforcement.
- Idempotent compensating journals through `IFinancePostingEngine`.
- Immutable original run/schedule evidence plus linked reversal lineage.
- Asset-book and default-register restoration with negative correction movements.
- Reverse-order dependency checks for later depreciation, valuation, disposal, and GL reclassification.
- Corrected same-period run revisions through `CorrectionSequence`.
- Operator workspace on the existing depreciation page.
- Current-balance report and period-close exclusions for reversed schedules.
- Audits, migration, limitations-register update, handbook chapter, and focused regression tests.

## Verification

- API project no-dependency build: passed, zero errors.
- `Batch=FinanceGoLive-FixedAssetDepreciationReversal`: 3/3 passed.
- Combined depreciation foundation and reversal regression set: 23/23 passed.
- Targeted frontend ESLint: passed.
- Data project Debug build: passed with zero errors (existing warnings only).
- Full API test-project compilation completed as part of the focused test run.
- Repository Release verification remains unsuitable as a slice gate: the full build spent more than ten minutes compiling the existing multi-megabyte migration designer chain without completing, while `--no-dependencies` consumed stale Release dependency assemblies and reported unrelated missing Estate, Budget and Ad Hoc Reporting DbSets. No depreciation-slice compile error occurred in the successful Debug/test builds.
- `git diff --check`: passed.

## Migration

`20260809122817_AddFixedAssetDepreciationReversalControls` is schema-only and is not applied automatically by this PR. It must be applied through the team's normal dry-run/UAT migration sequence before the UI/API is exercised against a database.

## Rollback

Before use, the migration down path removes only the new correction register, lineage columns, and revision key. After corrections exist, do not roll back without accountant-approved preservation of the original/reversal journal and subledger evidence.

## Remaining Boundaries

- `FIN-LIM-0031`: additional method algorithms; implementation policy is now documented separately.
- `FIN-LIM-0037`: valuation/impairment correction.
- `FIN-LIM-0039`: automatic final/partial-period depreciation at disposal.
- Client/UAT acceptance remains required before production sign-off.
