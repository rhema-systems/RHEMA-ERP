# Finance Legacy Posting Path Lockdown and Posting-Engine Bypass Elimination

## Scope

This batch locks down normal Finance posting paths so AP, AR, cash/bank, tax, FX, fixed assets, reconciliation, and standard Finance workflows cannot bypass `IFinancePostingEngine`.

No frontend UI, reversal redesign, workflow-routing hardening, data migration/sign-off execution, or final go-live scenario pack is included.

## Posting Boundary

`IFinancePostingEngine` is the central normal posting boundary for Finance subledger and operational posting.

Manual journal posting remains the approved manual GL path through `IJournalEntryService` and its workflow/lifecycle controls. Normal subledger posting must not call legacy direct GL writers.

## Posting Path Inventory

| Posting surface | Classification | Current treatment |
|---|---|---|
| AP vendor invoice | Uses `IFinancePostingEngine` | Normal AP invoice posting remains centralized through `VendorInvoiceService`. |
| AP vendor payment | Uses `IFinancePostingEngine` | Normal AP payment posting remains centralized through `VendorPaymentService`. |
| Finance purchase order receipt / GRV | Uses `IFinancePostingEngine` | Receipt posting now builds a `FinancePostingRequestDto` and posts through the engine. |
| Supplier debit note | Uses `IFinancePostingEngine` | Debit-note posting now builds a `FinancePostingRequestDto` and posts through the engine. |
| AR invoice | Uses `IFinancePostingEngine` | Normal AR invoice posting remains centralized through `InvoiceService`. |
| AR receipt/customer payment | Uses `IFinancePostingEngine` | Normal AR receipt posting remains centralized through `PaymentService`. |
| Sales credit note / return order credit note | Uses `IFinancePostingEngine` | Normal credit-note posting uses the posting engine through the owning sales/AR service. |
| Cash/bank receipt/payment/transfer | Uses `IFinancePostingEngine` | Cash/bank posting remains centralized through `CashTransactionService`. |
| Bank reconciliation adjustment | Uses `IFinancePostingEngine` | Adjustments continue through the cash/bank posting path. |
| Subledger AP/AR adjustment journal | Uses `IFinancePostingEngine` | Adjustment and reversal journals now post through the engine with source-document idempotency. |
| Ghana tax posting | Uses `IFinancePostingEngine` | Tax accounting is posted through AP/AR/payment posting flows and tax snapshots. |
| FX realized/unrealized/revaluation | Uses `IFinancePostingEngine` | FX accounting services post through the engine. |
| Fixed asset capitalization/depreciation/valuation/disposal | Uses `IFinancePostingEngine` | Fixed asset accounting services post through the engine. |
| Fixed asset custody/location transfer | Read-side/history only | Supported transfer types do not create GL journals. Unsupported GL reclassification transfers remain rejected under `FIN-LIM-0038`. |
| Manual journals | Approved manual journal path | Use `JournalEntryService` lifecycle, workflow controls, and posting-engine validation where implemented. |
| Generic `FinanceController` post-to-ledger endpoint | Unsafe legacy/direct posting path | Disabled. Attempts are rejected and audited with `Finance.PostingEngine.BypassRejected`. |
| `GeneralLedgerService.PostJournalEntryAsync` | Unsafe legacy/direct posting path | Disabled and marked obsolete. Callers must use manual journal lifecycle or module services through `IFinancePostingEngine`. |
| `ISubledgerPostingService` / `SubledgerPostingService` | Legacy non-runtime reference | Marked obsolete, documented as not a normal posting path, and removed from normal DI registration. |
| Opening balance and migration posting | Uses `IFinancePostingEngine` for balanced GL opening-balance batches | `FIN-LIM-0006` is resolved for explicit balanced GL opening-balance batches. `ALL_ACTIVE_BOOKS`, unbalanced imports, and subledger opening-document imports remain rejected/deferred under precise migration limitations. |
| Bank account nonzero opening balance create path | Migration-only guarded path | Normal bank account create still rejects nonzero opening balances; bank GL opening balances must be included in a controlled balanced opening-balance batch rather than mutating `BankAccount.CurrentBalance`. |

## Legacy Paths Removed, Disabled, Or Guarded

- Removed normal DI registration for `ISubledgerPostingService`.
- Removed stale normal service/controller constructor dependencies on `ISubledgerPostingService`.
- Migrated Finance PO receipt/GRV posting from `ISubledgerPostingService.PostFinancePurchaseOrderReceiptAsync` to `IFinancePostingEngine`.
- Migrated supplier debit-note posting from `ISubledgerPostingService.PostSupplierDebitNoteAsync` to `IFinancePostingEngine`.
- Disabled the old `post-finance-grv` maintenance command.
- Disabled `FinanceController` generic `journal-entries/post-to-ledger` direct posting.
- Disabled `GeneralLedgerService.PostJournalEntryAsync`.
- Migrated subledger AP/AR adjustment journals from direct `IJournalEntryService` posting to `IFinancePostingEngine`.
- Disabled normal bank account nonzero opening-balance mutation; bank GL opening balances now belong in the controlled opening-balance posting flow.
- Marked `ISubledgerPostingService` and `SubledgerPostingService` obsolete and documented as non-runtime legacy surfaces.

## Controller And Permission Safety

Normal posting-capable controller actions now route through owning module services that use the posting engine.

The old generic direct GL posting endpoint remains present only as a rejected compatibility surface so clients receive an explicit failure instead of silently reaching a second accounting path.

Balanced GL opening-balance posting is exposed only through the controlled opening-balance service/controller and posts through `IFinancePostingEngine`. Subledger opening-document migration remains tracked separately in the limitations register.

## Audit Events

Added audit constants:

- `Finance.LegacyPostingPath.Blocked`
- `Finance.LegacyPostingPath.MigrationOnlyAttempted`
- `Finance.PostingEngine.BypassRejected`

Implemented runtime call:

- `Finance.PostingEngine.BypassRejected` when the disabled generic direct GL posting endpoint is called.

## Tenant Isolation

The migrated GRV and supplier debit-note posting paths validate same-tenant source records, settings, accounts, and currency context before calling the posting engine.

The posting engine remains responsible for the final tenant, account, currency, period, balance, idempotency, and posting-event validation.

## Tests

Added architecture and lockdown tests in `tests/ErpSystem.Api.Tests/Services/Finance/LegacyPostingPathLockdownTests.cs`:

- `NormalRuntime_ShouldNotRegisterLegacySubledgerPostingService`
- `NormalRuntime_ShouldNotDependOnLegacySubledgerPostingService`
- `GenericFinanceControllerPostingEndpoint_ShouldRejectBypassInsteadOfCallingGeneralLedgerDirectPost`
- `CurrentFinancePostingServices_ShouldRouteThroughPostingEngine`
- `BankOpeningBalancePosting_ShouldRemainDisabledUntilPostingEngineMigration`

The architecture tests fail if normal runtime code reintroduces legacy `ISubledgerPostingService` dependencies, calls old subledger posting methods outside the quarantined legacy files, re-enables the generic direct GL endpoint, or drops posting-engine usage from current posting services.

## Diagnostics And Static Checks

Static test coverage now verifies:

- no normal DI registration for `ISubledgerPostingService`;
- no `Program.cs` runtime resolution of `ISubledgerPostingService`;
- disabled `post-finance-grv` command behavior;
- no normal runtime dependency on old subledger posting method names;
- no normal bank opening-balance direct journal creation/posting path;
- disabled generic `FinanceController` posting bypass;
- posting-engine usage across AP, AR, cash/bank, subledger adjustments, FX, fixed assets, GRV, and supplier debit-note posting surfaces.

## Limitations Register

- `FIN-LIM-0018` is resolved for normal runtime Finance posting paths.
- `FIN-LIM-0006` remains open for opening-balance and migration posting through a future controlled posting-engine batch.
- Reversal/correction limitations remain open where not directly addressed by this lockdown batch.

## Rollback Considerations

Rollback is code-only:

- restore legacy DI registration and constructor dependencies;
- restore direct endpoint behavior and `GeneralLedgerService.PostJournalEntryAsync`;
- restore GRV/debit-note calls to `ISubledgerPostingService`;
- remove lockdown tests and docs.

No migration was added and no posted accounting data is mutated by this batch.

## PR Definition Of Done

- Normal runtime Finance posting paths no longer depend on `ISubledgerPostingService`.
- Unsafe generic direct GL posting is rejected and audited.
- Remaining legacy code is obsolete, non-registered, and documented as not a normal posting path.
- Architecture tests guard against posting-engine bypass reintroduction.
- Finance regression slice passes.
