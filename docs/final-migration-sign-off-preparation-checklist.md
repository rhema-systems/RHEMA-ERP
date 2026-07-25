# Final Finance Migration and Accountant Sign-Off Preparation Checklist

## Purpose

This checklist defines the evidence required before final Finance migration/sign-off scenario execution.

It is a preparation artifact, not a production migration runbook. Production migration execution remains a separate final scenario pack.

The backend dry-run/UAT evidence pack is generated through:

- `POST /api/finance/migration-signoff/final-signoff/run`
- `POST /api/finance/migration-signoff/final-signoff/review`

The run returns a tenant-scoped status, check list, evidence export manifest, limitation acceptance matrix, and `FIN-LIM-0048` cutover data-shape decision.

## Pre-Migration Configuration

- Tenant exists, is active, and has the correct functional currency.
- Functional currency is locked after accounting activity.
- Fiscal year and opening fiscal period are configured.
- Opening fiscal period is open/unlocked.
- Chart of accounts is complete, active, tenant-scoped, and direct-posting rules are reviewed.
- AP and AR control accounts are configured.
- Cash/bank GL accounts are configured and linked to bank accounts.
- Ghana tax accounts, tax groups, tax rules, WHT/VAT withholding settings, and certificate/reference fields are configured.
- FX rate tables are tenant-scoped, approved where workflow requires approval, and effective for cutover.
- Fixed asset category/account mappings are configured.
- Workflow definitions are seeded for opening-balance and high-risk Finance actions.
- Finance report/export permissions are assigned and tenant-safe.

## Opening-Balance Posting

- Balanced GL opening trial balance imported by explicit book classification.
- `ALL_ACTIVE_BOOKS` imports rejected.
- Unbalanced or single-sided import files rejected unless a controlled balanced format is provided.
- Opening-balance batch validation completed.
- Workflow approval completed where configured.
- Opening-balance batch posted through `IFinancePostingEngine`.
- Posted opening-balance journal, account transactions, and posting event exist.
- Opening-balance batch stores journal and posting-event references.
- No direct `Account.Balance` mutation occurred.
- No direct `BankAccount.CurrentBalance` mutation occurred during opening-balance posting.

## AP/AR Settlement Read Model

- AP opening invoices, if required, are loaded as posted AP source documents.
- AR opening invoices, if required, are loaded as posted AR source documents.
- AP settlement read model rebuilt.
- AR settlement read model rebuilt.
- AP aging generated from settlement read model.
- AR aging generated from settlement read model.
- AP control reconciliation generated and reviewed.
- AR control reconciliation generated and reviewed.
- Operational paid/credited fields compared to read model as diagnostics only.
- Unapplied payments/receipts and functional-currency advances reviewed through the settlement read-model balances; this is resolved under `FIN-LIM-0045`.

## Fixed Assets

- Fixed asset opening register loaded through supported source-document/import path if asset register sign-off is required.
- Fixed asset capitalization records tie to posted GL.
- Depreciation records tie to posted GL.
- Revaluation/impairment records tie to posted GL.
- Disposal records tie to posted GL.
- Fixed asset roll-forward generated.
- Fixed asset GL reconciliation generated.
- Any unsupported fixed asset opening registers remain rejected under `FIN-LIM-0048`.

## Bank Snapshot Diagnostics

- Bank snapshot diagnostic mode run tenant-wide.
- Variances reviewed against posted GL movement.
- Rebuild mode run only after review approval.
- Rebuild output retained as evidence.
- Rebuild confirmed to mutate only read-side bank snapshots.
- Bank reconciliation reports rerun after rebuild where relevant.

## Posting Back-Reference Repair

- Posting back-reference diagnostic mode run tenant-wide.
- Missing source-document links reviewed.
- Ambiguous or conflicting matches refused and escalated.
- Repair mode run only for unambiguous gaps.
- Repair output retained as evidence.
- Repair confirmed to mutate no posted GL.

## Trial Balance And Control Reconciliations

- Trial balance balances by tenant, book, and period.
- Detailed ledger ties to trial balance.
- AP outstanding ties to AP control account or documented timing differences.
- AR outstanding ties to AR control account or documented timing differences.
- Cash/bank ledger ties to posted GL and bank snapshot diagnostics.
- Tax account reconciliation ties snapshots to posted GL tax accounts.
- Fixed asset reconciliation ties subledger snapshots to posted GL.

## Export Pack Evidence

- Trial balance CSV export generated.
- Balance sheet CSV export generated.
- Income statement CSV export generated.
- Detailed ledger CSV export generated.
- Cash/bank ledger CSV export generated.
- AP aging and control reconciliation CSV exports generated.
- AR aging and control reconciliation CSV exports generated.
- Fixed asset register, roll-forward, and GL reconciliation CSV exports generated.
- Ghana tax report and reconciliation CSV exports generated.
- Export audit events reviewed.

## Limitation Acceptance

- Open go-live-blocking limitations reviewed.
- Any limitation accepted as non-blocking has accounting/product leadership approval.
- `FIN-LIM-0048` reviewed against production cutover subledger balance requirements.
- Sign-off run request declares whether AP/AR/fixed-asset source-level openings are in scope.
- `FIN-LIM-0048` is marked not applicable only when the tenant cutover has no open AP/AR/fixed-asset source balances requiring aging/register proof.
- `FIN-LIM-0048` blocks sign-off when source-level AP/AR/fixed-asset openings are required but not loaded through supported posted source-document/import paths.
- Unapplied payment/receipt and functional-currency advance balances are reviewed separately from invoice aging; foreign-currency advances remain safely rejected pending dedicated FX settlement support.
- Reversal/correction limitations reviewed against cutover correction policy.

## Rollback And Reset

- Dev/UAT reset strategy confirmed before migration rehearsal.
- Schema rollback impact documented.
- Posted GL rollback requires controlled reversal or environment reset.
- Read-side snapshot rebuilds are repeatable after posted GL changes.
- Back-reference repairs are repeatable in diagnostic mode and do not create journals.

## Final Go/No-Go

- Data/API/test builds pass.
- Finance regression slice passes.
- Opening-balance posting evidence reviewed.
- AP/AR settlement read model evidence reviewed.
- Bank snapshot evidence reviewed.
- Back-reference repair evidence reviewed.
- Tax and fixed asset reconciliation evidence reviewed.
- Export pack evidence reviewed.
- Remaining limitations accepted or scheduled.
- Backend sign-off run status is `Passed` or `PassedWithAcceptedLimitations`.
- Accountant sign-off recorded.
- Product/accounting go/no-go recorded.
