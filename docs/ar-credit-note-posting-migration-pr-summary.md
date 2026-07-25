# AR Credit Note Posting Migration to IFinancePostingEngine

## Definition Of Done

- [x] Sales credit-note posting uses `IFinancePostingEngine`.
- [x] Compatibility customer credit-note posting uses `IFinancePostingEngine`.
- [x] No live AR credit-note path calls legacy `ISubledgerPostingService.PostArPaymentAsync` or `PostSalesCreditNoteAsync`.
- [x] Legacy AR payment and Sales credit-note posting methods are guarded and marked obsolete/internal-only.
- [x] Credit-note tenant/account/source-document validation is enforced below the controller layer.
- [x] Posted Sales credit notes are immutable through direct void mutation.
- [x] Sales credit-note correction creates a linked, engine-posted reversal rather than mutating the original document.
- [x] Finance audit events are emitted for submitted, approved/rejected, posted, failed, and duplicate/idempotent credit-note posting attempts.
- [x] Back-reference diagnostics cover Sales credit notes and compatibility customer credit notes.
- [x] Tests were added and targeted posting regressions pass.

## Posting Design

Sales credit-note posting is routed through `ReturnOrderService.PostCreditNoteAsync(Guid id)`, which builds a `FinancePostingRequestDto` and delegates ledger creation to `IFinancePostingEngine`.

Compatibility customer credit notes created through the AR payment model are routed through `PaymentService.CreateAsync(...)` into a private `PostCustomerCreditNoteAsync(...)` helper that also delegates to `IFinancePostingEngine`.

Accounting treatment:

- Debit sales returns/allowance using the tenant finance settings discount-allowed account.
- Debit output tax reversal when existing credit-note tax amounts and tax control account are available.
- Credit AR control/receivables using the customer-specific AR account or tenant AR control account.
- Do not mutate `Account.Balance`; posted GL entries and `FinancePostingEvents` remain the accounting source of truth.

`CreditNotes.JournalEntryId` and compatibility `CustomerPayment.JournalEntryId` are operational back-references. The posted ledger and `FinancePostingEvents` are authoritative, and `docs/finance-posting-backreference-diagnostics.md` includes tenant-safe relink diagnostics and accountant sign-off requirements.

## Lifecycle Rules

- Draft Sales credit notes may be edited through the existing Sales return-order flow.
- Sales credit notes must be approved before posting.
- Sales credit notes linked to an original invoice require that original invoice to be posted as a `CustomerInvoice` posting event.
- Credit amount cannot exceed the original invoice total less already-posted same-tenant Sales credit notes.
- Posted Sales credit notes cannot be voided by mutation. `POST /api/sales/credit-notes/{id}/reverse` requires a reason and posts the engine-generated inverse journal with the AR void permission.
- A reversed credit note retains its original journal and records immutable reversal journal/event, date, and reason links. A corrected commercial amount must be issued as a new approved credit note.
- Duplicate post attempts return the existing posting result safely and create an audit event.

## Affected Files

- `src/ErpSystem.Api/Controllers/Sales/ReturnOrderController.cs`
- `src/ErpSystem.Api/Services/Finance/AR/PaymentService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/SubledgerPostingService.cs`
- `src/ErpSystem.Core/Services/Sales/ReturnOrderService.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ISubledgerPostingService.cs`
- `src/ErpSystem.Core/Interfaces/Sales/IReturnOrderService.cs`
- `src/ErpSystem.Core/DTOs/Sales/ReturnOrderDTOs.cs`
- `src/ErpSystem.Core/Entities/Sales/ReturnOrderEntities.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260705133000_AddSalesCreditNoteJournalEntryBackReference.cs`
- `src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/ArCreditNotePostingMigrationTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `docs/finance-posting-backreference-diagnostics.md`
- `docs/ar-credit-note-posting-migration-pr-summary.md`

## API Impact

- Added `POST /api/sales/credit-notes/{id}/post`.
- Added `POST /api/sales/credit-notes/{id}/reverse` for controlled correction of a posted Sales credit note.
- Added `CreditNoteSummaryDto.JournalEntryId` and `CreditNoteDetailDto.JournalEntryId` through inheritance.
- Existing compatibility customer credit-note creation still uses the AR payment create flow, but the posting call now uses the central posting engine.

## Migration Impact

Added migration `20260705133000_AddSalesCreditNoteJournalEntryBackReference`:

- Adds nullable `CreditNotes.JournalEntryId`.
- Adds same-tenant journal reference indexing.
- Adds the EF relationship to `JournalEntries`.

The migration is additive and should be applied after tenant-specific data checks. Existing Sales credit notes remain unlinked until posted or repaired through the documented diagnostic process.

## Tests

- `dotnet build tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\ar-credit-notes\bin\ --no-restore`
  - Passed with 5 existing warnings.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\ar-credit-notes\bin\ --no-build --no-restore --filter Batch=FinanceGoLive-ARCreditNotePosting`
  - 12 passed.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\ar-credit-notes\bin\ --no-build --no-restore --filter "Batch=FinanceGoLive-ARCreditNotePosting|Batch=FinanceGoLive-ARReceiptPosting|Batch=FinanceGoLive-ARInvoicePosting|Batch=FinanceGoLive-APPaymentPosting|Batch=FinanceGoLive-APPosting|Batch=FinanceGoLive-4|Batch=FinanceGoLive-5|Batch=FinanceAuditFoundation|Batch=FinanceAuditHardening"`
  - 86 passed.

## Static Verification

`rg -n "PostArPaymentAsync\(|PostSalesCreditNoteAsync\(" src tests --glob '!**/bin/**' --glob '!**/obj/**'` shows only:

- tests verifying the legacy methods are not called
- guarded legacy method definitions
- obsolete interface definitions

No live AR credit-note posting caller remains on the legacy subledger posting path.

## Tenant Isolation Verification

The posting path rejects:

- credit notes outside the current `TenantId`
- customers outside the current `TenantId`
- original invoices outside the current `TenantId`
- unposted original invoices
- AR control accounts outside the current `TenantId`
- sales returns/allowance accounts outside the current `TenantId`
- tax accounts outside the current `TenantId`
- inactive or non-postable posting accounts
- posting into closed fiscal periods

## Accounting Impact

This centralizes the remaining AR credit-note posting path after the AR invoice and receipt migrations. AR credit notes now reduce receivables through controlled GL postings instead of the legacy AR payment compatibility posting route.

The posted GL remains the accounting source of truth. Credit-note status and journal references are operational read-side fields and must be reconciled against `FinancePostingEvents` during go-live cleanup.

## Rollback Considerations

Code rollback would restore the legacy AR credit-note path unless the guarded subledger methods are kept disabled. If rollback is required after the migration is applied, suspend AR credit-note posting until the central posting path is restored or a controlled compensating release is prepared.

The database rollback drops only the nullable Sales credit-note journal back-reference and its indexes/foreign key. It does not remove posted GL entries or posting events.

## Known Limitations

- Sales credit-note reversal/adjustment posting is resolved by the follow-up Finance batch documented in `docs/ap-ar-advance-settlement-credit-note-reversal-pr-summary.md`; direct void mutation remains blocked for posted credit notes.
- Compatibility `CustomerPayment.IsCreditNote` reversal is not included in the Sales credit-note correction flow and remains governed by `FIN-LIM-0013`.
- Full Ghana VAT/NHIL/GETFund/VAT withholding logic is deferred to the tax batch. This batch maps only existing credit-note tax amounts/accounts.
- Inventory/COGS reversal is not posted because the current credit-note posting path does not expose complete return-to-inventory accounting data.
- Compatibility `CustomerPayment.IsCreditNote` posting is centralized but remains a compatibility path with weaker workflow semantics than the primary Sales `CreditNote` flow.
- Cash/bank, tax engine, FX, fixed assets, reporting, frontend, and data migration work were intentionally not started.
