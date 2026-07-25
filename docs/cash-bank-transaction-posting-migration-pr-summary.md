# Cash/Bank Transaction Posting Migration to IFinancePostingEngine

## Definition Of Done

- [x] Standalone cash/bank receipt, payment, and transfer posting uses `IFinancePostingEngine`.
- [x] `CashTransactionService` no longer creates or posts GL journals directly.
- [x] Cash/bank posting is tenant-aware and validates bank accounts, destination transfer accounts, offset accounts, and GL account postability below the controller layer.
- [x] Posted cash/bank transactions are immutable through direct delete mutation.
- [x] Duplicate/idempotent posting returns the existing posting result safely.
- [x] Finance audit events are emitted for captured, posted, failed, and duplicate/idempotent posting attempts.
- [x] Back-reference diagnostics cover `CashTransaction.JournalEntryId`, `IsPosted`, posted events, cross-tenant journal references, duplicates, and transfer destination-leg relinking.
- [x] Tests were added and targeted posting regressions pass.

## Posting Design

Cash/bank posting is routed through `CashTransactionService.PostAsync(Guid id)`, which builds a `FinancePostingRequestDto` and delegates GL creation to `IFinancePostingEngine`.

Accounting treatment:

- Receipt: debit linked bank/cash GL account, credit configured offset account.
- Payment: debit configured offset account, credit linked bank/cash GL account.
- Transfer: debit destination bank/cash GL account, credit source bank/cash GL account.
- Do not mutate `Account.Balance`; posted GL entries and `FinancePostingEvents` remain the accounting source of truth.

For transfers, the outgoing/source transfer leg is the source document for the posting event. The destination leg is linked to the same posted journal as a repairable operational back-reference.

## Lifecycle Rules

- Unposted cash/bank transactions can be captured as operational records.
- `PostAsync` is the controlled posting action.
- Posted transactions cannot be deleted by mutation.
- Transactions marked posted or linked to a journal without a valid posting event are rejected and must go through diagnostics/repair.
- Approval state is now modeled on `CashTransaction` by the follow-up cash/bank workflow hardening batch.
- Bank reconciliation behavior is not changed in this batch.

## Affected Files

- `src/ErpSystem.Api/Controllers/Finance/CashTransactionController.cs`
- `src/ErpSystem.Api/Services/Finance/Cash/CashTransactionService.cs`
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `src/ErpSystem.Core/DTOs/Finance/CashTransactionDtos.cs`
- `src/ErpSystem.Core/Entities/Finance/CashTransaction.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ICashManagementServices.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260705143000_AddCashTransactionJournalEntryBackReference.cs`
- `src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/CashBankTransactionPostingMigrationTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `docs/finance-posting-backreference-diagnostics.md`
- `docs/cash-bank-transaction-posting-migration-pr-summary.md`

## API Impact

- Added `POST /api/finance/cash-transactions/{id}/post`.
- Added `CashTransactionDto.JournalEntryId`.

Existing receipt/payment creation remains operational capture. The follow-up cash/bank workflow hardening batch changed transfer creation to capture only; posting now requires explicit workflow submission, approval, and post action.

## Migration Impact

Added migration `20260705143000_AddCashTransactionJournalEntryBackReference`:

- Adds nullable `CashTransaction.JournalEntryId`.
- Adds same-tenant journal reference indexing.
- Adds the EF relationship to `JournalEntries`.

The migration is additive. Existing cash/bank rows remain unlinked until posted or repaired through the documented diagnostic process.

## Tests

- `dotnet build tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\cash-bank\bin\ --no-restore --disable-build-servers -m:1`
  - Passed with 13 existing warnings.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\cash-bank\bin\ --no-build --no-restore --filter Batch=FinanceGoLive-CashBankPosting`
  - 10 passed.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj -p:BaseOutputPath=.\.codex-build\cash-bank\bin\ --no-build --no-restore --filter "Batch=FinanceGoLive-CashBankPosting|Batch=FinanceGoLive-ARCreditNotePosting|Batch=FinanceGoLive-ARReceiptPosting|Batch=FinanceGoLive-ARInvoicePosting|Batch=FinanceGoLive-APPaymentPosting|Batch=FinanceGoLive-APPosting|Batch=FinanceGoLive-4|Batch=FinanceGoLive-5|Batch=FinanceAuditFoundation|Batch=FinanceAuditHardening"`
  - 96 passed.

## Static Verification

`CashTransactionService` no longer references `IJournalEntryService`, `CreateJournalEntryAsync`, `PostJournalEntryAsync`, or `ISubledgerPostingService`.

The remaining cash/bank direct journal posting path is `BankAccountService` opening-balance posting. That is not a standalone cash/bank transaction path and remains part of the opening-balance/data-migration follow-up.

Manual journal, AP invoice, AP payment, AR invoice, AR receipt, AR credit note, and standalone cash/bank transaction posting now route through `IFinancePostingEngine`.

## Tenant Isolation Verification

The posting path rejects:

- cash/bank transactions outside the current `TenantId`
- bank/cash accounts outside the current `TenantId`
- transfer destination accounts outside the current `TenantId`
- offset accounts outside the current `TenantId`
- inactive bank/cash accounts
- inactive or non-direct-posting GL accounts
- transfer source/destination account equality
- source documents marked posted or journal-linked without a valid same-tenant posting event
- posting into closed fiscal periods

## Accounting Impact

This centralizes standalone cash/bank postings after the AP and AR posting loops. Receipts, payments, and transfers now generate balanced posted GL entries through one posting engine with posting-event idempotency.

Operational bank balances (`CurrentBalance`, `AvailableBalance`), `CashTransaction.IsPosted`, `PostedDate`, and `JournalEntryId` remain read-side/operational fields. The posted GL and `FinancePostingEvents` are authoritative and must be reconciled during go-live cleanup.

The follow-up cash/bank operational balance hardening batch removed receipt/payment capture-time balance updates. Stored bank balance snapshots now move from posted cash/bank transactions only and have dedicated diagnostics for GL reconciliation.

## Rollback Considerations

Code rollback would restore direct transfer posting through `IJournalEntryService` unless the old path is kept disabled. If rollback is required after the migration is applied, suspend standalone cash/bank posting until the central posting path is restored or a controlled compensating release is prepared.

The database rollback drops only the nullable cash transaction journal back-reference and its indexes/foreign key. It does not remove posted GL entries or posting events.

## Known Limitations

- Cash/bank workflow approval state is now modeled on `CashTransaction`; approval-before-posting is enforced by the follow-up cash/bank workflow hardening batch.
- Bank reconciliation, reconciliation adjustments, bank statement import, and cleared/uncleared posting behavior are intentionally not migrated in this batch.
- Cash/bank reversal/void posting is not implemented in this batch; direct delete mutation is blocked for posted transactions.
- Bank charges and interest income are supported through the generic receipt/payment offset-account pattern, not through a category-specific rules engine.
- Cross-currency bank transfers remain unsupported by the existing endpoint and are deferred to the FX batch.
- Bank account opening-balance posting still uses the existing opening-balance path and remains part of the data migration/opening-balance follow-up.
- Tax engine, FX, fixed assets, reporting, frontend, and data migration work were intentionally not started.
