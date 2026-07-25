# AP Payment Posting Migration to Finance Posting Engine

## Definition of Done

- [x] AP payment posting uses `IFinancePostingEngine`.
- [x] Legacy AP payment posting calls to `ISubledgerPostingService.PostApPaymentAsync` are removed from live AP payment paths.
- [x] Legacy AP payment subledger posting methods are marked obsolete and runtime-guarded.
- [x] AP payment posting validates tenant ownership, payment lifecycle, supplier, allocations, posted AP invoices, AP control account, bank GL account, open periods, balanced totals, and idempotency.
- [x] Posted AP payments cannot be mutated through allocation reversal, new allocation, or void mutation paths.
- [x] Finance audit events are recorded for AP payment approve/reject workflow outcomes, post, failed post, and duplicate post attempts. Submitted/voided event names are defined for the later workflow/reversal batches.
- [x] Automated tests cover success, failure, tenant isolation, duplicate posting, lifecycle immutability, and posting-engine regressions.

## Design

AP payment posting is now routed through `VendorPaymentService.PostAsync(Guid id)`, which builds a `FinancePostingRequestDto` and delegates ledger creation to `IFinancePostingEngine`.

The posted GL remains the accounting source of truth. AP payment posting creates posted `JournalEntry`, `AccountTransaction`, and `FinancePostingEvent` records through the central posting engine. It does not mutate `Account.Balance`.

Idempotency is enforced by the existing `FinancePostingEvent` tenant/source/action unique constraint and the posting engine's duplicate detection. Repeated AP payment posting returns the existing posting result safely and records an AP payment duplicate-posting audit event.

`VendorPayment.JournalEntryId` remains a repairable operational back-reference, not the accounting source of truth. The posting engine commits the journal, account transactions, and posting event atomically; the payment back-reference/status update is written immediately afterward and can be repaired by idempotent retry if interrupted.

## Lifecycle Rules

- Draft or pending-authorization payments cannot be posted.
- Authorized or processed payments can be posted.
- A payment must have at least one active allocation before posting.
- Every settled AP invoice must belong to the same tenant and already have a central posted AP invoice posting event.
- Overpayments are rejected in this batch because explicit AP overpayment/advance handling is not implemented yet.
- Posted payments cannot be allocated, allocation-reversed, or voided by mutation; reversal/void accounting remains a later controlled reversal batch.

## Accounting Treatment

- Debit AP control/payables for the settlement amount.
- Credit bank/cash GL account for the cash payment amount.
- Credit purchase discount received account for existing allocation discount amounts.
- Credit configured tax control account for existing WHT amounts.
- WHT uses existing payment/allocation fields only; full Ghana effective-dated WHT/tax rules remain in the tax batch.

## Affected Code

- `src/ErpSystem.Api/Services/Finance/AP/VendorPaymentService.cs`
  - Added `PostAsync`.
  - Added AP payment posting request builder and tenant/account/lifecycle validation helpers.
  - Removed live legacy `PostApPaymentAsync` and discount-adjustment calls from AP payment paths.
  - Stopped `CreateAsync` from auto-posting or creating cash transactions.
  - Migrated payment-batch processing to `PostAsync`.
  - Added posted-payment mutation guards.
  - Added AP payment audit writes.
- `src/ErpSystem.Api/Controllers/Finance/ApControllersConsolidated.cs`
  - Added `POST /api/ap/payments/{id}/post`.
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
  - Added AP payment approval/rejection audit writes.
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
  - Added AP payment `Post` action under `Finance.AP.Payments.Process`.
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
  - Registered `IVendorPaymentService`.
- `src/ErpSystem.Api/Services/Finance/GL/SubledgerPostingService.cs`
  - Marked legacy AP payment posting methods obsolete and runtime-guarded.
  - Marked legacy AP invoice posting obsolete and guarded for normal invoices.
- `src/ErpSystem.Core/Interfaces/Finance/IVendorPaymentService.cs`
  - Added `PostAsync`.
- `src/ErpSystem.Core/Interfaces/Finance/ISubledgerPostingService.cs`
  - Marked AP invoice/payment legacy methods obsolete.
- `src/ErpSystem.Core/DTOs/Finance/AccountsPayableDtos.cs`
  - Added `JournalEntryId` to `VendorPaymentDto`.
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
  - Added AP payment audit event taxonomy.
- `tests/ErpSystem.Api.Tests/Services/Finance/ApPaymentPostingMigrationTests.cs`
  - Added focused AP payment posting tests.

## Verification

- `dotnet build tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BaseOutputPath=.\.codex-build\ap-payments\bin\ -clp:ErrorsOnly`
  - Succeeded: 0 errors, 1 warning.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-build --filter "FullyQualifiedName~ApPaymentPostingMigrationTests" -p:BaseOutputPath=.\.codex-build\ap-payments\bin\`
  - Passed: 12, Failed: 0.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-build --filter "FullyQualifiedName~ApInvoicePostingMigrationTests" -p:BaseOutputPath=.\.codex-build\ap-payments\bin\`
  - Passed: 10, Failed: 0.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-build --filter "FullyQualifiedName~FinancePostingEngineTests|FullyQualifiedName~JournalEntryLifecycleBatch5Tests|FullyQualifiedName~FinanceAuditFoundationTests" -p:BaseOutputPath=.\.codex-build\ap-payments\bin\`
  - Passed: 30, Failed: 0.
- Final combined targeted run:
  - `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-build --filter "FullyQualifiedName~ApPaymentPostingMigrationTests|FullyQualifiedName~ApInvoicePostingMigrationTests|FullyQualifiedName~FinancePostingEngineTests|FullyQualifiedName~JournalEntryLifecycleBatch5Tests|FullyQualifiedName~FinanceAuditFoundationTests" -p:BaseOutputPath=.\.codex-build\ap-payments\bin\`
  - Passed: 52, Failed: 0.

## Rollback Considerations

Rollback is code-only. No database migration was added. Reverting this batch would restore the previous AP payment direct subledger posting behavior and automatic payment creation posting, which would reintroduce a ledger bypass around the central posting engine.

## Tenant Isolation

AP payment posting validates the current finance tenant against the payment, supplier, allocations, settled invoices, invoice posting events, AP control account, bank account, bank GL account, tax account, discount account, and posting engine request. Cross-tenant supplier, invoice settlement, AP control account, and bank account tests are included.

## Known Limitations

- Cash/bank operational transaction creation was deliberately not migrated in this batch; cash/bank posting and reconciliation integration remain in the cash/bank batch.
- AP payment reversal/void accounting is not implemented here; posted payment mutation is blocked until reversal posting is added.
- AP overpayments/advance payments are rejected because explicit overpayment handling is not yet implemented.
- WHT uses existing AP payment/allocation fields and configured tax control account only. Full Ghana effective-dated WHT/tax logic remains in the tax batch.
- Existing `VendorInvoice.PaidAmount` and `VendorPayment.AllocatedAmount` remain operational status/read fields. Go-live reporting must derive accounting balances from posted GL entries or controlled rebuildable read models.

## Accounting Impact

The AP invoice-to-payment accounting loop is now on the controlled posting engine: AP invoice posting creates the payable, and AP payment posting settles the payable against bank/cash, discounts, and existing WHT data. The implementation keeps the posted GL as source of truth and avoids mutable `Account.Balance` updates.
