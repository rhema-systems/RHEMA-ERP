# Cash/Bank Operational Balance Hardening PR Summary

## Scope

This batch hardens cash/bank operational balance behavior after cash/bank workflow approval and posting-engine migration.

No bank reconciliation, tax, FX, fixed asset, reporting, frontend, or data migration implementation was started in this batch.

## Source-Of-Truth Decision

The posted General Ledger and `FinancePostingEvent` remain the accounting source of truth.

`BankAccount.CurrentBalance` and `BankAccount.AvailableBalance` are treated as read-side operational snapshots only. They are not authoritative accounting balances and must be rebuildable or repairable from posted ledger activity and cash/bank posting events.

Unapproved, rejected, cancelled, or otherwise unposted cash/bank transactions must not update stored bank balance snapshots.

## Changes Made

### Cash Transaction Service

Affected file:

- `src/ErpSystem.Api/Services/Finance/Cash/CashTransactionService.cs`

Affected members:

- `CreateReceiptAsync`
- `CreatePaymentAsync`
- `PostAsync`
- `DeleteAsync`
- `ApplyPostedCashBankBalanceSnapshotAsync`
- `AdjustBankBalanceSnapshotAsync`

Changes:

- Removed capture-time stored balance updates for receipt transactions.
- Removed capture-time stored balance updates for payment transactions.
- Removed delete-time balance reversal for unposted receipt/payment deletes because capture no longer mutates balances.
- Standardized stored bank snapshot updates to occur only after successful approved posting.
- Added tenant-guarded balance snapshot adjustment logic for posted receipt, payment, and transfer transactions.
- Skipped balance snapshot adjustment for duplicate/idempotent posting results so repeated post attempts do not double-update snapshots.
- Kept `Account.Balance` untouched.

### Bank Account Service

Affected file:

- `src/ErpSystem.Api/Services/Finance/Cash/BankAccountService.cs`

Notes:

- `GetBalanceAsync` and `UpdateBalanceAsync` were inspected.
- `UpdateBalanceAsync` remains available for existing compatibility paths, but normal cash transaction capture/posting now uses the cash transaction posting flow and tenant-guarded snapshot helper instead of capture-time mutation.

### Diagnostics

Added file:

- `docs/sql/finance-cash-bank-operational-balance-diagnostics.sql`

Diagnostics include tenant-safe checks for:

- Stored bank snapshots that do not match posted GL movement for linked bank GL accounts.
- Unposted cash/bank transactions that may explain snapshot variance.
- Posted cash/bank transactions with missing or inconsistent posting back-references.
- Cross-tenant posting event, journal, account transaction, or bank account links.
- Transfer source/destination legs that are not consistently reflected.

Diagnostics are accountant-reviewable and must be run per tenant before repair or migration sign-off.

### Tests

Added file:

- `tests/ErpSystem.Api.Tests/Services/Finance/CashBankOperationalBalanceHardeningTests.cs`

Updated file:

- `tests/ErpSystem.Api.Tests/Services/Finance/CashBankWorkflowApprovalHardeningTests.cs`

Project file updated:

- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`

Coverage added or updated for:

- Capturing a receipt does not change stored bank balance snapshots.
- Capturing a payment does not change stored bank balance snapshots.
- Approved receipt posting updates stored read-side snapshot once.
- Approved payment posting updates stored read-side snapshot once.
- Cancelled unposted transaction does not affect stored bank balance snapshots.
- Transfer capture does not affect balances before posting.
- Approved transfer posting updates source and destination snapshots consistently.
- Duplicate/idempotent posting does not double-update stored balance snapshots.
- Cross-tenant bank account reference cannot affect another tenant's bank balance snapshot.
- Diagnostic query detects mismatch between stored balance snapshot and posted GL movement.

## Definition Of Done

- [x] Receipt/payment capture no longer mutates stored bank balances.
- [x] Posted receipt/payment/transfer transactions update read-side bank snapshots only after posting.
- [x] Duplicate/idempotent posting does not double-apply stored balance snapshot updates.
- [x] Cross-tenant balance mutation is rejected.
- [x] `Account.Balance` is not mutated.
- [x] Tenant-safe diagnostics were added for snapshot-to-GL reconciliation.
- [x] Automated tests were added and existing cash/bank regression slices passed.
- [x] No bank reconciliation, tax, FX, fixed asset, reporting, frontend, or data migration work was started.

## Build And Test Results

Backend build:

- `dotnet build tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\cash-bank-balance\bin\ --no-restore --disable-build-servers -m:1`
- Result: Passed.
- Warnings: 5 existing warnings unrelated to this batch.

Targeted balance tests:

- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\cash-bank-balance\bin\ --no-build --no-restore --filter Batch=FinanceGoLive-CashBankBalance`
- Result: Passed, 9/9.

Cash/bank regression slices:

- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\cash-bank-balance\bin\ --no-build --no-restore --filter "Batch=FinanceGoLive-CashBankBalance|Batch=FinanceGoLive-CashBankWorkflow|Batch=FinanceGoLive-CashBankPosting"`
- Result: Passed, 31/31.

Finance go-live regression slice:

- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\cash-bank-balance\bin\ --no-build --no-restore --filter Batch~FinanceGoLive`
- Result: Passed, 147/147.

Frontend build/type-check:

- Not applicable. No frontend files changed.

## Migration Impact

No EF migration was added in this batch.

This is a behavior and diagnostic hardening change. Existing stored cash/bank balances may require tenant-safe diagnostic review and repair before go-live.

## API Impact

No new API endpoints were added.

Behavioral impact:

- Creating/capturing cash/bank receipts and payments no longer changes stored bank balance snapshots.
- Stored bank snapshots move only after approved posting through the existing cash/bank posting flow.

## Rollback Considerations

Rollback would restore capture-time balance mutation for receipt/payment transactions, which can make unapproved or unposted transactions appear in operational balances before GL posting. That behavior conflicts with the go-live source-of-truth decision and should not be reintroduced without an explicit product decision.

If operational balance differences are found after deployment, use the tenant-safe diagnostic script before applying accountant-reviewed repair adjustments.

## Tenant-Isolation Verification

Tenant guardrails added or retained:

- Snapshot adjustment loads bank accounts by `TenantId`, bank account ID, and non-deleted status.
- Cross-tenant bank account references are rejected before stored snapshot mutation.
- Diagnostics filter by `@TenantId` and flag cross-tenant journal/posting/account links.
- Tests verify cross-tenant cash/bank references cannot mutate another tenant's bank balance snapshot.

## Accounting Impact

This batch aligns cash/bank operational balances with the Finance go-live principle that posted GL activity is the accounting source of truth.

Stored bank balances are now treated as read-side operational snapshots updated from posted cash/bank activity, not as authoritative balances changed during capture. This reduces the risk that unapproved or unposted cash/bank activity distorts liquidity reporting before GL posting.

## Known Limitations

- A full rebuild/repair command for stored bank snapshots is not implemented in this batch. The tenant-safe SQL diagnostic is the go-live control until the rebuild command is added.
- Opening-balance and data-migration balance repair remains part of the migration/opening-balance workstream.
- Bank reconciliation and reconciliation adjustment posting were not started.
- Reversal/void balance snapshot behavior remains tied to the later reversal/void implementation.
- FX and cross-currency bank account handling remain deferred to the FX batch.
