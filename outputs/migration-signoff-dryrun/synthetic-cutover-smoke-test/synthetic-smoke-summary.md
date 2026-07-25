# Synthetic UAT Cutover Smoke Test Result

This is technical smoke-test evidence only. It is not accountant approval or production sign-off evidence.

- Status: `PassedWithAcceptedLimitations`
- Run reference: `SIGNOFF-20260710010353-7bd1d05b`
- Blocking findings: `0`
- Warnings: `1`
- Checks: `11`
- Accepted limitations: `24`
- Not applicable limitations: `1`

## UAT Demo Data Reset
- `FA-2024-BLDG-001` / `e09e906d-6d03-4685-ac98-6eb91ade421d`: Soft-deleted in RhemaERP_UAT_DryRun only for synthetic GL-only smoke scope. Row had no source, capitalization, journal, or posting-event evidence; posted GL was not mutated.
- `FA-2024-EQP-001` / `23e99b72-9af9-492c-baca-653777236142`: Soft-deleted in RhemaERP_UAT_DryRun only for synthetic GL-only smoke scope. Row had no source, capitalization, journal, or posting-event evidence; posted GL was not mutated.
- `FA-2024-EQP-002` / `de297d7f-c7ad-4927-b627-1e43ec9586a7`: Soft-deleted in RhemaERP_UAT_DryRun only for synthetic GL-only smoke scope. Row had no source, capitalization, journal, or posting-event evidence; posted GL was not mutated.
- `FA-2024-VEH-001` / `ba1f441e-2025-41ca-82f7-1bb9213cc9c2`: Soft-deleted in RhemaERP_UAT_DryRun only for synthetic GL-only smoke scope. Row had no source, capitalization, journal, or posting-event evidence; posted GL was not mutated.

## Checks
- `Passed` / `Info` / `TRIAL-BALANCE-BALANCED`: Posted GL debit and credit totals balance for the sign-off scope.
- `Passed` / `Info` / `OPENING-BALANCE-CHECKS`: Opening-balance batches are posted, balanced, referenced, and not duplicated for the sign-off scope.
- `Passed` / `Info` / `BACK-REFERENCE-DIAGNOSTIC`: Posting back-reference diagnostics found no unresolved gaps.
- `Passed` / `Info` / `BANK-SNAPSHOT-DIAGNOSTIC`: Bank snapshot diagnostics completed without unaccepted blocking variance.
- `Passed` / `Info` / `AP-SETTLEMENT-CHECKS`: AP settlement read-model and control checks did not find blocking variance.
- `Passed` / `Info` / `AR-SETTLEMENT-CHECKS`: AR settlement read-model and control checks did not find blocking variance.
- `Passed` / `Info` / `FA-RECONCILIATION-CHECKS`: Fixed asset sign-off checks did not find missing references or unreconciled disposal NBV.
- `Passed` / `Info` / `TAX-RECONCILIATION-CHECKS`: Tax snapshot/configuration checks did not find blocking variance.
- `Warning` / `Warning` / `WORKFLOW-PENDING-HIGH-RISK`: Pending workflow instances exist and must be reviewed before production sign-off.
- `Passed` / `Info` / `POSTING-BYPASS-BLOCKED`: No legacy posting bypass evidence was detected in FinancePostingEvents.
- `Passed` / `Info` / `FIN-LIM-0048-CUTOVER-SCOPE`: FIN-LIM-0048 is not applicable to this tenant cutover because no AP/AR/fixed-asset source-level opening balances were declared.

## Evidence Manifest
- `Available` / `TrialBalance` / `Posted GL`: Report is listed as required sign-off evidence.
- `Available` / `BalanceSheet` / `Posted GL`: Report is listed as required sign-off evidence.
- `Available` / `IncomeStatement` / `Posted GL`: Report is listed as required sign-off evidence.
- `Available` / `DetailedLedger` / `Posted GL`: Report is listed as required sign-off evidence.
- `Available` / `CashBankLedger` / `Posted GL`: Report is listed as required sign-off evidence.
- `Available` / `ApAging` / `Settlement read model built from posted subledger facts`: Report is listed as required sign-off evidence.
- `Available` / `ArAging` / `Settlement read model built from posted subledger facts`: Report is listed as required sign-off evidence.
- `Available` / `ApControlReconciliation` / `Posted GL`: Report is listed as required sign-off evidence.
- `Available` / `ArControlReconciliation` / `Posted GL`: Report is listed as required sign-off evidence.
- `Available` / `FixedAssetRegister` / `Posted GL reconciled to fixed asset subledger snapshots`: Report is listed as required sign-off evidence.
- `Available` / `FixedAssetRollForward` / `Posted GL reconciled to fixed asset subledger snapshots`: Report is listed as required sign-off evidence.
- `Available` / `FixedAssetGlReconciliation` / `Posted GL reconciled to fixed asset subledger snapshots`: Report is listed as required sign-off evidence.
- `Available` / `TaxOutput` / `Posted tax snapshots and withholding records reconciled to posted GL`: Report is listed as required sign-off evidence.
- `Available` / `TaxInput` / `Posted tax snapshots and withholding records reconciled to posted GL`: Report is listed as required sign-off evidence.
- `Available` / `TaxNetSummary` / `Posted tax snapshots and withholding records reconciled to posted GL`: Report is listed as required sign-off evidence.
- `Available` / `VatWithholding` / `Posted tax snapshots and withholding records reconciled to posted GL`: Report is listed as required sign-off evidence.
- `Available` / `WhtPayable` / `Posted tax snapshots and withholding records reconciled to posted GL`: Report is listed as required sign-off evidence.
- `Available` / `WhtReceivable` / `Posted tax snapshots and withholding records reconciled to posted GL`: Report is listed as required sign-off evidence.
- `Available` / `TaxAccountReconciliation` / `Posted tax snapshots and withholding records reconciled to posted GL`: Report is listed as required sign-off evidence.
- `Available` / `OpeningBalanceDiagnostics` / `Controlled opening-balance batches and FinancePostingEvents`: Generated by the migration sign-off run checks and SQL diagnostics.
- `Available` / `BankSnapshotDiagnostics` / `Posted GL movement compared to bank read-side snapshots`: Generated by IMigrationSignOffService bank snapshot diagnostics.
- `Available` / `PostingBackReferenceDiagnostics` / `FinancePostingEvents and source-document back-references`: Generated by IMigrationSignOffService posting back-reference diagnostics.
- `Available` / `LimitationAcceptanceMatrix` / `Finance go-live limitations register and run-time acceptance request`: Generated by the migration sign-off run output.
