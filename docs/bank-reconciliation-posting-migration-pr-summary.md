# Bank Reconciliation And Adjustment Posting Migration PR Summary

## Scope

This batch hardens bank reconciliation and adds reconciliation adjustment posting through the central Finance posting path.

No tax engine, FX, fixed asset, reporting, frontend, or data migration implementation was started in this batch.

## Reconciliation Source Of Truth

Posted GL entries remain the accounting source of truth.

Bank reconciliation book balance is calculated from the linked bank GL account's posted `AccountTransaction` movement plus the bank account opening balance. `BankAccount.CurrentBalance` and `AvailableBalance` are not used as authoritative reconciliation book balances.

## Lifecycle

Existing status values are mapped as follows:

- `Pending`: draft/open placeholder state.
- `InProgress`: active reconciliation and matching.
- `Completed`: finalized and ready for approval.
- `Approved`: approved/closed through the existing workflow engine.
- `Rejected`: rejected through workflow approval.
- `Cancelled`: cancelled before finalization.

Finalized or approved reconciliations cannot be mutated by matching, unmatching, adjustment posting, or cancellation.

## Matching Rules

Matching now enforces:

- Same tenant reconciliation, bank account, cash transaction, statement line, and statement.
- Same bank/cash account for the statement line and cash transaction.
- Cash/bank transaction must be posted, have `ApprovalStatus = Posted`, and have a same-tenant posted journal.
- Statement line must be unmatched and same-tenant.
- Cash transaction must not already be reconciled or matched in another reconciliation.
- Direction and amount must be consistent for manual matching.

Unposted cash/bank transactions are excluded from reconciliation counts, summary, and auto-match candidates.

## Adjustment Posting Design

Reconciliation adjustments are represented as explicit cash/bank adjustment transactions linked by `CashTransaction.ReconciliationId`.

Supported adjustment types:

- Bank charge
- Bank fee
- Interest income
- Adjustment receipt
- Adjustment payment
- Correction receipt
- Correction payment

Posting treatment reuses the existing cash/bank posting engine path:

- Bank charge/payment: debit configured offset expense/account, credit bank GL.
- Interest/receipt: debit bank GL, credit configured offset income/account.

The adjustment path uses `CashTransactionService.PostAsync`, which calls `IFinancePostingEngine`. It does not create a separate GL posting path and does not mutate `Account.Balance`.

Adjustment idempotency:

- `IdempotencyKey` is required for reconciliation adjustments. Duplicate adjustment requests for the same reconciliation return the existing posted adjustment and record a reconciliation duplicate-posting audit event.
- The underlying posting event is still protected by the central posting engine's source document/action idempotency.

## Workflow Engine Integration

Bank reconciliation approval continues to use the existing workflow service for `BankReconciliation`.

The central Finance approval queue now records Finance audit events for bank reconciliation approval and rejection outcomes.

## Audit Events Added

Added Finance audit event constants for:

- Reconciliation created
- Statement line created
- Transaction matched
- Transaction unmatched
- Reconciliation submitted
- Reconciliation approved
- Reconciliation rejected
- Reconciliation adjustment posted
- Duplicate reconciliation adjustment posting attempt
- Reconciliation finalized
- Reconciliation cancelled

This batch records reconciliation created, matched, unmatched, adjustment posted, duplicate adjustment attempt, finalized, cancelled, approved, and rejected events where those paths are implemented.

## Diagnostics Added

Added:

- `docs/sql/finance-bank-reconciliation-diagnostics.sql`

Checks include:

- Cross-tenant reconciliation matches.
- Finalized/approved reconciliations with non-zero differences.
- Statement lines matched to unposted transactions.
- Cash/bank transactions reconciled more than once.
- Reconciliation adjustments missing posting events.
- Posting events without same-tenant posted journal references.
- Stored bank snapshots not matching posted GL after reconciliation activity.

## Affected Files

- `src/ErpSystem.Api/Services/Finance/Cash/BankReconciliationService.cs`
- `src/ErpSystem.Api/Controllers/Finance/BankReconciliationController.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `src/ErpSystem.Core/DTOs/Finance/BankReconciliationDtos.cs`
- `src/ErpSystem.Core/Enums/CashManagementEnums.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ICashManagementServices.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/BankReconciliationPostingMigrationTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `docs/sql/finance-bank-reconciliation-diagnostics.sql`

## Definition Of Done

- [x] Reconciliation book balance derives from posted GL activity.
- [x] Unposted cash/bank transactions cannot be reconciled.
- [x] Matching enforces tenant, bank account, statement line, posted journal, amount, and direction rules.
- [x] Finalized/approved reconciliations are immutable through normal match/unmatch/cancel paths.
- [x] Reconciliation adjustments post through `IFinancePostingEngine` via the existing cash/bank posting service.
- [x] Reconciliation adjustment duplicate requests can return the existing posted adjustment when an idempotency key is supplied.
- [x] Reconciliation audit events are added for implemented lifecycle and adjustment paths.
- [x] Tenant-safe diagnostics are added.
- [x] Automated tests were added and Finance go-live regression tests passed.

## Build And Test Results

Backend build:

- `dotnet build tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\bank-reconciliation\bin\ -p:WarningLevel=0 --no-restore --disable-build-servers -m:1 -v:minimal`
- Result: Passed.

Targeted reconciliation tests:

- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\bank-reconciliation\bin\ -p:WarningLevel=0 --no-restore --disable-build-servers -m:1 --filter Batch=FinanceGoLive-BankReconciliation -v:minimal`
- Result: Passed, 11/11.

Cash/bank and reconciliation regression slices:

- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\bank-reconciliation\bin\ --no-build --no-restore --filter "Batch=FinanceGoLive-BankReconciliation|Batch=FinanceGoLive-CashBankBalance|Batch=FinanceGoLive-CashBankWorkflow|Batch=FinanceGoLive-CashBankPosting" -v:minimal`
- Result: Passed, 42/42.

Finance go-live regression slice:

- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\bank-reconciliation\bin\ --no-build --no-restore --filter Batch~FinanceGoLive -v:minimal`
- Result: Passed, 158/158.

Note: warning output was suppressed for the build/test compile command because the repository currently emits a very large number of pre-existing warnings.

Frontend build/type-check:

- Not applicable. No frontend files changed in this batch.

## Migration Impact

No EF migration was added.

This batch uses existing `BankReconciliation`, `ReconciliationMatch`, `BankStatementLine`, `CashTransaction`, `JournalEntry`, `AccountTransaction`, and `FinancePostingEvent` structures.

## API Impact

Added service/controller endpoints for:

- Create and post reconciliation adjustment.
- Finalize reconciliation.
- Cancel open reconciliation.

Existing approve behavior remains tied to the workflow engine.

## Rollback Considerations

Rollback would restore reconciliation reliance on stored bank snapshots and allow weaker matching rules. If rollback is required, accountant review is needed for any reconciliation finalized or adjusted under this batch because those records were validated against posted GL.

## Tenant-Isolation Verification

Implemented and tested tenant checks for:

- Bank account ownership.
- Reconciliation header ownership.
- Statement and statement line ownership.
- Cash transaction ownership.
- Matched item ownership.
- Adjustment offset account ownership.
- Posted journal ownership.
- Posting event ownership.

## Accounting Impact

This batch aligns reconciliation with the GL source-of-truth principle. Reconciliation adjustments now create normal posted cash/bank accounting entries through the central posting engine, preserving double-entry posting, posting event idempotency, and audit trail linkage.

## Known Limitations

- Reconciliation adjustments are represented as linked cash/bank transactions rather than a separate adjustment header table. This avoids new schema in this batch but means adjustment metadata is carried by cash transaction fields plus audit events.
- Full reversal/void of finalized reconciliation adjustments remains part of the later reversal/void workstream.
- Bank statement import UI/API was not changed in this batch.
- Snapshot rebuild/repair remains a migration/opening-balance or reconciliation sign-off follow-up.
- FX/cross-currency reconciliation remains deferred to the FX batch.
