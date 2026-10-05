# Finance AR Receipt Source-Line Remediation - 2026-10-05

## Objective

Diagnose and correct AR receipt processing that fails with: At least one authoritative Finance source line is required.

## Scope and evidence

- Branch: codex/finance-uat-remediation-20261004
- Worktree: .w/fin-uat-remediation-20261004
- Exact base: 121f051149c1bdcf4d7385521c08c31db9fe88c8
- User evidence: receipt cash GHS 1,332 plus WHT GHS 108 reduces the selected invoice by GHS 1,440, but processing is rejected by the Finance source-line validator.

## Root cause and classification

- Confirmed code integration defect.
- The customer-payment route is governed at SettlementAllocationLine grain.
- For an allocated receipt, PaymentService correctly determines that receipt-owned source lines are empty because dimensions must inherit from the exact originating invoice lines through FinancePaymentDimensionAdapter.
- PaymentService then incorrectly invokes IFinanceSourceDimensionService with that empty list before the settlement adapter runs.
- FinanceSourceDimensionAssignmentStore correctly rejects empty authoritative source-line registration, producing the displayed error.
- The reference, WHT certificate, notes, cash amount, and allocation arithmetic are not the cause.

## Safe remediation

- Do not synchronize or freeze receipt-owned source-line evidence when an allocated customer receipt has no receipt-owned economic line.
- Continue synchronizing and freezing the governed settlement-allocation evidence.
- Preserve receipt-owned source-line registration for unallocated customer advances and fixed-asset disposal sale receipts.
- Continue rejecting browser-supplied line dimensions for allocated receipts.

## Changed files

- src/ErpSystem.Api/Services/Finance/AR/PaymentService.cs
- tests/ErpSystem.Api.Tests/Services/Finance/ArReceiptPostingMigrationTests.cs
- this ledger

## Verification

- Isolated project-reference build through the focused test run: passed with 0 compile errors.
- `AllocatedArReceipt_ShouldUseSettlementEvidenceWithoutEmptySourceLineRegistration`: passed 1/1, then passed again in the combined receipt/till run.
- Combined receipt/till backend contracts: passed 2/2.
- `git diff --check`: passed (line-ending conversion notices only).

## Migrations and application state

- Database migrations: none.
- UAT data mutation: none.
- Deployment/restart: not performed.

## Completed commit

- `4ce834927a78b742ea264194ab6274b5338636e4` - `fix(finance): close receipt and cash approval gaps`

## Authorization boundaries

Local diagnosis, implementation, testing, commit, and push to existing PR #352 are within the active Finance remediation scope. Do not merge, deploy, restart services, apply migrations, or mutate UAT data without separate authorization.
