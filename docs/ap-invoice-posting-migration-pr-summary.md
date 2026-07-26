# AP Invoice Posting Migration to Finance Posting Engine

## Definition of Done

- [x] AP invoice posting uses `IFinancePostingEngine`.
- [x] Legacy AP invoice calls to `ISubledgerPostingService.PostApInvoiceAsync` are bypassed from AP approval paths.
- [x] AP invoice posting validates tenant ownership, lifecycle state, configured accounts, open periods, balanced totals, and idempotency.
- [x] Posted AP invoices cannot be edited or deleted through the AP service.
- [x] Finance audit events are recorded for AP submit, approve, reject, post, failed post, and duplicate post attempts.
- [x] Automated tests cover success, failure, tenant isolation, duplicate posting, lifecycle immutability, and regressions.

## Design

AP invoice posting is now routed through `VendorInvoiceService.PostAsync(Guid id)`, which builds a `FinancePostingRequestDto` and delegates ledger creation to `IFinancePostingEngine`.

The posted GL remains the accounting source of truth. AP invoice posting creates posted `JournalEntry`, `AccountTransaction`, and `FinancePostingEvent` records through the central posting engine. It does not mutate `Account.Balance`.

Idempotency is enforced by the existing `FinancePostingEvent` tenant/source/action unique constraint and the posting engine's duplicate detection. Repeated AP invoice posting returns the existing posting result safely and records an AP duplicate-posting audit event.

## Affected Code

- `src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.cs`
  - Added `PostAsync`.
  - Added AP posting request builder and tenant/account validation helpers.
  - Replaced final approval legacy posting call with `PostAsync`.
  - Added posted-invoice edit/delete guards.
  - Added AP-specific audit events.
- `src/ErpSystem.Api/Controllers/Finance/ApControllersConsolidated.cs`
  - Added `POST /api/ap/invoices/{id}/post` guarded by `Finance.AP.Invoices.Post`.
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
  - Replaced generic workflow AP finalizer legacy posting call with `IVendorInvoiceService.PostAsync`.
  - Added AP approve/reject audit writes in workflow outcome handling.
- `src/ErpSystem.Core/Interfaces/Finance/IVendorInvoiceService.cs`
  - Added `PostAsync`.
- `src/ErpSystem.Core/DTOs/Finance/AccountsPayableDtos.cs`
  - Added `JournalEntryId` to AP invoice DTOs.
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
  - Added AP invoice audit event taxonomy.
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
  - Registered `IVendorInvoiceService`.
- `tests/ErpSystem.Api.Tests/Services/Finance/ApInvoicePostingMigrationTests.cs`
  - Added focused AP posting tests.

## Accounting Treatment

- Debit AP invoice line expense, inventory, clearing, or GRV accrual accounts based on existing line/settings data.
- Debit input tax control account when AP invoice tax exists and a tenant tax control account is configured.
- Credit AP control account using invoice/account/supplier/settings priority.
- Discounts are credited to the configured purchase discount received account.
- WHT is not posted at invoice posting in this batch; it remains part of payment/tax batches.

## Known Limitations

- Opening-balance AP invoice posting is intentionally blocked and deferred to the data migration/opening balance batch.
- Full Ghana effective-dated tax calculation is not implemented here; this batch only maps existing AP tax amounts to an existing configured tax control account.
- AP payments, AR, cash/bank, FX, fixed assets, reporting, and frontend changes are out of scope.
- Static verification before AP payment migration confirmed no remaining live AP invoice posting path calls `ISubledgerPostingService.PostApInvoiceAsync`; only the legacy method/interface and the regression test that verifies it is not called remain.
- `ISubledgerPostingService.PostApInvoiceAsync` is marked obsolete and guarded so normal AP invoices cannot use the legacy path. It remains only as a deferred opening-balance/migration compatibility surface.
- `JournalEntry`, `AccountTransaction`, and `FinancePostingEvent` are committed atomically inside `IFinancePostingEngine`. `VendorInvoice.JournalEntryId` is a repairable source-document back-reference written immediately after the posting engine returns; if that write fails, idempotent retry returns the existing posting and can relink the invoice. The posted GL and `FinancePostingEvent` remain the source of truth.

## Verification

- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\.codex-build\ap-posting\bin\ --filter "Batch=FinanceGoLive-APPosting" -maxcpucount:1 -v:minimal`
  - Passed: 10, Failed: 0.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -c Debug --no-build -p:BaseOutputPath=.\.codex-build\ap-posting\bin\ --filter "Batch=FinanceGoLive-4|Batch=FinanceGoLive-5" -maxcpucount:1 -v:minimal`
  - Passed: 19, Failed: 0.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -c Debug --no-build -p:BaseOutputPath=.\.codex-build\ap-posting\bin\ --filter "Batch=FinanceAuditFoundation|Batch=FinanceAuditHardening" -maxcpucount:1 -v:minimal`
  - Passed: 11, Failed: 0.

## Rollback Considerations

Rollback is code-only. No database migration was added. Reverting this batch restores AP invoice approval paths to the previous legacy subledger posting behavior, but that would reintroduce the old direct journal-posting bypass.

## Tenant Isolation

AP posting validates the current finance tenant against the invoice, supplier, invoice lines, AP control account, line accounts, GRV/tax/discount accounts, posting event, and posting engine request. Cross-tenant supplier, AP control account, and line account tests are included.

## Accounting Impact

AP invoices now post to the immutable ledger through the same controlled posting engine used by manual journals. This reduces duplicate-posting risk, keeps the posted GL as source of truth, and prepares AP payments/tax/FX/fixed asset batches to integrate against a single posting path.
