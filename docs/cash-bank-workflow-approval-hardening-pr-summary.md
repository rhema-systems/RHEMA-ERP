# Cash/Bank Workflow Approval And Posting Eligibility Hardening

## Definition Of Done

- [x] Cash/bank approval lifecycle is modeled on `CashTransaction`.
- [x] Workflow actions use the existing `IWorkflowIntegrationService` and Finance approval queue; no parallel approval system was added.
- [x] Cash/bank posting now fails closed unless the transaction is approved or already safely posted/idempotent.
- [x] Transfer creation no longer posts immediately; transfers are captured first and require explicit submit, approve, and post.
- [x] Controller actions map to existing Finance workflow permissions.
- [x] Tenant-aware tests cover blocked states, workflow submit/approve, unauthorized approval, cross-tenant workflow access, posted immutability, transfer capture, and approved transfer posting.
- [x] Finance audit events are emitted for captured, submitted, approved, rejected/returned/cancelled, blocked posting, and posted-after-approval outcomes.

## Lifecycle

`CashTransaction.ApprovalStatus` now supports:

| State | Meaning |
| --- | --- |
| `Captured` | Draft/captured operational transaction. Not postable. |
| `Submitted` | Submitted to the configured workflow engine. Not postable. |
| `Approved` | Workflow-approved and eligible for posting. |
| `Rejected` | Rejected by workflow approver. Not postable. |
| `Returned` | Returned/requested changes. Not postable until resubmitted and approved. |
| `Cancelled` | Cancelled before posting. Not postable. |
| `Posted` | Posted through `IFinancePostingEngine`; direct mutation remains blocked. |

## Workflow Engine Mapping

Service-level actions in `CashTransactionService` call the existing workflow integration layer:

- `SubmitAsync` -> `IWorkflowIntegrationService.SubmitAsync("CashTransaction", id)`
- `ApproveAsync` -> `CanUserApproveAsync`, then `ProcessApprovalAsync(..., "Approve", comments)`
- `RejectAsync` -> `CanUserApproveAsync`, then `ProcessApprovalAsync(..., "Reject", reason)`
- `ReturnAsync` -> `CanUserApproveAsync`, then `ProcessApprovalAsync(..., "RequestChanges", comments)`
- `CancelAsync` -> `CancelWorkflowAsync` when a submitted workflow is active

`FinanceApprovalsController` now also applies approved/rejected outcomes for `CashTransaction`, so decisions made through the central Finance approval queue update the cash/bank lifecycle state.

## Affected Files

- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `src/ErpSystem.Api/Controllers/Finance/CashTransactionController.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `src/ErpSystem.Api/Services/Finance/Cash/CashTransactionService.cs`
- `src/ErpSystem.Core/DTOs/Finance/CashTransactionDtos.cs`
- `src/ErpSystem.Core/Entities/Finance/CashTransaction.cs`
- `src/ErpSystem.Core/Enums/CashManagementEnums.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ICashManagementServices.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260705153000_AddCashTransactionWorkflowApprovalState.cs`
- `src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Controllers/Finance/FinanceControllerSecurityTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/CashBankTransactionPostingMigrationTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/CashBankWorkflowApprovalHardeningTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `docs/finance-permission-matrix.md`
- `docs/cash-bank-transaction-posting-migration-pr-summary.md`
- `docs/cash-bank-workflow-approval-hardening-pr-summary.md`

## Database And Migration Impact

Migration `20260705153000_AddCashTransactionWorkflowApprovalState` adds:

- `ApprovalStatus`
- `WorkflowInstanceId`
- `SubmittedAt`, `SubmittedById`
- `ApprovedAt`, `ApprovedById`
- `RejectedAt`, `RejectedById`
- `ApprovalComments`, `RejectionReason`
- `CancelledAt`, `CancelledById`, `CancellationReason`
- indexes on `(TenantId, ApprovalStatus)` and `(TenantId, WorkflowInstanceId)`

Existing rows are backfilled safely:

- posted rows -> `ApprovalStatus = Posted`
- unposted rows -> `ApprovalStatus = Captured`

## API Impact

Added cash/bank workflow endpoints:

- `POST /api/finance/cash-transactions/{id}/submit`
- `POST /api/finance/cash-transactions/{id}/approve`
- `POST /api/finance/cash-transactions/{id}/reject`
- `POST /api/finance/cash-transactions/{id}/return`
- `POST /api/finance/cash-transactions/{id}/cancel`

`POST /api/finance/cash-transactions/{id}/post` now requires `ApprovalStatus = Approved` unless it is returning an existing idempotent posted result.

Transfer creation behavior changed: `CreateTransferAsync` now captures the source and destination legs only. It does not auto-post and does not adjust transfer bank balances until the approved source leg is posted.

## Frontend Impact

No frontend changes were made in this batch. Existing clients that create transfers must now call submit, approval, and post actions before expecting GL posting or posted transfer balance movement.

## Tests

- `dotnet build tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\cash-bank-workflow\bin\ --no-restore --disable-build-servers -m:1`
  - Passed with 5 existing warnings.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\cash-bank-workflow\bin\ --no-build --no-restore --filter Batch=FinanceGoLive-CashBankWorkflow`
  - 12 passed.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\cash-bank-workflow\bin\ --no-build --no-restore --filter Batch~FinanceGoLive`
  - 138 passed.

## Static Verification

`rg -n "ISubledgerPostingService|PostApInvoiceAsync|PostApPaymentAsync|PostArInvoiceAsync|PostArPaymentAsync|PostSalesCreditNoteAsync" src\ErpSystem.Api\Services\Finance\Cash src\ErpSystem.Api\Controllers\Finance tests\ErpSystem.Api.Tests\Services\Finance --glob "*CashBank*.cs"` returned no matches.

No live cash/bank path uses legacy subledger posting.

## Tenant Isolation Verification

Cash/bank workflow actions load transactions by current `TenantId`. Posting still validates tenant ownership for:

- transaction header
- bank/cash account
- transfer destination account
- GL offset account
- posting event
- journal back-reference
- workflow outcome actions through tenant-filtered Finance approvals

Cross-tenant workflow submit is rejected by service tests.

## Accounting Impact

Cash/bank transactions that directly affect cash and liquidity now require workflow approval before posting to the GL. The posted GL and `FinancePostingEvent` remain the accounting source of truth. `CashTransaction.ApprovalStatus`, `IsPosted`, `PostedDate`, `JournalEntryId`, and bank operational balances remain operational/read-side fields that must reconcile back to posted ledger activity.

The follow-up cash/bank operational balance hardening batch removed receipt/payment capture-time balance mutation and now updates stored bank balance snapshots only from posted cash/bank transactions.

## Rollback Considerations

Rollback of the migration removes workflow state columns and indexes. If code is rolled back after users have relied on workflow statuses, suspend cash/bank posting until either the hardening release is restored or the operational statuses are reconciled from `FinancePostingEvents`.

The transfer behavior change is important: rolling back code could reintroduce immediate transfer posting unless the older compatibility path stays disabled.

## Known Limitations

- Full reversal/void posting for cash/bank transactions remains deferred.
- Reconciliation matching, reconciliation adjustments, and bank statement import are still out of scope for this batch.
- Receipt/payment operational bank balances are no longer updated at capture; stored bank balance snapshots are updated from posted cash/bank transactions only.
- Cross-currency bank transfers remain deferred to the FX batch.
- Frontend workflow buttons and status UX were not changed in this backend batch.
