# AR Invoice Posting Migration to IFinancePostingEngine

## Definition of Done

- [x] Normal AR invoice posting uses `IFinancePostingEngine`.
- [x] No live AR invoice path calls `ISubledgerPostingService.PostArInvoiceAsync`.
- [x] Tenant checks exist for invoice, customer, revenue accounts, AR control account, tax account, discount account, and posting event source data.
- [x] Posted customer invoices cannot be edited or deleted by mutation.
- [x] Posting creates balanced GL entries without mutating `Account.Balance`.
- [x] Finance audit events are recorded for AR invoice workflow approve/reject outcomes, posted events, failed post attempts, and duplicate/idempotent post attempts.
- [x] Automated tests cover valid posting, duplicate posting, tenant violations, account violations, closed periods, unbalanced totals, draft posting rejection, and posted invoice immutability.

## Posting Design

AR invoice posting is now routed through `InvoiceService.PostAsync(Guid id)`, which builds a `FinancePostingRequestDto` and delegates ledger creation to `IFinancePostingEngine`.

`InvoiceService.SendInvoiceAsync(Guid id)` still owns the existing AR lifecycle transition from `Draft` to `Sent`, but GL posting is performed through the central posting engine instead of the legacy subledger posting service.

The posted GL remains the accounting source of truth. AR invoice posting creates posted `JournalEntry`, `AccountTransaction`, and `FinancePostingEvent` records through the central posting engine. It does not mutate `Account.Balance`.

Expected entry shape for standard invoices:

- Debit AR control account for the receivable amount.
- Credit revenue accounts from invoice lines.
- Credit output tax control account when existing invoice tax data is present.
- Debit sales discounts allowed when existing discount fields are present.
- Debit COGS and credit inventory control for inventory invoice lines where cost data already exists.

Full effective-dated Ghana VAT/NHIL/GETFund/VAT withholding logic is intentionally deferred to the tax batch.

## Lifecycle Rules

- `Draft` invoices can be edited or deleted.
- `Draft` invoices cannot be posted through `PostAsync`.
- `SendInvoiceAsync` transitions a draft invoice to `Sent` and posts it through `IFinancePostingEngine`.
- Workflow approval completion for `Invoice` continues to use the existing workflow engine and calls `SendInvoiceAsync`.
- `Sent` invoices with a `JournalEntryId` are treated as posted and immutable.
- Corrections remain deferred to reversal, credit-note, or adjustment workflows.
- Opening-balance AR invoice posting is deferred to the data migration/opening balance batch.

## Files Changed

- `src/ErpSystem.Api/Services/Finance/AR/InvoiceService.cs`
  - Added optional `IFinancePostingEngine` and `IFinanceAuditService` dependencies.
  - Added `PostAsync`.
  - Replaced `SendInvoiceAsync` legacy GL posting call with central posting-engine posting.
  - Added AR invoice posting request builder and tenant/account validation helpers.
  - Added posted invoice edit/delete guards.
  - Added AR invoice audit logging.

- `src/ErpSystem.Core/Interfaces/Finance/IInvoiceService.cs`
  - Added `PostAsync`.

- `src/ErpSystem.Core/DTOs/Finance/InvoiceDtos.cs`
  - Added `JournalEntryId`.

- `src/ErpSystem.Api/Controllers/Finance/ArControllersConsolidated.cs`
  - Added `POST /api/ar/invoices/{id}/post`.

- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
  - Mapped AR invoice `Post` to `Finance.AR.Invoices.ApprovePost`.

- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
  - Added AR invoice approval/rejection audit events while preserving the existing workflow engine path.

- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
  - Added AR invoice audit event constants.

- `src/ErpSystem.Core/Interfaces/Finance/ISubledgerPostingService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/SubledgerPostingService.cs`
  - Marked legacy AR invoice posting obsolete and guarded it against normal invoice posting.

- `tests/ErpSystem.Api.Tests/Services/Finance/ArInvoicePostingMigrationTests.cs`
  - Added targeted AR invoice posting migration tests.

- `docs/finance-posting-backreference-diagnostics.md`
  - Added AP invoice/payment posting back-reference diagnostics and repair guidance before this batch.

## Legacy Posting Path

Static verification found no live AR invoice service/controller caller of `PostArInvoiceAsync`. Remaining references are:

- `ISubledgerPostingService.PostArInvoiceAsync`
- `SubledgerPostingService.PostArInvoiceAsync`

The implementation is obsolete and rejects normal AR invoice posting with a message directing callers to `IInvoiceService.PostAsync`.

## Idempotency

AR invoice posting uses:

- `SourceModule = "AR"`
- `SourceDocumentType = "CustomerInvoice"`
- `SourceDocumentId = invoice.Id`
- `PostingAction = "Post"`
- `IdempotencyKey = AR:CustomerInvoice:{TenantId}:{InvoiceId}:Post`

Duplicate posting attempts return the existing central posting result and create an AR duplicate-posting audit event.

## Tests

Build:

- `dotnet build tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BaseOutputPath=.\.codex-build\ar-invoice\bin\ -v:minimal`
- Result: passed, 0 errors, existing warnings only.

Targeted tests:

- `ArInvoicePostingMigrationTests`: 10 passed.
- `FinancePostingEngineTests`, `ApInvoicePostingMigrationTests`, `ApPaymentPostingMigrationTests`: 31 passed.

## Migration Impact

No database migration was added in this batch.

## Rollback Considerations

Code rollback would restore the old AR invoice posting path, but that path bypasses `IFinancePostingEngine`, does not use central posting idempotency, and may mutate legacy balance fields through older GL services. If rollback is required, do not use normal AR invoice posting in production until the central path is restored.

## Tenant Isolation Verification

Tests verify rejection of:

- Cross-tenant customer reference.
- Cross-tenant revenue account.
- Cross-tenant AR control account.

The central posting engine continues to enforce tenant ownership for posting source documents and accounts below the controller layer.

## Accounting Impact

AR invoice posting now uses the central posting engine and records balanced debit/credit entries in the posted GL. The implementation keeps `Account.Balance` unchanged and treats stored balances as non-authoritative read models.

## Remaining Limitations

- `Invoice.JournalEntryId` remains a repairable operational back-reference written after the central posting event exists. The AP back-reference diagnostics document should be extended to include AR invoices and AR receipts before go-live.
- `BusinessPartner.OutstandingBalance` remains an operational read model updated by AR send/payment workflows. GL and `FinancePostingEvent` remain the accounting source of truth.
- Full Ghana statutory tax handling is deferred to the tax batch.
- AR receipt/customer payment posting is not migrated in this batch.
- AR invoice reversal, credit-note integration, and opening-balance AR invoice posting remain future batches.
